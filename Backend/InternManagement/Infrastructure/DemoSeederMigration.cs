using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class DemoSeederMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF COL_LENGTH('dbo.Users', 'IsDemoAccount') IS NULL
                ALTER TABLE dbo.Users ADD IsDemoAccount BIT NOT NULL
                    CONSTRAINT DF_Users_IsDemoAccount DEFAULT 0;

            IF COL_LENGTH('dbo.Users', 'EmployeeCode') IS NULL
                ALTER TABLE dbo.Users ADD EmployeeCode NVARCHAR(50) NULL;

            IF COL_LENGTH('dbo.InternshipPrograms', 'Code') IS NULL
                ALTER TABLE dbo.InternshipPrograms ADD Code NVARCHAR(100) NULL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = """
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID('dbo.Users')
                  AND name = 'UX_Users_EmployeeCode'
            )
                CREATE UNIQUE INDEX UX_Users_EmployeeCode
                    ON dbo.Users(EmployeeCode) WHERE EmployeeCode IS NOT NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID('dbo.InternshipPrograms')
                  AND name = 'UX_InternshipPrograms_Code'
            )
                CREATE UNIQUE INDEX UX_InternshipPrograms_Code
                    ON dbo.InternshipPrograms(Code) WHERE Code IS NOT NULL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
