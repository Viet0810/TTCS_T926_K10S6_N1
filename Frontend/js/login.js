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

      switch (role) {
        case "ADMIN":
          window.location.href = "pages/user-manage.html";
          break;

        case "HR":
          window.location.href = "pages/intern-manage.html";
          break;

        case "MENTOR":
          alert("Đăng nhập thành công. Giao diện dành cho mentor chưa được tạo.");
          break;

        case "INTERN":
          alert("Đăng nhập thành công. Giao diện dành cho thực tập sinh chưa được tạo.");
          break;

        default:
          alert("Vai trò tài khoản không hợp lệ!");
          localStorage.clear();
      }
    } catch (error) {
      alert(error.message);
    }
  });
