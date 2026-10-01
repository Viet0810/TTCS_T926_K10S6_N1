namespace InternManagement.Services;

public static class PermissionNames
{
    public const string ManageUsers = "MANAGE_USERS";
    public const string CreateUser = "CREATE_USER";
    public const string DeleteUser = "DELETE_USER";
    public const string ViewInterns = "VIEW_INTERNS";
    public const string ManageInterns = "MANAGE_INTERNS";
    public const string ViewProfile = "VIEW_PROFILE";
    public const string ViewDocuments = "VIEW_DOCUMENTS";
    public const string ApproveDocuments = "APPROVE_DOCUMENTS";
    public const string ManagePermissions = "MANAGE_PERMISSIONS";
}

public sealed class RolePermissionService
{
    private static readonly IReadOnlyDictionary<string, string[]> PermissionsByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] =
            [
                PermissionNames.ManageUsers,
                PermissionNames.CreateUser,
                PermissionNames.DeleteUser,
                PermissionNames.ViewInterns,
                PermissionNames.ManageInterns,
                PermissionNames.ViewProfile,
                PermissionNames.ViewDocuments,
                PermissionNames.ApproveDocuments,
                PermissionNames.ManagePermissions
            ],
            ["HR"] =
            [
                PermissionNames.ViewInterns,
                PermissionNames.ManageInterns,
                PermissionNames.ViewProfile,
                PermissionNames.ViewDocuments,
                PermissionNames.ApproveDocuments
            ],
            ["MENTOR"] =
            [
                PermissionNames.ViewInterns,
                PermissionNames.ViewProfile,
                PermissionNames.ViewDocuments
            ],
            ["INTERN"] =
            [
                PermissionNames.ViewProfile,
                PermissionNames.ViewDocuments
            ]
        };

    public IReadOnlyList<string> GetPermissions(string? role) =>
        role is not null && PermissionsByRole.TryGetValue(role, out var permissions)
            ? permissions
            : Array.Empty<string>();

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetRolePermissions() =>
        PermissionsByRole.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)Array.AsReadOnly(entry.Value),
            StringComparer.OrdinalIgnoreCase);

    public bool HasPermission(string? role, string permission) =>
        GetPermissions(role).Contains(permission, StringComparer.OrdinalIgnoreCase);
}