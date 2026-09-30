using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using InternManagement.Models;

namespace InternManagement.Services;

public sealed record AuthenticatedUser(int Id, string Username, string Role);

public sealed class AuthTokenService
{
    private readonly IDataProtector protector;

    public AuthTokenService(IDataProtectionProvider provider)
    {
        protector = provider.CreateProtector("InternManagement.AuthToken.v1");
    }

    public string Issue(User user)
    {
        var payload = JsonSerializer.Serialize(new TokenPayload(
            user.Id, user.Username, user.Role, DateTimeOffset.UtcNow.AddHours(8)));
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

            user = new AuthenticatedUser(payload.Id, payload.Username, payload.Role);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private sealed record TokenPayload(int Id, string Username, string Role, DateTimeOffset ExpiresAt);
}
