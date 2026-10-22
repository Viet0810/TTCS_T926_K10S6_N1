
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

        PermissionNames.FinalizeEvaluationSummaries or PermissionNames.PublishEvaluationSummaries
            => [PermissionNames.ViewEvaluationSummaries],

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
        ["HR", "MENTOR", "INTERN"];

    public static readonly PermissionDescriptor[] Entries =
    [
        new(PermissionNames.ReadWorkSchedules, "work-schedules", "Lịch làm việc", "READ", ["HR", "MENTOR", "INTERN"]),
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
        new(PermissionNames.CreateNotifications, "notifications", "Thông báo", "CREATE", ["HR"]),

        // Quản lý thực tập sinh
        new(PermissionNames.ViewInterns, "interns", "Hồ sơ thực tập sinh", "READ", ["HR", "MENTOR"]),
        new(PermissionNames.ManageInterns, "interns", "Hồ sơ thực tập sinh", "CREATE", ["HR"]),
        new(PermissionNames.EditInterns, "interns", "Hồ sơ thực tập sinh", "UPDATE", ["HR"]),
        new(PermissionNames.SearchInterns, "search", "Tìm kiếm hồ sơ", "READ", ["HR", "MENTOR"]),

        // Duyệt tài liệu
        new(PermissionNames.ReadDocumentReviews, "reviews", "Duyệt tài liệu", "READ", ["HR"]),
        new(PermissionNames.ApproveDocuments, "reviews", "Duyệt tài liệu", "APPROVE", ["HR"]),
        new(PermissionNames.RejectDocuments, "reviews", "Duyệt tài liệu", "REJECT", ["HR"]),

        // Tài liệu thực tập sinh
        new(PermissionNames.ViewDocuments, "documents", "Tài liệu của tôi", "READ", ["INTERN"]),
        new(PermissionNames.UploadDocuments, "documents", "Tài liệu của tôi", "CREATE", ["INTERN"]),

        // Quản lý tài khoản
        new(PermissionNames.ManageUsers, "accounts", "Quản lý tài khoản", "READ", ["ADMIN", "HR"]),
        new(PermissionNames.CreateUser, "accounts", "Quản lý tài khoản", "CREATE", ["ADMIN", "HR"]),
        new(PermissionNames.DeleteUser, "accounts", "Quản lý tài khoản", "DELETE", ["ADMIN"]),

        // Phân quyền RBAC
        new(PermissionNames.ReadPermissions, "rbac", "Cấu hình RBAC", "READ", ["ADMIN"]),
        new(PermissionNames.ManagePermissions, "rbac", "Cấu hình RBAC", "UPDATE", ["ADMIN"]),

        // Hồ sơ cá nhân
        new(PermissionNames.ViewProfile, "profile", "Thông tin tài khoản", "READ", ["ADMIN", "HR", "MENTOR", "INTERN"]),
        new(PermissionNames.ViewOwnProfile, "own-profile", "Hồ sơ thực tập của tôi", "READ", ["INTERN"]),
        new(PermissionNames.EditOwnProfile, "own-profile", "Hồ sơ thực tập của tôi", "UPDATE", ["INTERN"]),
        new(PermissionNames.ViewOwnSchedule, "own-schedule", "Lịch thực tập của tôi", "READ", ["INTERN"]),

        // Hợp đồng
        new(PermissionNames.ManageContracts, "contracts", "Quản lý hợp đồng", "READ", ["HR"]),
        new(PermissionNames.CreateContract, "contracts", "Quản lý hợp đồng", "CREATE", ["HR"]),
        new(PermissionNames.UpdateContract, "contracts", "Quản lý hợp đồng", "UPDATE", ["HR"]),
        new(PermissionNames.DeleteContract, "contracts", "Quản lý hợp đồng", "DELETE", ["HR"]),
        new(PermissionNames.ViewOwnContract, "own-contract", "Hợp đồng của tôi", "FEATURE", ["INTERN"]),

        // Phân công Mentor
        new(PermissionNames.AssignMentor, "assignment", "Phân công Mentor", "READ", ["HR"]),
        new(PermissionNames.CreateAssignment, "assignment", "Phân công Mentor", "CREATE", ["HR"]),
        new(PermissionNames.UpdateAssignment, "assignment", "Phân công Mentor", "UPDATE", ["HR"]),
        new(PermissionNames.DeleteAssignment, "assignment", "Phân công Mentor", "DELETE", ["HR"]),

        // Quản lý chương trình
        new(PermissionNames.ManagePrograms, "programs", "Quản lý chương trình", "READ", ["HR"]),
        new(PermissionNames.CreateProgram, "programs", "Quản lý chương trình", "CREATE", ["HR"]),
        new(PermissionNames.UpdateProgram, "programs", "Quản lý chương trình", "UPDATE", ["HR"]),
        new(PermissionNames.DeleteProgram, "programs", "Quản lý chương trình", "DELETE", ["HR"]),

        // Báo cáo và chấm công
        new(PermissionNames.ViewAttendanceReport, "reports", "Báo cáo chuyên cần", "READ", ["HR"]),
        new(PermissionNames.OwnAttendance, "attendance", "Chấm công của tôi", "READ", ["INTERN"]),
        new(PermissionNames.AttendanceCheckIn, "attendance", "Chấm công của tôi", "CHECK_IN", ["INTERN"]),
        new(PermissionNames.AttendanceCheckOut, "attendance", "Chấm công của tôi", "CHECK_OUT", ["INTERN"]),

        // Đánh giá cuối kỳ
        new(
            PermissionNames.ViewFinalEvaluations,
            "final-evaluations",
            "Đánh giá cuối kỳ",
            "READ",
            ["HR", "MENTOR", "INTERN"]
        ),

        new(
            PermissionNames.UpdateFinalEvaluations,
            "final-evaluations",
            "Đánh giá cuối kỳ",
            "UPDATE",
            ["MENTOR"]
        ),
        new(PermissionNames.ViewEvaluationSummaries, "evaluation-summaries", "Tổng hợp đánh giá", "READ", ["HR"]),
        new(PermissionNames.FinalizeEvaluationSummaries, "evaluation-summaries", "Tổng hợp đánh giá", "FINALIZE", ["HR"]),
        new(PermissionNames.PublishEvaluationSummaries, "evaluation-summaries", "Tổng hợp đánh giá", "PUBLISH", ["HR"])
    ];
}
