/* CI test for Database/MultiRoomKeys.sql, run by .github/workflows/release.yml
   against a throwaway SQL Server after the schema and every migration (twice).
   Any failed check THROWs, which fails the build before anything is released. */
USE [AccessControl]
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @now DATETIME2(0) = SYSDATETIME();
DECLARE @from DATETIME2(0) = DATEADD(HOUR, 1, @now), @to DATETIME2(0) = DATEADD(HOUR, 4, @now);

INSERT INTO dbo.Sites ([Name], [IsActive], [CreatedAt]) VALUES (N'CI site', 1, @now);
DECLARE @site INT = SCOPE_IDENTITY();
INSERT INTO dbo.WorkAreas ([SiteId], [Name]) VALUES (@site, N'Tower A');
DECLARE @wa INT = SCOPE_IDENTITY();
INSERT INTO dbo.Rooms ([WorkAreaId], [RoomNumber], [Name]) VALUES (@wa, N'1.01', N'Apartment 1.01');
DECLARE @r1 INT = SCOPE_IDENTITY();
INSERT INTO dbo.Rooms ([WorkAreaId], [RoomNumber], [Name]) VALUES (@wa, N'1.02', N'Apartment 1.02');
DECLARE @r2 INT = SCOPE_IDENTITY();
INSERT INTO dbo.Users ([FirstName], [LastName], [Email], [Role], [Password], [salt])
VALUES (N'Ci', N'Manager', N'ci.manager@example.invalid', N'Manager', N'x', N'x');
DECLARE @mgr INT = SCOPE_IDENTITY();
INSERT INTO dbo.Workers ([FirstName], [LastName], [PhoneNumber]) VALUES (N'Ci', N'Worker', N'+440000000000');
DECLARE @w INT = SCOPE_IDENTITY();
INSERT INTO dbo.Workers ([FirstName], [LastName], [PhoneNumber]) VALUES (N'Ci', N'Other', N'+440000000001');
DECLARE @w2 INT = SCOPE_IDENTITY();

/* Keys */
DECLARE @t TABLE (Id INT);
INSERT INTO @t EXEC dbo.KeysInsert @RoomId = @r1, @Label = N'K-1.01-A', @TagNumber = N'T1';
DECLARE @k1 INT = (SELECT TOP 1 Id FROM @t); DELETE FROM @t;
INSERT INTO @t EXEC dbo.KeysInsert @RoomId = @r2, @Label = N'K-1.02-A';
DECLARE @k2 INT = (SELECT TOP 1 Id FROM @t); DELETE FROM @t;

/* A key for the wrong room is refused, and nothing is saved. */
DECLARE @items NVARCHAR(MAX) = CONCAT(N'[{"roomId":', @r1, N',"keyIds":[', @k2, N']}]');
DECLARE @groupsBefore INT = (SELECT COUNT(*) FROM dbo.AccessRequestGroups);
BEGIN TRY
    EXEC dbo.AccessRequestGroupsInsert @WorkerId = @w, @ApproverUserId = @mgr, @WorkFrom = @from, @WorkTo = @to, @Items = @items;
    THROW 60001, N'FAIL: a key for another room was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50011 THROW;
END CATCH
IF (SELECT COUNT(*) FROM dbo.AccessRequestGroups) <> @groupsBefore THROW 60002, N'FAIL: refused submission left a group behind.', 1;

/* Two rooms, one key each (the first room listed twice: kept once). */
SET @items = CONCAT(N'[{"roomId":', @r1, N',"keyIds":[', @k1, N']},{"roomId":', @r2, N',"keyIds":[', @k2, N']},{"roomId":', @r1, N'}]');
DECLARE @created TABLE (GroupId INT, AccessRequestId INT, RoomId INT);
INSERT INTO @created EXEC dbo.AccessRequestGroupsInsert
    @WorkerId = @w, @ApproverUserId = @mgr, @Reason = N'CI', @WorkType = N'General',
    @WorkFrom = @from, @WorkTo = @to, @Items = @items;
DECLARE @g INT = (SELECT TOP 1 GroupId FROM @created);
IF (SELECT COUNT(*) FROM @created) <> 2 THROW 60003, N'FAIL: expected 2 access requests in the group.', 1;
IF (SELECT COUNT(*) FROM dbo.AccessRequestApprovals a JOIN @created c ON c.AccessRequestId = a.AccessRequestId WHERE a.[Status] = 'PENDING') <> 2
    THROW 60004, N'FAIL: expected a pending approval per room.', 1;
IF (SELECT COUNT(*) FROM dbo.AccessRequestKeys k JOIN @created c ON c.AccessRequestId = k.AccessRequestId WHERE k.[Status] = 'Requested') <> 2
    THROW 60005, N'FAIL: expected 2 requested keys.', 1;
DECLARE @a1 INT = (SELECT AccessRequestId FROM @created WHERE RoomId = @r1);
DECLARE @a2 INT = (SELECT AccessRequestId FROM @created WHERE RoomId = @r2);

/* AccessRequestsGet now returns GroupId and GroupSize. */
DECLARE @cols TABLE (name SYSNAME);
INSERT INTO @cols SELECT name FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.AccessRequestsGet', NULL, 0);
IF NOT EXISTS (SELECT 1 FROM @cols WHERE name = 'GroupId') OR NOT EXISTS (SELECT 1 FROM @cols WHERE name = 'GroupSize')
    THROW 60006, N'FAIL: AccessRequestsGet does not return GroupId / GroupSize.', 1;

