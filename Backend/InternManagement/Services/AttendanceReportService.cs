using System.Data;
using System.Text;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

/// <summary>
/// Chức năng: Báo cáo chuyên cần & nghỉ phép (K10S6N1-91 / K10S6N1-60)
/// Dịch vụ tổng hợp dữ liệu chấm công, nghỉ phép từ cơ sở dữ liệu và tính toán KPI
/// </summary>
public sealed class AttendanceReportService : IAttendanceReportService
{
    private readonly string connectionString;

    public AttendanceReportService(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("Chưa cấu hình connection string InternManagement.");
    }

    public async Task<AttendanceReportResponse> GetReportAsync(AttendanceReportRequest? filter, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        var records = new List<AttendanceRecordResponse>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        var sql = new StringBuilder("""
            SELECT 
                a.Id,
                a.InternId,
                COALESCE(i.StudentCode, N'') AS StudentCode,
                COALESCE(i.FullName, N'Thực tập sinh') AS FullName,
                COALESCE(p.Department, i.Department, i.Major, N'Thực tập') AS Department,
                CONVERT(VARCHAR(10), a.Date, 23) AS [Date],
                COALESCE(a.CheckIn, N'—') AS CheckIn,
                COALESCE(a.CheckOut, N'—') AS CheckOut,
                a.WorkingHours,
                a.Status,
                a.Note,
                a.Approver
            FROM dbo.AttendanceRecords a
            JOIN dbo.Interns i ON a.InternId = i.Id
            LEFT JOIN dbo.InternshipPrograms p ON p.Id = i.InternshipProgramId
            WHERE 1 = 1
            """);

        if (filter?.InternId.HasValue == true)
        {
            sql.Append(" AND a.InternId = @internId");
            command.Parameters.Add("@internId", SqlDbType.Int).Value = filter.InternId.Value;
        }

        if (!string.IsNullOrWhiteSpace(filter?.Search))
        {
            sql.Append(" AND (i.FullName LIKE @search OR i.StudentCode LIKE @search OR i.Department LIKE @search OR i.Major LIKE @search OR p.Department LIKE @search OR p.Name LIKE @search)");
            command.Parameters.Add("@search", SqlDbType.NVarChar, 256).Value = $"%{filter.Search.Trim()}%";
        }

        if (!string.IsNullOrWhiteSpace(filter?.StartDate) && DateTime.TryParse(filter.StartDate, out var start))
        {
            sql.Append(" AND a.Date >= @startDate");
            command.Parameters.Add("@startDate", SqlDbType.Date).Value = start.Date;
        }

        if (!string.IsNullOrWhiteSpace(filter?.EndDate) && DateTime.TryParse(filter.EndDate, out var end))
        {
            sql.Append(" AND a.Date <= @endDate");
            command.Parameters.Add("@endDate", SqlDbType.Date).Value = end.Date;
        }

        if (!string.IsNullOrWhiteSpace(filter?.Status) && !filter.Status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            sql.Append(" AND a.Status = @status");
            command.Parameters.Add("@status", SqlDbType.VarChar, 30).Value = filter.Status.Trim();
        }

        sql.Append(" ORDER BY a.Date DESC, a.Id DESC");
        command.CommandText = sql.ToString();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = reader.GetString(9);
            records.Add(new AttendanceRecordResponse
            {
                Id = reader.GetInt32(0),
                InternId = reader.GetInt32(1),
                Mssv = reader.GetString(2),
                Name = reader.GetString(3),
                Department = reader.GetString(4),
                Date = reader.GetString(5),
                CheckIn = reader.IsDBNull(6) ? "—" : reader.GetString(6),
                CheckOut = reader.IsDBNull(7) ? "—" : reader.GetString(7),
                Hours = reader.GetDecimal(8),
                Status = status,
                StatusText = GetStatusText(status),
                Note = reader.IsDBNull(10) ? null : reader.GetString(10),
                Approver = reader.IsDBNull(11) ? null : reader.GetString(11),
            });
        }

