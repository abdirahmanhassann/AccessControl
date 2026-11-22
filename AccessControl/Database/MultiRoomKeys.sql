USE [AccessControl]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* Multi-room access requests and keys.

   One submission from a worker is an AccessRequestGroups row. Each room in it
   stays an ordinary AccessRequests row (GroupId points at the group), so the
   per-room approval, photos, room-leave review and Overdue logic keep working
   unchanged. The manager accepts or rejects each room; the worker hears once,
   when every room in the group has been decided (NotifiedAt).

   Keys belong to one room. A worker asks for keys with the rooms they request;
   AccessRequestKeys tracks each one from Requested through Approved, Issued
   (Keyman hands it over) and Returned or Missing.

   Safe to run more than once: the deploy pipeline runs it before every API
   deploy (deploy/migrations.txt). */

/* 1. Groups. */
IF OBJECT_ID(N'[dbo].[AccessRequestGroups]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessRequestGroups](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [WorkerId] INT NOT NULL,
        [ApproverUserId] INT NULL,
        [Reason] NVARCHAR(500) NULL,
        [WorkType] NVARCHAR(200) NULL,
        [Description] NVARCHAR(1000) NULL,
        [SupervisorName] NVARCHAR(200) NULL,
        [WorkFrom] DATETIME2(0) NULL,
        [WorkTo] DATETIME2(0) NULL,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_AccessRequestGroups_CreatedAt] DEFAULT (GETDATE()),
        /* Set once, when the worker has been told the outcome of every room. */
        [NotifiedAt] DATETIME2(0) NULL,
        CONSTRAINT [PK_AccessRequestGroups] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessRequestGroups_Workers] FOREIGN KEY ([WorkerId])
            REFERENCES [dbo].[Workers] ([Id]),
        CONSTRAINT [FK_AccessRequestGroups_Users] FOREIGN KEY ([ApproverUserId])
            REFERENCES [dbo].[Users] ([Id])
    );
END
GO

IF COL_LENGTH('dbo.AccessRequests', 'GroupId') IS NULL
    ALTER TABLE [dbo].[AccessRequests] ADD [GroupId] INT NULL;
GO

IF OBJECT_ID(N'[dbo].[FK_AccessRequests_AccessRequestGroups]', N'F') IS NULL
    ALTER TABLE [dbo].[AccessRequests] WITH CHECK
        ADD CONSTRAINT [FK_AccessRequests_AccessRequestGroups] FOREIGN KEY ([GroupId])
        REFERENCES [dbo].[AccessRequestGroups] ([Id]);
GO

/* A room appears once per group. Filtered, so older single-room rows (no group) are unaffected. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'UX_AccessRequests_Group_Room'
               AND [object_id] = OBJECT_ID(N'[dbo].[AccessRequests]'))
    CREATE UNIQUE NONCLUSTERED INDEX [UX_AccessRequests_Group_Room]
        ON [dbo].[AccessRequests] ([GroupId], [RoomId])
        WHERE [GroupId] IS NOT NULL;
GO

/* 2. Keys: each one opens exactly one room. */
IF OBJECT_ID(N'[dbo].[Keys]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Keys](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RoomId] INT NOT NULL,
        [Label] NVARCHAR(100) NOT NULL,
        [TagNumber] NVARCHAR(50) NULL,
        [Notes] NVARCHAR(500) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Keys_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_Keys_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_Keys] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Keys_Rooms] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([Id]),
        CONSTRAINT [UQ_Keys_Room_Label] UNIQUE ([RoomId], [Label])
    );
END
GO

