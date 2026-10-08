using System.Data;
using InternManagement.DTOs;
using InternManagement.Models;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class AccountService(IConfiguration configuration, PasswordHasher passwords)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public async Task<User?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, FullName, Email, PasswordHash, Role, MustChangePassword
            FROM dbo.Users WHERE Username = @identifier OR Email = @identifier;
            """;
        command.Parameters.AddWithValue("@identifier", request.Username!.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        var user = new User
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            FullName = reader.GetString(2),
            Email = reader.GetString(3),
            PasswordHash = reader.GetString(4),
            Role = reader.GetString(5),
            MustChangePassword = reader.GetBoolean(6)
        };
        return passwords.Verify(request.Password!, user.PasswordHash) ? user : null;
    }

    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, FullName, Email, Role, MustChangePassword FROM dbo.Users ORDER BY Id DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<UserResponse>();
        while (await reader.ReadAsync(cancellationToken)) users.Add(MapUser(reader));
        return users;
    }

    public async Task<UserResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, FullName, Email, Role, MustChangePassword FROM dbo.Users WHERE Id = @id";
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, string role, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role, MustChangePassword)
            OUTPUT INSERTED.Id, INSERTED.Username, INSERTED.FullName, INSERTED.Email, INSERTED.Role, INSERTED.MustChangePassword
            VALUES (@username, @fullName, @email, @passwordHash, @role, @mustChange);
            """;
        command.Parameters.AddWithValue("@username", request.Email.Trim());
        command.Parameters.AddWithValue("@fullName", request.FullName.Trim());
        command.Parameters.AddWithValue("@email", request.Email.Trim());
        command.Parameters.AddWithValue("@passwordHash", passwords.Hash(request.Password ?? throw new ArgumentException("Thiếu mật khẩu tài khoản.")));
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.Add("@mustChange", SqlDbType.Bit).Value = role is "HR" or "MENTOR";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return MapUser(reader);
    }

    public async Task<UserResponse> RegisterInternAsync(RegisterInternRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
            OUTPUT INSERTED.Id
            VALUES (@email, @name, @email, @hash, 'INTERN');
            """;
        command.Parameters.Add("@email", SqlDbType.NVarChar, 100).Value = request.Email.Trim();
        command.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = request.FullName.Trim();
        command.Parameters.Add("@hash", SqlDbType.NVarChar, 512).Value = passwords.Hash(request.Password);
        var id = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        command.CommandText = """
            INSERT INTO dbo.Interns (FullName, Email, Phone, School, Major)
            VALUES (@name, @email, @phone, @school, @major);
            """;
        command.Parameters.Add("@phone", SqlDbType.NVarChar, 20).Value = request.Phone;
        command.Parameters.Add("@school", SqlDbType.NVarChar, 200).Value = request.School.Trim();
        command.Parameters.Add("@major", SqlDbType.NVarChar, 200).Value = request.Major.Trim();
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new UserResponse(id, request.Email.Trim(), request.FullName.Trim(), request.Email.Trim(), "INTERN");
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Users WHERE Id = @id AND Role <> 'ADMIN'";
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) != 0;
    }

    private static UserResponse MapUser(SqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetBoolean(5));

    public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!new StrongPasswordAttribute().IsValid(request.NewPassword)
            || request.NewPassword != request.ConfirmPassword || request.NewPassword == request.CurrentPassword)
            throw new ArgumentException("Mật khẩu mới hoặc xác nhận mật khẩu không hợp lệ.");
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT PasswordHash FROM dbo.Users WHERE Id = @id";
        command.Parameters.Add("@id", SqlDbType.Int).Value = userId;
        var oldHash = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (oldHash is null || !passwords.Verify(request.CurrentPassword, oldHash)) return false;
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        command.Transaction = transaction;
        command.CommandText = "UPDATE dbo.Users SET PasswordHash = @hash, MustChangePassword = 0 WHERE Id = @id AND PasswordHash = @oldHash";
        command.Parameters.Add("@hash", SqlDbType.NVarChar, 512).Value = passwords.Hash(request.NewPassword);
        command.Parameters.Add("@oldHash", SqlDbType.NVarChar, 512).Value = oldHash;
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        command.CommandText = "UPDATE dbo.PasswordResetTokens SET UsedAt = SYSUTCDATETIME() WHERE UserId = @id AND UsedAt IS NULL";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetTemporaryPasswordAsync(int userId, string password, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "UPDATE dbo.Users SET PasswordHash = @hash, MustChangePassword = 1 WHERE Id = @id AND Role IN ('HR','MENTOR')";
        command.Parameters.Add("@id", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@hash", SqlDbType.NVarChar, 512).Value = passwords.Hash(password);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return false;
        command.CommandText = "UPDATE dbo.PasswordResetTokens SET UsedAt = SYSUTCDATETIME() WHERE UserId = @id AND UsedAt IS NULL";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
