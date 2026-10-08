namespace InternManagement.DTOs;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// DTO thống kê KPI chuyên cần
/// </summary>
public sealed record AttendanceStatsResponse
{
    public int TotalShifts { get; init; }
    public int OnTimeCount { get; init; }
    public int LateOrEarlyCount { get; init; }
    public int ApprovedLeaveCount { get; init; }
    public int AbsentCount { get; init; }
    public string AttendanceRate { get; init; } = "100.0%";
}