        // Tính toán các chỉ số thống kê KPI chuyên cần
        var totalShifts = records.Count;
        var onTimeCount = records.Count(r => r.Status == "ON_TIME");
        var lateCount = records.Count(r => r.Status == "LATE");
        var earlyCount = records.Count(r => r.Status == "EARLY");
        var approvedLeaveCount = records.Count(r => r.Status == "LEAVE_APPROVED");
        var absentCount = records.Count(r => r.Status == "ABSENT");

        var workingDays = totalShifts - approvedLeaveCount;
        var attendanceRate = workingDays > 0
            ? $"{(onTimeCount * 100.0m / workingDays):F1}%"
            : "100.0%";

        return new AttendanceReportResponse
        {
            Stats = new AttendanceStatsResponse
            {
                TotalShifts = totalShifts,
                OnTimeCount = onTimeCount,
                LateOrEarlyCount = lateCount + earlyCount,
                ApprovedLeaveCount = approvedLeaveCount,
                AbsentCount = absentCount,
                AttendanceRate = attendanceRate,
            },
            Records = records,
        };
    }

    public async Task<int> RecordAttendanceAsync(CreateAttendanceRecordRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = "SELECT COUNT(*) FROM dbo.Interns WHERE Id=@internId";
        command.Parameters.Add("@internId", SqlDbType.Int).Value = request.InternId;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 0)
            throw new ArgumentException("Không tìm thấy thực tập sinh.");
        command.CommandText = """
            SELECT COUNT(*) FROM dbo.AttendanceRecords WITH (UPDLOCK,HOLDLOCK)
            WHERE InternId=@internId AND Date=@date
                AND ISNULL(CheckIn,N'')=ISNULL(@checkIn,N'') AND ISNULL(CheckOut,N'')=ISNULL(@checkOut,N'')
            """;
        command.Parameters.Add("@date", SqlDbType.Date).Value = DateTime.Parse(request.Date).Date;
        command.Parameters.Add("@checkIn", SqlDbType.NVarChar, 10).Value = (object?)request.CheckIn ?? DBNull.Value;
        command.Parameters.Add("@checkOut", SqlDbType.NVarChar, 10).Value = (object?)request.CheckOut ?? DBNull.Value;
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0)
            throw new ArgumentException("Ca điểm danh này đã tồn tại.");

        command.CommandText = """
            INSERT INTO dbo.AttendanceRecords (InternId, Date, CheckIn, CheckOut, WorkingHours, Status, Note, Approver)
            OUTPUT INSERTED.Id
            VALUES (@internId, @date, @checkIn, @checkOut, @hours, @status, @note, @approver)
            """;

        command.Parameters.Add("@hours", SqlDbType.Decimal).Value = request.Hours;
        command.Parameters.Add("@status", SqlDbType.VarChar, 30).Value = request.Status;
        command.Parameters.Add("@note", SqlDbType.NVarChar, 500).Value = (object?)request.Note ?? DBNull.Value;
        command.Parameters.Add("@approver", SqlDbType.NVarChar, 200).Value = (object?)request.Approver ?? DBNull.Value;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<int> SeedSampleDataAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Lấy danh sách ID các sinh viên hiện có trong bảng dbo.Interns
        var internIds = new List<int>();
        await using (var getInterns = connection.CreateCommand())
        {
            getInterns.CommandText = "SELECT Id FROM dbo.Interns ORDER BY Id";
            await using var reader = await getInterns.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                internIds.Add(reader.GetInt32(0));
            }
        }

        if (internIds.Count == 0)
            return 0;

        // Kiểm tra xem đã có bản ghi nào chưa
        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM dbo.AttendanceRecords";
            var count = (int)(await checkCmd.ExecuteScalarAsync(cancellationToken) ?? 0);
            if (count > 0) return 0; // Đã có dữ liệu
        }

        var sampleRecords = new (int InternIndex, string Date, string? In, string? Out, decimal Hours, string Status, string? Note, string? Approver)[]
        {
            (0, "2026-10-05", "08:24", "17:31", 8.0m, "ON_TIME", "Làm việc tại văn phòng", null),
            (1, "2026-10-05", "08:52", "17:35", 7.7m, "LATE", "Kẹt xe tuyến đường Cầu Giấy", "Nguyễn Vũ Long (Mentor)"),
            (2, "2026-10-05", "08:20", "16:15", 6.9m, "EARLY", "Xin phép về sớm 1h làm thủ tục đồ án", "Trần Văn Minh (Mentor)"),
            (3, "2026-10-05", null, null, 0.0m, "LEAVE_APPROVED", "Nghỉ ốm (Đã gửi giấy chứng nhận y tế)", "Hoàng Lan Anh (HR)"),
            (0, "2026-10-02", "08:25", "17:30", 8.0m, "ON_TIME", "Làm việc tại văn phòng", null),
            (1, "2026-10-02", "08:20", "17:35", 8.2m, "ON_TIME", "Kiểm thử hồi quy Sprint 1", null),
            (2, "2026-10-02", null, null, 0.0m, "ABSENT", "Không có mặt, chưa nộp đơn xin phép", null),
            (3, "2026-10-02", "08:29", "17:30", 8.0m, "ON_TIME", "Làm việc tại văn phòng", null),
            (0, "2026-10-01", "08:25", "17:30", 8.0m, "ON_TIME", "Làm việc tại văn phòng", null),
            (1, "2026-10-01", "08:27", "17:30", 8.0m, "ON_TIME", "Làm việc tại văn phòng", null),
        };

        var inserted = 0;
        foreach (var r in sampleRecords)
        {
            var internId = internIds[r.InternIndex % internIds.Count];
            await using var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = """
                INSERT INTO dbo.AttendanceRecords (InternId, Date, CheckIn, CheckOut, WorkingHours, Status, Note, Approver)
                VALUES (@internId, @date, @checkIn, @checkOut, @hours, @status, @note, @approver)
                """;
            insertCmd.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            insertCmd.Parameters.Add("@date", SqlDbType.Date).Value = DateTime.Parse(r.Date).Date;
            insertCmd.Parameters.Add("@checkIn", SqlDbType.NVarChar, 10).Value = (object?)r.In ?? DBNull.Value;
            insertCmd.Parameters.Add("@checkOut", SqlDbType.NVarChar, 10).Value = (object?)r.Out ?? DBNull.Value;
            insertCmd.Parameters.Add("@hours", SqlDbType.Decimal).Value = r.Hours;
            insertCmd.Parameters.Add("@status", SqlDbType.VarChar, 30).Value = r.Status;
            insertCmd.Parameters.Add("@note", SqlDbType.NVarChar, 500).Value = (object?)r.Note ?? DBNull.Value;
            insertCmd.Parameters.Add("@approver", SqlDbType.NVarChar, 200).Value = (object?)r.Approver ?? DBNull.Value;
            await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            inserted++;
        }

        return inserted;
    }

    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.AttendanceRecords', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AttendanceRecords (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AttendanceRecords PRIMARY KEY,
                    InternId INT NOT NULL CONSTRAINT FK_AttendanceRecords_Interns REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    Date DATE NOT NULL,
                    CheckIn NVARCHAR(10) NULL,
                    CheckOut NVARCHAR(10) NULL,
                    WorkingHours DECIMAL(4,1) NOT NULL DEFAULT 0.0,
                    Status VARCHAR(30) NOT NULL CONSTRAINT CK_Attendance_Status CHECK (Status IN ('ON_TIME', 'LATE', 'EARLY', 'LEAVE_APPROVED', 'ABSENT')),
                    Note NVARCHAR(500) NULL,
                    Approver NVARCHAR(200) NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Attendance_CreatedAt DEFAULT SYSDATETIMEOFFSET()
                );
                CREATE INDEX IX_AttendanceRecords_InternId_Date ON dbo.AttendanceRecords(InternId, Date);
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string GetStatusText(string status) => status switch
    {
        "ON_TIME" => "Đúng giờ",
        "LATE" => "Đi muộn",
        "EARLY" => "Về sớm",
        "LEAVE_APPROVED" => "Nghỉ có phép",
        "ABSENT" => "Nghỉ không phép",
        _ => status
    };
}
