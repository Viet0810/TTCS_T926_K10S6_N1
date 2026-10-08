namespace InternManagement.DTOs;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// DTO phản hồi một ca làm việc / nghỉ phép
/// </summary>
public sealed record AttendanceRecordResponse
{
    public int Id { get; init; }
    public int InternId { get; init; }
    public string Mssv { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public string CheckIn { get; init; } = "—";
    public string CheckOut { get; init; } = "—";
    public decimal Hours { get; init; }
    public string Status { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string? Approver { get; init; }
}
