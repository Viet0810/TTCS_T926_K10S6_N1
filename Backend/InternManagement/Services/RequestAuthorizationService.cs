using InternManagement.Models;

namespace InternManagement.Services;

public enum AuthorizationStatus
{
    Authorized,
    Unauthenticated,
    Forbidden,
    PasswordChangeRequired
}

public sealed record AuthorizationDecision(AuthorizationStatus Status, AuthenticatedUser? User);

public sealed class RequestAuthorizationService
{
    private readonly AuthTokenService tokens;
    private readonly RolePermissionService permissions;

    public RequestAuthorizationService(AuthTokenService tokens, RolePermissionService permissions)
    {
        this.tokens = tokens;
        this.permissions = permissions;
    }

    public AuthorizationDecision Evaluate(HttpRequest request, string? requiredPermission = null)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !tokens.TryValidate(header[7..].Trim(), out var user))
            return new AuthorizationDecision(AuthorizationStatus.Unauthenticated, null);

        if (requiredPermission is not null && !permissions.HasPermission(user!.Role, requiredPermission))
            return new AuthorizationDecision(AuthorizationStatus.Forbidden, user);

        if (user!.MustChangePassword && RequestRequiresPasswordChange(request))
            return new AuthorizationDecision(AuthorizationStatus.PasswordChangeRequired, user);

        return new AuthorizationDecision(AuthorizationStatus.Authorized, user);
    }

    private static bool RequestRequiresPasswordChange(HttpRequest request) =>
        !string.Equals(request.Path.Value, "/api/auth/me", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(request.Path.Value, "/api/account/change-password", StringComparison.OrdinalIgnoreCase);
}
