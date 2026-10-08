using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// DTO chứa các tham số lọc: internId, search, startDate, endDate, status
/// </summary>
public sealed record AttendanceReportRequest
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
}
