using InternManagement.DTOs;

namespace InternManagement.Services;

public interface INotificationService
{
    Task<NotificationListResponse> GetNotificationsAsync(int userId, int page = 1, int pageSize = 15, bool unreadOnly = false, string? category = null, CancellationToken ct = default);
    Task<UnreadCountResponse> GetUnreadCountAsync(int userId, CancellationToken ct = default);
    Task<MarkReadResponse?> MarkAsReadAsync(int userId, long notificationId, CancellationToken ct = default);
    Task<MarkAllReadResponse> MarkAllAsReadAsync(int userId, CancellationToken ct = default);
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationRequest request, int? creatorId = null, CancellationToken ct = default);
    Task<int> SeedSampleNotificationsAsync(int userId, CancellationToken ct = default);
}
