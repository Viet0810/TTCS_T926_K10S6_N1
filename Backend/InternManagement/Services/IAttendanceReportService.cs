using InternManagement.DTOs;

namespace InternManagement.Services;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// Giao diện dịch vụ tổng hợp số liệu điểm danh, nghỉ phép và lọc theo thời gian
/// </summary>
public interface IAttendanceReportService
{
    Task<AttendanceReportResponse> GetReportAsync(AttendanceReportRequest? filter, CancellationToken cancellationToken = default);
    Task<int> RecordAttendanceAsync(CreateAttendanceRecordRequest request, CancellationToken cancellationToken = default);
    Task<int> SeedSampleDataAsync(CancellationToken cancellationToken = default);
}
