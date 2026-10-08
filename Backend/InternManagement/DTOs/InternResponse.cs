namespace InternManagement.DTOs;

/// <summary>Hồ sơ thực tập sinh được trả về từ hệ thống.</summary>
public sealed record InternResponse(
    int Id,
    string FullName,
    string Email,
    string Phone,
    string School,
    string Major,
    DateTimeOffset CreatedAt,
    string? StudentCode = null,
    string? ClassName = null,
    string? Faculty = null,
    DateOnly? DateOfBirth = null,
    string? Address = null,
    string? Organization = null,
    string? OrganizationAddress = null,
    string? Department = null,
    string? Position = null,
    string? Mentor = null,
    string? MentorEmail = null,
    string? MentorPhone = null,
    string? AcademicSupervisor = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    string? Status = null,
    string? InternshipTopic = null,
    string? Notes = null);