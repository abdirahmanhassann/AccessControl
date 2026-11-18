USE [AccessControl]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* Auto-blocking repeat offenders.

   Two things a worker does earn a strike:

     Overdue          signed out of a room after their approved time frame had
                      already ended (in practice a manager closes these, since
                      the worker is refused a late clock-out).
     PhotoRejected    a Keyman rejected their room leave, which is this system's
                      record of a room left badly or photographed badly.

   When the open strikes of either kind reach the threshold a manager set on the
   Settings tab, the worker is blocked and cannot submit new access requests.
   A manager unblocking them forgives their open strikes, so they start clean
   rather than being re-blocked by the next incident.

   Thresholds live in Settings. A threshold of 0 turns that rule off.
   Safe to run more than once. */

/* 1. Settings: one row per named value, so a new setting is an INSERT and not
      another migration that reshapes a table. */
IF OBJECT_ID(N'[dbo].[Settings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Settings](
        [Name] NVARCHAR(100) NOT NULL,
        [Value] NVARCHAR(400) NOT NULL,
        [UpdatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_Settings_UpdatedAt] DEFAULT (GETDATE()),
        [UpdatedByUserId] INT NULL,
        CONSTRAINT [PK_Settings] PRIMARY KEY CLUSTERED ([Name] ASC),
        CONSTRAINT [FK_Settings_Users] FOREIGN KEY ([UpdatedByUserId])
            REFERENCES [dbo].[Users] ([Id])
    );
END
GO

/* Defaults only on first run; a manager's own numbers are never overwritten. */
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = N'AutoBlockOverdueCount')
    INSERT INTO [dbo].[Settings] ([Name], [Value]) VALUES (N'AutoBlockOverdueCount', N'3');
GO
IF NOT EXISTS (SELECT 1 FROM [dbo].[Settings] WHERE [Name] = N'AutoBlockPhotoRejectedCount')
    INSERT INTO [dbo].[Settings] ([Name], [Value]) VALUES (N'AutoBlockPhotoRejectedCount', N'3');
GO

/* 2. Block state on the worker.

      Separate from IsActive on purpose: IsActive is the manual on/off switch a
      manager already has on the Workers tab, and overloading it would lose the
      difference between "retired this contractor" and "blocked by the rules". */
IF COL_LENGTH('dbo.Workers', 'BlockedAt') IS NULL
    ALTER TABLE [dbo].[Workers] ADD [BlockedAt] DATETIME2(0) NULL;
GO
IF COL_LENGTH('dbo.Workers', 'BlockedReason') IS NULL
    ALTER TABLE [dbo].[Workers] ADD [BlockedReason] NVARCHAR(400) NULL;
GO

/* 3. The strikes themselves, kept as rows rather than a counter so a manager
      can see what a block was built from and so forgiving is auditable. */
IF OBJECT_ID(N'[dbo].[WorkerStrikes]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkerStrikes](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [WorkerId] INT NOT NULL,
        [AccessRequestId] INT NULL,
        [Kind] NVARCHAR(30) NOT NULL,
        [Detail] NVARCHAR(400) NULL,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_WorkerStrikes_CreatedAt] DEFAULT (GETDATE()),
        [ForgivenAt] DATETIME2(0) NULL,
        CONSTRAINT [PK_WorkerStrikes] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_WorkerStrikes_Workers] FOREIGN KEY ([WorkerId])
            REFERENCES [dbo].[Workers] ([Id]),
        CONSTRAINT [CK_WorkerStrikes_Kind] CHECK ([Kind] IN ('Overdue', 'PhotoRejected'))
    );

    CREATE INDEX [IX_WorkerStrikes_Worker]
        ON [dbo].[WorkerStrikes] ([WorkerId], [Kind]) INCLUDE ([ForgivenAt]);
END
GO

/* 4. Read every setting. The caller decides what to do with names it does not
      recognise, so adding a setting never needs this procedure changed. */
CREATE OR ALTER PROCEDURE [dbo].[SettingsGet]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT [Name], [Value], [UpdatedAt], [UpdatedByUserId]
    FROM [dbo].[Settings]
    ORDER BY [Name];
END
GO

