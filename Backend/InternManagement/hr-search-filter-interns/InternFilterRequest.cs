namespace InternManagement.HrSearchFilterInterns;

/// <summary>
/// Chức năng: Tìm kiếm và lọc thực tập sinh (K10S6N1-48)
/// DTO chứa các tham số lọc từ URL: search, school, major
/// </summary>
public sealed record InternFilterRequest
{
    public string? Search { get; init; }
    public string? School { get; init; }
    public string? Major { get; init; }
}
