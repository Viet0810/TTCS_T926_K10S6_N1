using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace InternManagement.DTOs;

/// <summary>
/// DTO ghi nhận ca làm việc / điểm danh mới
/// </summary>
public sealed record CreateAttendanceRecordRequest : IValidatableObject
{
    [Required, Range(1, int.MaxValue)]
    public int InternId { get; init; }

    [Required]
    [StringLength(10)]
    public string Date { get; init; } = string.Empty;

    [StringLength(10)]
    public string? CheckIn { get; init; }

    [StringLength(10)]
    public string? CheckOut { get; init; }

    [Range(typeof(decimal), "0", "24")]
    public decimal Hours { get; init; }

    [Required]
    [StringLength(30)]
    public string Status { get; init; } = "ON_TIME";

    [StringLength(500)]
    public string? Note { get; init; }

    [StringLength(200)]
    public string? Approver { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (!DateOnly.TryParseExact(Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            yield return new ValidationResult("Ngày điểm danh không hợp lệ.", [nameof(Date)]);
        if (!new[] { "ON_TIME", "LATE", "EARLY", "LEAVE_APPROVED", "ABSENT" }.Contains(Status))
            yield return new ValidationResult("Trạng thái không hợp lệ.", [nameof(Status)]);
        var hasIn = !string.IsNullOrEmpty(CheckIn) && CheckIn != "—";
        var hasOut = !string.IsNullOrEmpty(CheckOut) && CheckOut != "—";
        TimeOnly start = default, end = default;
        if (hasIn && !TimeOnly.TryParseExact(CheckIn, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
            yield return new ValidationResult("Giờ vào không hợp lệ.", [nameof(CheckIn)]);
        if (hasOut && !TimeOnly.TryParseExact(CheckOut, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
            yield return new ValidationResult("Giờ ra không hợp lệ.", [nameof(CheckOut)]);
        if (hasOut && !hasIn || hasIn && hasOut && end < start)
            yield return new ValidationResult("Giờ ra không được trước giờ vào.", [nameof(CheckOut)]);
    }
}