/* 3. Keys asked for on an access request (one room of a group). */
IF OBJECT_ID(N'[dbo].[AccessRequestKeys]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessRequestKeys](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [AccessRequestId] INT NOT NULL,
        [KeyId] INT NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_AccessRequestKeys_Status] DEFAULT ('Requested'),
        [ReturnBy] DATETIME2(0) NULL,
        [IssuedAt] DATETIME2(0) NULL,
        [IssuedByUserId] INT NULL,
        [ReturnedAt] DATETIME2(0) NULL,
        [ReturnedToUserId] INT NULL,
        [Note] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_AccessRequestKeys_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_AccessRequestKeys] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_AccessRequestKeys_Request_Key] UNIQUE ([AccessRequestId], [KeyId]),
        CONSTRAINT [FK_AccessRequestKeys_AccessRequests] FOREIGN KEY ([AccessRequestId])
            REFERENCES [dbo].[AccessRequests] ([Id]),
        CONSTRAINT [FK_AccessRequestKeys_Keys] FOREIGN KEY ([KeyId]) REFERENCES [dbo].[Keys] ([Id]),
        CONSTRAINT [FK_AccessRequestKeys_IssuedBy] FOREIGN KEY ([IssuedByUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [FK_AccessRequestKeys_ReturnedTo] FOREIGN KEY ([ReturnedToUserId]) REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [CK_AccessRequestKeys_Status]
            CHECK ([Status] IN ('Requested', 'Approved', 'Rejected', 'Issued', 'Returned', 'Missing'))
    );
    CREATE NONCLUSTERED INDEX [IX_AccessRequestKeys_Key_Status]
        ON [dbo].[AccessRequestKeys] ([KeyId], [Status]);
END
GO

/* 4. AccessRequestsGet also returns GroupId and how many rooms the group has.
      Edited in place rather than replaced, so whichever version of the
      procedure this database already runs keeps its own columns and filters. */
DECLARE @def NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID(N'[dbo].[AccessRequestsGet]'));
IF @def IS NOT NULL AND CHARINDEX(N'[GroupSize]', @def) = 0
BEGIN
    DECLARE @col INT = CHARINDEX(N'ar.[Id],', @def);
    DECLARE @proc INT = CHARINDEX(N'PROCEDURE', @def);
    IF @col > 0 AND @proc > 0
    BEGIN
        SET @def = STUFF(@def, @col, LEN(N'ar.[Id],'),
            N'ar.[Id], ar.[GroupId], (SELECT COUNT(*) FROM [dbo].[AccessRequests] grp WHERE grp.[GroupId] = ar.[GroupId]) AS [GroupSize],');
        /* "CREATE [OR ALTER] PROCEDURE" -> "ALTER PROCEDURE" */
        DECLARE @head NVARCHAR(MAX) = LEFT(@def, @proc - 1);
        DECLARE @create INT = DATALENGTH(@head) / 2 - CHARINDEX(N'ETAERC', REVERSE(@head)) - 4;
        IF @create > 0
        BEGIN
            SET @def = LEFT(@def, @create - 1) + N'ALTER ' + SUBSTRING(@def, @proc, DATALENGTH(@def) / 2);
            EXEC sys.sp_executesql @def;
        END
    END
END
GO

/* 5. One strike per visit: a late sign-out (or a rejected room leave) on a
      three-room request is one incident, not three. Same as AutoBlock.sql's
      version otherwise; this file runs after it, so this version wins. */
CREATE OR ALTER PROCEDURE [dbo].[WorkerStrikeAdd]
    @WorkerId INT,
    @Kind NVARCHAR(30),
    @AccessRequestId INT = NULL,
    @Detail NVARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @WorkerId IS NULL OR @WorkerId < 1
    BEGIN
        SELECT CAST(0 AS INT) AS [OpenCount];
        RETURN;
    END

    DECLARE @GroupId INT = (SELECT [GroupId] FROM [dbo].[AccessRequests] WHERE [Id] = @AccessRequestId);

    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[WorkerStrikes] s
        WHERE s.[WorkerId] = @WorkerId
          AND s.[Kind] = @Kind
          AND s.[AccessRequestId] IS NOT NULL
          AND (s.[AccessRequestId] = @AccessRequestId
               OR (@GroupId IS NOT NULL AND s.[AccessRequestId] IN (
                    SELECT [Id] FROM [dbo].[AccessRequests] WHERE [GroupId] = @GroupId))))
    BEGIN
        INSERT INTO [dbo].[WorkerStrikes] ([WorkerId], [AccessRequestId], [Kind], [Detail])
        VALUES (@WorkerId, @AccessRequestId, @Kind, @Detail);
    END

    SELECT COUNT(*) AS [OpenCount]
    FROM [dbo].[WorkerStrikes]
    WHERE [WorkerId] = @WorkerId
      AND [Kind] = @Kind
      AND [ForgivenAt] IS NULL;
END
GO

/* 6. Keys. Available is worked out for a time frame when one is given: a key is
      taken if it is requested, approved or out (issued) on a visit that overlaps. */
