using Microsoft.Data.SqlClient;
namespace InternManagement.Infrastructure;

public static class Sprint2CompletionMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF COL_LENGTH('dbo.InternshipPrograms','Name') IS NULL ALTER TABLE dbo.InternshipPrograms ADD Name NVARCHAR(200) NULL;
            IF COL_LENGTH('dbo.InternshipPrograms','Department') IS NULL ALTER TABLE dbo.InternshipPrograms ADD Department NVARCHAR(200) NULL;
            IF OBJECT_ID('dbo.InternContracts','U') IS NULL
            CREATE TABLE dbo.InternContracts (
                InternId INT NOT NULL PRIMARY KEY REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                FileName NVARCHAR(255) NOT NULL,
                Content VARBINARY(MAX) NOT NULL CHECK (DATALENGTH(Content) BETWEEN 5 AND 5242880),
                UploadedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                UploadedBy INT NULL REFERENCES dbo.Users(Id) ON DELETE SET NULL,
                ConfirmedAt DATETIME2 NULL,
                Version ROWVERSION NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(ct);
    }
}
