using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

internal static class NotificationChecks
{
    public static async Task RunAsync(HttpClient http, Action<bool, string> check)
    {
        // 1. Kiểm tra từ chối yêu cầu không có token (401 Unauthorized)
        http.DefaultRequestHeaders.Authorization = null;
        using var noAuth = await http.GetAsync("/api/notifications");
        check(noAuth.StatusCode == HttpStatusCode.Unauthorized, "notifications API rejects unauthenticated requests");

        using var noAuthCount = await http.GetAsync("/api/notifications/unread-count");
        check(noAuthCount.StatusCode == HttpStatusCode.Unauthorized, "notification unread-count API rejects unauthenticated requests");

        // 2. Đăng nhập tài khoản Intern
        using var internLogin = await http.PostAsJsonAsync("/api/auth/login", new { username = "permission-intern", password = "Permission-test-9" });
        if (internLogin.StatusCode == HttpStatusCode.OK)
        {
            using var internSession = JsonDocument.Parse(await internLogin.Content.ReadAsStringAsync());
            var internToken = internSession.RootElement.GetProperty("token").GetString();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", internToken);

            // 3. Gọi seed notifications mẫu cho intern
            using var seedRes = await http.PostAsync("/api/notifications/seed", null);
            check(seedRes.StatusCode == HttpStatusCode.OK, "notifications seed API succeeds");

            // 4. Lấy số lượng thông báo chưa đọc
            using var countRes = await http.GetAsync("/api/notifications/unread-count");
            check(countRes.StatusCode == HttpStatusCode.OK, "unread-count returns 200 OK");
            using var countDoc = JsonDocument.Parse(await countRes.Content.ReadAsStringAsync());
            int unreadCount = countDoc.RootElement.GetProperty("unreadCount").GetInt32();
            check(unreadCount >= 0, "unreadCount is non-negative number");

            // 5. Lấy danh sách thông báo
            using var listRes = await http.GetAsync("/api/notifications");
            check(listRes.StatusCode == HttpStatusCode.OK, "get notifications returns 200 OK");
            using var listDoc = JsonDocument.Parse(await listRes.Content.ReadAsStringAsync());
            var items = listDoc.RootElement.GetProperty("items");
            check(items.GetArrayLength() > 0, "notifications list contains items");

            var firstItem = items[0];
            long firstId = firstItem.GetProperty("id").GetInt64();
            check(firstItem.TryGetProperty("title", out _), "notification has title property");
            check(firstItem.TryGetProperty("category", out _), "notification has category property");

            // 6. Đánh dấu đã đọc một thông báo
            using var markRes = await http.PostAsync($"/api/notifications/{firstId}/read", null);
            check(markRes.StatusCode == HttpStatusCode.OK, "mark single notification as read returns 200 OK");

            // 7. Đánh dấu tất cả đã đọc
            using var markAllRes = await http.PostAsync("/api/notifications/mark-all-read", null);
            check(markAllRes.StatusCode == HttpStatusCode.OK, "mark all as read returns 200 OK");

            // 8. Kiểm tra lại unread count sau khi mark all read
            using var afterCountRes = await http.GetAsync("/api/notifications/unread-count");
            using var afterCountDoc = JsonDocument.Parse(await afterCountRes.Content.ReadAsStringAsync());
            int afterCount = afterCountDoc.RootElement.GetProperty("unreadCount").GetInt32();
            check(afterCount == 0, "unread count is 0 after mark-all-read");
        }
    }
}
