const loginForm = document.getElementById("loginForm");
const forgotForm = document.getElementById("forgotForm");
function showRecovery(show) {
  document.getElementById("loginView").hidden = show;
  document.getElementById("forgotView").hidden = !show;
  document.title = `${show ? "Quên mật khẩu" : "Đăng nhập"} | Hệ thống quản lý thực tập sinh`;
  (show ? document.getElementById("recoveryEmail") : document.getElementById("username")).focus();
}
document.getElementById("forgotPasswordLink").addEventListener("click", () => {
  const username = document.getElementById("username").value.trim();
  if (username.includes("@")) document.getElementById("recoveryEmail").value = username;
  showRecovery(true);
});
document.getElementById("backToLogin").addEventListener("click", () => showRecovery(false));
if (new URLSearchParams(location.search).get("view") === "forgot") showRecovery(true);
loginForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (loginForm.getAttribute("aria-busy") === "true") return;
  AuthUI.message("loginMessage", "");
  AuthUI.busy(loginForm, true, "Đang đăng nhập…");
  try {
    const data = await API.login({username: document.getElementById("username").value.trim(), password: document.getElementById("password").value});
    const role = data.user?.role?.toUpperCase();
    if (!data.token || !["ADMIN", "HR", "MENTOR", "INTERN"].includes(role)) throw new Error("Thông tin tài khoản không hợp lệ. Vui lòng liên hệ quản trị viên.");
    localStorage.setItem("token", data.token);
    localStorage.setItem("role", role);
    localStorage.setItem("user", JSON.stringify(data.user));
    location.href = "pages/dashboard.html";
  } catch (error) {
    AuthUI.clearSession();
    AuthUI.message("loginMessage", error.message);
  } finally { AuthUI.busy(loginForm, false, "Đăng nhập"); }
});
forgotForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (forgotForm.getAttribute("aria-busy") === "true") return;
  AuthUI.message("forgotMessage", "");
  AuthUI.busy(forgotForm, true, "Đang gửi yêu cầu…");
  try {
    const data = await API.forgotPassword(document.getElementById("recoveryEmail").value.trim());
    AuthUI.message("forgotMessage", data.message, true);
  } catch (error) { AuthUI.message("forgotMessage", error.message); }
  finally { AuthUI.busy(forgotForm, false, "Gửi liên kết đặt lại"); }
});
