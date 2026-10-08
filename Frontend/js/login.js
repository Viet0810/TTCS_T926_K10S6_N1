const loginForm = document.getElementById("loginForm");
const forgotForm = document.getElementById("forgotForm");
const loginFields = [document.getElementById("username"), document.getElementById("password")];
function validateLoginField(input) {
  const message = input.id === "username" ? Validation.validateEmail(input.value) : Validation.validatePassword(input.value);
  Validation.showFieldError(input, message);
  return !message;
}
for (const input of loginFields) {
  input.addEventListener("blur", () => {
    if (loginForm.getAttribute("aria-busy") !== "true") validateLoginField(input);
  });
  input.addEventListener("input", () => {
    Validation.clearFieldError(input);
    AuthUI.message("loginMessage", "");
  });
}
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
  let firstInvalid = null;
  for (const input of loginFields) {
    if (!validateLoginField(input) && !firstInvalid) firstInvalid = input;
  }
  if (firstInvalid) { firstInvalid.focus(); return; }
  AuthUI.busy(loginForm, true, "Đang đăng nhập...");
  let invalidField = null;
  try {
    const data = await API.login({username: document.getElementById("username").value.trim(), password: document.getElementById("password").value});
    const role = data.user?.role?.toUpperCase();
    if (!data.token || !["ADMIN", "HR", "MENTOR", "INTERN"].includes(role)) throw new Error("Thông tin tài khoản không hợp lệ. Vui lòng liên hệ quản trị viên.");
    localStorage.setItem("token", data.token);
    localStorage.setItem("role", role);
    localStorage.setItem("user", JSON.stringify(data.user));
    location.href = data.user.mustChangePassword ? "pages/change-password.html" : "pages/dashboard.html";
  } catch (error) {
    AuthUI.clearSession();
    const code = error.code || error.message;
    if (code === "ACCOUNT_NOT_FOUND") {
      invalidField = loginFields[0];
      Validation.showFieldError(invalidField, "Tài khoản này không tồn tại.");
    } else if (code === "INVALID_PASSWORD") {
      invalidField = loginFields[1];
      Validation.showFieldError(invalidField, "Mật khẩu bạn nhập không chính xác.");
    } else {
      AuthUI.message("loginMessage", Validation.requestMessage(error,
        error.status === 401 ? "Tên đăng nhập hoặc mật khẩu không đúng." : "Không thể đăng nhập lúc này. Vui lòng thử lại."));
    }
  } finally {
    AuthUI.busy(loginForm, false, "Đăng nhập");
    if (invalidField) invalidField.focus();
  }
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
