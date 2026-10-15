
namespace InternManagement.Services;

public sealed record PermissionDescriptor(
    string Code,
    string Module,
    string Label,
    string Action,
    string[] Roles,
    bool Editable = true)
{
    public string[] Requires => Code switch
    {
        PermissionNames.ManageInterns or PermissionNames.EditInterns
            => [PermissionNames.ViewInterns],

        PermissionNames.ApproveDocuments or PermissionNames.RejectDocuments
            => [PermissionNames.ReadDocumentReviews],

        PermissionNames.AttendanceCheckIn or PermissionNames.AttendanceCheckOut
            => [PermissionNames.OwnAttendance],

        PermissionNames.UploadDocuments
            => [PermissionNames.ViewDocuments],

        PermissionNames.EditOwnProfile
            => [PermissionNames.ViewOwnProfile],

        PermissionNames.CreateUser or PermissionNames.DeleteUser
            => [PermissionNames.ManageUsers],

        PermissionNames.CreateContract or PermissionNames.UpdateContract
            or PermissionNames.DeleteContract
            => [PermissionNames.ManageContracts],

        PermissionNames.CreateProgram or PermissionNames.UpdateProgram
            or PermissionNames.DeleteProgram
            => [PermissionNames.ManagePrograms],

        PermissionNames.CreateAssignment or PermissionNames.UpdateAssignment
            or PermissionNames.DeleteAssignment
            => [PermissionNames.AssignMentor],

        // Giữ quyền giao nhiệm vụ từ develop
        PermissionNames.CreateTask
            => [PermissionNames.ReadMentorTasks],

        // Quyền cập nhật đánh giá yêu cầu quyền xem
        PermissionNames.UpdateFinalEvaluations
            => [PermissionNames.ViewFinalEvaluations],

        PermissionNames.CreateWorkSchedule or PermissionNames.UpdateWorkSchedule => [PermissionNames.ReadWorkSchedules],
        PermissionNames.CreateMeeting => [PermissionNames.ReadMeetings],
        PermissionNames.UpdateOwnTasks => [PermissionNames.ReadOwnTasks],
        PermissionNames.CreateWeeklyReport => [PermissionNames.ReadOwnWeeklyReports],
        PermissionNames.CreateWeeklyReportFeedback => [PermissionNames.ReadMentorWeeklyReports],
        PermissionNames.CreateOwnLeaveRequest or PermissionNames.CancelOwnLeaveRequest => [PermissionNames.ReadOwnLeaveRequests],
        PermissionNames.ReviewLeaveRequests => [PermissionNames.ReadMentorLeaveRequests],
        PermissionNames.UpdateNotifications or PermissionNames.CreateNotifications => [PermissionNames.ReadNotifications],
        _ => []
    };
}

public static class PermissionCatalog
{
    private static readonly string[] Staff =
        ["ADMIN", "HR", "MENTOR", "INTERN"];

