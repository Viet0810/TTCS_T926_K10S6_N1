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
            IF COL_LENGTH('dbo.InternTasks','Progress') IS NULL
                ALTER TABLE dbo.InternTasks ADD Progress INT NOT NULL CONSTRAINT DF_InternTasks_Progress DEFAULT 0,
                    ProgressNote NVARCHAR(2000) NULL, UpdatedAt DATETIMEOFFSET NULL;
            DECLARE @oldStatusCheck SYSNAME;
            DECLARE statusChecks CURSOR LOCAL FAST_FORWARD FOR
                SELECT name FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.InternTasks')
                    AND definition LIKE '%Status%' AND definition NOT LIKE '%IN_PROGRESS%';
            OPEN statusChecks;
            FETCH NEXT FROM statusChecks INTO @oldStatusCheck;
            WHILE @@FETCH_STATUS=0
            BEGIN
                DECLARE @dropStatusSql NVARCHAR(500)='ALTER TABLE dbo.InternTasks DROP CONSTRAINT '+QUOTENAME(@oldStatusCheck);
                EXEC sp_executesql @dropStatusSql;
                FETCH NEXT FROM statusChecks INTO @oldStatusCheck;
            END;
            CLOSE statusChecks;
            DEALLOCATE statusChecks;
            IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name='CK_InternTasks_ProgressStatus' AND parent_object_id=OBJECT_ID('dbo.InternTasks'))
                EXEC('ALTER TABLE dbo.InternTasks ADD CONSTRAINT CK_InternTasks_ProgressStatus CHECK(
                    Progress BETWEEN 0 AND 100 AND
                    ((Progress=0 AND Status=''ASSIGNED'') OR (Progress BETWEEN 1 AND 99 AND Status=''IN_PROGRESS'') OR
                     (Progress=100 AND Status=''COMPLETED'')))');
            IF NOT EXISTS(SELECT 1 FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='US16TaskProgress')
            BEGIN
                IF NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE Role='INTERN' AND Permission='OWN_TASK_UPDATE')
                    INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','OWN_TASK_UPDATE');
                INSERT dbo.PermissionMigrations(Name) VALUES('US16TaskProgress');
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
