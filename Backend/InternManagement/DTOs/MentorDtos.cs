namespace InternManagement.DTOs;

public sealed record MentorTaskRequest(int InternId, string? Title, string? Description, DateTime? Deadline, string? Priority);
public sealed record MentorGroupTaskRequest(int[]? InternIds, string? Title, string? Description, DateTime? Deadline, string? Priority);
public sealed record TaskProgressRequest(string? Status);
public sealed record TaskReviewRequest(string? Status, string? Feedback);
public sealed record ReportReviewRequest(string? Status, string? Feedback, decimal? Score);
public sealed record InternReportRequest(string? Title, string? Content, string? AttachmentUrl);
public sealed record EvaluationRequest(Dictionary<string, decimal>? Scores, string? Comments, string? Recommendation, bool Submit);
public sealed record MentorFeedbackRequest(int InternId, string? Content, string? Type, string? Severity);
public sealed record MentoringScheduleRequest(int InternId, string? Title, string? Content, DateTime StartsAt,
    DateTime EndsAt, string? Location, string? Notes);
public sealed record MentorProfileRequest(string? Phone, string? AvatarUrl, string? Position,
    string? Department, string? Skills, string? Experience);
public sealed record MentorAssignmentRequest(int InternId, int MentorUserId);
public sealed record BulkMentorAssignmentRequest(int[]? InternIds, int MentorUserId);
