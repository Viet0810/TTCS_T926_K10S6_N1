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

      if (!["ADMIN", "HR", "MENTOR", "INTERN"].includes(role)) {
        alert("Vai trò tài khoản không hợp lệ!");
        localStorage.clear();
        return;
      }

      window.location.href = "pages/dashboard.html";
    } catch (error) {
      alert(error.message);
    }
  });
