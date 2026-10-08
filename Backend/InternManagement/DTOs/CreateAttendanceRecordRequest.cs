using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

/// <summary>
/// DTO ghi nhận ca làm việc / điểm danh mới
/// </summary>
public sealed record CreateAttendanceRecordRequest
{
    [Required]
    public int InternId { get; init; }

    [Required]
    [StringLength(10)]
    public string Date { get; init; } = string.Empty;

    [StringLength(10)]
    public string? CheckIn { get; init; }

    [StringLength(10)]
    public string? CheckOut { get; init; }

    public decimal Hours { get; init; }

    [Required]
    [StringLength(30)]
    public string Status { get; init; } = "ON_TIME";

    [StringLength(500)]
    public string? Note { get; init; }

    [StringLength(200)]
    public string? Approver { get; init; }
}
