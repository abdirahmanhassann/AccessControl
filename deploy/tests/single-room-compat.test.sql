/* CI test: single-room requests (the path every request used before
   MultiRoomKeys.sql) behave exactly as before. Any failed check THROWs. */
USE [AccessControl]
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @now DATETIME2(0) = SYSDATETIME();
DECLARE @from DATETIME2(0) = DATEADD(HOUR, 1, @now), @to DATETIME2(0) = DATEADD(HOUR, 3, @now);

INSERT INTO dbo.Sites ([Name], [Reference], [IsActive], [CreatedAt]) VALUES (N'Compat site', CONCAT(N'CI-', NEWID()), 1, @now);
DECLARE @site INT = SCOPE_IDENTITY();
INSERT INTO dbo.WorkAreas ([SiteId], [Name]) VALUES (@site, N'Compat tower');
DECLARE @wa INT = SCOPE_IDENTITY();
INSERT INTO dbo.Rooms ([WorkAreaId], [RoomNumber], [Name]) VALUES (@wa, N'9.01', N'Apartment 9.01');
DECLARE @room INT = SCOPE_IDENTITY();
INSERT INTO dbo.Users ([FirstName], [LastName], [Email], [Role], [Password], [salt])
VALUES (N'Compat', N'Manager', N'compat.manager@example.invalid', N'Manager', N'x', N'x');
DECLARE @mgr INT = SCOPE_IDENTITY();
INSERT INTO dbo.Workers ([FirstName], [LastName], [PhoneNumber]) VALUES (N'Compat', N'Worker', N'+440000000009');
DECLARE @w INT = SCOPE_IDENTITY();

/* The original single-room insert still works and leaves GroupId empty. */
DECLARE @ids TABLE (Id INT);
INSERT INTO @ids EXEC dbo.AccessRequestsInsert @WorkerId = @w, @RoomId = @room, @Reason = N'Old path',
    @WorkType = N'General', @WorkFrom = @from, @WorkTo = @to;
DECLARE @a1 INT = (SELECT TOP 1 Id FROM @ids); DELETE FROM @ids;
IF (SELECT [GroupId] FROM dbo.AccessRequests WHERE Id = @a1) IS NOT NULL
    THROW 61001, N'FAIL: a single-room request got a GroupId.', 1;

/* The same room twice without a group is still allowed (the unique index is only for groups). */
INSERT INTO @ids EXEC dbo.AccessRequestsInsert @WorkerId = @w, @RoomId = @room, @Reason = N'Old path again',
    @WorkType = N'General', @WorkFrom = @from, @WorkTo = @to;
DECLARE @a2 INT = (SELECT TOP 1 Id FROM @ids); DELETE FROM @ids;
IF @a2 IS NULL OR @a2 = @a1 THROW 61002, N'FAIL: a second single-room request for the same room was refused.', 1;

/* AccessRequestsGet still returns the row, with no group. */
DECLARE @cols TABLE (name SYSNAME, column_ordinal INT);
INSERT INTO @cols SELECT name, column_ordinal FROM sys.dm_exec_describe_first_result_set(N'EXEC dbo.AccessRequestsGet', NULL, 0);
IF (SELECT MIN(column_ordinal) FROM @cols WHERE name = 'Id') <> 1
    THROW 61003, N'FAIL: AccessRequestsGet no longer starts with Id.', 1;
DECLARE @dupes INT = (SELECT COUNT(*) - COUNT(DISTINCT name) FROM @cols);
IF @dupes <> 0 THROW 61004, N'FAIL: AccessRequestsGet returns a column twice.', 1;

/* Approvals: insert and update as before. */
DECLARE @appr TABLE (Id INT);
INSERT INTO @appr EXEC dbo.AccessRequestApprovalsInsert @AccessRequestId = @a1, @ApproverUserId = @mgr;
DECLARE @apprId INT = (SELECT TOP 1 Id FROM @appr);
DECLARE @rows TABLE (n INT);
INSERT INTO @rows EXEC dbo.AccessRequestApprovalsUpdate @Id = @apprId, @Status = N'Approved', @Comment = NULL;
IF (SELECT TOP 1 n FROM @rows) <> 1 THROW 61005, N'FAIL: approval update changed nothing.', 1;

