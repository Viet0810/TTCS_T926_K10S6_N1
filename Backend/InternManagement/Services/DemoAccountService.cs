using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class DemoAccountService(IConfiguration configuration, PasswordHasher passwords)
{
    private const string DemoDomain = "demo.intern.local";

    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public async Task<PageResponse<DemoAccountResponse>> GetAccountsAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        search = search?.Trim();
        await using var countCommand = new SqlCommand("""
            SELECT COUNT(*) FROM dbo.Users
            WHERE IsDemoAccount = 1
              AND (@search IS NULL OR FullName LIKE @pattern OR Email LIKE @pattern OR Role LIKE @pattern);
            """, connection);
        countCommand.Parameters.Add("@search", SqlDbType.NVarChar, 200).Value =
            string.IsNullOrWhiteSpace(search) ? DBNull.Value : search;
        countCommand.Parameters.Add("@pattern", SqlDbType.NVarChar, 204).Value = $"%{search}%";
        var totalItems = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));

        await using var command = new SqlCommand("""
            SELECT u.Id, u.FullName, u.Email, u.Role, u.EmployeeCode,
                   i.StudentCode, mentor.FullName, u.IsDemoAccount
            FROM dbo.Users u
            LEFT JOIN dbo.Interns i ON i.Email = u.Email AND u.Role = 'INTERN'
            LEFT JOIN dbo.Users mentor ON mentor.Id = i.MentorUserId
            WHERE u.IsDemoAccount = 1
              AND (@search IS NULL OR u.FullName LIKE @pattern OR u.Email LIKE @pattern OR u.Role LIKE @pattern)
            ORDER BY CASE u.Role WHEN 'ADMIN' THEN 1 WHEN 'HR' THEN 2
                     WHEN 'MENTOR' THEN 3 ELSE 4 END, u.Email
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """, connection);
        command.Parameters.Add("@offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@pageSize", SqlDbType.Int).Value = pageSize;
        command.Parameters.Add("@search", SqlDbType.NVarChar, 200).Value =
            string.IsNullOrWhiteSpace(search) ? DBNull.Value : search;
        command.Parameters.Add("@pattern", SqlDbType.NVarChar, 204).Value = $"%{search}%";

        var accounts = new List<DemoAccountResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(new DemoAccountResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetBoolean(7)));
        }

        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        return new PageResponse<DemoAccountResponse>(accounts, page, pageSize, totalItems, totalPages);
    }

    public async Task<bool> ResetPasswordAsync(int userId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT Role FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Id = @id AND IsDemoAccount = 1",
            connection,
            transaction);
        command.Parameters.Add("@id", SqlDbType.Int).Value = userId;
        var role = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (role is null) return false;

        command.CommandText = """
            UPDATE dbo.Users
            SET PasswordHash = @hash, MustChangePassword = 0
            WHERE Id = @id AND IsDemoAccount = 1;
            UPDATE dbo.PasswordResetTokens
            SET UsedAt = SYSUTCDATETIME()
            WHERE UserId = @id AND UsedAt IS NULL;
            """;
        command.Parameters.Add("@hash", SqlDbType.NVarChar, 512).Value = passwords.Hash(DefaultPassword(role));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public static string DefaultPassword(string role) => role.ToUpperInvariant() switch
    {
        "ADMIN" => "Admin@123",
        "HR" => "Hr@123456",
        "MENTOR" => "Mentor@123",
        "INTERN" => "Intern@123",
        _ => throw new ArgumentOutOfRangeException(nameof(role), "Vai trò demo không hợp lệ.")
    };

    public static bool IsDemoEmail(string email) =>
        email.EndsWith($"@{DemoDomain}", StringComparison.OrdinalIgnoreCase);
}
