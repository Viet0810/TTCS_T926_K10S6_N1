const AuthUI = {
  message(id, text, success = false) {
    const element = document.getElementById(id);
    AppUI.feedback(element, text, success ? "success" : "error");
  },
  busy(form, busy, label) {
    form.setAttribute("aria-busy", String(busy));
    for (const control of form.elements) control.disabled = busy;
    form.querySelector('[type="submit"]').textContent = label;
  },
  clearSession() {
    Session.clear();
  }
};
document.querySelectorAll("[data-password-toggle]").forEach((button) => {
  button.addEventListener("click", () => {
    const input = document.getElementById(button.dataset.passwordToggle);
    const show = input.type === "password";
    input.type = show ? "text" : "password";
    button.classList.toggle("is-visible", show);
    button.setAttribute("aria-pressed", String(show));
    button.setAttribute("aria-label", show ? "Ẩn mật khẩu" : "Hiển thị mật khẩu");
  });
});
