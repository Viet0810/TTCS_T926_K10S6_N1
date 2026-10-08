using InternManagement.DTOs;

namespace InternManagement.Services;

/// <summary>
/// Chức năng: Tìm kiếm và lọc thực tập sinh (K10S6N1-48)
/// Interface định nghĩa nghiệp vụ tìm kiếm và lọc dữ liệu từ PostgreSQL
/// </summary>
public interface IInternFilterService
{
    Task<IReadOnlyList<InternResponse>> FilterInternsAsync(InternFilterRequest? filter, CancellationToken cancellationToken = default);
}
