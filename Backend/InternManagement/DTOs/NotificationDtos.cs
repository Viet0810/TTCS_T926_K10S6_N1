namespace InternManagement.DTOs;

public sealed record AppNotification(long Id, string Title, string Message, string? ActionUrl, DateTime CreatedAt, DateTime? ReadAt);
