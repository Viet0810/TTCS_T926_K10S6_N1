namespace InternManagement.DTOs;

/// <summary>Hồ sơ thực tập sinh được trả về từ hệ thống.</summary>
public sealed record InternResponse(
    int Id,
    string FullName,
    string Email,
    string Phone,
    string School,
    string Major,
    DateTimeOffset CreatedAt);