namespace InternManagement.DTOs;

/// <summary>Bộ lọc tìm kiếm thực tập sinh từ giao diện HR.</summary>
public sealed record InternFilterRequest
{
    public string? Search { get; init; }
    public string? School { get; init; }
    public string? Major { get; init; }
}