CREATE OR ALTER PROCEDURE [dbo].[KeysGet]
    @Id INT = NULL,
    @RoomId INT = NULL,
    @WorkAreaId INT = NULL,
    @SiteId INT = NULL,
    @IncludeInactive BIT = 0,
    @WorkFrom DATETIME2(0) = NULL,
    @WorkTo DATETIME2(0) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        k.[Id],
        k.[RoomId],
        k.[Label],
        k.[TagNumber],
        k.[Notes],
        k.[IsActive],
        k.[CreatedAt],
        r.[RoomNumber],
        r.[Name] AS [RoomName],
        r.[WorkAreaId],
        wa.[Name] AS [WorkAreaName],
        wa.[SiteId],
        CAST(CASE WHEN k.[IsActive] = 1 AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[AccessRequestKeys] ark
            INNER JOIN [dbo].[AccessRequests] ar ON ar.[Id] = ark.[AccessRequestId]
            WHERE ark.[KeyId] = k.[Id]
              AND (
                    ark.[Status] IN ('Issued', 'Missing')
                    OR (ark.[Status] IN ('Requested', 'Approved')
                        AND @WorkFrom IS NOT NULL AND @WorkTo IS NOT NULL
                        AND ISNULL(ar.[WorkFrom], '00010101') < @WorkTo
                        AND ISNULL(ar.[WorkTo], '99991231') > @WorkFrom)
                  )
        ) THEN 1 ELSE 0 END AS BIT) AS [Available],
        (SELECT TOP 1 CONCAT(w.[FirstName], N' ', w.[LastName])
           FROM [dbo].[AccessRequestKeys] ark
           INNER JOIN [dbo].[AccessRequests] ar ON ar.[Id] = ark.[AccessRequestId]
           INNER JOIN [dbo].[Workers] w ON w.[Id] = ar.[WorkerId]
          WHERE ark.[KeyId] = k.[Id] AND ark.[Status] = 'Issued'
          ORDER BY ark.[IssuedAt] DESC) AS [HeldBy]
    FROM [dbo].[Keys] k
    INNER JOIN [dbo].[Rooms] r ON r.[Id] = k.[RoomId]
    INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    WHERE (@Id IS NULL OR k.[Id] = @Id)
      AND (@RoomId IS NULL OR k.[RoomId] = @RoomId)
      AND (@WorkAreaId IS NULL OR r.[WorkAreaId] = @WorkAreaId)
      AND (@SiteId IS NULL OR wa.[SiteId] = @SiteId)
      AND (@IncludeInactive = 1 OR k.[IsActive] = 1)
    ORDER BY r.[RoomNumber], k.[Label];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[KeysInsert]
    @RoomId INT,
    @Label NVARCHAR(100),
    @TagNumber NVARCHAR(50) = NULL,
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[Keys] ([RoomId], [Label], [TagNumber], [Notes])
    VALUES (@RoomId, LTRIM(RTRIM(@Label)), NULLIF(LTRIM(RTRIM(@TagNumber)), N''), NULLIF(LTRIM(RTRIM(@Notes)), N''));
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[KeysUpdate]
    @Id INT,
    @Label NVARCHAR(100),
    @TagNumber NVARCHAR(50) = NULL,
    @Notes NVARCHAR(500) = NULL,
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Keys]
    SET [Label] = LTRIM(RTRIM(@Label)),
        [TagNumber] = NULLIF(LTRIM(RTRIM(@TagNumber)), N''),
        [Notes] = NULLIF(LTRIM(RTRIM(@Notes)), N''),
        [IsActive] = @IsActive
    WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

/* 7. Submit a multi-room request in one transaction: the group, one access
      request and one pending approval per room, and the keys asked for.
      @Items is JSON: [{"roomId": 12, "keyIds": [3, 4], "keyLabels": ["Gate key"]}, ...]
      keyLabels are keys the worker typed: matched to the room's key with the same name
      (ignoring capitals, spaces, hyphens, underscores), or added to the register.
      Fails as a whole (nothing saved) if a key does not open that room or is
      already taken for an overlapping time frame. */
