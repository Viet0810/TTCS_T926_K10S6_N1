using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class WeeklyReportMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF OBJECT_ID('dbo.WeeklyReports','U') IS NULL
            BEGIN
                CREATE TABLE dbo.WeeklyReports(
                    Id INT IDENTITY PRIMARY KEY,
                    InternId INT NOT NULL REFERENCES dbo.Interns(Id),
                    SubmittedByUserId INT NOT NULL REFERENCES dbo.Users(Id),
                    InternshipProgramId INT NULL REFERENCES dbo.InternshipPrograms(Id),
                    WeekStartDate DATE NOT NULL,
                    Content NVARCHAR(4000) NOT NULL CHECK(LEN(LTRIM(RTRIM(Content))) > 0),
                    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                    CONSTRAINT UQ_WeeklyReports_InternWeek UNIQUE(InternId,WeekStartDate)
                );
                CREATE TABLE dbo.WeeklyReportFeedbacks(
                    Id INT IDENTITY PRIMARY KEY,
                    ReportId INT NOT NULL REFERENCES dbo.WeeklyReports(Id),
                    MentorUserId INT NOT NULL REFERENCES dbo.Users(Id),
                    Content NVARCHAR(2000) NOT NULL CHECK(LEN(LTRIM(RTRIM(Content))) > 0),
                    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
                );
                CREATE INDEX IX_WeeklyReportFeedbacks_Report ON dbo.WeeklyReportFeedbacks(ReportId,Id);
            END;
            IF COL_LENGTH('dbo.WeeklyReports','WorkDone') IS NULL
                ALTER TABLE dbo.WeeklyReports ADD WorkDone NVARCHAR(4000) NULL,Results NVARCHAR(4000) NULL,Difficulties NVARCHAR(4000) NULL,NextPlan NVARCHAR(4000) NULL;
            IF NOT EXISTS(SELECT 1 FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='US17US18WeeklyReports')
            BEGIN
                INSERT dbo.RolePermissions(Role,Permission)
                SELECT v.Role,v.Permission FROM (VALUES
                    ('INTERN','OWN_WEEKLY_REPORT_READ'),('INTERN','WEEKLY_REPORT_CREATE'),
                    ('MENTOR','MENTOR_WEEKLY_REPORT_READ'),('MENTOR','WEEKLY_REPORT_FEEDBACK')
                ) v(Role,Permission)
                WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions r WHERE r.Role=v.Role AND r.Permission=v.Permission);
                INSERT dbo.PermissionMigrations(Name) VALUES('US17US18WeeklyReports');
            END;
            """;
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
