USE [AccessControl]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* Departments are org units on a site. WorkAreas stay as physical zones.
   A manager may approve a room only if they are a DepartmentManager of a
   department that (a) maps to that room and (b) belongs to the room's site.
   A worker may request a room only if they are mapped to that site and to
   a department that contains the room. */

IF OBJECT_ID(N'[dbo].[Departments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Departments](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [SiteId] INT NOT NULL,
        [Name] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Departments_IsActive] DEFAULT (1),
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_Departments_CreatedAt] DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_Departments] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_Departments_Site_Name] UNIQUE NONCLUSTERED ([SiteId] ASC, [Name] ASC),
        CONSTRAINT [FK_Departments_Sites] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Sites]([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[DepartmentRoomMapping]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DepartmentRoomMapping](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [DepartmentId] INT NOT NULL,
        [RoomId] INT NOT NULL,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_DepartmentRoomMapping_CreatedAt] DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_DepartmentRoomMapping] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_DepartmentRoomMapping] UNIQUE NONCLUSTERED ([DepartmentId] ASC, [RoomId] ASC),
        CONSTRAINT [FK_DepartmentRoomMapping_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]),
        CONSTRAINT [FK_DepartmentRoomMapping_Rooms] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms]([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[DepartmentWorkerMapping]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DepartmentWorkerMapping](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [DepartmentId] INT NOT NULL,
        [WorkerId] INT NOT NULL,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_DepartmentWorkerMapping_CreatedAt] DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_DepartmentWorkerMapping] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_DepartmentWorkerMapping] UNIQUE NONCLUSTERED ([DepartmentId] ASC, [WorkerId] ASC),
        CONSTRAINT [FK_DepartmentWorkerMapping_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]),
        CONSTRAINT [FK_DepartmentWorkerMapping_Workers] FOREIGN KEY ([WorkerId]) REFERENCES [dbo].[Workers]([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[DepartmentManagerMapping]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DepartmentManagerMapping](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [DepartmentId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [IsPrimary] BIT NOT NULL CONSTRAINT [DF_DepartmentManagerMapping_IsPrimary] DEFAULT (0),
        [AssignedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_DepartmentManagerMapping_AssignedAt] DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_DepartmentManagerMapping] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_DepartmentManagerMapping] UNIQUE NONCLUSTERED ([DepartmentId] ASC, [UserId] ASC),
        CONSTRAINT [FK_DepartmentManagerMapping_Departments] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Departments]([Id]),
        CONSTRAINT [FK_DepartmentManagerMapping_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[WorkerSiteMapping]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorkerSiteMapping](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [WorkerId] INT NOT NULL,
        [SiteId] INT NOT NULL,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_WorkerSiteMapping_CreatedAt] DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_WorkerSiteMapping] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_WorkerSiteMapping] UNIQUE NONCLUSTERED ([WorkerId] ASC, [SiteId] ASC),
        CONSTRAINT [FK_WorkerSiteMapping_Workers] FOREIGN KEY ([WorkerId]) REFERENCES [dbo].[Workers]([Id]),
        CONSTRAINT [FK_WorkerSiteMapping_Sites] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Sites]([Id])
    );
END
GO

INSERT INTO [dbo].[Departments] ([SiteId], [Name], [Description], [IsActive])
SELECT wa.[SiteId], wa.[Name], wa.[Description], wa.[IsActive]
FROM [dbo].[WorkAreas] wa
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Departments] d
    WHERE d.[SiteId] = wa.[SiteId] AND d.[Name] = wa.[Name]
);

INSERT INTO [dbo].[DepartmentRoomMapping] ([DepartmentId], [RoomId])
SELECT d.[Id], r.[Id]
FROM [dbo].[Rooms] r
INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
INNER JOIN [dbo].[Departments] d ON d.[SiteId] = wa.[SiteId] AND d.[Name] = wa.[Name]
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[DepartmentRoomMapping] m
    WHERE m.[DepartmentId] = d.[Id] AND m.[RoomId] = r.[Id]
);