CREATE OR ALTER PROCEDURE [dbo].[AccessRequestGroupsInsert]
    @WorkerId INT,
    @ApproverUserId INT,
    @Reason NVARCHAR(500) = NULL,
    @WorkType NVARCHAR(200) = NULL,
    @Description NVARCHAR(1000) = NULL,
    @SupervisorName NVARCHAR(200) = NULL,
    @WorkFrom DATETIME2(0) = NULL,
    @WorkTo DATETIME2(0) = NULL,
    @Items NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @rooms TABLE ([Seq] INT IDENTITY(1,1), [RoomId] INT NOT NULL, [KeyIds] NVARCHAR(MAX) NULL, [KeyLabels] NVARCHAR(MAX) NULL);
    INSERT INTO @rooms ([RoomId], [KeyIds], [KeyLabels])
    SELECT j.[roomId], j.[keyIds], j.[keyLabels]
    FROM OPENJSON(@Items) WITH ([roomId] INT '$.roomId', [keyIds] NVARCHAR(MAX) '$.keyIds' AS JSON,
                                [keyLabels] NVARCHAR(MAX) '$.keyLabels' AS JSON) j
    WHERE j.[roomId] IS NOT NULL AND j.[roomId] > 0;

    /* The same room twice keeps its first entry. */
    WITH dupes AS (
        SELECT [Seq], ROW_NUMBER() OVER (PARTITION BY [RoomId] ORDER BY [Seq]) AS [Rn] FROM @rooms)
    DELETE FROM dupes WHERE [Rn] > 1;

    IF NOT EXISTS (SELECT 1 FROM @rooms)
        THROW 50010, N'Choose at least one room.', 1;

    DECLARE @keys TABLE ([RoomId] INT NOT NULL, [KeyId] INT NOT NULL);
    INSERT INTO @keys ([RoomId], [KeyId])
    SELECT DISTINCT r.[RoomId], CAST(k.[value] AS INT)
    FROM @rooms r
    CROSS APPLY OPENJSON(ISNULL(r.[KeyIds], N'[]')) k;

    DECLARE @bad NVARCHAR(400);

    BEGIN TRY
        /* Checks and inserts in one transaction; the locks stop two people booking the same key at once. */
        BEGIN TRANSACTION;

        /* Keys the worker typed: use the room's key with the same name (ignoring capitals,
           spaces, hyphens and underscores), bringing a retired one back, or register a new one. */
        DECLARE @typed TABLE ([RoomId] INT NOT NULL, [Label] NVARCHAR(100) NOT NULL, [Norm] NVARCHAR(100) NOT NULL, [KeyId] INT NULL);
        INSERT INTO @typed ([RoomId], [Label], [Norm])
        SELECT t.[RoomId], t.[Label], t.[Norm]
        FROM (
            SELECT r.[RoomId], x.[Label], x.[Norm],
                   ROW_NUMBER() OVER (PARTITION BY r.[RoomId], x.[Norm] ORDER BY r.[Seq], CAST(l.[key] AS INT)) AS [Rn]
            FROM @rooms r
            CROSS APPLY OPENJSON(ISNULL(r.[KeyLabels], N'[]')) l
            CROSS APPLY (SELECT LEFT(LTRIM(RTRIM(CAST(l.[value] AS NVARCHAR(400)))), 100) AS [Label]) v
            CROSS APPLY (SELECT v.[Label] AS [Label],
                                UPPER(REPLACE(REPLACE(REPLACE(v.[Label], N' ', N''), N'-', N''), N'_', N'')) AS [Norm]) x
            WHERE l.[type] = 1 AND x.[Norm] <> N''
        ) t
        WHERE t.[Rn] = 1;

        IF EXISTS (SELECT 1 FROM @typed)
        BEGIN
            UPDATE t SET [KeyId] = m.[Id]
            FROM @typed t
            CROSS APPLY (
                SELECT TOP 1 k.[Id]
                FROM [dbo].[Keys] k WITH (UPDLOCK, HOLDLOCK)
                WHERE k.[RoomId] = t.[RoomId]
                  AND UPPER(REPLACE(REPLACE(REPLACE(k.[Label], N' ', N''), N'-', N''), N'_', N'')) = t.[Norm]
                ORDER BY k.[IsActive] DESC, k.[Id]
            ) m;

            UPDATE k SET [IsActive] = 1
            FROM [dbo].[Keys] k
            INNER JOIN @typed t ON t.[KeyId] = k.[Id]
            WHERE k.[IsActive] = 0;

            DECLARE @newKeys TABLE ([Id] INT NOT NULL, [RoomId] INT NOT NULL, [Label] NVARCHAR(100) NOT NULL);
            INSERT INTO [dbo].[Keys] ([RoomId], [Label], [Notes])
            OUTPUT inserted.[Id], inserted.[RoomId], inserted.[Label] INTO @newKeys ([Id], [RoomId], [Label])
            SELECT t.[RoomId], t.[Label], N'Added from a worker request'
            FROM @typed t
            WHERE t.[KeyId] IS NULL;

            UPDATE t SET [KeyId] = n.[Id]
            FROM @typed t
            INNER JOIN @newKeys n ON n.[RoomId] = t.[RoomId] AND n.[Label] = t.[Label]
            WHERE t.[KeyId] IS NULL;

            INSERT INTO @keys ([RoomId], [KeyId])
            SELECT DISTINCT t.[RoomId], t.[KeyId]
            FROM @typed t
            WHERE t.[KeyId] IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM @keys x WHERE x.[RoomId] = t.[RoomId] AND x.[KeyId] = t.[KeyId]);
        END

        SELECT TOP 1 @bad = CONCAT(N'Key #', x.[KeyId], N' does not open that room.')
        FROM @keys x
        LEFT JOIN [dbo].[Keys] k ON k.[Id] = x.[KeyId] AND k.[RoomId] = x.[RoomId] AND k.[IsActive] = 1
        WHERE k.[Id] IS NULL;
        IF @bad IS NOT NULL THROW 50011, @bad, 1;

        SELECT TOP 1 @bad = CONCAT(N'Key ', k.[Label], N' is already booked or out for that time. Choose another key or time.')
        FROM @keys x
        INNER JOIN [dbo].[Keys] k ON k.[Id] = x.[KeyId]
        WHERE EXISTS (
            SELECT 1
            FROM [dbo].[AccessRequestKeys] ark WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN [dbo].[AccessRequests] ar ON ar.[Id] = ark.[AccessRequestId]
            WHERE ark.[KeyId] = x.[KeyId]
              AND (ark.[Status] IN ('Issued', 'Missing')
                   OR (ark.[Status] IN ('Requested', 'Approved')
                       AND ISNULL(ar.[WorkFrom], '00010101') < ISNULL(@WorkTo, '99991231')
                       AND ISNULL(ar.[WorkTo], '99991231') > ISNULL(@WorkFrom, '00010101'))));
        IF @bad IS NOT NULL THROW 50012, @bad, 1;

        INSERT INTO [dbo].[AccessRequestGroups]
            ([WorkerId], [ApproverUserId], [Reason], [WorkType], [Description], [SupervisorName], [WorkFrom], [WorkTo])
        VALUES
            (@WorkerId, @ApproverUserId, @Reason, @WorkType, @Description, @SupervisorName, @WorkFrom, @WorkTo);
        DECLARE @GroupId INT = CAST(SCOPE_IDENTITY() AS INT);

        DECLARE @created TABLE ([AccessRequestId] INT NOT NULL, [RoomId] INT NOT NULL);

        INSERT INTO [dbo].[AccessRequests]
            ([WorkerId], [RoomId], [Reason], [WorkType], [Description], [SupervisorName],
             [WorkFrom], [WorkTo], [ExpectedClockOutAt], [CreatedAt], [GroupId])
        OUTPUT inserted.[Id], inserted.[RoomId] INTO @created ([AccessRequestId], [RoomId])
        SELECT @WorkerId, r.[RoomId], @Reason, @WorkType, @Description, @SupervisorName,
               @WorkFrom, @WorkTo, @WorkTo, GETDATE(), @GroupId
        FROM @rooms r
        ORDER BY r.[Seq];

        INSERT INTO [dbo].[AccessRequestApprovals] ([AccessRequestId], [ApproverUserId], [Status])
        SELECT c.[AccessRequestId], @ApproverUserId, 'PENDING'
        FROM @created c;

        INSERT INTO [dbo].[AccessRequestKeys] ([AccessRequestId], [KeyId], [Status], [ReturnBy])
        SELECT c.[AccessRequestId], x.[KeyId], 'Requested', @WorkTo
        FROM @created c
        INNER JOIN @keys x ON x.[RoomId] = c.[RoomId];

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        /* Undo everything, then pass the same error (50011/50012 etc.) back to the caller. */
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    SELECT @GroupId AS [GroupId], c.[AccessRequestId], c.[RoomId]
    FROM @created c
    ORDER BY c.[AccessRequestId];
