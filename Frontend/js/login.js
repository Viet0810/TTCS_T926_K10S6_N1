const passwordInput = document.getElementById("password");
const togglePassword = document.getElementById("togglePassword");

togglePassword.addEventListener("click", () => {
  const isVisible = passwordInput.type === "text";
  passwordInput.type = isVisible ? "password" : "text";
  togglePassword.classList.toggle("is-visible", !isVisible);
  togglePassword.setAttribute("aria-pressed", String(!isVisible));
  togglePassword.setAttribute(
    "aria-label",
    isVisible ? "Hiển thị mật khẩu" : "Ẩn mật khẩu",
  );
});

document
  .getElementById("loginForm")
  .addEventListener("submit", async function (event) {
    event.preventDefault();

    const username = document.getElementById("username").value.trim();

    const password = document.getElementById("password").value;

    try {
      const data = await API.login({
        username,
        password,
      });

      const role = data.user.role.toUpperCase();

      localStorage.setItem("token", data.token);
      localStorage.setItem("role", role);
      localStorage.setItem("user", JSON.stringify(data.user));

      const destinationByRole = {
        ADMIN: "pages/user-manage.html",
        HR: "hr-search-filter-interns/index.html",
        MENTOR: "pages/mentor-dashboard.html",
        INTERN: "pages/intern-portal.html",
      };
      const destination = destinationByRole[role];

      if (!destination) {
        alert("Vai trò tài khoản không hợp lệ!");
        localStorage.clear();
        return;
      }

      window.location.replace(destination);
    } catch (error) {
      alert(error.message);
    }
  });

const forgotPasswordLink = document.getElementById("forgotPasswordLink");
const forgotPasswordFlow = document.getElementById("forgotPasswordFlow");
const resetMessage = document.getElementById("resetMessage");
let resetEmail = "";
let resetToken = "";

function showResetMessage(message, type = "") {
  resetMessage.textContent = message;
  resetMessage.className = `reset-message${type ? ` is-${type}` : ""}`;
}

function setResetStep(step) {
  document.getElementById("resetEmailStep").hidden = step !== "email";
  document.getElementById("resetOtpStep").hidden = step !== "otp";
  document.getElementById("resetPasswordStep").hidden = step !== "password";
  showResetMessage("");
}

forgotPasswordLink.addEventListener("click", () => {
  document.getElementById("loginForm").hidden = true;
  forgotPasswordLink.hidden = true;
  forgotPasswordFlow.hidden = false;
  setResetStep("email");
  document.getElementById("resetEmail").value = document.getElementById("username").value.includes("@")
    ? document.getElementById("username").value.trim() : "";
});

document.getElementById("backToLogin").addEventListener("click", () => {
  forgotPasswordFlow.hidden = true;
  document.getElementById("loginForm").hidden = false;
  forgotPasswordLink.hidden = false;
  resetToken = "";
  showResetMessage("");
});

document.getElementById("requestResetForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = event.currentTarget.querySelector("button[type=submit]");
  button.disabled = true;
  showResetMessage("Đang gửi yêu cầu...");
  resetEmail = document.getElementById("resetEmail").value.trim();
  try {
    const result = await API.requestPasswordReset(resetEmail);
    setResetStep("otp");
    showResetMessage(result.message, "success");
    document.getElementById("resetOtp").focus();
  } catch (error) {
    showResetMessage(error.message, "error");
  } finally {
    button.disabled = false;
  }
});

document.getElementById("verifyResetForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const button = event.currentTarget.querySelector("button[type=submit]");
  button.disabled = true;
  showResetMessage("Đang xác minh mã...");
  try {
    const result = await API.verifyPasswordResetCode(resetEmail, document.getElementById("resetOtp").value.trim());
    resetToken = result.resetToken;
    setResetStep("password");
    document.getElementById("newPassword").focus();
  } catch (error) {
    showResetMessage(error.message, "error");
  } finally {
    button.disabled = false;
  }
});

document.getElementById("setNewPasswordForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const newPassword = document.getElementById("newPassword").value;
  const confirmPassword = document.getElementById("confirmNewPassword").value;
  if (newPassword !== confirmPassword) {
    showResetMessage("Mật khẩu xác nhận không khớp.", "error");
    return;
  }
  const button = event.currentTarget.querySelector("button[type=submit]");
  button.disabled = true;
  showResetMessage("Đang cập nhật mật khẩu...");
  try {
    const result = await API.resetPassword({
      email: resetEmail,
      resetToken,
      newPassword,
      confirmPassword,
    });
    setResetStep("email");
    forgotPasswordFlow.hidden = true;
    document.getElementById("loginForm").hidden = false;
    forgotPasswordLink.hidden = false;
    document.getElementById("username").value = resetEmail;
    document.getElementById("password").value = "";
    resetToken = "";
    showResetMessage("");
    window.alert(result.message);
    document.getElementById("password").focus();
  } catch (error) {
    showResetMessage(error.message, "error");
  } finally {
    button.disabled = false;
  }
});
