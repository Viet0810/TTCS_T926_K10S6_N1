"""Check Notifications UI and unread count functionality (K10S6N1-157)."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess

root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
if not chrome.exists():
    raise SystemExit('Chrome is required to run this check.')

mock_notifications = {
    "total": 3,
    "unreadCount": 2,
    "page": 1,
    "pageSize": 15,
    "items": [
        {
            "id": 1,
            "userId": 1,
            "title": "Đơn xin nghỉ phép đã được duyệt",
            "message": "Đơn xin nghỉ ngày 15/10/2026 của bạn đã được Mentor phê duyệt.",
            "category": "ATTENDANCE",
            "type": "LEAVE_APPROVED",
            "priority": "SUCCESS",
            "actionUrl": "intern-attendance.html",
            "isRead": False,
            "readAt": None,
            "createdAt": "2026-10-14T08:00:00Z"
        },
        {
            "id": 2,
            "userId": 1,
            "title": "Nhắc nhở chấm công",
            "message": "Đã đến ca làm việc sáng. Đừng quên điểm danh đúng giờ nhé!",
            "category": "ATTENDANCE",
            "type": "CHECKIN_REMINDER",
            "priority": "WARNING",
            "actionUrl": "intern-attendance.html",
            "isRead": False,
            "readAt": None,
            "createdAt": "2026-10-14T07:30:00Z"
        },
        {
            "id": 3,
            "userId": 1,
            "title": "Hồ sơ thực tập đã được duyệt",
            "message": "CV và đơn xin thực tập của bạn đã được phòng Nhân sự phê duyệt.",
            "category": "DOCUMENT",
            "type": "DOCUMENT_APPROVED",
            "priority": "SUCCESS",
            "actionUrl": "internship-profile.html",
            "isRead": True,
            "readAt": "2026-10-13T10:00:00Z",
            "createdAt": "2026-10-13T09:00:00Z"
        }
    ]
}

cases = [
    ('notifications-center-page', 'notifications.html', '''
// 1. Kiểm tra 3 thẻ thống kê
await tick(); await tick(); await tick();
const totalEl = document.getElementById("totalNotifsCount");
const unreadEl = document.getElementById("unreadNotifsCount");
const readEl = document.getElementById("readNotifsCount");
assert(totalEl && totalEl.textContent === "3", "total notifications count card displays 3");
assert(unreadEl && unreadEl.textContent === "2", "unread notifications count card displays 2");
assert(readEl && readEl.textContent === "1", "read notifications count card displays 1");

// 2. Kiểm tra bộ lọc và công cụ
assert(document.getElementById("filterCategory"), "category filter select is rendered");
assert(document.getElementById("filterStatus"), "status filter select is rendered");
assert(document.getElementById("filterSearch"), "search input is rendered");
assert(document.getElementById("pageMarkAllReadBtn"), "mark all read button is rendered");

// 3. Kiểm tra danh sách thông báo
const items = document.querySelectorAll("#fullNotificationList .notification-item");
assert(items.length === 3, "full notifications list renders 3 notification items");

// 4. Kiểm tra chuông thông báo trên Header và Badge số lượng chưa đọc
const bellBtn = document.getElementById("notificationBellBtn");
const badge = document.getElementById("notificationBadge");
assert(bellBtn, "header notification bell button is rendered");
assert(badge && badge.textContent === "2", "notification badge displays 2 unread count");

// 5. Kiểm tra mở popover thông báo khi click chuông
bellBtn.click();
await tick();
const popover = document.getElementById("notificationPopover");
assert(popover && popover.classList.contains("open"), "popover opens upon clicking bell button");

// 6. Kiểm tra chức năng đánh dấu tất cả đã đọc
const markAllBtn = document.getElementById("pageMarkAllReadBtn");
markAllBtn.click();
await tick();
assert(document.getElementById("unreadNotifsCount").textContent === "0", "unread count becomes 0 after mark-all-read");
assert(badge.classList.contains("hidden") || badge.getAttribute("data-count") === "0", "badge hides after mark-all-read");
''')
]

with TemporaryDirectory(prefix='notifications-ui-test-') as temporary:
    temporary = Path(temporary)
    for name, page, actions in cases:
        setup = '''<script>
localStorage.clear();
localStorage.setItem('token', 'isolated-notifications-token');
let fixture = ''' + json.dumps(mock_notifications) + ''';
window.fetch = async(url, options = {}) => {
    if (url.endsWith('/auth/me')) {
        return {
            ok: true,
            status: 200,
            json: async () => ({
                user: { id: 1, role: "INTERN", fullName: "Nguyễn Văn Thực Tập" },
                permissions: ["VIEW_PROFILE", "VIEW_OWN_PROFILE", "OWN_ATTENDANCE"]
            })
        };
    }
    if (url.includes('/api/notifications/unread-count')) {
        const unread = fixture.items.filter(x => !x.isRead).length;
        return {
            ok: true,
            status: 200,
            json: async () => ({ unreadCount: unread })
        };
    }
    if (url.includes('/api/notifications/mark-all-read')) {
        fixture.items.forEach(x => { x.isRead = true; });
        return {
            ok: true,
            status: 200,
            json: async () => ({ success: true, updatedCount: fixture.items.length })
        };
    }
    if (url.includes('/api/notifications')) {
        const u = new URL(url, "http://localhost");
        const unreadOnly = u.searchParams.get("unreadOnly") === "true";
        const cat = u.searchParams.get("category");
        let items = [...fixture.items];
        if (unreadOnly) items = items.filter(x => !x.isRead);
        if (cat && cat !== "ALL") items = items.filter(x => x.category === cat);
        const unread = fixture.items.filter(x => !x.isRead).length;
        return {
            ok: true,
            status: 200,
            json: async () => ({
                total: items.length,
                unreadCount: unread,
                page: 1,
                pageSize: 15,
                items: items
            })
        };
    }
    return { ok: true, status: 200, json: async () => ({}) };
};
</script>'''

        contents = (root / 'pages' / page).read_text(encoding='utf-8').replace(
            '<head>',
            '<head><base href="' + (root / 'pages').as_uri() + '/">' + setup
        )

        test_harness = '''<pre id="checkResult"></pre><script>
(async () => {
    try {
        const checks = [];
        const tick = () => new Promise(resolve => setTimeout(resolve, 0));
        function assert(ok, label) {
            if (!ok) throw Error(label);
            checks.push(label);
        }
        await tick(); await tick();
        ''' + actions + '''
        document.getElementById('checkResult').textContent = 'PASS: ''' + name + ''' ' + checks.join('; ');
    } catch (err) {
        document.getElementById('checkResult').textContent = 'FAIL: ' + err.message;
    }
})();
</script>'''

        target = temporary / (name + '.html')
        target.write_text(contents.replace('</body>', test_harness + '</body>'), encoding='utf-8')

        result = subprocess.run([
            str(chrome),
            '--headless',
            '--disable-gpu',
            '--no-sandbox',
            '--user-data-dir=' + str(temporary / 'profile'),
            '--virtual-time-budget=2000',
            '--dump-dom',
            target.as_uri()
        ], capture_output=True, timeout=30)

        output_str = result.stdout.decode('utf-8', errors='replace')
        match = re.search(r'<pre id="checkResult">(.*?)</pre>', output_str, re.S)
        assert match and match.group(1).startswith('PASS:'), (name, 'Browser test failed: ' + (match.group(1) if match else output_str))
        print(match.group(1))
