namespace InternManagement.Services;

public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<string, string[]> PermissionsByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] = ["users.manage", "interns.manage"],
            ["HR"] = ["interns.manage"],
            ["MENTOR"] = ["interns.assigned.read", "progress.review"],
            ["INTERN"] = ["profile.own.read", "progress.own.read", "tasks.own.read"]
        };

    public static IReadOnlyList<string> ForRole(string? role) =>
        role is not null && PermissionsByRole.TryGetValue(role.Trim(), out var permissions)
            ? permissions
            : Array.Empty<string>();

    public static bool HasPermission(string? role, string permission) =>
        ForRole(role).Contains(permission, StringComparer.OrdinalIgnoreCase);
}
