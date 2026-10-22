const registerForm = document.getElementById("registerForm");
const registerFields = ["fullName", "email", "password", "phone", "school", "major"];
const validationFields = ["fullName", "email", "password", "confirmPassword", "phone", "school", "major"];
const fieldLabels = { fullName: "họ và tên", email: "email", password: "mật khẩu", phone: "số điện thoại", school: "trường", major: "chuyên ngành" };
let registering = false;
let emailConflict = false;

function setFieldError(name, text) {
  Validation.showFieldError(registerForm.elements[name], text);
}

function validateRegisterField(name) {
  const input = registerForm.elements[name];
  const value = ["password", "confirmPassword"].includes(name) ? input.value : input.value.trim();
  let error = "";
  if (name === "email") error = Validation.validateEmail(value);
  else if (name === "password") error = Validation.validatePassword(value);
  else if (name === "phone") error = Validation.validatePhone(value);
  else if (name === "confirmPassword") {
    if (!value) error = "Vui lòng xác nhận mật khẩu.";
    else if (value !== registerForm.elements.password.value) error = "Xác nhận mật khẩu không khớp.";
  } else if (!value.trim()) error = `Vui lòng nhập ${fieldLabels[name]}.`;
  if (!error && input.maxLength > 0 && value.length > input.maxLength) error = `Vui lòng nhập tối đa ${input.maxLength} ký tự.`;
  if (!error && name === "email" && emailConflict) error = "Email đã được sử dụng. Vui lòng đăng nhập hoặc sử dụng email khác.";
  setFieldError(name, error);
  return !error;
}

function setRegisterBusy(busy) {
  registerForm.setAttribute("aria-busy", String(busy));
  for (const control of registerForm.elements) control.disabled = busy;
  document.getElementById("registerSubmitLabel").textContent = busy ? "Đang tạo tài khoản…" : "Đăng ký tài khoản";
  registerForm.querySelector(".register-spinner").hidden = !busy;
}

for (const name of validationFields) {
  const input = registerForm.elements[name];
  input.addEventListener("blur", () => {
    if (registering) return;
    if (!["password", "confirmPassword"].includes(name)) input.value = input.value.trim();
    validateRegisterField(name);
  });
  input.addEventListener("input", () => {
    if (name === "phone") {
      const caret = input.selectionStart;
      const beforeCaret = input.value.slice(0, caret ?? input.value.length);
      const digits = input.value.replace(/[^0-9]/g, "").slice(0, 10);
      if (input.value !== digits) {
        input.value = digits;
        if (caret !== null) {
          const position = Math.min(beforeCaret.replace(/[^0-9]/g, "").length, digits.length);
          input.setSelectionRange(position, position);
        }
      }
    }
    if (name === "email") emailConflict = false;
    Validation.clearFieldError(input);
    if (name === "password") Validation.clearFieldError(registerForm.elements.confirmPassword);
    AuthUI.message("registerMessage", "");
  });
}

registerForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (registering || registerForm.hidden) return;
  AuthUI.message("registerMessage", "");
  let firstInvalid = null;
  for (const name of validationFields) {
    if (!["password", "confirmPassword"].includes(name)) registerForm.elements[name].value = registerForm.elements[name].value.trim();
    if (!validateRegisterField(name) && !firstInvalid) firstInvalid = registerForm.elements[name];
  }
  if (firstInvalid) { firstInvalid.focus(); return; }
  const payload = Object.fromEntries(registerFields.map((name) => [name, registerForm.elements[name].value]));
  registering = true;
  setRegisterBusy(true);
  try {
    await requestApi("/auth/register", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload),
    });
    registerForm.reset();
    registerForm.hidden = true;
    document.getElementById("registeredLogin").hidden = false;
    AuthUI.message("registerMessage", "Đăng ký thành công! Đăng nhập bằng email của bạn để nộp CV và đơn xin thực tập.", true);
    document.getElementById("registerMessage").focus();
  } catch (error) {
    if (error.status === 409) {
      emailConflict = true;
      validateRegisterField("email");
    } else AuthUI.message("registerMessage", Validation.requestMessage(error, "Không thể đăng ký lúc này. Vui lòng thử lại."));
  } finally {
    registering = false;
    setRegisterBusy(false);
    if (emailConflict) registerForm.elements.email.focus();
  }
});
