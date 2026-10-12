using System.ComponentModel.DataAnnotations;
namespace InternManagement.DTOs;
public sealed record PageRequest
{
    [Range(1,1000000)] public int Page {get;init;}=1;
    [Range(1,100)] public int PageSize {get;init;}=10;
    [StringLength(254)] public string? Search {get;init;}
}
public sealed record PageResponse<T>(IReadOnlyList<T> Items,int Page,int PageSize,int TotalItems,int TotalPages);
