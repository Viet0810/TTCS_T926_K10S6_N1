using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class MeetingMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID('dbo.Meetings','U') IS NULL
            BEGIN
                CREATE TABLE dbo.Meetings(
                    Id INT IDENTITY PRIMARY KEY,
                    Title NVARCHAR(200) NOT NULL CHECK(LEN(LTRIM(RTRIM(Title))) > 0),
                    StartTime DATETIMEOFFSET NOT NULL,
                    EndTime DATETIMEOFFSET NOT NULL,
                    LocationOrLink NVARCHAR(500) NULL,
                    Description NVARCHAR(2000) NULL,
                    CreatedByUserId INT NOT NULL REFERENCES dbo.Users(Id),
                    ClientRequestId UNIQUEIDENTIFIER NOT NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
                );
                CREATE UNIQUE INDEX UX_Meetings_CreatorRequest ON dbo.Meetings(CreatedByUserId,ClientRequestId);
                CREATE TABLE dbo.MeetingParticipants(
                    MeetingId INT NOT NULL REFERENCES dbo.Meetings(Id) ON DELETE CASCADE,
                    UserId INT NOT NULL REFERENCES dbo.Users(Id),
                    PRIMARY KEY(MeetingId,UserId)
                );
                CREATE TABLE dbo.MeetingEmailDeliveries(
                    MeetingId INT NOT NULL REFERENCES dbo.Meetings(Id) ON DELETE CASCADE,
                    UserId INT NOT NULL REFERENCES dbo.Users(Id),
                    TriggerType VARCHAR(32) NOT NULL,
                    Status VARCHAR(16) NOT NULL CHECK(Status IN ('SENDING','SENT')),
                    SentAt DATETIMEOFFSET NULL,
                    PRIMARY KEY(MeetingId,UserId,TriggerType)
                );
                CREATE INDEX IX_MeetingParticipants_UserId ON dbo.MeetingParticipants(UserId,MeetingId);
            END;
            IF COL_LENGTH('dbo.Meetings','EndTime') IS NULL
            BEGIN
                ALTER TABLE dbo.Meetings ADD EndTime DATETIMEOFFSET NULL;
                EXEC('UPDATE dbo.Meetings SET EndTime=DATEADD(hour,1,StartTime) WHERE EndTime IS NULL');
                ALTER TABLE dbo.Meetings ALTER COLUMN EndTime DATETIMEOFFSET NOT NULL;
            END;
            IF COL_LENGTH('dbo.Meetings','ClientRequestId') IS NULL
            BEGIN
                ALTER TABLE dbo.Meetings ADD ClientRequestId UNIQUEIDENTIFIER NULL;
                EXEC('UPDATE dbo.Meetings SET ClientRequestId=NEWID() WHERE ClientRequestId IS NULL');
                ALTER TABLE dbo.Meetings ALTER COLUMN ClientRequestId UNIQUEIDENTIFIER NOT NULL;
                CREATE UNIQUE INDEX UX_Meetings_CreatorRequest ON dbo.Meetings(CreatedByUserId,ClientRequestId);
            END;
            """;
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = """
            IF OBJECT_ID('dbo.PermissionMigrations','U') IS NOT NULL
            BEGIN
                BEGIN TRANSACTION;
                IF NOT EXISTS(SELECT 1 FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='US35Meetings')
                BEGIN
                    INSERT dbo.RolePermissions(Role,Permission)
                    SELECT 'HR',v.Code FROM (VALUES('MEETING_READ'),('MEETING_CREATE')) v(Code)
                    WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions r WHERE r.Role='HR' AND r.Permission=v.Code);
                    INSERT dbo.PermissionMigrations(Name) VALUES('US35Meetings');
                END;
                COMMIT TRANSACTION;
            END;
            """;
        await command.ExecuteNonQueryAsync(ct);
    }
}
