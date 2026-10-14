using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class NotificationMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Notifications (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
                    UserId INT NOT NULL CONSTRAINT FK_Notifications_Users REFERENCES dbo.Users(Id) ON DELETE CASCADE,
                    Title NVARCHAR(200) NOT NULL,
                    Message NVARCHAR(1000) NOT NULL,
                    Category VARCHAR(50) NOT NULL CONSTRAINT CK_Notifications_Category CHECK (Category IN ('ATTENDANCE', 'DOCUMENT', 'CONTRACT', 'ASSIGNMENT', 'PROGRAM', 'SYSTEM')),
                    Type VARCHAR(50) NOT NULL,
                    Priority VARCHAR(20) NOT NULL CONSTRAINT DF_Notifications_Priority DEFAULT 'INFO' CONSTRAINT CK_Notifications_Priority CHECK (Priority IN ('INFO', 'SUCCESS', 'WARNING', 'DANGER')),
                    ActionUrl NVARCHAR(500) NULL,
                    IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT 0,
                    ReadAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CreatedBy INT NULL CONSTRAINT FK_Notifications_Creator REFERENCES dbo.Users(Id)
                );

                CREATE NONCLUSTERED INDEX IX_Notifications_UserId_IsRead_CreatedAt 
                ON dbo.Notifications (UserId, IsRead, CreatedAt DESC);
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
