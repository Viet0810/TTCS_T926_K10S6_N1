
using System.Data;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public static class PermissionNames
{
    public const string ReadMeetings = "MEETING_READ";
    public const string CreateMeeting = "MEETING_CREATE";
    public const string ReadMentorTasks = "MENTOR_TASK_READ";
    public const string ReadWorkSchedules = "WORK_SCHEDULE_READ";
    public const string CreateWorkSchedule = "WORK_SCHEDULE_CREATE";
    public const string UpdateWorkSchedule = "WORK_SCHEDULE_UPDATE";
    public const string CreateTask = "TASK_CREATE";
    public const string ReadOwnTasks = "OWN_TASK_READ";
    public const string UpdateOwnTasks = "OWN_TASK_UPDATE";
    public const string ReadOwnWeeklyReports = "OWN_WEEKLY_REPORT_READ";
    public const string CreateWeeklyReport = "WEEKLY_REPORT_CREATE";
    public const string ReadMentorWeeklyReports = "MENTOR_WEEKLY_REPORT_READ";
    public const string CreateWeeklyReportFeedback = "WEEKLY_REPORT_FEEDBACK";
    public const string ReadMentorWorkload = "MENTOR_WORKLOAD_READ";
    public const string ReadOwnLeaveRequests = "OWN_LEAVE_REQUEST_READ";
    public const string CreateOwnLeaveRequest = "LEAVE_REQUEST_CREATE";
    public const string CancelOwnLeaveRequest = "LEAVE_REQUEST_CANCEL";
    public const string ReadMentorLeaveRequests = "MENTOR_LEAVE_REQUEST_READ";
    public const string ReviewLeaveRequests = "LEAVE_REQUEST_REVIEW";
    public const string ReadNotifications = "NOTIFICATION_READ";
    public const string UpdateNotifications = "NOTIFICATION_UPDATE";
    public const string CreateNotifications = "NOTIFICATION_CREATE";
    public const string RejectDocuments = "REJECT_DOCUMENTS";
    public const string AttendanceCheckIn = "ATTENDANCE_CHECK_IN";
    public const string AttendanceCheckOut = "ATTENDANCE_CHECK_OUT";
    public const string ViewOwnProfile = "VIEW_OWN_PROFILE";
    public const string EditOwnProfile = "EDIT_OWN_PROFILE";
    public const string ViewOwnSchedule = "VIEW_OWN_SCHEDULE";
    public const string CreateContract = "CONTRACT_CREATE";
    public const string UpdateContract = "CONTRACT_UPDATE";
    public const string DeleteContract = "CONTRACT_DELETE";
    public const string CreateProgram = "PROGRAM_CREATE";
    public const string UpdateProgram = "PROGRAM_UPDATE";
    public const string DeleteProgram = "PROGRAM_DELETE";
    public const string CreateAssignment = "ASSIGNMENT_CREATE";
    public const string UpdateAssignment = "ASSIGNMENT_UPDATE";
    public const string DeleteAssignment = "ASSIGNMENT_DELETE";
    public const string ManageContracts = "MANAGE_CONTRACTS";
    public const string ViewOwnContract = "VIEW_OWN_CONTRACT";
    public const string OwnAttendance = "OWN_ATTENDANCE";
    public const string AssignMentor = "ASSIGN_MENTOR";
    public const string ManageUsers = "MANAGE_USERS";
    public const string CreateUser = "CREATE_USER";
    public const string DeleteUser = "DELETE_USER";
    public const string ViewInterns = "VIEW_INTERNS";
    public const string ManageInterns = "MANAGE_INTERNS";
    public const string SearchInterns = "SEARCH_INTERNS";
    public const string EditInterns = "EDIT_INTERNS";
    public const string ViewProfile = "VIEW_PROFILE";
    public const string UploadDocuments = "UPLOAD_DOCUMENTS";
    public const string ViewDocuments = "VIEW_DOCUMENTS";
    public const string ApproveDocuments = "APPROVE_DOCUMENTS";
    public const string ManagePrograms = "MANAGE_PROGRAMS";
    public const string ViewAttendanceReport = "VIEW_ATTENDANCE_REPORT";
    public const string ManagePermissions = "MANAGE_PERMISSIONS";
    public const string ReadPermissions = "RBAC_READ";
    public const string ReadDocumentReviews = "DOCUMENT_REVIEW_READ";

    // Quyền đánh giá cuối kỳ
    public const string ViewFinalEvaluations = "VIEW_FINAL_EVALUATIONS";
    public const string UpdateFinalEvaluations = "UPDATE_FINAL_EVALUATIONS";
    public const string ViewEvaluationSummaries = "EVALUATION_SUMMARY_READ";
    public const string FinalizeEvaluationSummaries = "EVALUATION_SUMMARY_FINALIZE";
    public const string PublishEvaluationSummaries = "EVALUATION_SUMMARY_PUBLISH";
}

public sealed class RolePermissionService
{
    private readonly string? connectionString;

    public RolePermissionService() { }

