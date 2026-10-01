using InternManagement.DTOs;

namespace InternManagement.HrSearchFilterInterns;

/// <summary>
/// Chức năng: Tìm kiếm và lọc thực tập sinh (K10S6N1-48)
/// Interface định nghĩa nghiệp vụ tìm kiếm và lọc dữ liệu từ SQL Server
/// </summary>
public interface IInternFilterService
{
    Task<IReadOnlyList<InternResponse>> FilterInternsAsync(InternFilterRequest? filter, CancellationToken cancellationToken = default);
}