/* Insert or update one setting. */
CREATE OR ALTER PROCEDURE [dbo].[SettingsSet]
    @Name NVARCHAR(100),
    @Value NVARCHAR(400),
    @UpdatedByUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[Settings]
    SET [Value] = @Value,
        [UpdatedAt] = GETDATE(),
        [UpdatedByUserId] = @UpdatedByUserId
    WHERE [Name] = @Name;

    IF @@ROWCOUNT = 0
        INSERT INTO [dbo].[Settings] ([Name], [Value], [UpdatedByUserId])
        VALUES (@Name, @Value, @UpdatedByUserId);
END
GO

/* 5. Record a strike and report how many of that kind the worker now has open.

      A visit can be saved more than once (the sign-out is retried, a manager
      edits the row), so the same access request cannot earn the same kind of
      strike twice. The count comes back from the same round trip the insert
      happens in, so the caller never reads a number that another sign-out has
      already moved. */
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

    IF NOT EXISTS (
        SELECT 1 FROM [dbo].[WorkerStrikes]
        WHERE [WorkerId] = @WorkerId
          AND [Kind] = @Kind
          AND [AccessRequestId] IS NOT NULL
          AND [AccessRequestId] = @AccessRequestId)
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

/* Open strike counts, per worker, for the console. */
CREATE OR ALTER PROCEDURE [dbo].[WorkerStrikeCountsGet]
    @WorkerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        [WorkerId],
        SUM(CASE WHEN [Kind] = 'Overdue' THEN 1 ELSE 0 END) AS [OverdueCount],
        SUM(CASE WHEN [Kind] = 'PhotoRejected' THEN 1 ELSE 0 END) AS [PhotoRejectedCount]
    FROM [dbo].[WorkerStrikes]
    WHERE [ForgivenAt] IS NULL
      AND (@WorkerId IS NULL OR [WorkerId] = @WorkerId)
    GROUP BY [WorkerId];
END
GO

/* 6. Block or unblock. Unblocking forgives the open strikes, otherwise the next
      incident would put the worker straight back over the threshold. */
CREATE OR ALTER PROCEDURE [dbo].[WorkerSetBlock]
    @WorkerId INT,
    @Blocked BIT,
    @Reason NVARCHAR(400) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Blocked = 1
    BEGIN
        UPDATE [dbo].[Workers]
        SET [BlockedAt] = CASE WHEN [BlockedAt] IS NULL THEN GETDATE() ELSE [BlockedAt] END,
            [BlockedReason] = @Reason
        WHERE [Id] = @WorkerId;
    END
    ELSE
    BEGIN
        UPDATE [dbo].[Workers]
        SET [BlockedAt] = NULL,
            [BlockedReason] = NULL
        WHERE [Id] = @WorkerId;

        UPDATE [dbo].[WorkerStrikes]
        SET [ForgivenAt] = GETDATE()
        WHERE [WorkerId] = @WorkerId
          AND [ForgivenAt] IS NULL;
    END
END
GO

/* 7. The workers list carries the block state, so the console can show it
      without a second call. */
CREATE OR ALTER PROCEDURE [dbo].[WorkersGet]
    @Id INT = NULL,
    @PhoneNumber NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        w.[Id],
        w.[FirstName],
        w.[LastName],
        w.[PhoneNumber],
        w.[CompanyName],
        w.[IsActive],
        w.[CreatedAt],
        w.[LastSeenAt],
        w.[BlockedAt],
        w.[BlockedReason],
        ISNULL(s.[OverdueCount], 0) AS [OverdueCount],
        ISNULL(s.[PhotoRejectedCount], 0) AS [PhotoRejectedCount]
    FROM [dbo].[Workers] w
    LEFT JOIN (
        SELECT
            [WorkerId],
            SUM(CASE WHEN [Kind] = 'Overdue' THEN 1 ELSE 0 END) AS [OverdueCount],
            SUM(CASE WHEN [Kind] = 'PhotoRejected' THEN 1 ELSE 0 END) AS [PhotoRejectedCount]
        FROM [dbo].[WorkerStrikes]
        WHERE [ForgivenAt] IS NULL
        GROUP BY [WorkerId]
    ) s ON s.[WorkerId] = w.[Id]
    WHERE (@Id IS NULL OR w.[Id] = @Id)
      AND (@PhoneNumber IS NULL OR w.[PhoneNumber] = @PhoneNumber)
    ORDER BY w.[LastName], w.[FirstName];
END
GO
