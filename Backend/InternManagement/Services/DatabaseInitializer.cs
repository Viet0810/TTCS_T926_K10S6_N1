using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class DatabaseInitializer
{
    public async Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Users (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                    Username NVARCHAR(100) NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
                    FullName NVARCHAR(200) NOT NULL,
                    Email NVARCHAR(254) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
                    PasswordHash NVARCHAR(512) NOT NULL,
                    Role VARCHAR(10) NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('ADMIN', 'HR', 'MENTOR', 'INTERN')),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME()
                );
            END;

            IF OBJECT_ID(N'dbo.Interns', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Interns (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Interns PRIMARY KEY,
                    FullName NVARCHAR(200) NOT NULL,
                    Email NVARCHAR(254) NOT NULL CONSTRAINT UQ_Interns_Email UNIQUE,
                    Phone NVARCHAR(20) NOT NULL,
                    School NVARCHAR(200) NOT NULL,
                    Major NVARCHAR(200) NOT NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Interns_CreatedAt DEFAULT SYSDATETIMEOFFSET()
                );
            END;

            IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.PasswordResetTokens (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
                    UserId INT NOT NULL CONSTRAINT FK_PasswordResetTokens_Users REFERENCES dbo.Users(Id) ON DELETE CASCADE,
                    TokenHash BINARY(32) NOT NULL CONSTRAINT UQ_PasswordResetTokens_Hash UNIQUE,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PasswordResetTokens_Created DEFAULT SYSUTCDATETIME(),
                    ExpiresAt DATETIME2 NOT NULL,
                    UsedAt DATETIME2 NULL
                );
                CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens(UserId, CreatedAt);
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = """
            IF COL_LENGTH(N'dbo.Interns', N'StudentCode') IS NULL
                ALTER TABLE dbo.Interns ADD [StudentCode] NVARCHAR(50) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'ClassName') IS NULL
                ALTER TABLE dbo.Interns ADD [ClassName] NVARCHAR(100) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Faculty') IS NULL
                ALTER TABLE dbo.Interns ADD [Faculty] NVARCHAR(200) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'DateOfBirth') IS NULL
                ALTER TABLE dbo.Interns ADD [DateOfBirth] DATE NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Address') IS NULL
                ALTER TABLE dbo.Interns ADD [Address] NVARCHAR(500) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Organization') IS NULL
                ALTER TABLE dbo.Interns ADD [Organization] NVARCHAR(200) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'OrganizationAddress') IS NULL
                ALTER TABLE dbo.Interns ADD [OrganizationAddress] NVARCHAR(500) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Department') IS NULL
                ALTER TABLE dbo.Interns ADD [Department] NVARCHAR(200) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Position') IS NULL
                ALTER TABLE dbo.Interns ADD [Position] NVARCHAR(200) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Mentor') IS NULL
                ALTER TABLE dbo.Interns ADD [Mentor] NVARCHAR(200) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'MentorEmail') IS NULL
                ALTER TABLE dbo.Interns ADD [MentorEmail] NVARCHAR(254) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'MentorPhone') IS NULL
                ALTER TABLE dbo.Interns ADD [MentorPhone] NVARCHAR(20) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'AcademicSupervisor') IS NULL
                ALTER TABLE dbo.Interns ADD [AcademicSupervisor] NVARCHAR(200) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'StartDate') IS NULL
                ALTER TABLE dbo.Interns ADD [StartDate] DATE NULL;
            IF COL_LENGTH(N'dbo.Interns', N'EndDate') IS NULL
                ALTER TABLE dbo.Interns ADD [EndDate] DATE NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Status') IS NULL
                ALTER TABLE dbo.Interns ADD [Status] NVARCHAR(50) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'InternshipTopic') IS NULL
                ALTER TABLE dbo.Interns ADD [InternshipTopic] NVARCHAR(500) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Notes') IS NULL
                ALTER TABLE dbo.Interns ADD [Notes] NVARCHAR(2000) NULL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = """
            IF OBJECT_ID(N'dbo.InternAttendances', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.InternAttendances (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InternAttendances PRIMARY KEY,
                    InternId INT NOT NULL CONSTRAINT FK_InternAttendances_Interns REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    WorkDate DATE NOT NULL,
                    CheckInAt DATETIMEOFFSET NOT NULL,
                    CheckOutAt DATETIMEOFFSET NULL,
                    CONSTRAINT UQ_InternAttendances_InternId_WorkDate UNIQUE (InternId, WorkDate)
                );
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = """
            IF OBJECT_ID(N'dbo.InternDocuments', N'U') IS NULL
            CREATE TABLE dbo.InternDocuments (
                InternId INT NOT NULL REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                Kind VARCHAR(20) NOT NULL CHECK (Kind IN ('cv','application')),
                FileName NVARCHAR(255) NOT NULL,
                Content VARBINARY(MAX) NOT NULL CHECK (DATALENGTH(Content) BETWEEN 1 AND 5242880),
                UploadedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                CONSTRAINT PK_InternDocuments PRIMARY KEY (InternId,Kind)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = """
            IF COL_LENGTH('dbo.InternDocuments','ReviewStatus') IS NULL
                ALTER TABLE dbo.InternDocuments ADD ReviewStatus VARCHAR(20) NOT NULL CONSTRAINT DF_DocumentReviewStatus DEFAULT 'pending' CHECK (ReviewStatus IN ('pending','approved','rejected'));
            IF COL_LENGTH('dbo.InternDocuments','ReviewComment') IS NULL ALTER TABLE dbo.InternDocuments ADD ReviewComment NVARCHAR(2000) NULL;
            IF COL_LENGTH('dbo.InternDocuments','ReviewedAt') IS NULL ALTER TABLE dbo.InternDocuments ADD ReviewedAt DATETIME2 NULL;
            IF COL_LENGTH('dbo.InternDocuments','ReviewedBy') IS NULL ALTER TABLE dbo.InternDocuments ADD ReviewedBy INT NULL REFERENCES dbo.Users(Id) ON DELETE SET NULL;
            IF COL_LENGTH('dbo.InternDocuments','Version') IS NULL ALTER TABLE dbo.InternDocuments ADD Version ROWVERSION;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
