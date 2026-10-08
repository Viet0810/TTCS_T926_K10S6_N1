using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;
using Microsoft.AspNetCore.DataProtection;
using InternManagement.Models;

namespace InternManagement.Services;

public sealed record AuthenticatedUser(int Id, string Username, string Role);

public sealed class AuthTokenService
{
    private readonly IDataProtector protector;
    private readonly string connectionString;

    public AuthTokenService(IDataProtectionProvider provider, IConfiguration configuration)
    {
        protector = provider.CreateProtector("InternManagement.AuthToken.v1");
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");
    }

    public string Issue(User user)
    {
        var payload = JsonSerializer.Serialize(new TokenPayload(
            user.Id, user.Username, user.Role, DateTimeOffset.UtcNow.AddHours(8), PasswordVersion(user.PasswordHash)));
        return protector.Protect(payload);
    }

    public bool TryValidate(string token, out AuthenticatedUser? user)
    {
        user = null;
        try
        {
            var payload = JsonSerializer.Deserialize<TokenPayload>(protector.Unprotect(token));
            if (payload is null || payload.ExpiresAt <= DateTimeOffset.UtcNow)
                return false;

            // Reject sessions issued before a password reset or account change.
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Username, Role, PasswordHash FROM dbo.Users WHERE Id = @id";
            command.Parameters.Add("@id", NpgsqlTypes.NpgsqlDbType.Integer).Value = payload.Id;
            using var reader = command.ExecuteReader();
            if (!reader.Read() || payload.PasswordVersion != PasswordVersion(reader.GetString(2)))
                return false;

            user = new AuthenticatedUser(payload.Id, reader.GetString(0), reader.GetString(1));
            return true;
        }
        catch (Exception error) when (error is CryptographicException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static string PasswordVersion(string hash) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hash)));

    private sealed record TokenPayload(int Id, string Username, string Role, DateTimeOffset ExpiresAt, string? PasswordVersion);
}
