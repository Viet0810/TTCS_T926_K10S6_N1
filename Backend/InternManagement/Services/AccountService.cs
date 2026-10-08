using System.Data;
using InternManagement.DTOs;
using InternManagement.Models;
using Npgsql;
using NpgsqlTypes;

namespace InternManagement.Services;

public sealed class AccountService(IConfiguration configuration, PasswordHasher passwords)
{
    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public async Task<User?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Username, FullName, Email, PasswordHash, Role
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
            Role = reader.GetString(5)
        };
        return passwords.Verify(request.Password!, user.PasswordHash) ? user : null;
    }

    public async Task<IReadOnlyList<UserResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, FullName, Email, Role FROM dbo.Users ORDER BY Id DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<UserResponse>();
        while (await reader.ReadAsync(cancellationToken)) users.Add(MapUser(reader));
        return users;
    }

    public async Task<UserResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, FullName, Email, Role FROM dbo.Users WHERE Id = @id";
        command.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapUser(reader) : null;
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, string role, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
            VALUES (@username, @fullName, @email, @passwordHash, @role)
            RETURNING Id, Username, FullName, Email, Role;
            """;
        command.Parameters.AddWithValue("@username", request.Email.Trim());
        command.Parameters.AddWithValue("@fullName", request.FullName.Trim());
        command.Parameters.AddWithValue("@email", request.Email.Trim());
        command.Parameters.AddWithValue("@passwordHash", passwords.Hash(request.Password));
        command.Parameters.AddWithValue("@role", role);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return MapUser(reader);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dbo.Users WHERE Id = @id AND Role <> 'ADMIN'";
        command.Parameters.Add("@id", NpgsqlDbType.Integer).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) != 0;
    }

    private static UserResponse MapUser(NpgsqlDataReader reader) => new(
        reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4));
}
