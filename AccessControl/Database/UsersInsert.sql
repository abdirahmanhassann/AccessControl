USE [AccessControl]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Works with both the current API (inserts, then calls UsersSetPassword)
-- and the fixed API (passes @Password/@Salt directly).
CREATE OR ALTER PROCEDURE [dbo].[UsersInsert]
    @FirstName   NVARCHAR(100),
    @LastName    NVARCHAR(100),
    @Email       NVARCHAR(255),
    @PhoneNumber NVARCHAR(30) = NULL,
    @Role        NVARCHAR(50),
    @Password    NVARCHAR(50) = NULL,
    @Salt        NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Users] ([FirstName], [LastName], [Email], [PhoneNumber], [Role], [Password], [salt])
    VALUES (@FirstName, @LastName, @Email, @PhoneNumber, @Role, ISNULL(@Password, N''), ISNULL(@Salt, N''));

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS [Id];
END;
GO
