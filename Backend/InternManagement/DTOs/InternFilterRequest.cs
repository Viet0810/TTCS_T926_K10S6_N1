using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

/// <summary>
/// Chức năng: Tìm kiếm và lọc thực tập sinh (K10S6N1-48)
/// DTO chứa các tham số lọc từ URL: search, school, major
/// </summary>
public sealed record InternFilterRequest
{
    [StringLength(254)]
    public string? Search { get; init; }
    [StringLength(200)]
    public string? School { get; init; }
    [StringLength(200)]
    public string? Major { get; init; }
}