END
GO

/* 8. Every room of a group with its latest approval, for the approver and the worker. */
CREATE OR ALTER PROCEDURE [dbo].[AccessRequestGroupGet]
    @GroupId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ar.[GroupId],
        ar.[Id] AS [AccessRequestId],
        ar.[WorkerId],
        w.[FirstName] AS [WorkerFirstName],
        w.[LastName] AS [WorkerLastName],
        w.[PhoneNumber] AS [WorkerPhoneNumber],
        ar.[RoomId],
        r.[RoomNumber],
        r.[Name] AS [RoomName],
        r.[WorkAreaId],
        wa.[Name] AS [WorkAreaName],
        ar.[Status],
        ar.[Reason],
        ar.[WorkType],
        ar.[Description],
        ar.[SupervisorName],
        ar.[WorkFrom],
        ar.[WorkTo],
        ar.[ApprovedAt],
        ar.[RejectedAt],
        ar.[ClockedInAt],
        ar.[ClockedOutAt],
        ar.[CompletedAt],
        ara.[Id] AS [ApprovalId],
        ara.[ApproverUserId],
        ara.[Status] AS [ApprovalStatus],
        ara.[Comment],
        ara.[ReviewedAt],
        (SELECT COUNT(*) FROM [dbo].[AccessRequestKeys] ark WHERE ark.[AccessRequestId] = ar.[Id]) AS [KeyCount]
    FROM [dbo].[AccessRequests] ar
    INNER JOIN [dbo].[Workers] w ON w.[Id] = ar.[WorkerId]
    INNER JOIN [dbo].[Rooms] r ON r.[Id] = ar.[RoomId]
    LEFT JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    OUTER APPLY (
        SELECT TOP 1 a.[Id], a.[ApproverUserId], a.[Status], a.[Comment], a.[ReviewedAt]
        FROM [dbo].[AccessRequestApprovals] a
        WHERE a.[AccessRequestId] = ar.[Id]
        ORDER BY a.[Id] DESC
    ) ara
    WHERE ar.[GroupId] = @GroupId
    ORDER BY ar.[Id];
