using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

internal static class LeaveRequestChecks
{
    public static async Task RunAsync(HttpClient http, Action<bool, string> check)
    {
        // 1. Kiểm tra từ chối yêu cầu không có token (401 Unauthorized)
        http.DefaultRequestHeaders.Authorization = null;
        using var noAuthGet = await http.GetAsync("/api/interns/me/leave-requests");
        check(noAuthGet.StatusCode == HttpStatusCode.Unauthorized, "leave requests API rejects unauthenticated GET requests");

        using var noAuthPost = await http.PostAsJsonAsync("/api/interns/me/leave-requests", new
        {
            leaveType = "SICK_LEAVE",
            durationType = "FULL_DAY",
            startDate = "2026-10-20",
            endDate = "2026-10-20",
            reason = "Em bị sốt xuất huyết cần nghỉ điều trị tại nhà."
        });
        check(noAuthPost.StatusCode == HttpStatusCode.Unauthorized, "leave requests API rejects unauthenticated POST requests");

        // 2. Đăng nhập tài khoản Intern
        using var internLogin = await http.PostAsJsonAsync("/api/auth/login", new { username = "permission-intern", password = "Permission-test-9" });
        if (internLogin.StatusCode == HttpStatusCode.OK)
        {
            using var internSession = JsonDocument.Parse(await internLogin.Content.ReadAsStringAsync());
            var internToken = internSession.RootElement.GetProperty("token").GetString();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", internToken);

            // 3. Lấy danh sách ban đầu
            using var listRes = await http.GetAsync("/api/interns/me/leave-requests");
            check(listRes.StatusCode == HttpStatusCode.OK, "get my leave requests returns 200 OK");
            using var listDoc = JsonDocument.Parse(await listRes.Content.ReadAsStringAsync());
            check(listDoc.RootElement.TryGetProperty("pendingCount", out _), "list response has pendingCount");
            check(listDoc.RootElement.TryGetProperty("approvedCount", out _), "list response has approvedCount");
            check(listDoc.RootElement.TryGetProperty("items", out _), "list response has items");
        }
    }
}
