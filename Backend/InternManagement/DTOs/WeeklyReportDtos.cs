using System.ComponentModel.DataAnnotations;

namespace InternManagement.DTOs;

public sealed record WeeklyReportResponse(
    long Id,
    int InternId,
    string InternName,
    string? StudentCode,
    string? Organization,
    DateOnly WeekStart,
    DateOnly WeekEnd,
    string Summary,
    string? Accomplishments,
    string? Challenges,
    string? NextWeekPlan,
    DateTimeOffset SubmittedAt,
    string? MentorFeedback,
    DateTimeOffset? FeedbackAt);

public sealed record SaveWeeklyReportFeedbackRequest(
    [Required, StringLength(2000, MinimumLength = 1)] string Feedback);

public sealed record CreateWeeklyReportRequest(
    [Required] DateOnly? WeekStart,
    [Required] DateOnly? WeekEnd,
    [Required, StringLength(4000, MinimumLength = 1)] string Summary,
    [StringLength(4000)] string? Accomplishments,
    [StringLength(4000)] string? Challenges,
    [StringLength(4000)] string? NextWeekPlan);