    public RolePermissionService(IConfiguration configuration)
        => connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình cơ sở dữ liệu.");

    private static readonly IReadOnlyDictionary<string, string[]> DefaultPermissionsByRole =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADMIN"] =
            [
                PermissionNames.ManageUsers,
                PermissionNames.CreateUser,
                PermissionNames.DeleteUser,
                PermissionNames.ViewProfile,
                PermissionNames.ManagePermissions,
                PermissionNames.ReadPermissions
            ],

            ["HR"] =
            [
                PermissionNames.ReadWorkSchedules, PermissionNames.CreateWorkSchedule, PermissionNames.UpdateWorkSchedule,
                PermissionNames.ReadMeetings,
                PermissionNames.CreateMeeting,
                PermissionNames.ManageUsers, PermissionNames.CreateUser,
                PermissionNames.ReadMentorWorkload,
                PermissionNames.ReadNotifications, PermissionNames.UpdateNotifications,
                PermissionNames.ManageContracts,
                PermissionNames.AssignMentor,
                PermissionNames.ViewInterns,
                PermissionNames.ManageInterns,
                PermissionNames.SearchInterns,
                PermissionNames.EditInterns,
                PermissionNames.ViewProfile,
                PermissionNames.ApproveDocuments,
                PermissionNames.RejectDocuments,
                PermissionNames.ManagePrograms,
                PermissionNames.ViewAttendanceReport,
                PermissionNames.ReadDocumentReviews,
                PermissionNames.CreateContract,
                PermissionNames.UpdateContract,
                PermissionNames.CreateProgram,
                PermissionNames.UpdateProgram,
                PermissionNames.DeleteProgram,
                PermissionNames.CreateAssignment,
                PermissionNames.UpdateAssignment,

                // HR tổng hợp và theo dõi kết quả đánh giá cuối kỳ.
                PermissionNames.ViewFinalEvaluations,
                PermissionNames.ViewEvaluationSummaries,
                PermissionNames.FinalizeEvaluationSummaries,
                PermissionNames.PublishEvaluationSummaries
            ],

            ["MENTOR"] =
            [
                PermissionNames.ReadWorkSchedules,
                PermissionNames.ReadMentorTasks, PermissionNames.CreateTask,
                PermissionNames.ReadMentorWeeklyReports, PermissionNames.CreateWeeklyReportFeedback,
                PermissionNames.ReadMentorLeaveRequests, PermissionNames.ReviewLeaveRequests,
                PermissionNames.ReadMeetings,
                PermissionNames.ReadNotifications, PermissionNames.UpdateNotifications,
                PermissionNames.ViewInterns,
                PermissionNames.SearchInterns,
                PermissionNames.ViewProfile,

                // MENTOR được xem, thêm và sửa đánh giá
                PermissionNames.ViewFinalEvaluations,
                PermissionNames.UpdateFinalEvaluations
            ],

