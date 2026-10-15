
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

        PermissionNames.CreateContract or PermissionNames.UpdateContract or PermissionNames.DeleteContract
            => [PermissionNames.ManageContracts],

        PermissionNames.CreateProgram or PermissionNames.UpdateProgram or PermissionNames.DeleteProgram
            => [PermissionNames.ManagePrograms],

        PermissionNames.CreateAssignment or PermissionNames.UpdateAssignment or PermissionNames.DeleteAssignment
            => [PermissionNames.AssignMentor],

        // Cập nhật đánh giá yêu cầu có quyền xem đánh giá.
        PermissionNames.UpdateFinalEvaluations
            => [PermissionNames.ViewFinalEvaluations],

        _ => []
    };
}

public static class PermissionCatalog
{
    private static readonly string[] Staff =
        ["ADMIN", "HR", "MENTOR", "INTERN"];

    public static readonly PermissionDescriptor[] Entries =
    [
        new(PermissionNames.ViewInterns, "interns", "Hồ sơ thực tập sinh", "READ", Staff),
        new(PermissionNames.ManageInterns, "interns", "Hồ sơ thực tập sinh", "CREATE", Staff),
        new(PermissionNames.EditInterns, "interns", "Hồ sơ thực tập sinh", "UPDATE", Staff),
        new(PermissionNames.SearchInterns, "search", "Tìm kiếm hồ sơ", "READ", Staff),

        new(PermissionNames.ReadDocumentReviews, "reviews", "Duyệt tài liệu", "READ", Staff),
        new(PermissionNames.ApproveDocuments, "reviews", "Duyệt tài liệu", "APPROVE", Staff),
        new(PermissionNames.RejectDocuments, "reviews", "Duyệt tài liệu", "REJECT", Staff),

        new(PermissionNames.ViewDocuments, "documents", "Tài liệu của tôi", "READ", Staff),
        new(PermissionNames.UploadDocuments, "documents", "Tài liệu của tôi", "CREATE", Staff),

        new(PermissionNames.ManageUsers, "accounts", "Quản lý tài khoản", "READ", Staff),
        new(PermissionNames.CreateUser, "accounts", "Quản lý tài khoản", "CREATE", Staff),
        new(PermissionNames.DeleteUser, "accounts", "Quản lý tài khoản", "DELETE", Staff),

        new(PermissionNames.ReadPermissions, "rbac", "Cấu hình RBAC", "READ", ["ADMIN"]),
        new(PermissionNames.ManagePermissions, "rbac", "Cấu hình RBAC", "UPDATE", ["ADMIN"]),

        new(PermissionNames.ViewProfile, "profile", "Thông tin tài khoản", "READ", Staff),
        new(PermissionNames.ViewOwnProfile, "own-profile", "Hồ sơ thực tập của tôi", "READ", Staff),
        new(PermissionNames.EditOwnProfile, "own-profile", "Hồ sơ thực tập của tôi", "UPDATE", Staff),
        new(PermissionNames.ViewOwnSchedule, "own-schedule", "Lịch thực tập của tôi", "READ", Staff),

        new(PermissionNames.ManageContracts, "contracts", "Quản lý hợp đồng", "READ", Staff),
        new(PermissionNames.CreateContract, "contracts", "Quản lý hợp đồng", "CREATE", Staff),
        new(PermissionNames.UpdateContract, "contracts", "Quản lý hợp đồng", "UPDATE", Staff),
        new(PermissionNames.DeleteContract, "contracts", "Quản lý hợp đồng", "DELETE", Staff),
        new(PermissionNames.ViewOwnContract, "own-contract", "Hợp đồng của tôi", "FEATURE", Staff),

        new(PermissionNames.AssignMentor, "assignment", "Phân công Mentor", "READ", Staff),
        new(PermissionNames.CreateAssignment, "assignment", "Phân công Mentor", "CREATE", Staff),
        new(PermissionNames.UpdateAssignment, "assignment", "Phân công Mentor", "UPDATE", Staff),
        new(PermissionNames.DeleteAssignment, "assignment", "Phân công Mentor", "DELETE", Staff),

        new(PermissionNames.ManagePrograms, "programs", "Quản lý chương trình", "READ", Staff),
        new(PermissionNames.CreateProgram, "programs", "Quản lý chương trình", "CREATE", Staff),
        new(PermissionNames.UpdateProgram, "programs", "Quản lý chương trình", "UPDATE", Staff),
        new(PermissionNames.DeleteProgram, "programs", "Quản lý chương trình", "DELETE", Staff),

        new(PermissionNames.ViewAttendanceReport, "reports", "Báo cáo chuyên cần", "READ", Staff),
        new(PermissionNames.OwnAttendance, "attendance", "Chấm công của tôi", "READ", Staff),
        new(PermissionNames.AttendanceCheckIn, "attendance", "Chấm công của tôi", "CHECK_IN", Staff),
        new(PermissionNames.AttendanceCheckOut, "attendance", "Chấm công của tôi", "CHECK_OUT", Staff),

        // QUYỀN ĐÁNH GIÁ CUỐI KỲ
        new(
            PermissionNames.ViewFinalEvaluations,
            "final-evaluations",
            "Đánh giá cuối kỳ",
            "READ",
            ["ADMIN", "HR", "MENTOR"]
        ),

        new(
            PermissionNames.UpdateFinalEvaluations,
            "final-evaluations",
            "Đánh giá cuối kỳ",
            "UPDATE",
            ["ADMIN", "HR", "MENTOR"]
        )
    ];
}