END
GO

/* 9. Keys on requests, for the worker, the approver and the Keyman's lists. */
CREATE OR ALTER PROCEDURE [dbo].[AccessRequestKeysGet]
    @Id INT = NULL,
    @AccessRequestId INT = NULL,
    @GroupId INT = NULL,
    @Status NVARCHAR(20) = NULL,
    @Outstanding BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ark.[Id],
        ark.[AccessRequestId],
        ar.[GroupId],
        ark.[KeyId],
        k.[Label] AS [KeyLabel],
        k.[TagNumber],
        ark.[Status],
        ark.[ReturnBy],
        ark.[IssuedAt],
        ark.[IssuedByUserId],
        ark.[ReturnedAt],
        ark.[ReturnedToUserId],
        ark.[Note],
        ark.[CreatedAt],
        ar.[WorkerId],
        w.[FirstName] AS [WorkerFirstName],
        w.[LastName] AS [WorkerLastName],
        w.[PhoneNumber] AS [WorkerPhoneNumber],
        w.[CompanyName],
        ar.[RoomId],
        r.[RoomNumber],
        r.[Name] AS [RoomName],
        wa.[Name] AS [WorkAreaName],
        ar.[Status] AS [RequestStatus],
        ar.[WorkFrom],
        ar.[WorkTo],
        ar.[ClockedInAt],
        ar.[ClockedOutAt]
    FROM [dbo].[AccessRequestKeys] ark
    INNER JOIN [dbo].[Keys] k ON k.[Id] = ark.[KeyId]
    INNER JOIN [dbo].[AccessRequests] ar ON ar.[Id] = ark.[AccessRequestId]
    INNER JOIN [dbo].[Workers] w ON w.[Id] = ar.[WorkerId]
    INNER JOIN [dbo].[Rooms] r ON r.[Id] = ar.[RoomId]
    LEFT JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    WHERE (@Id IS NULL OR ark.[Id] = @Id)
      AND (@AccessRequestId IS NULL OR ark.[AccessRequestId] = @AccessRequestId)
      AND (@GroupId IS NULL OR ar.[GroupId] = @GroupId)
      AND (@Status IS NULL OR ark.[Status] = @Status)
      AND (@Outstanding = 0 OR ark.[Status] IN ('Approved', 'Issued', 'Missing'))
    ORDER BY ark.[ReturnBy], k.[Label];
