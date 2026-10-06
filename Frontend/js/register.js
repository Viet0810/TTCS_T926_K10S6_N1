const registerForm = document.getElementById("registerForm");
const registerFields = ["fullName", "email", "password", "phone", "school", "major"];
const fieldLabels = { fullName: "họ và tên", email: "email", password: "mật khẩu", phone: "số điện thoại", school: "trường", major: "chuyên ngành" };
const touchedFields = new Set();
let registering = false;
let emailConflict = false;

function setFieldError(name, text) {
  const input = registerForm.elements[name];
  const error = document.getElementById(`${name}Error`);
  input.setAttribute("aria-invalid", String(Boolean(text)));
  error.textContent = text;
  error.hidden = !text;
}

function validateRegisterField(name) {
  const input = registerForm.elements[name];
  const value = name === "password" ? input.value : input.value.trim();
  let error = "";
  if (!value.trim()) error = `Vui lòng nhập ${fieldLabels[name]}.`;
  else if (name === "email" && !/^[a-zA-Z0-9._%+-]+@gmail\.com$/.test(value)) error = "Vui lòng nhập đúng địa chỉ Gmail.";
  else if (name === "password" && !/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$/.test(value)) error = "Mật khẩu phải có ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";
  else if (name === "phone" && !/^0[35789]\d{8}$/.test(value)) error = "Số điện thoại phải gồm 10 chữ số và đúng định dạng số di động Việt Nam.";
  else if (input.maxLength > 0 && value.length > input.maxLength) error = `Vui lòng nhập tối đa ${input.maxLength} ký tự.`;
  else if (name === "email" && emailConflict) error = "Email đã được sử dụng. Vui lòng đăng nhập hoặc sử dụng email khác.";
  setFieldError(name, error);
  return !error;
}

function setRegisterBusy(busy) {
  registerForm.setAttribute("aria-busy", String(busy));
  for (const control of registerForm.elements) control.disabled = busy;
  document.getElementById("registerSubmitLabel").textContent = busy ? "Đang tạo tài khoản…" : "Đăng ký tài khoản";
  registerForm.querySelector(".register-spinner").hidden = !busy;
}

for (const name of registerFields) {
  const input = registerForm.elements[name];
  input.addEventListener("blur", () => {
    if (registering) return;
    if (name !== "password") input.value = input.value.trim();
    touchedFields.add(name);
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
    if (touchedFields.has(name)) validateRegisterField(name);
    AuthUI.message("registerMessage", "");
  });
}

registerForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (registering || registerForm.hidden) return;
  AuthUI.message("registerMessage", "");
  let firstInvalid = null;
  for (const name of registerFields) {
    if (name !== "password") registerForm.elements[name].value = registerForm.elements[name].value.trim();
    touchedFields.add(name);
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
    AuthUI.message("registerMessage", error.message || "Không thể đăng ký lúc này. Vui lòng thử lại.");
    if (error.status === 409) {
      emailConflict = true;
      validateRegisterField("email");
    }
  } finally {
    registering = false;
    setRegisterBusy(false);
    if (emailConflict) registerForm.elements.email.focus();
  }
});
