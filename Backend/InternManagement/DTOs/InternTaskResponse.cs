using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record CreateInternTaskRequest(
    [Range(1, int.MaxValue)] int InternId,
    [Required, StringLength(2000)] string Content,
    DateTimeOffset Deadline);

public sealed record TaskInternOption(int InternId, string FullName, string Email);
public sealed record InternTaskResponse(int Id, int InternId, string InternName, int MentorUserId,
    string MentorName, string Content, DateTimeOffset Deadline, string Status, DateTimeOffset CreatedAt,
    int Progress, string? ProgressNote, DateTimeOffset? UpdatedAt);

public sealed record UpdateTaskProgressRequest(
    [Required, Range(0, 100)] int? Progress,
    [StringLength(2000)] string? ProgressNote);