    public static readonly PermissionDescriptor[] Entries =
    [
        new(PermissionNames.ReadWorkSchedules, "work-schedules", "Lịch làm việc", "READ", ["HR", "INTERN"]),
        new(PermissionNames.CreateWorkSchedule, "work-schedules", "Lịch làm việc", "CREATE", ["HR"]),
        new(PermissionNames.UpdateWorkSchedule, "work-schedules", "Lịch làm việc", "UPDATE", ["HR"]),
        new(PermissionNames.ReadMeetings, "meetings", "Lịch họp liên quan", "READ", Staff),
        new(PermissionNames.CreateMeeting, "meetings", "Tạo lịch họp", "CREATE", ["HR"]),
        // Quản lý nhiệm vụ
        new(PermissionNames.ReadMentorTasks, "mentor-tasks", "Nhiệm vụ đã giao", "READ", ["MENTOR"]),
        new(PermissionNames.CreateTask, "mentor-tasks", "Giao nhiệm vụ", "CREATE", ["MENTOR"]),
        new(PermissionNames.ReadOwnTasks, "own-tasks", "Nhiệm vụ của tôi", "READ", ["INTERN"]),
        new(PermissionNames.UpdateOwnTasks, "own-tasks", "Nhiệm vụ của tôi", "UPDATE", ["INTERN"]),
        new(PermissionNames.ReadOwnWeeklyReports, "own-weekly-reports", "Báo cáo tuần của tôi", "READ", ["INTERN"]),
        new(PermissionNames.CreateWeeklyReport, "own-weekly-reports", "Báo cáo tuần của tôi", "CREATE", ["INTERN"]),
        new(PermissionNames.ReadMentorWeeklyReports, "mentor-weekly-reports", "Báo cáo tuần Intern", "READ", ["MENTOR"]),
        new(PermissionNames.CreateWeeklyReportFeedback, "mentor-weekly-reports", "Báo cáo tuần Intern", "FEEDBACK", ["MENTOR"]),
        new(PermissionNames.ReadMentorWorkload, "mentor-workload", "Thống kê Mentor", "READ", ["HR"]),
        new(PermissionNames.ReadOwnLeaveRequests, "leave-requests", "Đăng ký nghỉ", "READ", ["INTERN"]),
        new(PermissionNames.CreateOwnLeaveRequest, "leave-requests", "Đăng ký nghỉ", "CREATE", ["INTERN"]),
        new(PermissionNames.CancelOwnLeaveRequest, "leave-requests", "Đăng ký nghỉ", "CANCEL", ["INTERN"]),
        new(PermissionNames.ReadMentorLeaveRequests, "mentor-leave-requests", "Duyệt đơn nghỉ", "READ", ["MENTOR"]),
        new(PermissionNames.ReviewLeaveRequests, "mentor-leave-requests", "Duyệt đơn nghỉ", "APPROVE", ["MENTOR"]),
        new(PermissionNames.ReadNotifications, "notifications", "Thông báo", "READ", Staff),
        new(PermissionNames.UpdateNotifications, "notifications", "Thông báo", "UPDATE", Staff),
        new(PermissionNames.CreateNotifications, "notifications", "Thông báo", "CREATE", ["ADMIN", "HR"]),

        // Quản lý thực tập sinh
        new(PermissionNames.ViewInterns, "interns", "Hồ sơ thực tập sinh", "READ", Staff),
        new(PermissionNames.ManageInterns, "interns", "Hồ sơ thực tập sinh", "CREATE", Staff),
        new(PermissionNames.EditInterns, "interns", "Hồ sơ thực tập sinh", "UPDATE", Staff),
        new(PermissionNames.SearchInterns, "search", "Tìm kiếm hồ sơ", "READ", Staff),

        // Duyệt tài liệu
        new(PermissionNames.ReadDocumentReviews, "reviews", "Duyệt tài liệu", "READ", Staff),
        new(PermissionNames.ApproveDocuments, "reviews", "Duyệt tài liệu", "APPROVE", Staff),
        new(PermissionNames.RejectDocuments, "reviews", "Duyệt tài liệu", "REJECT", Staff),

        // Tài liệu thực tập sinh
        new(PermissionNames.ViewDocuments, "documents", "Tài liệu của tôi", "READ", Staff),
        new(PermissionNames.UploadDocuments, "documents", "Tài liệu của tôi", "CREATE", Staff),

        // Quản lý tài khoản
        new(PermissionNames.ManageUsers, "accounts", "Quản lý tài khoản", "READ", Staff),
        new(PermissionNames.CreateUser, "accounts", "Quản lý tài khoản", "CREATE", Staff),
        new(PermissionNames.DeleteUser, "accounts", "Quản lý tài khoản", "DELETE", Staff),

        // Phân quyền RBAC
        new(PermissionNames.ReadPermissions, "rbac", "Cấu hình RBAC", "READ", ["ADMIN"]),
        new(PermissionNames.ManagePermissions, "rbac", "Cấu hình RBAC", "UPDATE", ["ADMIN"]),

        // Hồ sơ cá nhân
        new(PermissionNames.ViewProfile, "profile", "Thông tin tài khoản", "READ", Staff),
        new(PermissionNames.ViewOwnProfile, "own-profile", "Hồ sơ thực tập của tôi", "READ", Staff),
        new(PermissionNames.EditOwnProfile, "own-profile", "Hồ sơ thực tập của tôi", "UPDATE", Staff),
        new(PermissionNames.ViewOwnSchedule, "own-schedule", "Lịch thực tập của tôi", "READ", Staff),

        // Hợp đồng
        new(PermissionNames.ManageContracts, "contracts", "Quản lý hợp đồng", "READ", Staff),
        new(PermissionNames.CreateContract, "contracts", "Quản lý hợp đồng", "CREATE", Staff),
        new(PermissionNames.UpdateContract, "contracts", "Quản lý hợp đồng", "UPDATE", Staff),
        new(PermissionNames.DeleteContract, "contracts", "Quản lý hợp đồng", "DELETE", Staff),
        new(PermissionNames.ViewOwnContract, "own-contract", "Hợp đồng của tôi", "FEATURE", Staff),

        // Phân công Mentor
        new(PermissionNames.AssignMentor, "assignment", "Phân công Mentor", "READ", Staff),
        new(PermissionNames.CreateAssignment, "assignment", "Phân công Mentor", "CREATE", Staff),
        new(PermissionNames.UpdateAssignment, "assignment", "Phân công Mentor", "UPDATE", Staff),
        new(PermissionNames.DeleteAssignment, "assignment", "Phân công Mentor", "DELETE", Staff),

        // Quản lý chương trình
        new(PermissionNames.ManagePrograms, "programs", "Quản lý chương trình", "READ", Staff),
        new(PermissionNames.CreateProgram, "programs", "Quản lý chương trình", "CREATE", Staff),
        new(PermissionNames.UpdateProgram, "programs", "Quản lý chương trình", "UPDATE", Staff),
        new(PermissionNames.DeleteProgram, "programs", "Quản lý chương trình", "DELETE", Staff),

        // Báo cáo và chấm công
        new(PermissionNames.ViewAttendanceReport, "reports", "Báo cáo chuyên cần", "READ", Staff),
        new(PermissionNames.OwnAttendance, "attendance", "Chấm công của tôi", "READ", Staff),
        new(PermissionNames.AttendanceCheckIn, "attendance", "Chấm công của tôi", "CHECK_IN", Staff),
        new(PermissionNames.AttendanceCheckOut, "attendance", "Chấm công của tôi", "CHECK_OUT", Staff),

        // Đánh giá cuối kỳ
        new(
            PermissionNames.ViewFinalEvaluations,
            "final-evaluations",
            "Đánh giá cuối kỳ",
            "READ",
            ["ADMIN", "HR", "MENTOR", "INTERN"]
        ),

        new(
            PermissionNames.UpdateFinalEvaluations,
            "final-evaluations",
            "Đánh giá cuối kỳ",
            "UPDATE",
            ["HR", "MENTOR"]
        )
    ];
}
