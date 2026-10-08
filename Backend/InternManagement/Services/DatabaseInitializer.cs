using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class DatabaseInitializer
{
    private readonly PasswordHasher passwords;

    public DatabaseInitializer(PasswordHasher passwords)
    {
        this.passwords = passwords;
    }

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

            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin.demo')
            BEGIN
                INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
                VALUES (N'admin.demo', N'Quản trị viên Demo', N'admin.demo@example.com', @passwordHash, 'ADMIN');
            END;

            -- Existing installs may already use a singular/legacy profile table.
            -- Only add the table used by this feature when no equivalent is present.
            IF OBJECT_ID(N'dbo.Interns', N'U') IS NULL
               AND OBJECT_ID(N'dbo.Intern', N'U') IS NULL
               AND OBJECT_ID(N'dbo.InternProfiles', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Interns (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Interns PRIMARY KEY,
                    FullName NVARCHAR(200) NOT NULL,
                    Mssv NVARCHAR(50) NOT NULL CONSTRAINT UQ_Interns_Mssv UNIQUE,
                    Email NVARCHAR(254) NOT NULL,
                    Phone NVARCHAR(30) NULL,
                    School NVARCHAR(200) NOT NULL,
                    Major NVARCHAR(150) NOT NULL,
                    InternshipPosition NVARCHAR(150) NOT NULL,
                    Status NVARCHAR(50) NOT NULL,
                    Mentor NVARCHAR(200) NULL,
                    Progress INT NOT NULL CONSTRAINT DF_Interns_Progress DEFAULT 0,
                    Gpa NVARCHAR(20) NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Interns_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT CK_Interns_Progress CHECK (Progress BETWEEN 0 AND 100)
                );
            END;

            IF OBJECT_ID(N'dbo.InternContracts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.InternContracts (
                    ContractId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InternContracts PRIMARY KEY,
                    InternId INT NOT NULL CONSTRAINT UQ_InternContracts_InternId UNIQUE,
                    FileName NVARCHAR(255) NOT NULL,
                    StoredFileName NVARCHAR(100) NOT NULL,
                    ContentType NVARCHAR(100) NOT NULL,
                    FileSize BIGINT NOT NULL,
                    UploadedAt DATETIME2 NOT NULL CONSTRAINT DF_InternContracts_UploadedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_InternContracts_Interns FOREIGN KEY (InternId) REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_InternContracts_FileSize CHECK (FileSize > 0 AND FileSize <= 10485760)
                );
            END;

            IF OBJECT_ID(N'dbo.PasswordResetRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.PasswordResetRequests (
                    UserId INT NOT NULL CONSTRAINT PK_PasswordResetRequests PRIMARY KEY,
                    CodeProtected NVARCHAR(2000) NOT NULL,
                    ExpiresAt DATETIME2 NOT NULL,
                    Attempts INT NOT NULL CONSTRAINT DF_PasswordResetRequests_Attempts DEFAULT 0,
                    ResetTokenHash CHAR(64) NULL,
                    ResetTokenExpiresAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_PasswordResetRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_PasswordResetRequests_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
                );
            END;

            IF COL_LENGTH(N'dbo.Users', N'Phone') IS NULL ALTER TABLE dbo.Users ADD Phone NVARCHAR(30) NULL;
            IF COL_LENGTH(N'dbo.Users', N'AvatarUrl') IS NULL ALTER TABLE dbo.Users ADD AvatarUrl NVARCHAR(1000) NULL;
            IF COL_LENGTH(N'dbo.Users', N'Position') IS NULL ALTER TABLE dbo.Users ADD Position NVARCHAR(150) NULL;
            IF COL_LENGTH(N'dbo.Users', N'Department') IS NULL ALTER TABLE dbo.Users ADD Department NVARCHAR(150) NULL;
            IF COL_LENGTH(N'dbo.Users', N'Skills') IS NULL ALTER TABLE dbo.Users ADD Skills NVARCHAR(2000) NULL;
            IF COL_LENGTH(N'dbo.Users', N'Experience') IS NULL ALTER TABLE dbo.Users ADD Experience NVARCHAR(4000) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Department') IS NULL ALTER TABLE dbo.Interns ADD Department NVARCHAR(150) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'Batch') IS NULL ALTER TABLE dbo.Interns ADD Batch NVARCHAR(100) NULL;
            IF COL_LENGTH(N'dbo.Interns', N'StartDate') IS NULL ALTER TABLE dbo.Interns ADD StartDate DATE NULL;
            IF COL_LENGTH(N'dbo.Interns', N'EndDate') IS NULL ALTER TABLE dbo.Interns ADD EndDate DATE NULL;
            IF COL_LENGTH(N'dbo.Interns', N'UserId') IS NULL ALTER TABLE dbo.Interns ADD UserId INT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Interns_Users')
                EXEC(N'ALTER TABLE dbo.Interns ADD CONSTRAINT FK_Interns_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)');
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Interns') AND name=N'UX_Interns_UserId')
                EXEC(N'CREATE UNIQUE INDEX UX_Interns_UserId ON dbo.Interns(UserId) WHERE UserId IS NOT NULL');

            IF OBJECT_ID(N'dbo.MentorInternAssignments', N'U') IS NULL
                CREATE TABLE dbo.MentorInternAssignments (
                    InternId INT NOT NULL CONSTRAINT PK_MentorInternAssignments PRIMARY KEY,
                    MentorUserId INT NOT NULL,
                    AssignedAt DATETIME2 NOT NULL CONSTRAINT DF_MentorInternAssignments_AssignedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_MentorInternAssignments_Interns FOREIGN KEY (InternId) REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_MentorInternAssignments_Users FOREIGN KEY (MentorUserId) REFERENCES dbo.Users(Id)
                );

            IF OBJECT_ID(N'dbo.InternTasks', N'U') IS NULL
                CREATE TABLE dbo.InternTasks (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InternTasks PRIMARY KEY,
                    InternId INT NOT NULL,
                    MentorUserId INT NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Description NVARCHAR(2000) NULL,
                    AssignedAt DATETIME2 NOT NULL CONSTRAINT DF_InternTasks_AssignedAt DEFAULT SYSUTCDATETIME(),
                    Deadline DATE NULL,
                    Priority NVARCHAR(20) NOT NULL CONSTRAINT DF_InternTasks_Priority DEFAULT N'Medium',
                    Status NVARCHAR(30) NOT NULL CONSTRAINT DF_InternTasks_Status DEFAULT N'NotStarted',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_InternTasks_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_InternTasks_Interns FOREIGN KEY (InternId) REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_InternTasks_Mentor FOREIGN KEY (MentorUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT CK_InternTasks_Status CHECK (Status IN (N'NotStarted',N'InProgress',N'PendingReview',N'Completed')),
                    CONSTRAINT CK_InternTasks_Priority CHECK (Priority IN (N'Low',N'Medium',N'High'))
                );

            IF OBJECT_ID(N'dbo.InternReports', N'U') IS NULL
                CREATE TABLE dbo.InternReports (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InternReports PRIMARY KEY,
                    InternId INT NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    AttachmentUrl NVARCHAR(1000) NULL,
                    Status NVARCHAR(30) NOT NULL CONSTRAINT DF_InternReports_Status DEFAULT N'Submitted',
                    SubmittedAt DATETIME2 NOT NULL CONSTRAINT DF_InternReports_SubmittedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_InternReports_Interns FOREIGN KEY (InternId) REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_InternReports_Status CHECK (Status IN (N'Submitted',N'InReview',N'RevisionRequested',N'Approved',N'Rejected'))
                );

            IF OBJECT_ID(N'dbo.ReportReviews', N'U') IS NULL
                CREATE TABLE dbo.ReportReviews (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReportReviews PRIMARY KEY,
                    ReportId INT NOT NULL,
                    MentorUserId INT NOT NULL,
                    Status NVARCHAR(30) NOT NULL,
                    Feedback NVARCHAR(2000) NOT NULL,
                    Score DECIMAL(4,2) NULL,
                    ReviewedAt DATETIME2 NOT NULL CONSTRAINT DF_ReportReviews_ReviewedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_ReportReviews_Reports FOREIGN KEY (ReportId) REFERENCES dbo.InternReports(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_ReportReviews_Mentor FOREIGN KEY (MentorUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT CK_ReportReviews_Status CHECK (Status IN (N'RevisionRequested',N'Approved',N'Rejected')),
                    CONSTRAINT CK_ReportReviews_Score CHECK (Score IS NULL OR Score BETWEEN 0 AND 10)
                );

            IF OBJECT_ID(N'dbo.InternEvaluations', N'U') IS NULL
                CREATE TABLE dbo.InternEvaluations (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InternEvaluations PRIMARY KEY,
                    InternId INT NOT NULL,
                    MentorUserId INT NOT NULL,
                    Comments NVARCHAR(3000) NULL,
                    Recommendation NVARCHAR(1000) NULL,
                    IsSubmitted BIT NOT NULL CONSTRAINT DF_InternEvaluations_IsSubmitted DEFAULT 0,
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_InternEvaluations_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT UQ_InternEvaluations_InternMentor UNIQUE (InternId,MentorUserId),
                    CONSTRAINT FK_InternEvaluations_Interns FOREIGN KEY (InternId) REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_InternEvaluations_Mentor FOREIGN KEY (MentorUserId) REFERENCES dbo.Users(Id)
                );

            IF OBJECT_ID(N'dbo.InternEvaluationScores', N'U') IS NULL
                CREATE TABLE dbo.InternEvaluationScores (
                    EvaluationId INT NOT NULL,
                    Criterion NVARCHAR(100) NOT NULL,
                    Score DECIMAL(4,2) NOT NULL,
                    CONSTRAINT PK_InternEvaluationScores PRIMARY KEY (EvaluationId,Criterion),
                    CONSTRAINT FK_InternEvaluationScores_Evaluation FOREIGN KEY (EvaluationId) REFERENCES dbo.InternEvaluations(Id) ON DELETE CASCADE,
                    CONSTRAINT CK_InternEvaluationScores_Score CHECK (Score BETWEEN 0 AND 10)
                );

            IF OBJECT_ID(N'dbo.InternFeedback', N'U') IS NULL
                CREATE TABLE dbo.InternFeedback (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_InternFeedback PRIMARY KEY,
                    InternId INT NOT NULL,
                    MentorUserId INT NOT NULL,
                    Content NVARCHAR(2000) NOT NULL,
                    Type NVARCHAR(30) NOT NULL,
                    Severity NVARCHAR(20) NOT NULL CONSTRAINT DF_InternFeedback_Severity DEFAULT N'Normal',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_InternFeedback_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_InternFeedback_Interns FOREIGN KEY (InternId) REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_InternFeedback_Mentor FOREIGN KEY (MentorUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT CK_InternFeedback_Type CHECK (Type IN (N'Praise',N'Suggestion',N'Warning',N'Improvement')),
                    CONSTRAINT CK_InternFeedback_Severity CHECK (Severity IN (N'Low',N'Normal',N'High'))
                );

            IF OBJECT_ID(N'dbo.MentoringSchedules', N'U') IS NULL
                CREATE TABLE dbo.MentoringSchedules (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MentoringSchedules PRIMARY KEY,
                    InternId INT NOT NULL,
                    MentorUserId INT NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Content NVARCHAR(2000) NULL,
                    StartsAt DATETIME2 NOT NULL,
                    EndsAt DATETIME2 NOT NULL,
                    Location NVARCHAR(500) NULL,
                    Notes NVARCHAR(1000) NULL,
                    Status NVARCHAR(20) NOT NULL CONSTRAINT DF_MentoringSchedules_Status DEFAULT N'Scheduled',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MentoringSchedules_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT FK_MentoringSchedules_Interns FOREIGN KEY (InternId) REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_MentoringSchedules_Mentor FOREIGN KEY (MentorUserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT CK_MentoringSchedules_Range CHECK (EndsAt > StartsAt),
                    CONSTRAINT CK_MentoringSchedules_Status CHECK (Status IN (N'Scheduled',N'Cancelled',N'Completed'))
                );

            IF OBJECT_ID(N'dbo.UserNotifications', N'U') IS NULL
                CREATE TABLE dbo.UserNotifications (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_UserNotifications PRIMARY KEY,
                    UserId INT NOT NULL,
                    Type NVARCHAR(50) NOT NULL,
                    Title NVARCHAR(200) NOT NULL,
                    Message NVARCHAR(1000) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_UserNotifications_CreatedAt DEFAULT SYSUTCDATETIME(),
                    ReadAt DATETIME2 NULL,
                    CONSTRAINT FK_UserNotifications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
                );
            """;
        command.Parameters.AddWithValue("@passwordHash", passwords.Hash("Demo@123456"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
