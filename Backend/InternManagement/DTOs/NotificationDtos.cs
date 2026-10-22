namespace InternManagement.DTOs;

public sealed record NotificationDto(
    long Id,
    int UserId,
    string Title,
    string Message,
    string Category,
    string Type,
    string Priority,
    string? ActionUrl,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt
);

public sealed record NotificationListResponse(
    int Total,
    int UnreadCount,
    int Page,
    int PageSize,
    List<NotificationDto> Items
);

public sealed record UnreadCountResponse(
    int UnreadCount
);

public sealed record MarkReadResponse(
    bool Success,
    long Id,
    bool IsRead,
    DateTime? ReadAt
);

public sealed record MarkAllReadResponse(
    bool Success,
    int UpdatedCount
);

public sealed record CreateNotificationRequest(
    int? UserId,
    string Title,
    string Message,
    string Category,
    string Type,
    string? Priority = "INFO",
    string? ActionUrl = null
);
