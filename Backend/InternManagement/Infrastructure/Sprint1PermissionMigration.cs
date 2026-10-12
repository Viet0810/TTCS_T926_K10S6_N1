using Microsoft.Data.SqlClient;
using InternManagement.Services;
namespace InternManagement.Infrastructure;

public static class Sprint1PermissionMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF OBJECT_ID('dbo.PermissionRoles','U') IS NULL
                CREATE TABLE dbo.PermissionRoles(Role VARCHAR(10) NOT NULL PRIMARY KEY);
            IF OBJECT_ID('dbo.RolePermissions','U') IS NULL
                CREATE TABLE dbo.RolePermissions(
                    Role VARCHAR(10) NOT NULL REFERENCES dbo.PermissionRoles(Role),
                    Permission VARCHAR(64) NOT NULL,
                    PRIMARY KEY(Role,Permission));
            """;
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = """
            IF OBJECT_ID('dbo.Permissions','U') IS NULL
                CREATE TABLE dbo.Permissions(Code VARCHAR(64) NOT NULL PRIMARY KEY,
                    Module VARCHAR(64) NOT NULL, Action VARCHAR(16) NOT NULL, DisplayName NVARCHAR(200) NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(ct);
        foreach (var entry in PermissionCatalog.Entries)
        {
            command.Parameters.Clear();
            command.CommandText = """
                IF NOT EXISTS(SELECT 1 FROM dbo.Permissions WITH(UPDLOCK,HOLDLOCK) WHERE Code=@code)
                BEGIN INSERT dbo.Permissions(Code,Module,Action,DisplayName) VALUES(@code,@module,@action,@label); SELECT 1; END
                ELSE BEGIN UPDATE dbo.Permissions SET Module=@module,Action=@action,DisplayName=@label WHERE Code=@code; SELECT 0; END;
                """;
            command.Parameters.AddWithValue("@code",entry.Code); command.Parameters.AddWithValue("@module",entry.Module);
            command.Parameters.AddWithValue("@action",entry.Action); command.Parameters.AddWithValue("@label",entry.Label);
            var introduced = Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
            // Convert existing bundled grants once. Revoked/newly absent grants are never reset at startup.
            var legacy = entry.Code switch
            {
                PermissionNames.CreateContract or PermissionNames.UpdateContract => PermissionNames.ManageContracts,
                PermissionNames.CreateProgram or PermissionNames.DeleteProgram => PermissionNames.ManagePrograms,
                PermissionNames.CreateAssignment or PermissionNames.UpdateAssignment => PermissionNames.AssignMentor,
                PermissionNames.ViewOwnProfile or PermissionNames.EditOwnProfile or PermissionNames.ViewOwnSchedule => PermissionNames.ViewProfile,
                _ => null
            };
            if (introduced && legacy is not null)
            {
                command.CommandText = "INSERT dbo.RolePermissions(Role,Permission) SELECT Role,@code FROM dbo.RolePermissions WHERE Permission=@legacy AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions x WHERE x.Role=RolePermissions.Role AND x.Permission=@code)";
                command.Parameters.AddWithValue("@legacy",legacy);
                if(entry.Code is PermissionNames.ViewOwnProfile or PermissionNames.EditOwnProfile or PermissionNames.ViewOwnSchedule)
                    command.CommandText += " AND Role='INTERN'";
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        command.Parameters.Clear();
        command.CommandText = """
            IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name='FK_RolePermissions_Permissions')
                ALTER TABLE dbo.RolePermissions ADD CONSTRAINT FK_RolePermissions_Permissions
                    FOREIGN KEY(Permission) REFERENCES dbo.Permissions(Code);
            """;
        await command.ExecuteNonQueryAsync(ct);
        foreach (var role in new[] { "ADMIN", "HR", "MENTOR", "INTERN" })
        {
            command.Parameters.Clear();
            command.CommandText = "IF NOT EXISTS(SELECT 1 FROM dbo.PermissionRoles WITH(UPDLOCK,HOLDLOCK) WHERE Role=@role) BEGIN INSERT dbo.PermissionRoles(Role) VALUES(@role); SELECT 1; END ELSE SELECT 0;";
            command.Parameters.AddWithValue("@role", role);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0) continue;
            command.CommandText = "INSERT dbo.RolePermissions(Role,Permission) VALUES(@role,@permission)";
            var parameter = command.Parameters.Add("@permission", System.Data.SqlDbType.VarChar, 64);
            foreach (var code in new RolePermissionService().GetDefaults(role))
            {
                parameter.Value = code;
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        // Existing usernames are login emails; retain all values and extend the limit.
        command.Parameters.Clear();
        command.CommandText = """
            IF COL_LENGTH('dbo.Users','Username') < 508
            BEGIN
                ALTER TABLE dbo.Users DROP CONSTRAINT UQ_Users_Username;
                ALTER TABLE dbo.Users ALTER COLUMN Username NVARCHAR(254) NOT NULL;
                ALTER TABLE dbo.Users ADD CONSTRAINT UQ_Users_Username UNIQUE(Username);
            END;
            """;
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