INSERT INTO [dbo].[DepartmentManagerMapping] ([DepartmentId], [UserId], [IsPrimary])
SELECT d.[Id], wam.[UserId], wam.[IsPrimary]
FROM [dbo].[WorkAreaManagers] wam
INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = wam.[WorkAreaId]
INNER JOIN [dbo].[Departments] d ON d.[SiteId] = wa.[SiteId] AND d.[Name] = wa.[Name]
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[DepartmentManagerMapping] m
    WHERE m.[DepartmentId] = d.[Id] AND m.[UserId] = wam.[UserId]
);

INSERT INTO [dbo].[WorkerSiteMapping] ([WorkerId], [SiteId])
SELECT DISTINCT ar.[WorkerId], wa.[SiteId]
FROM [dbo].[AccessRequests] ar
INNER JOIN [dbo].[Rooms] r ON r.[Id] = ar.[RoomId]
INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
WHERE ar.[WorkerId] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[WorkerSiteMapping] m
      WHERE m.[WorkerId] = ar.[WorkerId] AND m.[SiteId] = wa.[SiteId]
  );

INSERT INTO [dbo].[DepartmentWorkerMapping] ([DepartmentId], [WorkerId])
SELECT DISTINCT drm.[DepartmentId], ar.[WorkerId]
FROM [dbo].[AccessRequests] ar
INNER JOIN [dbo].[DepartmentRoomMapping] drm ON drm.[RoomId] = ar.[RoomId]
WHERE ar.[WorkerId] IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM [dbo].[DepartmentWorkerMapping] m
      WHERE m.[DepartmentId] = drm.[DepartmentId] AND m.[WorkerId] = ar.[WorkerId]
  );
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentsGet]
    @Id INT = NULL,
    @SiteId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        d.[Id],
        d.[SiteId],
        s.[Name] AS [SiteName],
        d.[Name],
        d.[Description],
        d.[IsActive],
        d.[CreatedAt]
    FROM [dbo].[Departments] d
    INNER JOIN [dbo].[Sites] s ON s.[Id] = d.[SiteId]
    WHERE (@Id IS NULL OR d.[Id] = @Id)
      AND (@SiteId IS NULL OR d.[SiteId] = @SiteId)
    ORDER BY s.[Name], d.[Name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentsInsert]
    @SiteId INT,
    @Name NVARCHAR(200),
    @Description NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[Departments] ([SiteId], [Name], [Description])
    VALUES (@SiteId, @Name, @Description);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentsUpdate]
    @Id INT,
    @SiteId INT,
    @Name NVARCHAR(200),
    @Description NVARCHAR(500) = NULL,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Departments]
    SET [SiteId] = @SiteId, [Name] = @Name, [Description] = @Description, [IsActive] = @IsActive
    WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentRoomMappingGet]
    @Id INT = NULL,
    @DepartmentId INT = NULL,
    @RoomId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.[Id],
        m.[DepartmentId],
        d.[Name] AS [DepartmentName],
        d.[SiteId],
        m.[RoomId],
        r.[RoomNumber],
        r.[Name] AS [RoomName]
    FROM [dbo].[DepartmentRoomMapping] m
    INNER JOIN [dbo].[Departments] d ON d.[Id] = m.[DepartmentId]
    INNER JOIN [dbo].[Rooms] r ON r.[Id] = m.[RoomId]
    WHERE (@Id IS NULL OR m.[Id] = @Id)
      AND (@DepartmentId IS NULL OR m.[DepartmentId] = @DepartmentId)
      AND (@RoomId IS NULL OR m.[RoomId] = @RoomId)
    ORDER BY d.[Name], r.[RoomNumber];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentRoomMappingInsert]
    @DepartmentId INT,
    @RoomId INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[DepartmentRoomMapping] ([DepartmentId], [RoomId])
    VALUES (@DepartmentId, @RoomId);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentRoomMappingDelete]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[DepartmentRoomMapping] WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentWorkerMappingGet]
    @Id INT = NULL,
    @DepartmentId INT = NULL,
    @WorkerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.[Id],
        m.[DepartmentId],
        d.[Name] AS [DepartmentName],
        m.[WorkerId],
        w.[FirstName] AS [WorkerFirstName],
        w.[LastName] AS [WorkerLastName],
        w.[PhoneNumber] AS [WorkerPhoneNumber]
    FROM [dbo].[DepartmentWorkerMapping] m
    INNER JOIN [dbo].[Departments] d ON d.[Id] = m.[DepartmentId]
    INNER JOIN [dbo].[Workers] w ON w.[Id] = m.[WorkerId]
    WHERE (@Id IS NULL OR m.[Id] = @Id)
      AND (@DepartmentId IS NULL OR m.[DepartmentId] = @DepartmentId)
      AND (@WorkerId IS NULL OR m.[WorkerId] = @WorkerId)
    ORDER BY d.[Name], w.[LastName], w.[FirstName];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentWorkerMappingInsert]
    @DepartmentId INT,
    @WorkerId INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[DepartmentWorkerMapping] ([DepartmentId], [WorkerId])
    VALUES (@DepartmentId, @WorkerId);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentWorkerMappingDelete]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[DepartmentWorkerMapping] WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentManagerMappingGet]
    @Id INT = NULL,
    @DepartmentId INT = NULL,
    @UserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.[Id],
        m.[DepartmentId],
        d.[Name] AS [DepartmentName],
        d.[SiteId],
        m.[UserId],
        u.[FirstName],
        u.[LastName],
        u.[Email],
        u.[Role],
        m.[IsPrimary],
        m.[AssignedAt]
    FROM [dbo].[DepartmentManagerMapping] m
    INNER JOIN [dbo].[Departments] d ON d.[Id] = m.[DepartmentId]
    INNER JOIN [dbo].[Users] u ON u.[Id] = m.[UserId]
    WHERE (@Id IS NULL OR m.[Id] = @Id)
      AND (@DepartmentId IS NULL OR m.[DepartmentId] = @DepartmentId)
      AND (@UserId IS NULL OR m.[UserId] = @UserId)
    ORDER BY d.[Name], u.[LastName], u.[FirstName];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentManagerMappingInsert]
    @DepartmentId INT,
    @UserId INT,
    @IsPrimary BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[DepartmentManagerMapping] ([DepartmentId], [UserId], [IsPrimary])
    VALUES (@DepartmentId, @UserId, @IsPrimary);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentManagerMappingUpdate]
    @Id INT,
    @DepartmentId INT,
    @UserId INT,
    @IsPrimary BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[DepartmentManagerMapping]
    SET [DepartmentId] = @DepartmentId, [UserId] = @UserId, [IsPrimary] = @IsPrimary
    WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentManagerMappingDelete]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[DepartmentManagerMapping] WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[WorkerSiteMappingGet]
    @Id INT = NULL,
    @WorkerId INT = NULL,
    @SiteId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.[Id],
        m.[WorkerId],
        w.[FirstName] AS [WorkerFirstName],
        w.[LastName] AS [WorkerLastName],
        m.[SiteId],
        s.[Name] AS [SiteName]
    FROM [dbo].[WorkerSiteMapping] m
    INNER JOIN [dbo].[Workers] w ON w.[Id] = m.[WorkerId]
    INNER JOIN [dbo].[Sites] s ON s.[Id] = m.[SiteId]
    WHERE (@Id IS NULL OR m.[Id] = @Id)
      AND (@WorkerId IS NULL OR m.[WorkerId] = @WorkerId)
      AND (@SiteId IS NULL OR m.[SiteId] = @SiteId)
    ORDER BY w.[LastName], s.[Name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[WorkerSiteMappingInsert]
    @WorkerId INT,
    @SiteId INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[WorkerSiteMapping] ([WorkerId], [SiteId])
    VALUES (@WorkerId, @SiteId);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[WorkerSiteMappingDelete]
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM [dbo].[WorkerSiteMapping] WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ManagersForRoomGet]
    @RoomId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT
        u.[Id],
        u.[FirstName],
        u.[LastName],
        u.[Email],
        u.[PhoneNumber],
        u.[Role],
        u.[IsActive],
        d.[Id] AS [DepartmentId],
        d.[Name] AS [DepartmentName],
        s.[Id] AS [SiteId],
        s.[Name] AS [SiteName]
    FROM [dbo].[DepartmentRoomMapping] drm
    INNER JOIN [dbo].[Departments] d ON d.[Id] = drm.[DepartmentId] AND d.[IsActive] = 1
    INNER JOIN [dbo].[DepartmentManagerMapping] dmm ON dmm.[DepartmentId] = d.[Id]
    INNER JOIN [dbo].[Users] u ON u.[Id] = dmm.[UserId] AND u.[IsActive] = 1
    INNER JOIN [dbo].[Rooms] r ON r.[Id] = drm.[RoomId]
    INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    INNER JOIN [dbo].[Sites] s ON s.[Id] = d.[SiteId]
    WHERE drm.[RoomId] = @RoomId
      AND d.[SiteId] = wa.[SiteId]
    ORDER BY u.[LastName], u.[FirstName];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[WorkerCanAccessRoom]
    @WorkerId INT,
    @RoomId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1
        FROM [dbo].[Rooms] r
        INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
        INNER JOIN [dbo].[WorkerSiteMapping] wsm ON wsm.[WorkerId] = @WorkerId AND wsm.[SiteId] = wa.[SiteId]
        INNER JOIN [dbo].[DepartmentRoomMapping] drm ON drm.[RoomId] = r.[Id]
        INNER JOIN [dbo].[Departments] d ON d.[Id] = drm.[DepartmentId] AND d.[IsActive] = 1 AND d.[SiteId] = wa.[SiteId]
        INNER JOIN [dbo].[DepartmentWorkerMapping] dwm ON dwm.[DepartmentId] = d.[Id] AND dwm.[WorkerId] = @WorkerId
        WHERE r.[Id] = @RoomId
    ) THEN 1 ELSE 0 END AS INT) AS [Allowed];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ManagerCanApprove]
    @ApproverUserId INT,
    @RoomId INT = NULL,
    @AccessRequestId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ResolvedRoomId INT = @RoomId;
    IF @ResolvedRoomId IS NULL AND @AccessRequestId IS NOT NULL
        SELECT @ResolvedRoomId = [RoomId] FROM [dbo].[AccessRequests] WHERE [Id] = @AccessRequestId;

    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1
        FROM [dbo].[DepartmentManagerMapping] dmm
        INNER JOIN [dbo].[Departments] d ON d.[Id] = dmm.[DepartmentId] AND d.[IsActive] = 1
        INNER JOIN [dbo].[DepartmentRoomMapping] drm ON drm.[DepartmentId] = d.[Id]
        INNER JOIN [dbo].[Rooms] r ON r.[Id] = drm.[RoomId]
        INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId] AND wa.[SiteId] = d.[SiteId]
        INNER JOIN [dbo].[Users] u ON u.[Id] = dmm.[UserId] AND u.[IsActive] = 1
        WHERE dmm.[UserId] = @ApproverUserId
          AND drm.[RoomId] = @ResolvedRoomId
    ) THEN 1 ELSE 0 END AS INT) AS [Allowed];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[DepartmentsForRoomGet]
    @RoomId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT
        d.[Id],
        d.[SiteId],
        s.[Name] AS [SiteName],
        d.[Name],
        d.[Description],
        d.[IsActive],
        d.[CreatedAt]
    FROM [dbo].[DepartmentRoomMapping] drm
    INNER JOIN [dbo].[Departments] d ON d.[Id] = drm.[DepartmentId] AND d.[IsActive] = 1
    INNER JOIN [dbo].[Rooms] r ON r.[Id] = drm.[RoomId]
    INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    INNER JOIN [dbo].[Sites] s ON s.[Id] = d.[SiteId]
    WHERE drm.[RoomId] = @RoomId
      AND d.[SiteId] = wa.[SiteId]
    ORDER BY d.[Name];
END
GO
