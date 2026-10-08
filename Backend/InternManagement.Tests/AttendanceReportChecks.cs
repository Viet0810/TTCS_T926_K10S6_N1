using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;

internal static class AttendanceReportChecks
{
    public static async Task RunAsync(HttpClient http, string connectionString, Action<bool, string> check)
    {
        // 1. Kiểm tra từ chối yêu cầu không có token (401 Unauthorized)
        http.DefaultRequestHeaders.Authorization = null;
        using var noAuth = await http.GetAsync("/api/attendance/report");
        check(noAuth.StatusCode == HttpStatusCode.Unauthorized, "attendance report API rejects unauthenticated requests");

        // 2. Đăng nhập vai trò INTERN -> bị từ chối 403 Forbidden
        using var internLogin = await http.PostAsJsonAsync("/api/auth/login", new { username = "permission-intern", password = "Permission-test-9" });
        check(internLogin.StatusCode == HttpStatusCode.OK, "intern can login for attendance permission check");
        using var internSession = JsonDocument.Parse(await internLogin.Content.ReadAsStringAsync());
        var internToken = internSession.RootElement.GetProperty("token").GetString();

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", internToken);
        using var internForbidden = await http.GetAsync("/api/attendance/report");
        check(internForbidden.StatusCode == HttpStatusCode.Forbidden, "attendance report API forbids INTERN role");

        // 3. Đăng nhập vai trò HR -> được phép 200 OK
        using var hrLogin = await http.PostAsJsonAsync("/api/auth/login", new { username = "permission-hr", password = "Permission-test-9" });
        check(hrLogin.StatusCode == HttpStatusCode.OK, "hr can login for attendance report check");
        using var hrSession = JsonDocument.Parse(await hrLogin.Content.ReadAsStringAsync());
        var hrToken = hrSession.RootElement.GetProperty("token").GetString();

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hrToken);
        using var hrAllowed = await http.GetAsync("/api/attendance/report");
        check(hrAllowed.StatusCode == HttpStatusCode.OK, "attendance report API allows HR role");

        // 4. Lấy một intern hợp lệ từ CSDL để tạo ca điểm danh test
        await using var sql = new SqlConnection(connectionString);
        await sql.OpenAsync();
        await using var selectIntern = sql.CreateCommand();
        selectIntern.CommandText = "SELECT TOP(1) Id FROM dbo.Interns ORDER BY Id";
        var internIdObj = await selectIntern.ExecuteScalarAsync();
        int internId = internIdObj is not null ? Convert.ToInt32(internIdObj) : 0;

        if (internId == 0)
        {
            await using var insertIntern = sql.CreateCommand();
            insertIntern.CommandText = """
                INSERT INTO dbo.Interns (FullName, Email, Phone, School, Major, StudentCode, Department)
                OUTPUT INSERTED.Id
                VALUES (N'Trần Văn Điểm Danh', N'attendance.check@example.invalid', N'0911223344', N'ĐH Bách Khoa', N'CNTT', N'SV-ATT01', N'Phát triển phần mềm .NET')
                """;
            internId = Convert.ToInt32(await insertIntern.ExecuteScalarAsync());
        }

        // 5. Ghi nhận ca điểm danh qua API POST /api/attendance/report/record
        var newRecord = new
        {
            internId,
            date = "2026-10-06",
            checkIn = "08:15",
            checkOut = "17:30",
            hours = 8.2m,
            status = "ON_TIME",
            note = "Làm việc tại văn phòng"
        };
        using var createResponse = await http.PostAsJsonAsync("/api/attendance/report/record", newRecord);
        check(createResponse.StatusCode == HttpStatusCode.Created, "attendance record can be created via API");

        // Thêm ca đi muộn và ca nghỉ phép
        var lateRecord = new
        {
            internId,
            date = "2026-10-05",
            checkIn = "08:50",
            checkOut = "17:30",
            hours = 7.7m,
            status = "LATE",
            note = "Đi muộn do kẹt xe",
            approver = "Mentor Test"
        };
        await http.PostAsJsonAsync("/api/attendance/report/record", lateRecord);

        var leaveRecord = new
        {
            internId,
            date = "2026-10-01",
            checkIn = "—",
            checkOut = "—",
            hours = 0.0m,
            status = "LEAVE_APPROVED",
            note = "Nghỉ ốm có phép",
            approver = "HR Test"
        };
        await http.PostAsJsonAsync("/api/attendance/report/record", leaveRecord);

        // 6. Kiểm tra truy vấn báo cáo và thống kê KPI
        using var reportResponse = await http.GetAsync($"/api/attendance/report?internId={internId}");
        check(reportResponse.StatusCode == HttpStatusCode.OK, "report can be queried with intern filter");
        using var reportData = JsonDocument.Parse(await reportResponse.Content.ReadAsStringAsync());
        var stats = reportData.RootElement.GetProperty("stats");
        var records = reportData.RootElement.GetProperty("records");

        check(stats.GetProperty("totalShifts").GetInt32() >= 3, "report aggregates total shifts correctly");
        check(stats.GetProperty("onTimeCount").GetInt32() >= 1, "report counts on-time shifts correctly");
        check(stats.GetProperty("lateOrEarlyCount").GetInt32() >= 1, "report counts late/early shifts correctly");
        check(stats.GetProperty("approvedLeaveCount").GetInt32() >= 1, "report counts approved leaves correctly");
        check(records.GetArrayLength() >= 3, "report returns individual attendance records");

        // 7. Kiểm tra lọc theo khoảng thời gian
        using var dateFiltered = await http.GetAsync($"/api/attendance/report?internId={internId}&startDate=2026-10-05&endDate=2026-10-06");
        using var dateData = JsonDocument.Parse(await dateFiltered.Content.ReadAsStringAsync());
        var dateRecords = dateData.RootElement.GetProperty("records");
        check(dateRecords.GetArrayLength() == 2, "report filters correctly by start and end date range");

        // 8. Kiểm tra lọc theo trạng thái
        using var statusFiltered = await http.GetAsync($"/api/attendance/report?internId={internId}&status=LATE");
        using var statusData = JsonDocument.Parse(await statusFiltered.Content.ReadAsStringAsync());
        var statusRecords = statusData.RootElement.GetProperty("records");
        check(statusRecords.GetArrayLength() == 1
            && statusRecords[0].GetProperty("status").GetString() == "LATE"
            && statusRecords[0].GetProperty("statusText").GetString() == "Đi muộn",
            "report filters correctly by status and provides Vietnamese status text");
    }
}
