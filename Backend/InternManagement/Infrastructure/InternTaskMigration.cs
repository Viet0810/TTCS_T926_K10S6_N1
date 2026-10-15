using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class InternTaskMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF OBJECT_ID('dbo.InternTasks','U') IS NULL
            BEGIN
                CREATE TABLE dbo.InternTasks (
                    Id INT IDENTITY PRIMARY KEY,
                    InternId INT NOT NULL REFERENCES dbo.Interns(Id),
                    MentorUserId INT NOT NULL REFERENCES dbo.Users(Id),
                    Content NVARCHAR(2000) NOT NULL CHECK(LEN(LTRIM(RTRIM(Content))) > 0),
                    Deadline DATETIMEOFFSET NOT NULL,
                    Status VARCHAR(20) NOT NULL DEFAULT 'ASSIGNED' CHECK(Status IN ('ASSIGNED')),
                    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET()
                );
                CREATE INDEX IX_InternTasks_InternId ON dbo.InternTasks(InternId,Id);
                CREATE INDEX IX_InternTasks_MentorUserId ON dbo.InternTasks(MentorUserId,Id);
            END;
            IF NOT EXISTS(SELECT 1 FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='US15Tasks')
            BEGIN
                INSERT dbo.RolePermissions(Role,Permission)
                SELECT v.Role,v.Permission FROM (VALUES
                    ('MENTOR','MENTOR_TASK_READ'),('MENTOR','TASK_CREATE'),('INTERN','OWN_TASK_READ')
                ) v(Role,Permission)
                WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions r WHERE r.Role=v.Role AND r.Permission=v.Permission);
                INSERT dbo.PermissionMigrations(Name) VALUES('US15Tasks');
            END;
            """;
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
