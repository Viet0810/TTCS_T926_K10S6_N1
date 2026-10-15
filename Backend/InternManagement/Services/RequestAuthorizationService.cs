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

        if (user!.MustChangePassword && RequestRequiresPasswordChange(request))
            return new AuthorizationDecision(AuthorizationStatus.PasswordChangeRequired, user);

        if (requiredPermission is not null)
        {
            var grants = permissions.GetPermissions(user.Role);
            var definition = PermissionCatalog.Entries.SingleOrDefault(p => p.Code == requiredPermission);
            if (!grants.Contains(requiredPermission) || definition is null
                || (user.Role != "ADMIN" && !definition.Roles.Contains(user.Role))
                || definition.Requires.Any(code => !grants.Contains(code)))
                return new AuthorizationDecision(AuthorizationStatus.Forbidden, user);
        }

        return new AuthorizationDecision(AuthorizationStatus.Authorized, user);
    }

    private static bool RequestRequiresPasswordChange(HttpRequest request) =>
        !string.Equals(request.Path.Value, "/api/auth/me", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(request.Path.Value, "/api/account/change-password", StringComparison.OrdinalIgnoreCase);
}
