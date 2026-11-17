USE [AccessControl]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* Keyman role and room-leave reviews.

   A Keyman holds the keys to the rooms. They get the same console access as a
   manager, plus one extra job: after a worker signs out of a room (and has
   taken their photos) the Keyman checks the room is in the state the worker
   left it, then approves the room leave or rejects it with a reason.

   One RoomLeaveReviews row per access request, created when the visit is
   clocked out. Safe to run more than once. */

/* 1. Allow the Keyman role on Users. */
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE [name] = N'CK_Users_Role' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Users]'))
    ALTER TABLE [dbo].[Users] DROP CONSTRAINT [CK_Users_Role];
GO

ALTER TABLE [dbo].[Users] WITH NOCHECK ADD CONSTRAINT [CK_Users_Role]
    CHECK ([Role] IN ('Admin', 'SiteManager', 'Manager', 'Keyman', 'Viewer', 'Worker'));
GO

/* 2. Room-leave reviews. */
IF OBJECT_ID(N'[dbo].[RoomLeaveReviews]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RoomLeaveReviews](
        [Id] INT IDENTITY(1,1) NOT NULL,
        [AccessRequestId] INT NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_RoomLeaveReviews_Status] DEFAULT ('Pending'),
        [ReviewedByUserId] INT NULL,
        [Reason] NVARCHAR(1000) NULL,
        [RequestedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_RoomLeaveReviews_RequestedAt] DEFAULT (GETDATE()),
        [ReviewedAt] DATETIME2(0) NULL,
        CONSTRAINT [PK_RoomLeaveReviews] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_RoomLeaveReviews_AccessRequestId] UNIQUE ([AccessRequestId]),
        CONSTRAINT [FK_RoomLeaveReviews_AccessRequests] FOREIGN KEY ([AccessRequestId])
            REFERENCES [dbo].[AccessRequests] ([Id]),
        CONSTRAINT [FK_RoomLeaveReviews_Users] FOREIGN KEY ([ReviewedByUserId])
            REFERENCES [dbo].[Users] ([Id]),
        CONSTRAINT [CK_RoomLeaveReviews_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected'))
    );
END
GO

/* Creates the review for a visit that has just been signed out of.
   Returns the review row and Created = 1 only the first time, so the caller
   pings the Keyman once even if the sign-out is sent twice. */
CREATE OR ALTER PROCEDURE [dbo].[RoomLeaveReviewsInsert]
    @AccessRequestId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Created BIT = 0;

    IF NOT EXISTS (SELECT 1 FROM [dbo].[RoomLeaveReviews] WHERE [AccessRequestId] = @AccessRequestId)
    BEGIN
        BEGIN TRY
            INSERT INTO [dbo].[RoomLeaveReviews] ([AccessRequestId]) VALUES (@AccessRequestId);
            SET @Created = 1;
        END TRY
        BEGIN CATCH
            /* 2627 / 2601: a parallel sign-out inserted it first. Anything else is real. */
            IF ERROR_NUMBER() NOT IN (2627, 2601) THROW;
        END CATCH
    END

    SELECT [Id], [AccessRequestId], [Status], @Created AS [Created]
    FROM [dbo].[RoomLeaveReviews]
    WHERE [AccessRequestId] = @AccessRequestId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[RoomLeaveReviewsGet]
    @Id INT = NULL,
    @AccessRequestId INT = NULL,
    @Status NVARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        rlr.[Id],
        rlr.[AccessRequestId],
        rlr.[Status],
        rlr.[Reason],
        rlr.[RequestedAt],
        rlr.[ReviewedAt],
        rlr.[ReviewedByUserId],
        u.[FirstName] AS [ReviewerFirstName],
        u.[LastName] AS [ReviewerLastName],
        ar.[WorkerId],
        w.[FirstName] AS [WorkerFirstName],
        w.[LastName] AS [WorkerLastName],
        w.[PhoneNumber] AS [WorkerPhoneNumber],
        w.[CompanyName],
        ar.[RoomId],
        r.[RoomNumber],
        r.[Name] AS [RoomName],
        wa.[Name] AS [WorkAreaName],
        s.[Name] AS [SiteName],
        ar.[WorkType],
        ar.[WorkFrom],
        ar.[WorkTo],
        ar.[ClockedInAt],
        ar.[ClockedOutAt],
        (SELECT COUNT(*) FROM [dbo].[AccessPhotos] p WHERE p.[AccessRequestId] = ar.[Id]) AS [PhotoCount]
    FROM [dbo].[RoomLeaveReviews] rlr
    INNER JOIN [dbo].[AccessRequests] ar ON ar.[Id] = rlr.[AccessRequestId]
    LEFT JOIN [dbo].[Workers] w ON w.[Id] = ar.[WorkerId]
    LEFT JOIN [dbo].[Rooms] r ON r.[Id] = ar.[RoomId]
    LEFT JOIN [dbo].[WorkAreas] wa ON wa.[Id] = r.[WorkAreaId]
    LEFT JOIN [dbo].[Sites] s ON s.[Id] = wa.[SiteId]
    LEFT JOIN [dbo].[Users] u ON u.[Id] = rlr.[ReviewedByUserId]
    WHERE (@Id IS NULL OR rlr.[Id] = @Id)
      AND (@AccessRequestId IS NULL OR rlr.[AccessRequestId] = @AccessRequestId)
      AND (@Status IS NULL OR rlr.[Status] = @Status)
    ORDER BY
        CASE WHEN rlr.[Status] = 'Pending' THEN 0 ELSE 1 END,
        CASE WHEN rlr.[Status] = 'Pending' THEN rlr.[RequestedAt] END ASC,
        rlr.[ReviewedAt] DESC;
END
GO

/* Records the Keyman's decision. Only a pending review can be decided, so two
   keymen acting at once cannot overwrite each other; RowsAffected = 0 says it
   was already decided. */
CREATE OR ALTER PROCEDURE [dbo].[RoomLeaveReviewsUpdate]
    @Id INT,
    @Status NVARCHAR(20),
    @ReviewedByUserId INT,
    @Reason NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[RoomLeaveReviews]
    SET [Status] = @Status,
        [ReviewedByUserId] = @ReviewedByUserId,
        [Reason] = @Reason,
        [ReviewedAt] = GETDATE()
    WHERE [Id] = @Id
      AND [Status] = 'Pending';

    SELECT @@ROWCOUNT AS [RowsAffected];
END
GO
