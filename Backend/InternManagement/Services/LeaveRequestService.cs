using System.Data;
using System.Globalization;
using System.Text;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class LeaveRequestService : ILeaveRequestService
{
    private readonly string connectionString;
    private readonly INotificationService? notificationService;
    private static volatile bool tableEnsured;

    public LeaveRequestService(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
        notificationService = serviceProvider.GetService(typeof(INotificationService)) as INotificationService;
    }

    private async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        if (tableEnsured) return;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await Infrastructure.LeaveRequestMigration.ApplyAsync(connection, ct);
        tableEnsured = true;
    }

    private async Task<int> GetInternIdAsync(int userId, SqlConnection connection, SqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        if (transaction != null) command.Transaction = transaction;

        command.CommandText = "SELECT i.Id FROM dbo.Interns i JOIN dbo.Users u ON LOWER(RTRIM(LTRIM(i.Email))) = LOWER(RTRIM(LTRIM(u.Email))) WHERE u.Id = @userId AND u.Role = 'INTERN'";
        command.Parameters.Add("@userId", SqlDbType.Int).Value = userId;

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            var internId = reader.GetInt32(0);
            if (!await reader.ReadAsync(ct)) return internId;
        }

        // Missing or ambiguous mapping must never fall back to unrelated identity IDs.
        throw new UnauthorizedAccessException("T\u00e0i kho\u1ea3n kh\u00f4ng c\u00f3 li\u00ean k\u1ebft h\u1ed3 s\u01a1 th\u1ef1c t\u1eadp sinh h\u1ee3p l\u1ec7.");
    }

    public async Task<LeaveRequestResponseDto> CreateLeaveRequestAsync(int userId, CreateLeaveRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureTableExistsAsync(ct);

        // 1. Chuẩn hóa & kiểm tra loại nghỉ
        var leaveType = (dto.LeaveType ?? string.Empty).Trim().ToUpperInvariant();
        if (leaveType is not ("SICK_LEAVE" or "ACADEMIC_LEAVE" or "PERSONAL_LEAVE" or "OTHER"))
        {
            throw new ArgumentException("Loại nghỉ phép không hợp lệ. Vui lòng chọn một trong các loại: SICK_LEAVE, ACADEMIC_LEAVE, PERSONAL_LEAVE, OTHER.");
        }

        // 2. Chuẩn hóa & kiểm tra hình thức thời gian
        var durationType = (dto.DurationType ?? string.Empty).Trim().ToUpperInvariant();
        if (durationType is not ("FULL_DAY" or "MORNING" or "AFTERNOON" or "MULTI_DAY"))
        {
            throw new ArgumentException("Hình thức thời gian không hợp lệ. Vui lòng chọn: FULL_DAY, MORNING, AFTERNOON, hoặc MULTI_DAY.");
        }

        // 3. Kiểm tra ngày bắt đầu và kết thúc
        var startRaw = (dto.StartDate ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(startRaw) || !DateOnly.TryParseExact(startRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate))
        {
            throw new ArgumentException("Ngày bắt đầu không hợp lệ (yêu cầu định dạng yyyy-MM-dd).");
        }

        DateOnly endDate;
        decimal totalDays;

        if (durationType is "FULL_DAY" or "MORNING" or "AFTERNOON")
        {
            endDate = startDate;
            totalDays = durationType == "FULL_DAY" ? 1.0m : 0.5m;
        }
        else
        {
            var endRaw = (dto.EndDate ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(endRaw) || !DateOnly.TryParseExact(endRaw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate))
            {
                throw new ArgumentException("Ngày kết thúc không hợp lệ (yêu cầu định dạng yyyy-MM-dd).");
            }

            if (endDate < startDate)
            {
                throw new ArgumentException("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            totalDays = (endDate.DayNumber - startDate.DayNumber) + 1;
            if (totalDays > 30)
            {
                throw new ArgumentException("Số ngày xin nghỉ tối đa cho một đơn là 30 ngày.");
            }
        }

        // 4. Kiểm tra lý do
        var reason = (dto.Reason ?? string.Empty).Trim();
        if (reason.Length < 10)
        {
            throw new ArgumentException("Lý do xin nghỉ bắt buộc phải có ít nhất 10 ký tự.");
        }
        if (reason.Length > 1000)
        {
            throw new ArgumentException("Lý do xin nghỉ không được vượt quá 1000 ký tự.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Khởi tạo Transaction nhằm đảm bảo tính toàn vẹn và chống race condition khi nộp đơn đồng thời
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        var internId = await GetInternIdAsync(userId, connection, transaction, ct);

        // Khóa bi quan bản ghi intern để chặn nhiều đơn nộp cùng một lúc
        await using (var lockCmd = connection.CreateCommand())
        {
            lockCmd.Transaction = transaction;
            lockCmd.CommandText = "SELECT Id FROM dbo.Interns WITH(UPDLOCK, HOLDLOCK) WHERE Id = @internId";
            lockCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            await lockCmd.ExecuteScalarAsync(ct);
        }

        // 5. Kiểm tra kỳ thực tập (nếu sinh viên đã có kỳ thực tập gán vào)
        int? mentorUserId = null;
        await using (var programCheckCmd = connection.CreateCommand())
        {
            programCheckCmd.Transaction = transaction;
            programCheckCmd.CommandText = """
                SELECT p.StartDate, p.EndDate, i.MentorUserId 
                FROM dbo.Interns i
                LEFT JOIN dbo.InternshipPrograms p ON i.InternshipProgramId = p.Id
                WHERE i.Id = @internId
                """;
            programCheckCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            await using var progReader = await programCheckCmd.ExecuteReaderAsync(ct);
            if (await progReader.ReadAsync(ct))
            {
                if (!progReader.IsDBNull(0) && !progReader.IsDBNull(1))
                {
                    var pStart = DateOnly.FromDateTime(progReader.GetDateTime(0));
                    var pEnd = DateOnly.FromDateTime(progReader.GetDateTime(1));
                    if (startDate < pStart || endDate > pEnd)
                    {
                        throw new ArgumentException($"Thời gian nghỉ phải nằm trong kỳ thực tập ({pStart:dd/MM/yyyy} đến {pEnd:dd/MM/yyyy}).");
                    }
                }
                if (!progReader.IsDBNull(2))
                {
                    mentorUserId = progReader.GetInt32(2);
                }
            }
        }

        // 6. Kiểm tra trùng lặp ngày nghỉ với đơn đang PENDING hoặc APPROVED
        await using (var overlapCmd = connection.CreateCommand())
        {
            overlapCmd.Transaction = transaction;
            overlapCmd.CommandText = """
                SELECT COUNT(*) FROM dbo.LeaveRequests WITH(UPDLOCK, HOLDLOCK)
                WHERE InternId = @internId
                  AND Status IN ('PENDING', 'APPROVED')
                  AND StartDate <= @endDate
                  AND EndDate >= @startDate
                """;
            overlapCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            overlapCmd.Parameters.Add("@startDate", SqlDbType.Date).Value = startDate.ToDateTime(TimeOnly.MinValue);
            overlapCmd.Parameters.Add("@endDate", SqlDbType.Date).Value = endDate.ToDateTime(TimeOnly.MinValue);

            var count = Convert.ToInt32(await overlapCmd.ExecuteScalarAsync(ct));
            if (count > 0)
            {
                throw new ArgumentException("Bạn đã có đơn xin nghỉ phép trong khoảng thời gian này đang chờ duyệt hoặc đã được duyệt.");
            }
        }

        // 7. Thêm đơn xin nghỉ mới
        long createdId;
        DateTime createdAt;
        DateTime updatedAt;

        await using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.Transaction = transaction;
            insertCmd.CommandText = """
                INSERT INTO dbo.LeaveRequests (InternId, UserId, LeaveType, DurationType, StartDate, EndDate, TotalDays, Reason, Status, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id, INSERTED.CreatedAt, INSERTED.UpdatedAt
                VALUES (@internId, @userId, @leaveType, @durationType, @startDate, @endDate, @totalDays, @reason, 'PENDING', SYSUTCDATETIME(), SYSUTCDATETIME());
                """;

            insertCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            insertCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            insertCmd.Parameters.Add("@leaveType", SqlDbType.VarChar, 30).Value = leaveType;
            insertCmd.Parameters.Add("@durationType", SqlDbType.VarChar, 20).Value = durationType;
            insertCmd.Parameters.Add("@startDate", SqlDbType.Date).Value = startDate.ToDateTime(TimeOnly.MinValue);
            insertCmd.Parameters.Add("@endDate", SqlDbType.Date).Value = endDate.ToDateTime(TimeOnly.MinValue);

            // Chỉ định rõ Precision và Scale cho TotalDays để tránh truncation
            var totalDaysParam = insertCmd.Parameters.Add("@totalDays", SqlDbType.Decimal);
            totalDaysParam.Precision = 3;
            totalDaysParam.Scale = 1;
            totalDaysParam.Value = totalDays;

            insertCmd.Parameters.Add("@reason", SqlDbType.NVarChar, 1000).Value = reason;

            await using var reader = await insertCmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                throw new InvalidOperationException("Không thể tạo đơn xin nghỉ phép.");
            }

            createdId = reader.GetInt64(0);
            createdAt = reader.GetDateTime(1);
            updatedAt = reader.GetDateTime(2);
        }

        await transaction.CommitAsync(ct);

        // 8. Bắn thông báo nội bộ sau khi transaction đã commit thành công
        if (notificationService != null)
        {
            try
            {
                await notificationService.CreateNotificationAsync(new CreateNotificationRequest(
                    UserId: userId,
                    Title: "Đã gửi đơn xin nghỉ phép",
                    Message: $"Đơn xin nghỉ phép ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}) đã được gửi thành công và đang chờ xét duyệt.",
                    Category: "ATTENDANCE",
                    Type: "LEAVE_SUBMITTED",
                    Priority: "INFO",
                    ActionUrl: "intern-leave-request.html"
                ), userId, ct);

                if (mentorUserId.HasValue && mentorUserId.Value > 0)
                {
                    await notificationService.CreateNotificationAsync(new CreateNotificationRequest(
                        UserId: mentorUserId.Value,
                        Title: "Đơn xin nghỉ phép mới cần duyệt",
                        Message: $"Thực tập sinh đã gửi đơn xin nghỉ ({startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}). Vui lòng xem xét.",
                        Category: "ATTENDANCE",
                        Type: "LEAVE_PENDING_REVIEW",
                        Priority: "WARNING",
                        ActionUrl: "mentor-leave-requests.html"
                    ), userId, ct);
                }
            }
            catch
            {
                // Bắn thông báo thất bại không được làm hỏng thao tác tạo đơn
            }
        }

        return new LeaveRequestResponseDto(
            Id: createdId,
            InternId: internId,
            UserId: userId,
            LeaveType: leaveType,
            LeaveTypeLabel: GetLeaveTypeLabel(leaveType),
            DurationType: durationType,
            DurationTypeLabel: GetDurationTypeLabel(durationType),
            StartDate: startDate.ToString("yyyy-MM-dd"),
            EndDate: endDate.ToString("yyyy-MM-dd"),
            TotalDays: totalDays,
            Reason: reason,
            Status: "PENDING",
            StatusLabel: "Chờ duyệt",
            ReviewerId: null,
            ReviewerName: null,
            ReviewerNote: null,
            ReviewedAt: null,
            CreatedAt: createdAt,
            UpdatedAt: updatedAt
        );
    }

    public async Task<LeaveRequestListResponse> GetMyLeaveRequestsAsync(int userId, string? status = null, int page = 1, int pageSize = 15, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 15;
        if (pageSize > 100) pageSize = 100;
        int offset = (page - 1) * pageSize;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var internId = await GetInternIdAsync(userId, connection, null, ct);

        // 1. Thống kê số lượng theo từng trạng thái
        int pendingCount = 0;
        int approvedCount = 0;
        int rejectedCount = 0;
        int cancelledCount = 0;

        await using (var statCmd = connection.CreateCommand())
        {
            statCmd.CommandText = """
                SELECT Status, COUNT(*) 
                FROM dbo.LeaveRequests 
                WHERE InternId = @internId AND UserId = @userId
                GROUP BY Status
                """;
            statCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            statCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            await using var statReader = await statCmd.ExecuteReaderAsync(ct);
            while (await statReader.ReadAsync(ct))
            {
                var s = statReader.GetString(0);
                var c = statReader.GetInt32(1);
                switch (s)
                {
                    case "PENDING": pendingCount = c; break;
                    case "APPROVED": approvedCount = c; break;
                    case "REJECTED": rejectedCount = c; break;
                    case "CANCELLED": cancelledCount = c; break;
                }
            }
        }

        // 2. Chuẩn hóa bộ lọc trạng thái (chỉ chấp nhận các giá trị hợp lệ)
        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var raw = status.Trim().ToUpperInvariant();
            if (raw is "PENDING" or "APPROVED" or "REJECTED" or "CANCELLED")
            {
                normalizedStatus = raw;
            }
        }

        var countBuilder = new StringBuilder("SELECT COUNT(*) FROM dbo.LeaveRequests WHERE InternId = @internId AND UserId = @userId");
        if (normalizedStatus != null)
        {
            countBuilder.Append(" AND Status = @status");
        }

        int total = 0;
        await using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = countBuilder.ToString();
            countCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            countCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            if (normalizedStatus != null)
            {
                countCmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = normalizedStatus;
            }
            var res = await countCmd.ExecuteScalarAsync(ct);
            total = res != null ? Convert.ToInt32(res) : 0;
        }

        // 3. Lấy danh sách bản ghi phân trang
        var queryBuilder = new StringBuilder("""
            SELECT 
                lr.Id,
                lr.InternId,
                lr.UserId,
                lr.LeaveType,
                lr.DurationType,
                CONVERT(VARCHAR(10), lr.StartDate, 23) AS StartDate,
                CONVERT(VARCHAR(10), lr.EndDate, 23) AS EndDate,
                lr.TotalDays,
                lr.Reason,
                lr.Status,
                lr.ReviewerId,
                u.FullName AS ReviewerName,
                lr.ReviewerNote,
                lr.ReviewedAt,
                lr.CreatedAt,
                lr.UpdatedAt
            FROM dbo.LeaveRequests lr
            LEFT JOIN dbo.Users u ON lr.ReviewerId = u.Id
            WHERE lr.InternId = @internId AND lr.UserId = @userId
            """);

        if (normalizedStatus != null)
        {
            queryBuilder.Append(" AND lr.Status = @status");
        }

        queryBuilder.Append(" ORDER BY lr.CreatedAt DESC, lr.Id DESC OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY");

        var items = new List<LeaveRequestResponseDto>();
        await using (var queryCmd = connection.CreateCommand())
        {
            queryCmd.CommandText = queryBuilder.ToString();
            queryCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            queryCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
            queryCmd.Parameters.Add("@offset", SqlDbType.Int).Value = offset;
            queryCmd.Parameters.Add("@pageSize", SqlDbType.Int).Value = pageSize;
            if (normalizedStatus != null)
            {
                queryCmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = normalizedStatus;
            }

            await using var reader = await queryCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var lType = reader.GetString(3);
                var dType = reader.GetString(4);
                var st = reader.GetString(9);

                items.Add(new LeaveRequestResponseDto(
                    Id: reader.GetInt64(0),
                    InternId: reader.GetInt32(1),
                    UserId: reader.GetInt32(2),
                    LeaveType: lType,
                    LeaveTypeLabel: GetLeaveTypeLabel(lType),
                    DurationType: dType,
                    DurationTypeLabel: GetDurationTypeLabel(dType),
                    StartDate: reader.GetString(5),
                    EndDate: reader.GetString(6),
                    TotalDays: reader.GetDecimal(7),
                    Reason: reader.GetString(8),
                    Status: st,
                    StatusLabel: GetStatusLabel(st),
                    ReviewerId: reader.IsDBNull(10) ? null : reader.GetInt32(10),
                    ReviewerName: reader.IsDBNull(11) ? null : reader.GetString(11),
                    ReviewerNote: reader.IsDBNull(12) ? null : reader.GetString(12),
                    ReviewedAt: reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                    CreatedAt: reader.GetDateTime(14),
                    UpdatedAt: reader.GetDateTime(15)
                ));
            }
        }

        return new LeaveRequestListResponse(
            Total: total,
            PendingCount: pendingCount,
            ApprovedCount: approvedCount,
            RejectedCount: rejectedCount,
            CancelledCount: cancelledCount,
            Page: page,
            PageSize: pageSize,
            Items: items
        );
    }

    public async Task<CancelLeaveRequestResponse> CancelLeaveRequestAsync(int userId, long requestId, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var internId = await GetInternIdAsync(userId, connection, null, ct);

        // Cập nhật nguyên tử (Atomic Update): Chỉ hủy khi đơn đúng của user/intern và đang ở trạng thái PENDING
        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = """
            UPDATE dbo.LeaveRequests 
            SET Status = 'CANCELLED', UpdatedAt = SYSUTCDATETIME()
            OUTPUT INSERTED.Id, INSERTED.Status
            WHERE Id = @id AND UserId = @userId AND InternId = @internId AND Status = 'PENDING';
            """;
        updateCmd.Parameters.Add("@id", SqlDbType.BigInt).Value = requestId;
        updateCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        updateCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;

        await using var updateReader = await updateCmd.ExecuteReaderAsync(ct);
        if (await updateReader.ReadAsync(ct))
        {
            return new CancelLeaveRequestResponse(true, requestId, "CANCELLED", "Đã hủy đơn xin nghỉ phép thành công.");
        }
        await updateReader.CloseAsync();

        // Nếu không có bản ghi nào được update, kiểm tra nguyên nhân để trả về thông báo chính xác
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT Status FROM dbo.LeaveRequests WHERE Id = @id AND UserId = @userId AND InternId = @internId";
        checkCmd.Parameters.Add("@id", SqlDbType.BigInt).Value = requestId;
        checkCmd.Parameters.Add("@userId", SqlDbType.Int).Value = userId;
        checkCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;

        var statusRes = await checkCmd.ExecuteScalarAsync(ct);
        if (statusRes == null)
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM dbo.LeaveRequests WHERE Id = @id";
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct)) > 0)
                throw new UnauthorizedAccessException("B\u1ea1n kh\u00f4ng c\u00f3 quy\u1ec1n h\u1ee7y \u0111\u01a1n ngh\u1ec9 ph\u00e9p n\u00e0y.");
            return new CancelLeaveRequestResponse(false, requestId, "NOT_FOUND", "Không tìm thấy đơn xin nghỉ phép.");
        }

        var curStatus = statusRes.ToString()!;
        return new CancelLeaveRequestResponse(
            false,
            requestId,
            curStatus,
            $"Chỉ có thể hủy đơn xin nghỉ phép khi đơn đang ở trạng thái Chờ duyệt (đơn hiện tại là {GetStatusLabel(curStatus)})."
        );
    }

    public async Task<IReadOnlyList<MentorLeaveRequestResponseDto>> GetMentorLeaveRequestsAsync(
        int mentorUserId, string? status = null, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);
        var normalizedStatus = string.IsNullOrWhiteSpace(status) || status.Equals("ALL", StringComparison.OrdinalIgnoreCase)
            ? null : status.Trim().ToUpperInvariant();
        if (normalizedStatus is not null && normalizedStatus is not ("PENDING" or "APPROVED" or "REJECTED" or "CANCELLED"))
            throw new ArgumentException("Trạng thái đơn nghỉ không hợp lệ.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT lr.Id,lr.InternId,i.FullName,i.Email,lr.LeaveType,lr.DurationType,
                   CONVERT(VARCHAR(10),lr.StartDate,23),CONVERT(VARCHAR(10),lr.EndDate,23),
                   lr.TotalDays,lr.Reason,lr.Status,lr.ReviewerNote,lr.ReviewedAt,lr.CreatedAt
            FROM dbo.LeaveRequests lr
            JOIN dbo.Interns i ON i.Id=lr.InternId
            WHERE i.MentorUserId=@mentorUserId
              AND (@status IS NULL OR lr.Status=@status)
            ORDER BY CASE WHEN lr.Status='PENDING' THEN 0 ELSE 1 END,lr.CreatedAt DESC,lr.Id DESC
            """;
        command.Parameters.Add("@mentorUserId", SqlDbType.Int).Value = mentorUserId;
        command.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = (object?)normalizedStatus ?? DBNull.Value;
        var items = new List<MentorLeaveRequestResponseDto>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) items.Add(MapMentorLeaveRequest(reader));
        return items;
    }

    public async Task<MentorLeaveRequestResponseDto> GetMentorLeaveRequestAsync(
        int mentorUserId, long requestId, CancellationToken ct = default)
    {
        await EnsureTableExistsAsync(ct);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT lr.Id,lr.InternId,i.FullName,i.Email,lr.LeaveType,lr.DurationType,
                   CONVERT(VARCHAR(10),lr.StartDate,23),CONVERT(VARCHAR(10),lr.EndDate,23),
                   lr.TotalDays,lr.Reason,lr.Status,lr.ReviewerNote,lr.ReviewedAt,lr.CreatedAt
            FROM dbo.LeaveRequests lr
            JOIN dbo.Interns i ON i.Id=lr.InternId
            WHERE lr.Id=@id AND i.MentorUserId=@mentorUserId
            """;
        command.Parameters.Add("@id", SqlDbType.BigInt).Value = requestId;
        command.Parameters.Add("@mentorUserId", SqlDbType.Int).Value = mentorUserId;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct)) return MapMentorLeaveRequest(reader);
        await reader.CloseAsync();
        command.CommandText = "SELECT COUNT(*) FROM dbo.LeaveRequests WHERE Id=@id";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0)
            throw new UnauthorizedAccessException("Bạn không được phân công hướng dẫn thực tập sinh của đơn nghỉ này.");
        throw new KeyNotFoundException("Không tìm thấy đơn xin nghỉ phép.");
    }

    public async Task<ReviewLeaveRequestResponse> ReviewLeaveRequestAsync(
        int mentorUserId, long requestId, ReviewLeaveRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureTableExistsAsync(ct);
        var decision = (dto.Decision ?? string.Empty).Trim().ToUpperInvariant();
        if (decision is not ("APPROVED" or "REJECTED"))
            throw new ArgumentException("Quyết định phải là APPROVED hoặc REJECTED.");
        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (note?.Length > 1000) throw new ArgumentException("Ghi chú không được vượt quá 1000 ký tự.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        int internUserId;
        string currentStatus;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = """
                SELECT lr.UserId,lr.Status
                FROM dbo.LeaveRequests lr WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.Interns i ON i.Id=lr.InternId
                WHERE lr.Id=@id AND i.MentorUserId=@mentorUserId
                """;
            select.Parameters.Add("@id", SqlDbType.BigInt).Value = requestId;
            select.Parameters.Add("@mentorUserId", SqlDbType.Int).Value = mentorUserId;
            await using var reader = await select.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                await reader.CloseAsync();
                select.CommandText = "SELECT COUNT(*) FROM dbo.LeaveRequests WHERE Id=@id";
                if (Convert.ToInt32(await select.ExecuteScalarAsync(ct)) > 0)
                    throw new UnauthorizedAccessException("Bạn không được phân công hướng dẫn thực tập sinh của đơn nghỉ này.");
                throw new KeyNotFoundException("Không tìm thấy đơn xin nghỉ phép.");
            }
            internUserId = reader.GetInt32(0);
            currentStatus = reader.GetString(1);
        }
        if (currentStatus != "PENDING")
            throw new InvalidOperationException($"Đơn đã được xử lý với trạng thái {GetStatusLabel(currentStatus)}.");

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE dbo.LeaveRequests
                SET Status=@status,ReviewerId=@mentorUserId,ReviewerNote=@note,
                    ReviewedAt=SYSUTCDATETIME(),UpdatedAt=SYSUTCDATETIME()
                WHERE Id=@id AND Status='PENDING'
                """;
            update.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = decision;
            update.Parameters.Add("@mentorUserId", SqlDbType.Int).Value = mentorUserId;
            update.Parameters.Add("@note", SqlDbType.NVarChar, 1000).Value = (object?)note ?? DBNull.Value;
            update.Parameters.Add("@id", SqlDbType.BigInt).Value = requestId;
            if (await update.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Đơn nghỉ đã được xử lý.");
        }
        await transaction.CommitAsync(ct);

        if (notificationService is not null)
        {
            try
            {
                await notificationService.CreateNotificationAsync(new CreateNotificationRequest(
                    internUserId,
                    decision == "APPROVED" ? "Đơn nghỉ đã được duyệt" : "Đơn nghỉ đã bị từ chối",
                    decision == "APPROVED"
                        ? "Mentor đã duyệt đơn xin nghỉ phép của bạn."
                        : $"Mentor đã từ chối đơn xin nghỉ phép của bạn.{(note is null ? string.Empty : " Lý do: " + note)}",
                    "ATTENDANCE",
                    decision == "APPROVED" ? "LEAVE_APPROVED" : "LEAVE_REJECTED",
                    decision == "APPROVED" ? "SUCCESS" : "DANGER",
                    "intern-leave-request.html"), mentorUserId, ct);
            }
            catch
            {
                // Kết quả duyệt đã commit; lỗi notification không được rollback nghiệp vụ.
            }
        }
        return new ReviewLeaveRequestResponse(requestId, decision, GetStatusLabel(decision),
            decision == "APPROVED" ? "Đã duyệt đơn xin nghỉ phép." : "Đã từ chối đơn xin nghỉ phép.");
    }

    private static MentorLeaveRequestResponseDto MapMentorLeaveRequest(SqlDataReader reader)
    {
        var leaveType = reader.GetString(4);
        var durationType = reader.GetString(5);
        var status = reader.GetString(10);
        return new MentorLeaveRequestResponseDto(
            reader.GetInt64(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
            leaveType, GetLeaveTypeLabel(leaveType), durationType, GetDurationTypeLabel(durationType),
            reader.GetString(6), reader.GetString(7), reader.GetDecimal(8), reader.GetString(9),
            status, GetStatusLabel(status), reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetDateTime(12), reader.GetDateTime(13));
    }

    private static string GetLeaveTypeLabel(string type) => type switch
    {
        "SICK_LEAVE" => "Nghỉ ốm / Khám bệnh",
        "ACADEMIC_LEAVE" => "Nghỉ học / Thi cử",
        "PERSONAL_LEAVE" => "Việc riêng gia đình",
        "OTHER" => "Lý do khác",
        _ => type
    };

    private static string GetDurationTypeLabel(string duration) => duration switch
    {
        "FULL_DAY" => "Cả ngày (1.0 ngày)",
        "MORNING" => "Ca sáng (0.5 ngày)",
        "AFTERNOON" => "Ca chiều (0.5 ngày)",
        "MULTI_DAY" => "Nhiều ngày",
        _ => duration
    };

    private static string GetStatusLabel(string status) => status switch
    {
        "PENDING" => "Chờ duyệt",
        "APPROVED" => "Đã duyệt",
        "REJECTED" => "Từ chối",
        "CANCELLED" => "Đã hủy",
        _ => status
    };
}
