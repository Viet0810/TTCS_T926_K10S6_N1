using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

/// <summary>Thông tin hồ sơ thực tập sinh cần tạo.</summary>
public sealed record CreateInternRequest : IValidatableObject
{
    [Required, StringLength(200)]
    public required string FullName { get; init; }

    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required, RegularExpression(@"^[0-9]{10,11}$"), StringLength(20)]
    public required string Phone { get; init; }

    [Required, StringLength(200)]
    public required string School { get; init; }

    [Required, StringLength(200)]
    public required string Major { get; init; }

    [StringLength(50)]
    public string? StudentCode { get; init; }

    [StringLength(100)]
    public string? ClassName { get; init; }

    [StringLength(200)]
    public string? Faculty { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(200)]
    public string? Organization { get; init; }

    [StringLength(500)]
    public string? OrganizationAddress { get; init; }

    [StringLength(200)]
    public string? Department { get; init; }

    [StringLength(200)]
    public string? Position { get; init; }

    [StringLength(200)]
    public string? Mentor { get; init; }

    [StringLength(254), EmailAddress]
    public string? MentorEmail { get; init; }

    [StringLength(20), RegularExpression(@"^[0-9]{10,11}$")]
    public string? MentorPhone { get; init; }

    [StringLength(200)]
    public string? AcademicSupervisor { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    [StringLength(50), RegularExpression(@"^(Chờ tiếp nhận|Đang thực tập|Đã hoàn thành|Đã dừng)$")]
    public string? Status { get; init; }

    [StringLength(500)]
    public string? InternshipTopic { get; init; }

    [StringLength(2000)]
    public string? Notes { get; init; }

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
