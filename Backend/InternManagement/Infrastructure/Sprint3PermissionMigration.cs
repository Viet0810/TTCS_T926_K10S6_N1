using InternManagement.Services;
using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class Sprint3PermissionMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='Sprint3DynamicRbacDefaults'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 0)
        {
            // Introduce only the new default entitlements once; do not reseed on restart.
            var grants = PermissionCatalog.Entries.Select(p => (Role: "ADMIN", Code: p.Code)).Concat(new[] {
                ("HR",PermissionNames.ManageUsers),("HR",PermissionNames.CreateUser),("HR",PermissionNames.UpdateProgram),
                ("HR",PermissionNames.ReadMentorWorkload),("MENTOR",PermissionNames.ReadMeetings),("INTERN",PermissionNames.ReadMeetings),
                ("INTERN",PermissionNames.ReadOwnLeaveRequests),("INTERN",PermissionNames.CreateOwnLeaveRequest),("INTERN",PermissionNames.CancelOwnLeaveRequest)
            }).Concat(new[] { "HR","MENTOR","INTERN" }.SelectMany(role => new[] {
                (role,PermissionNames.ReadNotifications),(role,PermissionNames.UpdateNotifications)
            }));
            command.CommandText = "INSERT dbo.RolePermissions(Role,Permission) SELECT @role,@permission WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE Role=@role AND Permission=@permission)";
            command.Parameters.Add("@role",System.Data.SqlDbType.VarChar,10);
            command.Parameters.Add("@permission",System.Data.SqlDbType.VarChar,64);
            foreach (var (role,code) in grants)
            {
                command.Parameters["@role"].Value = role; command.Parameters["@permission"].Value = code;
                await command.ExecuteNonQueryAsync(ct);
            }
            command.Parameters.Clear();
            command.CommandText = "INSERT dbo.PermissionMigrations(Name) VALUES('Sprint3DynamicRbacDefaults')";
            await command.ExecuteNonQueryAsync(ct);
        }
        // Repair databases that already ran the original Sprint 3 migration.
        // One-time only: later deliberate nonessential revocations must survive restart.
        command.Parameters.Clear();
        command.CommandText="SELECT COUNT(*) FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='Sprint3AdminCatalogRepair'";
        if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))==0)
        {
            command.CommandText="INSERT dbo.RolePermissions(Role,Permission) SELECT 'ADMIN',@permission WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE Role='ADMIN' AND Permission=@permission)";
            var permission=command.Parameters.Add("@permission",System.Data.SqlDbType.VarChar,64);
            foreach(var entry in PermissionCatalog.Entries)
            {
                permission.Value=entry.Code;
                await command.ExecuteNonQueryAsync(ct);
            }
            command.Parameters.Clear();
            command.CommandText="INSERT dbo.PermissionMigrations(Name) VALUES('Sprint3AdminCatalogRepair')";
            await command.ExecuteNonQueryAsync(ct);
        }
        command.Parameters.Clear();
        command.CommandText="SELECT COUNT(*) FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='Sprint3MentorLeaveReview'";
        if(Convert.ToInt32(await command.ExecuteScalarAsync(ct))==0)
        {
            command.CommandText="INSERT dbo.RolePermissions(Role,Permission) SELECT @role,@permission WHERE NOT EXISTS(SELECT 1 FROM dbo.RolePermissions WHERE Role=@role AND Permission=@permission)";
            command.Parameters.Add("@role",System.Data.SqlDbType.VarChar,10);
            command.Parameters.Add("@permission",System.Data.SqlDbType.VarChar,64);
            foreach(var grant in new[] {
                (Role:"ADMIN",Permission:PermissionNames.ReadMentorLeaveRequests),
                (Role:"ADMIN",Permission:PermissionNames.ReviewLeaveRequests),
                (Role:"MENTOR",Permission:PermissionNames.ReadMentorLeaveRequests),
                (Role:"MENTOR",Permission:PermissionNames.ReviewLeaveRequests)
            })
            {
                command.Parameters["@role"].Value=grant.Role;
                command.Parameters["@permission"].Value=grant.Permission;
                await command.ExecuteNonQueryAsync(ct);
            }
            command.Parameters.Clear();
            command.CommandText="INSERT dbo.PermissionMigrations(Name) VALUES('Sprint3MentorLeaveReview')";
            await command.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
