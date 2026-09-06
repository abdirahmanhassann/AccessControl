USE [AccessControl]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* Site-level gate QR, location kind, visit time window, and per-location managers.
   Run against the existing AccessControl database. Safe to re-run. */

IF COL_LENGTH('dbo.Sites', 'QrCodeIdentifier') IS NULL
BEGIN
    ALTER TABLE [dbo].[Sites] ADD [QrCodeIdentifier] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_Sites_QrCodeIdentifier' AND object_id = OBJECT_ID('dbo.Sites')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_Sites_QrCodeIdentifier]
        ON [dbo].[Sites]([QrCodeIdentifier])
        WHERE [QrCodeIdentifier] IS NOT NULL;
END
GO

UPDATE [dbo].[Sites]
SET [QrCodeIdentifier] = 'SG-SITE-' + RIGHT('000' + CAST([Id] AS VARCHAR(8)), 3)
WHERE [QrCodeIdentifier] IS NULL OR LTRIM(RTRIM([QrCodeIdentifier])) = '';
GO

IF COL_LENGTH('dbo.Rooms', 'LocationKind') IS NULL
BEGIN
    ALTER TABLE [dbo].[Rooms] ADD [LocationKind] NVARCHAR(30) NOT NULL
        CONSTRAINT [DF_Rooms_LocationKind] DEFAULT ('Apartment');
END
GO

IF COL_LENGTH('dbo.AccessRequests', 'SupervisorName') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequests] ADD [SupervisorName] NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH('dbo.AccessRequests', 'WorkFrom') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequests] ADD [WorkFrom] DATETIME2(0) NULL;
END
GO

IF COL_LENGTH('dbo.AccessRequests', 'WorkTo') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequests] ADD [WorkTo] DATETIME2(0) NULL;
END
GO

