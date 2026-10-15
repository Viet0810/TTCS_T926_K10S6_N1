namespace InternManagement.DTOs;

public sealed record CreateLeaveRequestDto(
    string LeaveType,
    string DurationType,
    string StartDate,
    string EndDate,
    string Reason
);

public sealed record LeaveRequestResponseDto(
    long Id,
    int InternId,
    int UserId,
    string LeaveType,
    string LeaveTypeLabel,
    string DurationType,
    string DurationTypeLabel,
    string StartDate,
    string EndDate,
    decimal TotalDays,
    string Reason,
    string Status,
    string StatusLabel,
    int? ReviewerId,
    string? ReviewerName,
    string? ReviewerNote,
    DateTime? ReviewedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed record LeaveRequestListResponse(
    int Total,
    int PendingCount,
    int ApprovedCount,
    int RejectedCount,
    int CancelledCount,
    int Page,
    int PageSize,
    IReadOnlyList<LeaveRequestResponseDto> Items
);

public sealed record CancelLeaveRequestResponse(
    bool Success,
    long Id,
    string Status,
    string Message
);

public sealed record ReviewLeaveRequestDto(string Decision, string? Note);

public sealed record MentorLeaveRequestResponseDto(
    long Id,
    int InternId,
    string InternName,
    string InternEmail,
    string LeaveType,
    string LeaveTypeLabel,
    string DurationType,
    string DurationTypeLabel,
    string StartDate,
    string EndDate,
    decimal TotalDays,
    string Reason,
    string Status,
    string StatusLabel,
    string? ReviewerNote,
    DateTime? ReviewedAt,
    DateTime CreatedAt
);

public sealed record ReviewLeaveRequestResponse(long Id, string Status, string StatusLabel, string Message);
