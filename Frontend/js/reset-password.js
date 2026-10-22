const resetForm = document.getElementById("resetForm");
let resetToken = new URLSearchParams(location.hash.slice(1)).get("token") || "";
// Keep the token in memory rather than browser history or localStorage.
history.replaceState(null, "", location.pathname);
if (!/^[a-f0-9]{64}$/i.test(resetToken)) {
  resetToken = "";
  AuthUI.message("resetMessage", "Liên kết đặt lại mật khẩu không hợp lệ. Vui lòng yêu cầu liên kết mới.");
  for (const control of resetForm.elements) control.disabled = true;
}
resetForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!resetToken || resetForm.getAttribute("aria-busy") === "true") return;
  for (const id of ["newPassword", "confirmPassword"]) Validation.clearFieldError(document.getElementById(id));
  const password = document.getElementById("newPassword").value;
  const passwordError = Validation.validatePassword(password);
  if (passwordError) { Validation.showFieldError(document.getElementById("newPassword"), passwordError); document.getElementById("newPassword").focus(); return; }
  if (password !== document.getElementById("confirmPassword").value) {
    Validation.showFieldError(document.getElementById("confirmPassword"), "Mật khẩu xác nhận không khớp.");
    document.getElementById("confirmPassword").focus();
    return;
  }
  AuthUI.message("resetMessage", "");
  AuthUI.busy(resetForm, true, "Đang lưu mật khẩu…");
  try {
    const data = await API.resetPassword(resetToken, password);
    resetToken = "";
    AuthUI.clearSession();
    resetForm.reset();
    AuthUI.message("resetMessage", data.message, true);
    document.getElementById("resetSubmit").hidden = true;
    document.getElementById("returnToLogin").hidden = false;
  } catch (error) { AuthUI.message("resetMessage", error.message); }
  finally {
    AuthUI.busy(resetForm, false, "Lưu mật khẩu mới");
    if (!resetToken) for (const control of resetForm.elements) control.disabled = true;
  }
});

for (const id of ["newPassword", "confirmPassword"]) {
  const input = document.getElementById(id);
  input.addEventListener("input", () => Validation.clearFieldError(input));
}
