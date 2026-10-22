using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

/// <summary>Thông tin hồ sơ thực tập sinh cần tạo.</summary>
public sealed record CreateInternRequest : IValidatableObject
{
    [Required, StringLength(200)]
    public required string FullName { get => _fullName; init => _fullName = value?.Trim() ?? ""; }

    [Required, EmailAddress, StringLength(254)]
    public required string Email { get => _email; init => _email = value?.Trim() ?? ""; }

    [Required, RegularExpression(@"^[0-9]{10,11}$"), StringLength(20)]
    public required string Phone { get => _phone; init => _phone = value?.Trim() ?? ""; }

    [Required, StringLength(200)]
    public required string School { get => _school; init => _school = value?.Trim() ?? ""; }

    [Required, StringLength(200)]
    public required string Major { get => _major; init => _major = value?.Trim() ?? ""; }

    [StringLength(50)]
    public string? StudentCode { get => _studentCode; init => _studentCode = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(100)]
    public string? ClassName { get => _className; init => _className = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(200)]
    public string? Faculty { get => _faculty; init => _faculty = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    public DateOnly? DateOfBirth { get; init; }

    [StringLength(500)]
    public string? Address { get => _address; init => _address = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(200)]
    public string? Organization { get => _organization; init => _organization = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(500)]
    public string? OrganizationAddress { get => _organizationAddress; init => _organizationAddress = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(200)]
    public string? Department { get => _department; init => _department = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(200)]
    public string? Position { get => _position; init => _position = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(200)]
    public string? Mentor { get => _mentor; init => _mentor = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(254), EmailAddress]
    public string? MentorEmail { get => _mentorEmail; init => _mentorEmail = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(20), RegularExpression(@"^[0-9]{10,11}$")]
    public string? MentorPhone { get => _mentorPhone; init => _mentorPhone = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(200)]
    public string? AcademicSupervisor { get => _academicSupervisor; init => _academicSupervisor = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    [StringLength(50), RegularExpression(@"^(Chờ tiếp nhận|Đang thực tập|Đã hoàn thành|Đã dừng)$")]
    public string? Status { get => _status; init => _status = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(500)]
    public string? InternshipTopic { get => _internshipTopic; init => _internshipTopic = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    [StringLength(2000)]
    public string? Notes { get => _notes; init => _notes = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); }

    private string _fullName = "";
    private string _email = "";
    private string _phone = "";
    private string _school = "";
    private string _major = "";
    private string? _studentCode;
    private string? _className;
    private string? _faculty;
    private string? _address;
    private string? _organization;
    private string? _organizationAddress;
    private string? _department;
    private string? _position;
    private string? _mentor;
    private string? _mentorEmail;
    private string? _mentorPhone;
    private string? _academicSupervisor;
    private string? _status;
    private string? _internshipTopic;
    private string? _notes;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (DateOfBirth.HasValue && DateOfBirth.Value > today)
            yield return new ValidationResult("Ngày sinh không được ở tương lai.", [nameof(DateOfBirth)]);
        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
            yield return new ValidationResult("Ngày kết thúc phải từ ngày bắt đầu trở đi.", [nameof(EndDate)]);
        if (DateOfBirth.HasValue && StartDate.HasValue && StartDate.Value <= DateOfBirth.Value)
            yield return new ValidationResult("Ngày bắt đầu thực tập phải sau ngày sinh.", [nameof(StartDate)]);
    }
}