            ["INTERN"] =
            [
                PermissionNames.ReadWorkSchedules,
                PermissionNames.ReadOwnTasks,
                PermissionNames.UpdateOwnTasks,
                PermissionNames.ReadOwnWeeklyReports, PermissionNames.CreateWeeklyReport,
                PermissionNames.ReadMeetings,
                PermissionNames.ReadOwnLeaveRequests, PermissionNames.CreateOwnLeaveRequest, PermissionNames.CancelOwnLeaveRequest,
                PermissionNames.ReadNotifications, PermissionNames.UpdateNotifications,
                PermissionNames.ViewOwnContract,
                PermissionNames.OwnAttendance,
                PermissionNames.AttendanceCheckIn,
                PermissionNames.AttendanceCheckOut,
                PermissionNames.UploadDocuments,
                PermissionNames.ViewProfile,
                PermissionNames.ViewDocuments,
                PermissionNames.ViewOwnProfile,
                PermissionNames.EditOwnProfile,
                PermissionNames.ViewOwnSchedule,

                // INTERN chỉ được xem đánh giá của bản thân
                PermissionNames.ViewFinalEvaluations
            ]
        };

    public IReadOnlyList<string> GetDefaults(string? role) =>
        role is not null && DefaultPermissionsByRole.TryGetValue(role, out var permissions)
            ? Array.AsReadOnly(permissions)
            : Array.Empty<string>();

    // Không dùng cache để thay đổi quyền có hiệu lực ngay.
    public IReadOnlyList<string> GetPermissions(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return [];

        if (connectionString is null)
            throw new InvalidOperationException("Runtime phân quyền yêu cầu database.");

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(
            "SELECT Permission FROM dbo.RolePermissions WHERE Role=@role ORDER BY Permission",
            connection
        );

        command.Parameters.Add("@role", SqlDbType.VarChar, 10).Value = role;

        using var reader = command.ExecuteReader();

        var result = new List<string>();

        while (reader.Read())
            result.Add(reader.GetString(0));

        return result
            .Where(code => IsAllowedForRole(role, code))
            .ToArray();
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> GetRolePermissions()
    {
        if (connectionString is null)
            throw new InvalidOperationException("Runtime phân quyền yêu cầu database.");

        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(
            """
            SELECT r.Role, p.Permission
            FROM dbo.PermissionRoles r
            LEFT JOIN dbo.RolePermissions p ON p.Role = r.Role
            ORDER BY r.Role, p.Permission
            """,
            connection
        );

        using var reader = command.ExecuteReader();

        var roles = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase
        );

        while (reader.Read())
        {
            var role = reader.GetString(0);

            if (!roles.ContainsKey(role))
                roles[role] = [];

            if (!reader.IsDBNull(1))
                roles[role].Add(reader.GetString(1));
        }

        return roles.ToDictionary(
            p => p.Key,
            p => (IReadOnlyList<string>)p.Value
                .Where(code => IsAllowedForRole(p.Key, code))
                .ToArray(),
            StringComparer.OrdinalIgnoreCase
        );
    }

    private static bool IsAllowedForRole(string role, string code)
    {
        var definition = PermissionCatalog.Entries.SingleOrDefault(entry => entry.Code == code);
        return definition is not null &&
            (role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
                ? DefaultPermissionsByRole["ADMIN"].Contains(code, StringComparer.OrdinalIgnoreCase)
                : definition.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }

    public IReadOnlyList<PermissionDescriptor> GetCatalog()
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = new SqlCommand(
            "SELECT Code,Module,DisplayName,Action FROM dbo.Permissions ORDER BY Code",
            connection
        );

        using var reader = command.ExecuteReader();

        var entries = new List<PermissionDescriptor>();

        while (reader.Read())
        {
            var definition = PermissionCatalog.Entries.Single(
                p => p.Code == reader.GetString(0)
            );

            entries.Add(definition with
            {
                Roles = definition.Roles,
                Module = reader.GetString(1),
                Label = reader.GetString(2),
                Action = reader.GetString(3)
            });
        }

        return PermissionCatalog.Entries
            .Select(definition =>
                entries.Single(p => p.Code == definition.Code))
            .ToArray();
    }

    public bool HasPermission(string? role, string permission) =>
        GetPermissions(role).Contains(
            permission,
            StringComparer.OrdinalIgnoreCase
        );

    public static readonly string[] EssentialAdminPermissions =
    [
        PermissionNames.ManageUsers,
        PermissionNames.CreateUser,
        PermissionNames.DeleteUser,
        PermissionNames.ReadPermissions,
        PermissionNames.ManagePermissions
    ];

    public async Task SaveAsync(
        string role,
        IReadOnlyList<string> permissions,
        CancellationToken ct)
    {
        if (!DefaultPermissionsByRole.ContainsKey(role))
            throw new ArgumentException("Vai trò không hợp lệ.");

        if (permissions.Any(code =>
            !PermissionCatalog.Entries.Any(p => p.Code == code)))
            throw new ArgumentException("Có quyền không hợp lệ.");

        if (permissions.Distinct().Count() != permissions.Count)
            throw new ArgumentException("Quyền bị trùng.");

        if (permissions.Any(code => !IsAllowedForRole(role, code)))
            throw new ArgumentException("Quyền không áp dụng cho vai trò này.");

        if (role == "ADMIN" &&
            EssentialAdminPermissions.Any(code => !permissions.Contains(code)))
            throw new ArgumentException(
                "Không thể bỏ quyền quản trị thiết yếu của Admin."
            );

        if (role != "ADMIN" &&
            permissions.Any(code =>
                code is PermissionNames.ReadPermissions
                    or PermissionNames.ManagePermissions))
            throw new ArgumentException(
                "Chỉ Admin được quản trị phân quyền."
            );

        foreach (var entry in PermissionCatalog.Entries)
        {
            if (permissions.Contains(entry.Code) &&
                entry.Requires.Any(read => !permissions.Contains(read)))
                throw new ArgumentException(
                    "Quyền thao tác cần đi kèm quyền xem của cùng phân hệ."
                );
        }

        if (connectionString is null)
            throw new InvalidOperationException(
                "Không có storage phân quyền."
            );

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var transaction =
            (SqlTransaction)await connection.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct
            );

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText =
            "SELECT Role FROM dbo.PermissionRoles WITH (UPDLOCK,HOLDLOCK) WHERE Role=@role";

        command.Parameters.Add("@role", SqlDbType.VarChar, 10).Value = role;

        if (await command.ExecuteScalarAsync(ct) is null)
            throw new ArgumentException(
                "Vai trò không tồn tại trong database."
            );

        command.CommandText =
            "DELETE FROM dbo.RolePermissions WHERE Role=@role";

        await command.ExecuteNonQueryAsync(ct);

        command.CommandText =
            "INSERT dbo.RolePermissions(Role,Permission) VALUES(@role,@permission)";

        var parameter = command.Parameters.Add(
            "@permission",
            SqlDbType.VarChar,
            64
        );

        foreach (var permission in permissions)
        {
            parameter.Value = permission;
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
