using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record AssignMentorRequest(
    [Range(1, int.MaxValue)] int MentorUserId,
    [Range(1, int.MaxValue)] int? InternshipProgramId);

public sealed record MentorOption(int Id, string FullName, string Email);

public sealed record InternAssignmentResponse(
    int InternId, string FullName, string Email, int? MentorUserId,
    string? MentorName, int? InternshipProgramId, string? Status);

public sealed record InternScheduleResponse(
    int InternId, int? InternshipProgramId, DateOnly? StartDate, DateOnly? EndDate,
    string? DepartmentName, string? MentorName, string? Status);
