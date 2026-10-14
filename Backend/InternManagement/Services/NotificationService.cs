using System.Data;
using System.Text;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class NotificationService : INotificationService
{
    private readonly string connectionString;

    private static volatile bool tableEnsured;

    public NotificationService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
    }

    private async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        if (tableEnsured) return;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await Infrastructure.NotificationMigration.ApplyAsync(connection, ct);
        tableEnsured = true;
    }

    public async Task<NotificationListResponse> GetNotificationsAsync(
        int userId,
        int page = 1,
        int pageSize = 15,
        bool unreadOnly = false,
        string? category = null,
        CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 15;
        if (pageSize > 100) pageSize = 100;
        int offset = (page - 1) * pageSize;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // 1. Đếm unreadCount tổng cộng của người dùng
        int totalUnread = 0;
        await using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId = @userId AND IsRead = 0";
            countCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            var res = await countCmd.ExecuteScalarAsync(ct);
            totalUnread = res != null ? Convert.ToInt32(res) : 0;
        }

        // 2. Đếm tổng số bản ghi khớp bộ lọc
        var countBuilder = new StringBuilder("SELECT COUNT(*) FROM dbo.Notifications WHERE UserId = @userId");
        if (unreadOnly)
        {
            countBuilder.Append(" AND IsRead = 0");
        }
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            countBuilder.Append(" AND Category = @category");
        }

        int totalFiltered = 0;
        await using (var filteredCmd = connection.CreateCommand())
        {
            filteredCmd.CommandText = countBuilder.ToString();
            filteredCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                filteredCmd.Parameters.Add("@category", SqlDbType.VarChar, 50).Value = category;
            }
            var res = await filteredCmd.ExecuteScalarAsync(ct);
            totalFiltered = res != null ? Convert.ToInt32(res) : 0;
        }

        // 3. Lấy danh sách phân trang
        var queryBuilder = new StringBuilder("""
            SELECT 
                Id, 
                UserId, 
                Title, 
                Message, 
                Category, 
                Type, 
                Priority, 
                ActionUrl, 
                IsRead, 
                ReadAt, 
                CreatedAt
            FROM dbo.Notifications
            WHERE UserId = @userId
            """);

        if (unreadOnly)
        {
            queryBuilder.Append(" AND IsRead = 0");
        }
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            queryBuilder.Append(" AND Category = @category");
        }

        queryBuilder.Append(" ORDER BY CreatedAt DESC, Id DESC OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY");

        var items = new List<NotificationDto>();
        await using (var queryCmd = connection.CreateCommand())
        {
            queryCmd.CommandText = queryBuilder.ToString();
            queryCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            queryCmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
            queryCmd.Parameters.Add("@pageSize", SqlDbType.Int).Value = pageSize;
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                queryCmd.Parameters.Add("@category", SqlDbType.VarChar, 50).Value = category;
            }

            await using var reader = await queryCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                items.Add(new NotificationDto(
                    Id: reader.GetInt64(0),
                    UserId: reader.GetInt32(1),
                    Title: reader.GetString(2),
                    Message: reader.GetString(3),
                    Category: reader.GetString(4),
                    Type: reader.GetString(5),
                    Priority: reader.GetString(6),
                    ActionUrl: reader.IsDBNull(7) ? null : reader.GetString(7),
                    IsRead: reader.GetBoolean(8),
                    ReadAt: reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                    CreatedAt: reader.GetDateTime(10)
                ));
            }
        }

        return new NotificationListResponse(
            Total: totalFiltered,
            UnreadCount: totalUnread,
            Page: page,
            PageSize: pageSize,
            Items: items
        );
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(int userId, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId = @userId AND IsRead = 0";
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;

        var result = await command.ExecuteScalarAsync(ct);
        int count = result != null ? Convert.ToInt32(result) : 0;
        return new UnreadCountResponse(count);
    }

    public async Task<MarkReadResponse?> MarkAsReadAsync(int userId, long notificationId, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var now = DateTime.UtcNow;
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Notifications
            SET IsRead = 1, ReadAt = COALESCE(ReadAt, @readAt)
            OUTPUT INSERTED.Id, INSERTED.IsRead, INSERTED.ReadAt
            WHERE Id = @id AND UserId = @userId;
            """;
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = notificationId;
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        command.Parameters.Add("@readAt", SqlDbType.DateTime2).Value = now;

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new MarkReadResponse(
                Success: true,
                Id: reader.GetInt64(0),
                IsRead: reader.GetBoolean(1),
                ReadAt: reader.IsDBNull(2) ? now : reader.GetDateTime(2)
            );
        }

        return null;
    }

    public async Task<MarkAllReadResponse> MarkAllAsReadAsync(int userId, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Notifications
            SET IsRead = 1, ReadAt = COALESCE(ReadAt, SYSUTCDATETIME())
            WHERE UserId = @userId AND IsRead = 0;
            """;
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;

        int affected = await command.ExecuteNonQueryAsync(ct);
        return new MarkAllReadResponse(Success: true, UpdatedCount: affected);
    }

    public async Task<NotificationDto> CreateNotificationAsync(
        CreateNotificationRequest request,
        int? creatorId = null,
        CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        if (request.UserId is null or <= 0)
        {
            throw new ArgumentException("UserId hợp lệ là bắt buộc.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var priorityRaw = string.IsNullOrWhiteSpace(request.Priority) ? "INFO" : request.Priority.Trim().ToUpperInvariant();
        var priority = priorityRaw is "INFO" or "SUCCESS" or "WARNING" or "DANGER" ? priorityRaw : "INFO";

        var categoryRaw = string.IsNullOrWhiteSpace(request.Category) ? "SYSTEM" : request.Category.Trim().ToUpperInvariant();
        var category = categoryRaw is "ATTENDANCE" or "DOCUMENT" or "CONTRACT" or "ASSIGNMENT" or "PROGRAM" or "SYSTEM" ? categoryRaw : "SYSTEM";

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length > 200) title = title[..200];
        if (string.IsNullOrEmpty(title)) title = "Thông báo hệ thống";

        var message = (request.Message ?? string.Empty).Trim();
        if (message.Length > 1000) message = message[..1000];

        var actionUrl = request.ActionUrl?.Trim();
        if (actionUrl != null && actionUrl.Length > 500) actionUrl = actionUrl[..500];

        var type = string.IsNullOrWhiteSpace(request.Type) ? "SYSTEM_ANNOUNCEMENT" : request.Type.Trim();
        if (type.Length > 50) type = type[..50];

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Notifications (UserId, Title, Message, Category, Type, Priority, ActionUrl, IsRead, CreatedAt, CreatedBy)
            OUTPUT 
                INSERTED.Id, 
                INSERTED.UserId, 
                INSERTED.Title, 
                INSERTED.Message, 
                INSERTED.Category, 
                INSERTED.Type, 
                INSERTED.Priority, 
                INSERTED.ActionUrl, 
                INSERTED.IsRead, 
                INSERTED.ReadAt, 
                INSERTED.CreatedAt
            VALUES (@userId, @title, @message, @category, @type, @priority, @actionUrl, 0, SYSUTCDATETIME(), @createdBy);
            """;

        command.Parameters.Add("@userId", SqlDbType.Int).Value = request.UserId.Value;
        command.Parameters.Add("@title", SqlDbType.NVarChar, 200).Value = title;
        command.Parameters.Add("@message", SqlDbType.NVarChar, 1000).Value = message;
        command.Parameters.Add("@category", SqlDbType.VarChar, 50).Value = category;
        command.Parameters.Add("@type", SqlDbType.VarChar, 50).Value = type;
        command.Parameters.Add("@priority", SqlDbType.VarChar, 20).Value = priority;
        command.Parameters.Add("@actionUrl", SqlDbType.NVarChar, 500).Value = (object?)actionUrl ?? DBNull.Value;
        command.Parameters.Add("@createdBy", SqlDbType.Int).Value = (object?)creatorId ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new NotificationDto(
                Id: reader.GetInt64(0),
                UserId: reader.GetInt32(1),
                Title: reader.GetString(2),
                Message: reader.GetString(3),
                Category: reader.GetString(4),
                Type: reader.GetString(5),
                Priority: reader.GetString(6),
                ActionUrl: reader.IsDBNull(7) ? null : reader.GetString(7),
                IsRead: reader.GetBoolean(8),
                ReadAt: reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                CreatedAt: reader.GetDateTime(10)
            );
        }

        throw new InvalidOperationException("Không thể tạo thông báo.");
    }

    public async Task<int> SeedSampleNotificationsAsync(int userId, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Kiểm tra xem user này đã có thông báo chưa
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM dbo.Notifications WHERE UserId = @userId";
            checkCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct));
            if (count > 0)
            {
                return 0; // Đã có dữ liệu, không ghi đè
            }
        }

        var samples = new[]
        {
            new CreateNotificationRequest(userId, "Đơn xin nghỉ phép đã được duyệt", "Đơn xin nghỉ ngày 15/10/2026 của bạn đã được Mentor duyệt thành công.", "ATTENDANCE", "LEAVE_APPROVED", "SUCCESS", "intern-attendance.html"),
            new CreateNotificationRequest(userId, "Nhắc nhở chấm công", "Đã đến ca làm việc sáng (08:00 - 17:30). Đừng quên điểm danh đúng giờ nhé!", "ATTENDANCE", "CHECKIN_REMINDER", "WARNING", "intern-attendance.html"),
            new CreateNotificationRequest(userId, "Hồ sơ thực tập đã được duyệt", "CV và đơn xin thực tập của bạn đã được phòng Nhân sự phê duyệt đạt yêu cầu.", "DOCUMENT", "DOCUMENT_APPROVED", "SUCCESS", "internship-profile.html"),
            new CreateNotificationRequest(userId, "Phân công người hướng dẫn", "Bạn đã được phân công Mentor hướng dẫn chuyên môn cho kỳ thực tập.", "ASSIGNMENT", "MENTOR_ASSIGNED", "INFO", "mentor-assignment.html"),
            new CreateNotificationRequest(userId, "Thông báo quy chế thực tập", "Ban nhân sự thông báo lịch họp phổ biến quy chế thực tập trực tuyến vào 14:00 thứ Sáu.", "SYSTEM", "SYSTEM_ANNOUNCEMENT", "INFO", "dashboard.html")
        };

        int created = 0;
        foreach (var sample in samples)
        {
            await CreateNotificationAsync(sample, null, ct);
            created++;
        }

        return created;
    }
}