/* The same key cannot be booked again for an overlapping time. */
SET @items = CONCAT(N'[{"roomId":', @r1, N',"keyIds":[', @k1, N']}]');
BEGIN TRY
    EXEC dbo.AccessRequestGroupsInsert @WorkerId = @w2, @ApproverUserId = @mgr, @WorkFrom = @from, @WorkTo = @to, @Items = @items;
    THROW 60007, N'FAIL: a key already booked for that time was accepted.', 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() <> 50012 THROW;
END CATCH
DECLARE @avail BIT;
DECLARE @keys TABLE (Id INT, RoomId INT, Label NVARCHAR(100), TagNumber NVARCHAR(50), Notes NVARCHAR(500), IsActive BIT, CreatedAt DATETIME2(0),
    RoomNumber NVARCHAR(100), RoomName NVARCHAR(200), WorkAreaId INT, WorkAreaName NVARCHAR(200), SiteId INT, Available BIT, HeldBy NVARCHAR(250));
INSERT INTO @keys EXEC dbo.KeysGet @RoomId = @r1, @WorkFrom = @from, @WorkTo = @to;
SELECT @avail = Available FROM @keys WHERE Id = @k1;
IF @avail <> 0 THROW 60008, N'FAIL: KeysGet shows a booked key as available.', 1;

/* Not claimable while a room is still pending. */
DECLARE @claim TABLE (Claimed INT);
UPDATE dbo.AccessRequests SET [Status] = 'Approved' WHERE Id = @a1;
EXEC dbo.AccessRequestKeysDecide @AccessRequestId = @a1, @Approved = 1;
INSERT INTO @claim EXEC dbo.AccessRequestGroupsClaimNotification @GroupId = @g;
IF (SELECT TOP 1 Claimed FROM @claim) <> 0 THROW 60009, N'FAIL: notification claimed with a room still pending.', 1;
DELETE FROM @claim;

/* Second room rejected: its key is released; claim succeeds once only. */
UPDATE dbo.AccessRequests SET [Status] = 'Rejected' WHERE Id = @a2;
EXEC dbo.AccessRequestKeysDecide @AccessRequestId = @a2, @Approved = 0;
INSERT INTO @claim EXEC dbo.AccessRequestGroupsClaimNotification @GroupId = @g;
INSERT INTO @claim EXEC dbo.AccessRequestGroupsClaimNotification @GroupId = @g;
IF (SELECT SUM(Claimed) FROM @claim) <> 1 THROW 60010, N'FAIL: the group notification must be claimed exactly once.', 1;
IF (SELECT [Status] FROM dbo.AccessRequestKeys WHERE AccessRequestId = @a2) <> 'Rejected' THROW 60011, N'FAIL: rejected room key not released.', 1;
IF (SELECT [Status] FROM dbo.AccessRequestKeys WHERE AccessRequestId = @a1) <> 'Approved' THROW 60012, N'FAIL: approved room key not approved.', 1;

/* Key tracking: Approved -> Issued -> Missing -> Returned; Returned cannot be issued again. */
DECLARE @ark INT = (SELECT Id FROM dbo.AccessRequestKeys WHERE AccessRequestId = @a1);
DECLARE @rows TABLE (n INT);
INSERT INTO @rows EXEC dbo.AccessRequestKeysUpdate @Id = @ark, @Status = 'Returned', @UserId = @mgr;
INSERT INTO @rows EXEC dbo.AccessRequestKeysUpdate @Id = @ark, @Status = 'Issued', @UserId = @mgr;
INSERT INTO @rows EXEC dbo.AccessRequestKeysUpdate @Id = @ark, @Status = 'Missing', @UserId = @mgr, @Note = N'CI';
INSERT INTO @rows EXEC dbo.AccessRequestKeysUpdate @Id = @ark, @Status = 'Returned', @UserId = @mgr;
INSERT INTO @rows EXEC dbo.AccessRequestKeysUpdate @Id = @ark, @Status = 'Issued', @UserId = @mgr;
IF (SELECT SUM(n) FROM @rows) <> 3 THROW 60013, N'FAIL: key status moves not guarded as expected.', 1;
IF (SELECT [Status] FROM dbo.AccessRequestKeys WHERE Id = @ark) <> 'Returned' THROW 60014, N'FAIL: key should end Returned.', 1;

/* One strike per group, not per room. */
DECLARE @s TABLE (OpenCount INT);
INSERT INTO @s EXEC dbo.WorkerStrikeAdd @WorkerId = @w, @Kind = 'Overdue', @AccessRequestId = @a1, @Detail = N'CI';
INSERT INTO @s EXEC dbo.WorkerStrikeAdd @WorkerId = @w, @Kind = 'Overdue', @AccessRequestId = @a2, @Detail = N'CI';
IF (SELECT COUNT(*) FROM dbo.WorkerStrikes WHERE WorkerId = @w AND Kind = 'Overdue') <> 1
    THROW 60015, N'FAIL: a multi-room visit earned more than one strike.', 1;

/* Group read for the approver. */
DECLARE @grp TABLE (n INT);
INSERT INTO @grp SELECT 1 FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.AccessRequestGroupGet @GroupId = 1', NULL, 0) WHERE name = 'ApprovalId';
IF NOT EXISTS (SELECT 1 FROM @grp) THROW 60016, N'FAIL: AccessRequestGroupGet has no ApprovalId column.', 1;

PRINT 'multiroom-keys tests passed';
GO
