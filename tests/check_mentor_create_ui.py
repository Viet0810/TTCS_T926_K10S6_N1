"""Exercise Mentor creation form behavior in a headless browser with a mocked API."""
from pathlib import Path
from tempfile import TemporaryDirectory
import os
import re
import subprocess

repo = Path(__file__).resolve().parents[1]
page = repo / "Frontend" / "pages" / "mentor-create.html"
chrome = Path(os.environ.get("PROGRAMFILES", "C:/Program Files")) / "Google/Chrome/Application/chrome.exe"

setup = r"""
<script>
localStorage.setItem("token", "ui-test-token");
window.testRole = "HR";
window.mentorFetchCalls = [];
window.mentorNextStatus = 201;
window.mentorNextBody = { success: true, message: "Mentor created", emailSent: false };
window.fetch = async (url, options = {}) => {
  if (url.endsWith("/auth/me")) {
    return { ok: true, status: 200, headers: { get: () => null },
      json: async () => ({ user: { role: window.testRole, fullName: "UI test" }, permissions: [] }) };
  }
  if (url.endsWith("/mentors")) {
    window.mentorFetchCalls.push({ url, options });
    const status = window.mentorNextStatus;
    const body = window.mentorNextBody;
    window.mentorNextStatus = 201;
    window.mentorNextBody = { success: true, message: "Mentor created", emailSent: false };
    return { ok: status >= 200 && status < 300, status, headers: { get: () => null },
      json: async () => body };
  }
  throw new Error("Unexpected API URL: " + url);
};
window.addEventListener("error", event => { window.testPageError = event.message; });
window.addEventListener("unhandledrejection", event => { window.testPageError = String(event.reason); });
</script>
"""

checks = r"""
<pre id="mentor-test-result"></pre>
<script>
(async () => {
  const assert = (condition, message) => { if (!condition) throw new Error(message); };
  const waitFor = async predicate => {
    for (let attempt = 0; attempt < 100; attempt++) {
      if (predicate()) return;
      await new Promise(resolve => setTimeout(resolve, 20));
    }
    throw new Error("Timed out waiting for UI state: role=" + window.testRole +
      ", hidden=" + document.getElementById("createMentorForm").hidden +
      ", message=" + document.getElementById("mentorAccessMessage").textContent);
  };
  try {
    const form = document.getElementById("createMentorForm");
    const fullName = document.getElementById("mentorFullName");
    const email = document.getElementById("mentorEmail");
    const submit = async (status, body) => {
      window.mentorNextStatus = status;
      window.mentorNextBody = body;
      fullName.value = "  Mentor Test  ";
      email.value = " mentor@example.invalid ";
      form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
      await waitFor(() => form.getAttribute("aria-busy") === "false" && window.mentorFetchCalls.length > 0);
    };
    await waitFor(() => window.testRole !== undefined && (!form.hidden || document.querySelector("#toastRegion .ui-toast-text")));
    assert(!form.hidden, "HR form is visible");
    assert(document.querySelector('.role-menu a[href="mentor-create.html"]'), "HR menu contains Mentor creation link");

    await submit(201, { success: true, message: "Mentor created", emailSent: false });
    const call = window.mentorFetchCalls[0];
    const payload = JSON.parse(call.options.body);
    assert(call.options.method === "POST", "request uses POST");
    assert(call.options.headers.Authorization === "Bearer ui-test-token", "request includes bearer token");
    assert(JSON.stringify(Object.keys(payload).sort()) === JSON.stringify(["email", "fullName"]), "request contains only fullName and email");
    assert(payload.fullName === "Mentor Test" && payload.email === "mentor@example.invalid", "request values are trimmed");
    assert(document.querySelector("#toastRegion .ui-toast.is-warning .ui-toast-text")?.textContent === "Mentor created", "email delivery warning is shown");

    const cases = [
      [400, { errors: { Email: ["Email không hợp lệ."] } }, "Email không hợp lệ."],
      [409, { success: false, message: "Email đã tồn tại.", data: null }, "Email đã tồn tại."],
      [403, { success: false, message: "Forbidden", data: null }, "Bạn không có quyền tạo Mentor."],
      [500, { success: false, message: "Internal error", data: null }, "Máy chủ chưa thể tạo Mentor. Vui lòng thử lại sau."]
    ];
    for (const [status, body, expected] of cases) {
      const count = window.mentorFetchCalls.length;
      await submit(status, body);
      assert(window.mentorFetchCalls.length === count + 1, "request is sent for HTTP " + status);
      assert(document.querySelector("#toastRegion .ui-toast-text")?.textContent === expected, "HTTP " + status + " message is shown");
    }

    let expiredRedirect = false;
    Session.redirectIfExpired = error => { expiredRedirect = error.status === 401; return expiredRedirect; };
    const count = window.mentorFetchCalls.length;
    await submit(401, { success: false, message: "Unauthenticated", data: null });
    assert(window.mentorFetchCalls.length === count + 1 && expiredRedirect, "401 uses existing expired-session handling");

    window.testRole = "ADMIN";
    window.AppNavigation.render({ user: { role: "ADMIN" }, permissions: [] });
    assert(!document.querySelector('.role-menu a[href="mentor-create.html"]'), "non-HR menu omits Mentor creation link");
    document.getElementById("mentor-test-result").textContent = "PASS Mentor form";
  } catch (error) {
    document.getElementById("mentor-test-result").textContent = "FAIL " + error.message;
  }
})();
</script>
"""

