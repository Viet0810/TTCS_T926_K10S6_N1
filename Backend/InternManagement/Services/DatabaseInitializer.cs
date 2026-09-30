using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class DatabaseInitializer
{
    private readonly PasswordHasher passwords;

    public DatabaseInitializer(PasswordHasher passwords)
    {
        this.passwords = passwords;
    }

    public async Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Users (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                    Username NVARCHAR(100) NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
                    FullName NVARCHAR(200) NOT NULL,
                    Email NVARCHAR(254) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
                    PasswordHash NVARCHAR(512) NOT NULL,
                    Role VARCHAR(10) NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('ADMIN', 'HR', 'MENTOR', 'INTERN')),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME()
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin.demo')
            BEGIN
                INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
                VALUES (N'admin.demo', N'Quản trị viên Demo', N'admin.demo@example.com', @passwordHash, 'ADMIN');
            END;
            """;
        command.Parameters.AddWithValue("@passwordHash", passwords.Hash("Demo@123456"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
