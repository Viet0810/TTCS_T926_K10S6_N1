using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class AccountPasswordMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF COL_LENGTH(N'dbo.Users', N'MustChangePassword') IS NULL
                ALTER TABLE dbo.Users ADD MustChangePassword BIT NOT NULL
                    CONSTRAINT DF_Users_MustChangePassword DEFAULT 0;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
