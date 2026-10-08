namespace InternManagement.DTOs;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// DTO tổng hợp kết quả báo cáo gồm chỉ số KPI và danh sách ca làm việc
/// </summary>
public sealed record AttendanceReportResponse
{
    public AttendanceStatsResponse Stats { get; init; } = new();
    public IReadOnlyList<AttendanceRecordResponse> Records { get; init; } = Array.Empty<AttendanceRecordResponse>();
}
