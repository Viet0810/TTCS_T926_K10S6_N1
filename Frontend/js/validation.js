const Validation = {
  validateEmail(value) {
    if (!value.trim()) return "Vui lòng nhập email.";
    return /^[a-zA-Z0-9._%+-]+@(gmail\.com|ictu\.edu\.vn)$/.test(value.trim())
      ? "" : "Vui lòng sử dụng email @gmail.com hoặc @ictu.edu.vn.";
  },
  validatePassword(value) {
    if (!value.trim()) return "Vui lòng nhập mật khẩu.";
    return /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(value)
      ? "" : "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";
  },
  validatePhone(value) {
    if (!value.trim()) return "Vui lòng nhập số điện thoại.";
    return /^0[35789]\d{8}$/.test(value.trim())
      ? "" : "Số điện thoại phải gồm 10 chữ số và đúng định dạng số di động Việt Nam.";
  },
  showFieldError(input, message) {
    const error = document.getElementById(`${input.id}Error`);
    input.setAttribute("aria-invalid", String(Boolean(message)));
    input.closest(".auth-field").classList.toggle("has-error", Boolean(message));
    error.textContent = message;
    error.hidden = !message;
  },
  clearFieldError(input) { this.showFieldError(input, ""); },
  requestMessage(error, fallback) {
    if (error.status === 0 || error instanceof TypeError) return "Không thể kết nối tới hệ thống. Vui lòng thử lại.";
    if (error.status === 429) return "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.";
    return fallback;
  },
};