/* A single-room request has no keys, so deciding keys or claiming a group notification is a no-op. */
DELETE FROM @rows;
INSERT INTO @rows EXEC dbo.AccessRequestKeysDecide @AccessRequestId = @a1, @Approved = 1;
IF (SELECT TOP 1 n FROM @rows) <> 0 THROW 61006, N'FAIL: keys changed on a request without keys.', 1;
DECLARE @claim TABLE (Claimed INT);
INSERT INTO @claim EXEC dbo.AccessRequestGroupsClaimNotification @GroupId = 2147483000;
IF (SELECT TOP 1 Claimed FROM @claim) <> 0 THROW 61007, N'FAIL: claimed a notification for a group that does not exist.', 1;

/* Strikes on separate single-room visits still count separately; the same visit only once. */
DECLARE @s TABLE (OpenCount INT);
INSERT INTO @s EXEC dbo.WorkerStrikeAdd @WorkerId = @w, @Kind = 'PhotoRejected', @AccessRequestId = @a1, @Detail = N'one';
INSERT INTO @s EXEC dbo.WorkerStrikeAdd @WorkerId = @w, @Kind = 'PhotoRejected', @AccessRequestId = @a1, @Detail = N'again';
INSERT INTO @s EXEC dbo.WorkerStrikeAdd @WorkerId = @w, @Kind = 'PhotoRejected', @AccessRequestId = @a2, @Detail = N'two';
IF (SELECT COUNT(*) FROM dbo.WorkerStrikes WHERE WorkerId = @w AND Kind = 'PhotoRejected') <> 2
    THROW 61008, N'FAIL: strikes on separate single-room visits should count separately.', 1;
IF (SELECT TOP 1 OpenCount FROM @s ORDER BY OpenCount DESC) <> 2 THROW 61009, N'FAIL: open strike count wrong.', 1;

/* A returned key is available again. */
DECLARE @t TABLE (Id INT);
INSERT INTO @t EXEC dbo.KeysInsert @RoomId = @room, @Label = N'9.01-A';
DECLARE @k INT = (SELECT TOP 1 Id FROM @t);
INSERT INTO dbo.AccessRequestKeys (AccessRequestId, KeyId, [Status], ReturnBy) VALUES (@a1, @k, 'Returned', @to);
DECLARE @keys TABLE (Id INT, RoomId INT, Label NVARCHAR(100), TagNumber NVARCHAR(50), Notes NVARCHAR(500), IsActive BIT, CreatedAt DATETIME2(0),
    RoomNumber NVARCHAR(100), RoomName NVARCHAR(200), WorkAreaId INT, WorkAreaName NVARCHAR(200), SiteId INT, Available BIT, HeldBy NVARCHAR(250));
INSERT INTO @keys EXEC dbo.KeysGet @RoomId = @room, @WorkFrom = @from, @WorkTo = @to;
IF (SELECT Available FROM @keys WHERE Id = @k) <> 1 THROW 61010, N'FAIL: a returned key is not available again.', 1;

/* A retired key is hidden unless asked for. */
DELETE FROM @rows;
INSERT INTO @rows EXEC dbo.KeysUpdate @Id = @k, @Label = N'9.01-A', @IsActive = 0;
DELETE FROM @keys;
INSERT INTO @keys EXEC dbo.KeysGet @RoomId = @room;
IF EXISTS (SELECT 1 FROM @keys WHERE Id = @k) THROW 61011, N'FAIL: a retired key is still offered.', 1;
DELETE FROM @keys;
INSERT INTO @keys EXEC dbo.KeysGet @RoomId = @room, @IncludeInactive = 1;
IF NOT EXISTS (SELECT 1 FROM @keys WHERE Id = @k) THROW 61012, N'FAIL: a retired key is missing from the register.', 1;

PRINT 'single-room compat tests passed';
GO