END
GO

/* 10. When a room is approved or rejected, its requested keys follow. */
CREATE OR ALTER PROCEDURE [dbo].[AccessRequestKeysDecide]
    @AccessRequestId INT,
    @Approved BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE ark
    SET ark.[Status] = CASE WHEN @Approved = 1 THEN 'Approved' ELSE 'Rejected' END,
        ark.[ReturnBy] = COALESCE(ar.[WorkTo], ar.[ExpectedClockOutAt], ark.[ReturnBy])
    FROM [dbo].[AccessRequestKeys] ark
    INNER JOIN [dbo].[AccessRequests] ar ON ar.[Id] = ark.[AccessRequestId]
    WHERE ark.[AccessRequestId] = @AccessRequestId
      AND ark.[Status] = 'Requested';
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

/* 11. The Keyman moves a key along: Approved -> Issued -> Returned, or Issued -> Missing
       (and Missing -> Returned if it turns up).
       Returns 1 if the key was in a state that allows the move, else 0. */
CREATE OR ALTER PROCEDURE [dbo].[AccessRequestKeysUpdate]
    @Id INT,
    @Status NVARCHAR(20),
    @UserId INT,
    @Note NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[AccessRequestKeys]
    SET [Status] = @Status,
        [IssuedAt] = CASE WHEN @Status = 'Issued' THEN GETDATE() ELSE [IssuedAt] END,
        [IssuedByUserId] = CASE WHEN @Status = 'Issued' THEN @UserId ELSE [IssuedByUserId] END,
        [ReturnedAt] = CASE WHEN @Status = 'Returned' THEN GETDATE() ELSE [ReturnedAt] END,
        [ReturnedToUserId] = CASE WHEN @Status = 'Returned' THEN @UserId ELSE [ReturnedToUserId] END,
        [Note] = COALESCE(NULLIF(LTRIM(RTRIM(@Note)), N''), [Note])
    WHERE [Id] = @Id
      AND (
            (@Status = 'Issued' AND [Status] = 'Approved')
         OR (@Status = 'Returned' AND [Status] IN ('Issued', 'Missing'))
         OR (@Status = 'Missing' AND [Status] = 'Issued')
          );
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

/* 12. Claim the one-off "all rooms decided" notification for a group.
       Returns 1 to exactly one caller, once every room has a decision. */
CREATE OR ALTER PROCEDURE [dbo].[AccessRequestGroupsClaimNotification]
    @GroupId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE g
    SET g.[NotifiedAt] = GETDATE()
    FROM [dbo].[AccessRequestGroups] g
    WHERE g.[Id] = @GroupId
      AND g.[NotifiedAt] IS NULL
      AND NOT EXISTS (
            SELECT 1 FROM [dbo].[AccessRequests] ar
            WHERE ar.[GroupId] = g.[Id] AND ar.[Status] = 'PENDING');
    SELECT @@ROWCOUNT AS [Claimed];
END
GO

/* 13. Late-key reminders: when a reminder was sent for a key that is still out
       after its return-by time, so each late key is reminded about once. */
IF COL_LENGTH(N'dbo.AccessRequestKeys', N'LateNotifiedAt') IS NULL
    ALTER TABLE [dbo].[AccessRequestKeys] ADD [LateNotifiedAt] DATETIME2(0) NULL;
GO

/* 14. Claim the keys that are out past their return-by time and not reminded about yet.
       @Now is the API's local time (the same clock the return-by times are written in).
       Each key is returned to exactly one caller, once. */
CREATE OR ALTER PROCEDURE [dbo].[AccessRequestKeysClaimLate]
    @Now DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @claimed TABLE ([Id] INT NOT NULL);
    UPDATE [dbo].[AccessRequestKeys]
    SET [LateNotifiedAt] = @Now
    OUTPUT inserted.[Id] INTO @claimed ([Id])
    WHERE [Status] = 'Issued'
      AND [ReturnBy] IS NOT NULL
      AND [ReturnBy] < @Now
      AND [LateNotifiedAt] IS NULL;
    SELECT [Id] FROM @claimed ORDER BY [Id];
END
GO
