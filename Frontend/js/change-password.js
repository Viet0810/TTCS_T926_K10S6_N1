(() => {
  const form = document.getElementById("changePasswordForm");
  const fields = ["currentPassword", "newPassword", "confirmPassword"].map(id => document.getElementById(id));
  let ready = false;
  function validate(input) {
    let message = "";
    if (input.id === "currentPassword") message = input.value ? "" : "Vui lòng nhập mật khẩu hiện tại.";
    if (input.id === "newPassword") message = Validation.validatePassword(input.value) || (input.value === fields[0].value ? "Mật khẩu mới phải khác mật khẩu hiện tại." : "");
    if (input.id === "confirmPassword") message = input.value === fields[1].value && input.value ? "" : "Xác nhận mật khẩu không khớp.";
    Validation.showFieldError(input, message); return !message;
  }
  fields.forEach(input => {
    input.addEventListener("input", () => { Validation.clearFieldError(input); AuthUI.message("changeMessage", ""); });
    input.addEventListener("blur", () => { if (form.getAttribute("aria-busy") !== "true") validate(input); });
  });
  form.addEventListener("submit", async event => {
    event.preventDefault(); if (!ready || form.getAttribute("aria-busy") === "true") return;
    let invalid = null; fields.forEach(input => { if (!validate(input) && !invalid) invalid = input; });
    if (invalid) { invalid.focus(); return; }
    const payload = Object.fromEntries(fields.map(input => [input.id, input.value]));
    AuthUI.busy(form, true, "Đang đổi mật khẩu...");
    try {
      const result = await API.changePassword(payload);
      localStorage.setItem("token", result.token); localStorage.setItem("user", JSON.stringify(result.user)); localStorage.setItem("role", result.user.role);
      form.reset(); location.replace("dashboard.html");
    } catch (error) {
      if (error.code === "INVALID_CURRENT_PASSWORD") Validation.showFieldError(fields[0], "Mật khẩu hiện tại không đúng.");
      else if (error.code === "SAME_PASSWORD") Validation.showFieldError(fields[1], "Mật khẩu mới phải khác mật khẩu hiện tại.");
      else AuthUI.message("changeMessage", error.message);
      Session.redirectIfExpired(error);
    } finally { AuthUI.busy(form, false, "Đổi mật khẩu"); }
  });
  (async () => {
    if (!localStorage.getItem("token")) { Session.logout(); return; }
    try {
      const session = await API.getCurrentUser();
      if (session.user.mustChangePassword) {
        document.getElementById("changeDescription").textContent = "Bạn cần đổi mật khẩu tạm trước khi sử dụng hệ thống.";
        document.getElementById("backToProfile").hidden = true;
      }
      ready = true; form.hidden = false; document.getElementById("loadingMessage").hidden = true;
    } catch (error) { document.getElementById("loadingMessage").textContent = error.message; Session.redirectIfExpired(error); }
  })();
})();