IF OBJECT_ID(N'[dbo].[RoomManagers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RoomManagers](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RoomId] INT NOT NULL,
        [UserId] INT NOT NULL,
        [IsPrimary] BIT NOT NULL CONSTRAINT [DF_RoomManagers_IsPrimary] DEFAULT (1),
        [AssignedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_RoomManagers_AssignedAt] DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_RoomManagers] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_RoomManagers_Room_User] UNIQUE NONCLUSTERED ([RoomId] ASC, [UserId] ASC),
        CONSTRAINT [FK_RoomManagers_Rooms] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms]([Id]),
        CONSTRAINT [FK_RoomManagers_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users]([Id])
    );
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SitesGet]
    @Id INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        [Id],
        [Name],
        [Address],
        [Reference],
        [QrCodeIdentifier],
        [IsActive],
        [CreatedAt]
    FROM [dbo].[Sites]
    WHERE @Id IS NULL OR [Id] = @Id
    ORDER BY [Name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SitesInsert]
    @Name NVARCHAR(200),
    @Address NVARCHAR(500) = NULL,
    @Reference NVARCHAR(100) = NULL,
    @QrCodeIdentifier NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @QrCodeIdentifier IS NULL OR LTRIM(RTRIM(@QrCodeIdentifier)) = ''
        SET @QrCodeIdentifier = 'SG-' + UPPER(LEFT(REPLACE(CONVERT(VARCHAR(36), NEWID()), '-', ''), 8));

    INSERT INTO [dbo].[Sites] ([Name], [Address], [Reference], [QrCodeIdentifier], [CreatedAt])
    VALUES (@Name, @Address, @Reference, @QrCodeIdentifier, GETDATE());

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SitesUpdate]
    @Id INT,
    @Name NVARCHAR(200),
    @Address NVARCHAR(500) = NULL,
    @Reference NVARCHAR(100) = NULL,
    @QrCodeIdentifier NVARCHAR(100) = NULL,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Sites]
    SET
        [Name] = @Name,
        [Address] = @Address,
        [Reference] = @Reference,
        [QrCodeIdentifier] = CASE
            WHEN @QrCodeIdentifier IS NULL OR LTRIM(RTRIM(@QrCodeIdentifier)) = '' THEN [QrCodeIdentifier]
            ELSE @QrCodeIdentifier
        END,
        [IsActive] = @IsActive
    WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SitesGetByQrCode]
    @QrCodeIdentifier NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        0 AS [Id],
        0 AS [WorkAreaId],
        CAST('' AS NVARCHAR(100)) AS [RoomNumber],
        s.[Name],
        s.[QrCodeIdentifier],
        s.[Address] AS [Description],
        s.[IsActive],
        s.[CreatedAt],
        CAST('' AS NVARCHAR(200)) AS [WorkAreaName],
        CAST('' AS NVARCHAR(500)) AS [WorkAreaDescription],
        s.[Id] AS [SiteId],
        s.[Name] AS [SiteName],
        s.[Address] AS [SiteAddress],
        s.[Reference] AS [SiteReference],
        CAST(N'site' AS NVARCHAR(20)) AS [ScanKind]
    FROM [dbo].[Sites] s
    WHERE s.[IsActive] = 1
      AND s.[QrCodeIdentifier] IS NOT NULL
      AND LOWER(s.[QrCodeIdentifier]) = LOWER(@QrCodeIdentifier);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[WorkAreasGet]
    @Id INT = NULL,
    @SiteId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        wa.[Id],
        wa.[SiteId],
        s.[Name] AS [SiteName],
        wa.[Name],
        wa.[Description],
        wa.[IsActive],
        wa.[CreatedAt]
    FROM [dbo].[WorkAreas] wa
    INNER JOIN [dbo].[Sites] s ON s.[Id] = wa.[SiteId]
    WHERE (@Id IS NULL OR wa.[Id] = @Id)
      AND (@SiteId IS NULL OR @SiteId = 0 OR wa.[SiteId] = @SiteId)
    ORDER BY wa.[Name];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomsGet]
    @Id INT = NULL,
    @WorkAreaId INT = NULL,
    @SiteId INT = NULL,
    @QrCodeIdentifier UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        r.[Id],
        r.[WorkAreaId],
        wa.[Name] AS [WorkAreaName],
        r.[RoomNumber],
        r.[Name] AS [RoomName],
        r.[Name] AS [Name],
        r.[LocationKind],
        s.[Id] AS [SiteId],
        s.[Name] AS [SiteName],
        s.[Address] AS [SiteAddress],
        r.[QrCodeIdentifier],
        r.[Description],
        r.[IsActive],
        r.[CreatedAt]
    FROM [dbo].[Rooms] r
    INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    INNER JOIN [dbo].[Sites] s ON s.[Id] = wa.[SiteId]
    WHERE (@Id IS NULL OR r.[Id] = @Id)
      AND (@WorkAreaId IS NULL OR @WorkAreaId = 0 OR r.[WorkAreaId] = @WorkAreaId)
      AND (@SiteId IS NULL OR @SiteId = 0 OR wa.[SiteId] = @SiteId)
      AND (@QrCodeIdentifier IS NULL OR r.[QrCodeIdentifier] = @QrCodeIdentifier)
    ORDER BY r.[LocationKind], r.[RoomNumber];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomsGetByQrCode]
    @QrCodeIdentifier VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        r.[Id],
        r.[WorkAreaId],
        r.[RoomNumber],
        r.[Name],
        r.[QrCodeIdentifier],
        r.[Description],
        r.[IsActive],
        r.[CreatedAt],
        r.[LocationKind],
        wa.[Name] AS [WorkAreaName],
        wa.[Description] AS [WorkAreaDescription],
        s.[Id] AS [SiteId],
        s.[Name] AS [SiteName],
        s.[Address] AS [SiteAddress],
        s.[Reference] AS [SiteReference],
        CAST(N'room' AS NVARCHAR(20)) AS [ScanKind]
    FROM [dbo].[Rooms] r
    INNER JOIN [dbo].[WorkAreas] wa ON r.[WorkAreaId] = wa.[Id]
    INNER JOIN [dbo].[Sites] s ON wa.[SiteId] = s.[Id]
    WHERE CONVERT(VARCHAR(36), r.[QrCodeIdentifier]) = @QrCodeIdentifier
      AND r.[IsActive] = 1
      AND wa.[IsActive] = 1
      AND s.[IsActive] = 1;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomsInsert]
    @WorkAreaId INT,
    @RoomNumber NVARCHAR(100),
    @Name NVARCHAR(200) = NULL,
    @Description NVARCHAR(500) = NULL,
    @LocationKind NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @LocationKind IS NULL OR LTRIM(RTRIM(@LocationKind)) = ''
        SET @LocationKind = N'Apartment';

    INSERT INTO [dbo].[Rooms] ([WorkAreaId], [RoomNumber], [Name], [Description], [LocationKind])
    VALUES (@WorkAreaId, @RoomNumber, @Name, @Description, @LocationKind);

    DECLARE @Id INT = CAST(SCOPE_IDENTITY() AS INT);
    SELECT [Id], [QrCodeIdentifier], [LocationKind]
    FROM [dbo].[Rooms]
    WHERE [Id] = @Id;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomsUpdate]
    @Id INT,
    @WorkAreaId INT,
    @RoomNumber NVARCHAR(100),
    @Name NVARCHAR(200) = NULL,
    @Description NVARCHAR(500) = NULL,
    @LocationKind NVARCHAR(30) = NULL,
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[Rooms]
    SET
        [WorkAreaId] = @WorkAreaId,
        [RoomNumber] = @RoomNumber,
        [Name] = @Name,
        [Description] = @Description,
        [LocationKind] = CASE
            WHEN @LocationKind IS NULL OR LTRIM(RTRIM(@LocationKind)) = '' THEN [LocationKind]
            ELSE @LocationKind
        END,
        [IsActive] = @IsActive
    WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequestsInsert]
    @WorkerId INT = NULL,
    @PhoneNumber NVARCHAR(30) = NULL,
    @RoomId INT,
    @Reason NVARCHAR(500) = NULL,
    @WorkType NVARCHAR(200) = NULL,
    @Description NVARCHAR(1000) = NULL,
    @SupervisorName NVARCHAR(200) = NULL,
    @WorkFrom DATETIME2(0) = NULL,
    @WorkTo DATETIME2(0) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[AccessRequests]
    (
        [WorkerId],
        [RoomId],
        [Reason],
        [WorkType],
        [Description],
        [SupervisorName],
        [WorkFrom],
        [WorkTo],
        [ExpectedClockOutAt],
        [CreatedAt]
    )
    VALUES
    (
        @WorkerId,
        @RoomId,
        @Reason,
        @WorkType,
        @Description,
        @SupervisorName,
        @WorkFrom,
        @WorkTo,
        @WorkTo,
        GETDATE()
    );
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequestsGet]
    @Id INT = NULL,
    @WorkerId INT = NULL,
    @RoomId INT = NULL,
    @SiteId INT = NULL,
    @Status NVARCHAR(30) = NULL,
    @Search NVARCHAR(200) = NULL,
    @Skip INT = 0,
    @Take INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Offset INT = CASE WHEN @Skip IS NULL OR @Skip < 0 THEN 0 ELSE @Skip END;
    DECLARE @RowCount INT = CASE WHEN @Take IS NULL OR @Take < 1 THEN 1000000 ELSE @Take END;

    SELECT
        ar.[Id],
        ar.[WorkerId],
        w.[FirstName] AS [WorkerFirstName],
        w.[LastName] AS [WorkerLastName],
        w.[PhoneNumber] AS [WorkerPhoneNumber],
        w.[CompanyName],
        ar.[RoomId],
        r.[RoomNumber],
        r.[Name] AS [RoomName],
        r.[LocationKind],
        wa.[Id] AS [WorkAreaId],
        wa.[Name] AS [WorkAreaName],
        wa.[Name] AS [TowerName],
        r.[Name] AS [LocationLabel],
        s.[Id] AS [SiteId],
        s.[Name] AS [SiteName],
        ar.[Status],
        ar.[Reason],
        ar.[WorkType],
        ar.[Description],
        ar.[SupervisorName],
        ar.[WorkFrom],
        ar.[WorkTo],
        ar.[RequestedAt],
        ar.[ApprovedAt],
        ar.[RejectedAt],
        ar.[ClockedInAt],
        ar.[ExpectedClockOutAt],
        ar.[ClockedOutAt],
        ar.[CompletedAt],
        COUNT(*) OVER() AS [Total]
    FROM [dbo].[AccessRequests] ar
    INNER JOIN [dbo].[Workers] w ON w.[Id] = ar.[WorkerId]
    INNER JOIN [dbo].[Rooms] r ON r.[Id] = ar.[RoomId]
    INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    INNER JOIN [dbo].[Sites] s ON s.[Id] = wa.[SiteId]
    WHERE (@Id IS NULL OR ar.[Id] = @Id)
      AND (@WorkerId IS NULL OR ar.[WorkerId] = @WorkerId)
      AND (@RoomId IS NULL OR ar.[RoomId] = @RoomId)
      AND (@SiteId IS NULL OR @SiteId = 0 OR wa.[SiteId] = @SiteId)
      AND (@Status IS NULL OR ar.[Status] = @Status)
      AND (
            @Search IS NULL OR LTRIM(RTRIM(@Search)) = ''
            OR w.[FirstName] LIKE '%' + @Search + '%'
            OR w.[LastName] LIKE '%' + @Search + '%'
            OR r.[RoomNumber] LIKE '%' + @Search + '%'
            OR r.[Name] LIKE '%' + @Search + '%'
            OR ar.[WorkType] LIKE '%' + @Search + '%'
            OR ar.[Reason] LIKE '%' + @Search + '%'
            OR ar.[SupervisorName] LIKE '%' + @Search + '%'
          )
    ORDER BY ar.[RequestedAt] DESC
    OFFSET @Offset ROWS
    FETCH NEXT @RowCount ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomManagersGet]
    @Id INT = NULL,
    @RoomId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.[Id],
        m.[RoomId],
        m.[UserId],
        m.[UserId] AS [ManagerUserId],
        m.[IsPrimary],
        m.[AssignedAt],
        u.[FirstName],
        u.[LastName],
        u.[Email],
        u.[Role]
    FROM [dbo].[RoomManagers] m
    INNER JOIN [dbo].[Users] u ON u.[Id] = m.[UserId]
    WHERE (@Id IS NULL OR m.[Id] = @Id)
      AND (@RoomId IS NULL OR @RoomId = 0 OR m.[RoomId] = @RoomId)
    ORDER BY m.[IsPrimary] DESC, u.[LastName], u.[FirstName];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomManagersInsert]
    @RoomId INT,
    @UserId INT,
    @IsPrimary BIT = 1
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO [dbo].[RoomManagers] ([RoomId], [UserId], [IsPrimary])
    VALUES (@RoomId, @UserId, @IsPrimary);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomManagersUpdate]
    @Id INT,
    @RoomId INT,
    @UserId INT,
    @IsPrimary BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE [dbo].[RoomManagers]
    SET [RoomId] = @RoomId, [UserId] = @UserId, [IsPrimary] = @IsPrimary
    WHERE [Id] = @Id;
    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ManagersForRoomGet]
    @RoomId INT,
    @DepartmentId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM [dbo].[RoomManagers] WHERE [RoomId] = @RoomId)
    BEGIN
        SELECT DISTINCT
            u.[Id],
            u.[Id] AS [UserId],
            u.[FirstName],
            u.[LastName],
            u.[Email],
            u.[PhoneNumber],
            u.[Role],
            u.[IsActive],
            CAST(NULL AS INT) AS [DepartmentId],
            CAST(N'Location' AS NVARCHAR(200)) AS [DepartmentName],
            wa.[SiteId],
            s.[Name] AS [SiteName]
        FROM [dbo].[RoomManagers] rm
        INNER JOIN [dbo].[Users] u ON u.[Id] = rm.[UserId] AND u.[IsActive] = 1
        INNER JOIN [dbo].[Rooms] r ON r.[Id] = rm.[RoomId]
        INNER JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
        LEFT JOIN [dbo].[Sites] s ON s.[Id] = wa.[SiteId]
        WHERE rm.[RoomId] = @RoomId
        ORDER BY u.[LastName], u.[FirstName];
        RETURN;
    END

    IF @DepartmentId IS NOT NULL AND @DepartmentId > 0
    BEGIN
        SELECT DISTINCT
            u.[Id],
            u.[Id] AS [UserId],
            u.[FirstName],
            u.[LastName],
            u.[Email],
            u.[PhoneNumber],
            u.[Role],
            u.[IsActive],
            d.[Id] AS [DepartmentId],
            d.[Name] AS [DepartmentName],
            d.[SiteId],
            s.[Name] AS [SiteName]
        FROM [dbo].[DepartmentManagerMapping] dmm
        INNER JOIN [dbo].[Departments] d ON d.[Id] = dmm.[DepartmentId] AND d.[IsActive] = 1
        INNER JOIN [dbo].[Users] u ON u.[Id] = dmm.[UserId]
        LEFT JOIN [dbo].[Sites] s ON s.[Id] = d.[SiteId]
        WHERE dmm.[DepartmentId] = @DepartmentId
        ORDER BY u.[LastName], u.[FirstName];
        RETURN;
    END

    SELECT DISTINCT
        u.[Id],
        u.[Id] AS [UserId],
        u.[FirstName],
        u.[LastName],
        u.[Email],
        u.[PhoneNumber],
        u.[Role],
        u.[IsActive],
        d.[Id] AS [DepartmentId],
        d.[Name] AS [DepartmentName],
        d.[SiteId],
        s.[Name] AS [SiteName]
    FROM [dbo].[DepartmentManagerMapping] dmm
    INNER JOIN [dbo].[Departments] d ON d.[Id] = dmm.[DepartmentId] AND d.[IsActive] = 1
    INNER JOIN [dbo].[Users] u ON u.[Id] = dmm.[UserId]
    INNER JOIN [dbo].[DepartmentRoomMapping] drm ON drm.[DepartmentId] = d.[Id]
    LEFT JOIN [dbo].[Sites] s ON s.[Id] = d.[SiteId]
    WHERE drm.[RoomId] = @RoomId
    ORDER BY u.[LastName], u.[FirstName];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ManagerCanApprove]
    @ApproverUserId INT,
    @RoomId INT = NULL,
    @AccessRequestId INT = NULL,
    @DepartmentId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ResolvedRoomId INT = @RoomId;
    IF @ResolvedRoomId IS NULL AND @AccessRequestId IS NOT NULL
        SELECT @ResolvedRoomId = [RoomId] FROM [dbo].[AccessRequests] WHERE [Id] = @AccessRequestId;

    SELECT CAST(CASE WHEN EXISTS (
        SELECT 1
        FROM [dbo].[RoomManagers] rm
        WHERE rm.[UserId] = @ApproverUserId
          AND rm.[RoomId] = @ResolvedRoomId
    ) OR EXISTS (
        SELECT 1
        FROM [dbo].[DepartmentManagerMapping] dmm
        INNER JOIN [dbo].[Departments] d ON d.[Id] = dmm.[DepartmentId] AND d.[IsActive] = 1
        INNER JOIN [dbo].[DepartmentRoomMapping] drm ON drm.[DepartmentId] = d.[Id]
        WHERE dmm.[UserId] = @ApproverUserId
          AND drm.[RoomId] = @ResolvedRoomId
    ) OR EXISTS (
        SELECT 1
        FROM [dbo].[DepartmentManagerMapping] dmm
        INNER JOIN [dbo].[Departments] d ON d.[Id] = dmm.[DepartmentId] AND d.[IsActive] = 1
        WHERE dmm.[UserId] = @ApproverUserId
          AND @DepartmentId IS NOT NULL
          AND @DepartmentId > 0
          AND dmm.[DepartmentId] = @DepartmentId
    ) OR EXISTS (
        SELECT 1
        FROM [dbo].[WorkAreaManagers] wam
        INNER JOIN [dbo].[Rooms] r ON r.[WorkAreaId] = wam.[WorkAreaId]
        WHERE wam.[UserId] = @ApproverUserId
          AND r.[Id] = @ResolvedRoomId
    ) THEN 1 ELSE 0 END AS INT) AS [Allowed];
END
GO