admin_checks = r"""
<pre id="mentor-test-result"></pre>
<script>
(async () => {
  const waitFor = async predicate => {
    for (let attempt = 0; attempt < 100; attempt++) {
      if (predicate()) return;
      await new Promise(resolve => setTimeout(resolve, 20));
    }
    throw new Error("Timed out waiting for UI state: role=" + window.testRole +
      ", hidden=" + document.getElementById("createMentorForm").hidden +
      ", message=" + document.getElementById("mentorAccessMessage").textContent);
  };
  try {
    await waitFor(() => document.querySelector("#toastRegion .ui-toast-text"));
    if (!document.getElementById("createMentorForm").hidden) throw new Error("non-HR form is hidden");
    if (document.querySelector("#toastRegion .ui-toast-text").textContent !== "Chỉ HR được phép tạo Mentor.") {
      throw new Error("non-HR access message is shown");
    }
    if (document.querySelector('.role-menu a[href="mentor-create.html"]')) {
      throw new Error("non-HR menu omits Mentor creation link");
    }
    document.getElementById("mentor-test-result").textContent = "PASS non-HR access guard";
  } catch (error) {
    document.getElementById("mentor-test-result").textContent = "FAIL " + error.message;
  }
})();
</script>
"""

with TemporaryDirectory(prefix="mentor-create-ui-") as directory:
    html = page.read_text(encoding="utf-8")
    html = html.replace("<head>", f'<head><base href="{page.parent.as_uri()}/">{setup}', 1)

    for name, role, assertions in [
        ("hr", "HR", checks),
        ("admin", "ADMIN", admin_checks),
    ]:
        harness = Path(directory) / f"mentor-create-{name}.html"
        case_html = html.replace('window.testRole = "HR";', f'window.testRole = "{role}";', 1)
        case_html = case_html.replace("</body>", assertions + "</body>", 1)
        harness.write_text(case_html, encoding="utf-8")
        output = subprocess.run(
            [
                str(chrome), "--headless", "--disable-gpu", "--no-sandbox",
                "--allow-file-access-from-files", "--user-data-dir=" + str(Path(directory) / f"profile-{name}"),
                "--virtual-time-budget=7000", "--window-size=1280,1000", "--dump-dom",
                harness.as_uri(),
            ],
            capture_output=True,
            text=True,
            encoding="utf-8",
            timeout=45,
        )
        result = re.search(r'<pre id="mentor-test-result">(.*?)</pre>', output.stdout, re.S)
        status = result.group(1) if result else "NO RESULT"
        print(status)
        if not status.startswith("PASS"):
            raise SystemExit(output.stderr or status)
