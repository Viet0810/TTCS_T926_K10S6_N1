using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace InternManagement.DTOs;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// DTO chứa các tham số lọc: internId, search, startDate, endDate, status
/// </summary>
public sealed record AttendanceReportRequest : IValidatableObject
{
    public int? InternId { get; init; }

    [StringLength(254)]
    public string? Search { get; init; }

    [StringLength(20)]
    public string? StartDate { get; init; }

    [StringLength(20)]
    public string? EndDate { get; init; }

    [StringLength(50)]
    public string? Status { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (InternId is <= 0) yield return new ValidationResult("Thực tập sinh không hợp lệ.", [nameof(InternId)]);
        DateOnly start = default, end = default;
        if (!string.IsNullOrEmpty(StartDate) && !DateOnly.TryParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start))
            yield return new ValidationResult("Ngày bắt đầu không hợp lệ.", [nameof(StartDate)]);
        if (!string.IsNullOrEmpty(EndDate) && !DateOnly.TryParseExact(EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end))
            yield return new ValidationResult("Ngày kết thúc không hợp lệ.", [nameof(EndDate)]);
        if (start != default && end != default && start > end)
            yield return new ValidationResult("Ngày kết thúc không được trước ngày bắt đầu.", [nameof(EndDate)]);
        if (!string.IsNullOrEmpty(Status) && !new[] { "ALL", "ON_TIME", "LATE", "EARLY", "LEAVE_APPROVED", "ABSENT" }.Contains(Status))
            yield return new ValidationResult("Trạng thái không hợp lệ.", [nameof(Status)]);
    }
}
