namespace InternManagement.DTOs;

public sealed record InternResponse(
    int Id, string Name, string Mssv, string Email, string Phone, string School,
    string Major, string Role, string Status, string Mentor, int Progress, string Gpa);

public sealed record UpdateInternRequest(
    string? Name, string? Mssv, string? Email, string? Phone, string? School,
    string? Major, string? Role, string? Status, string? Mentor, int Progress, string? Gpa);

public sealed record CreateInternRequest(
    string? Name, string? Mssv, string? Email, string? Phone, string? School,
    string? Major, string? Role, string? Status, string? Mentor, int Progress, string? Gpa,
    string? Department, DateTime? StartDate, DateTime? EndDate);
