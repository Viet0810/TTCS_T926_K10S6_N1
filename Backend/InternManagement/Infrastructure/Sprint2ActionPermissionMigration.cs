using Microsoft.Data.SqlClient;
namespace InternManagement.Infrastructure;

// Split previously bundled actions once, preserving all existing grants and later revocations.
public static class Sprint2ActionPermissionMigration
{
    public static async Task ApplyAsync(SqlConnection connection,CancellationToken ct)
    {
        await using var command=connection.CreateCommand();
        command.CommandText="""
            SET XACT_ABORT ON; BEGIN TRANSACTION;
            IF OBJECT_ID('dbo.PermissionMigrations','U') IS NULL
                CREATE TABLE dbo.PermissionMigrations(Name varchar(100) NOT NULL PRIMARY KEY);
            IF NOT EXISTS(SELECT 1 FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='Sprint2Actions')
            BEGIN
                INSERT dbo.RolePermissions(Role,Permission)
                SELECT r.Role,m.NewCode FROM dbo.RolePermissions r
                JOIN (VALUES('APPROVE_DOCUMENTS','REJECT_DOCUMENTS'),('OWN_ATTENDANCE','ATTENDANCE_CHECK_IN'),('OWN_ATTENDANCE','ATTENDANCE_CHECK_OUT')) m(OldCode,NewCode) ON r.Permission=m.OldCode
                WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions x WHERE x.Role=r.Role AND x.Permission=m.NewCode);
                INSERT dbo.PermissionMigrations(Name) VALUES('Sprint2Actions');
            END;
            COMMIT;
            """;
        await command.ExecuteNonQueryAsync(ct);
    }
}
