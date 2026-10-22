using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class EvaluationSummaryMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID('dbo.EvaluationClassificationRules', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EvaluationClassificationRules (
                    Id INT IDENTITY PRIMARY KEY,
                    Label NVARCHAR(50) NOT NULL UNIQUE,
                    MinimumScore DECIMAL(5,2) NOT NULL,
                    MaximumScore DECIMAL(5,2) NULL,
                    SortOrder INT NOT NULL,
                    IsActive BIT NOT NULL DEFAULT 1,
                    CONSTRAINT CK_EvaluationClassificationRules_Range CHECK (
                        MinimumScore BETWEEN 0 AND 100
                        AND (MaximumScore IS NULL OR MaximumScore > MinimumScore)
                    )
                );
            END;

            IF OBJECT_ID('dbo.EvaluationSummaries', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EvaluationSummaries (
                    Id INT IDENTITY PRIMARY KEY,
                    InternId INT NOT NULL UNIQUE REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    RequiredEvaluationCount INT NOT NULL DEFAULT 1,
                    AggregateComment NVARCHAR(2000) NULL,
                    FinalClassification NVARCHAR(50) NULL,
                    Status VARCHAR(20) NOT NULL DEFAULT 'DRAFT',
                    FinalizedByUserId INT NULL REFERENCES dbo.Users(Id),
                    FinalizedAt DATETIMEOFFSET NULL,
                    PublishedByUserId INT NULL REFERENCES dbo.Users(Id),
                    PublishedAt DATETIMEOFFSET NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                    UpdatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                    CONSTRAINT CK_EvaluationSummaries_Required CHECK (RequiredEvaluationCount > 0),
                    CONSTRAINT CK_EvaluationSummaries_Status CHECK (Status IN ('DRAFT','FINALIZED','PUBLISHED'))
                );
            END;

            IF OBJECT_ID('dbo.EvaluationSummaryHistory', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.EvaluationSummaryHistory (
                    Id BIGINT IDENTITY PRIMARY KEY,
                    SummaryId INT NOT NULL REFERENCES dbo.EvaluationSummaries(Id) ON DELETE CASCADE,
                    Action VARCHAR(20) NOT NULL CHECK (Action IN ('SAVED','FINALIZED','PUBLISHED')),
                    ActorUserId INT NOT NULL REFERENCES dbo.Users(Id),
                    AggregateComment NVARCHAR(2000) NULL,
                    FinalClassification NVARCHAR(50) NULL,
                    OccurredAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
                );
                CREATE INDEX IX_EvaluationSummaryHistory_SummaryId
                    ON dbo.EvaluationSummaryHistory(SummaryId, OccurredAt DESC);
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM dbo.EvaluationClassificationRules)
            BEGIN
                INSERT dbo.EvaluationClassificationRules(Label,MinimumScore,MaximumScore,SortOrder) VALUES
                    (N'Xuất sắc',90,NULL,1),(N'Tốt',80,90,2),(N'Khá',70,80,3),
                    (N'Đạt',50,70,4),(N'Chưa đạt',0,50,5);
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code='EVALUATION_SUMMARY_READ')
                INSERT dbo.Permissions(Code,Module,DisplayName,Action)
                VALUES('EVALUATION_SUMMARY_READ','evaluation-summary',N'Tổng hợp đánh giá','READ');
            IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code='EVALUATION_SUMMARY_FINALIZE')
                INSERT dbo.Permissions(Code,Module,DisplayName,Action)
                VALUES('EVALUATION_SUMMARY_FINALIZE','evaluation-summary',N'Tổng hợp đánh giá','FINALIZE');
            IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code='EVALUATION_SUMMARY_PUBLISH')
                INSERT dbo.Permissions(Code,Module,DisplayName,Action)
                VALUES('EVALUATION_SUMMARY_PUBLISH','evaluation-summary',N'Tổng hợp đánh giá','PUBLISH');

            IF NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE Role='INTERN' AND Permission='VIEW_FINAL_EVALUATIONS')
                INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','VIEW_FINAL_EVALUATIONS');
            IF NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE Role='MENTOR' AND Permission='VIEW_FINAL_EVALUATIONS')
                INSERT dbo.RolePermissions(Role,Permission) VALUES('MENTOR','VIEW_FINAL_EVALUATIONS');
            IF NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE Role='MENTOR' AND Permission='UPDATE_FINAL_EVALUATIONS')
                INSERT dbo.RolePermissions(Role,Permission) VALUES('MENTOR','UPDATE_FINAL_EVALUATIONS');

            IF NOT EXISTS (SELECT 1 FROM dbo.PermissionMigrations WHERE Name='EvaluationSummaryDefaults')
            BEGIN
                INSERT dbo.RolePermissions(Role,Permission)
                SELECT role.Code, permission.Code
                FROM (VALUES('ADMIN'),('HR')) role(Code)
                CROSS JOIN (VALUES('EVALUATION_SUMMARY_READ'),('EVALUATION_SUMMARY_FINALIZE'),('EVALUATION_SUMMARY_PUBLISH')) permission(Code)
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.RolePermissions existing
                    WHERE existing.Role=role.Code AND existing.Permission=permission.Code
                );
                INSERT dbo.PermissionMigrations(Name) VALUES('EvaluationSummaryDefaults');
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
