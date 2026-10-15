(() => {
  const form = document.getElementById("createMentorForm");
  const fullName = document.getElementById("mentorFullName");
  const email = document.getElementById("mentorEmail");
  const button = document.getElementById("createMentorButton");
  const message = document.getElementById("mentorAccessMessage");
  let ready = false;
  let saving = false;

  function notify(text, type = "error") {
    AppUI.feedback(message, text, type);
  }

  async function initialize() {
    if (!localStorage.getItem("token")) {
      location.href = "../index.html";
      return;
    }
    try {
      const session = await API.getCurrentUser();
      if (session.user?.role !== "HR") {
        notify("Chỉ HR được phép tạo Mentor.");
        return;
      }
      ready = true;
      form.hidden = false;
    } catch (error) {
      notify(`Không thể xác thực phiên: ${error.message}`);
      Session.redirectIfExpired(error);
    }
  }

  form.addEventListener("submit", async event => {
    event.preventDefault();
    if (!ready || saving || !form.reportValidity()) return;

    saving = true;
    button.disabled = true;
    button.textContent = "Đang tạo Mentor…";
    form.setAttribute("aria-busy", "true");
    try {
      const result = await API.createMentor({
        fullName: fullName.value.trim(),
        email: email.value.trim(),
      });
      form.reset();
      notify(result.message || "Đã tạo tài khoản Mentor.", result.emailSent === false ? "warning" : "success");
    } catch (error) {
      const text = error.status === 409
        ? error.message || "Email đã được sử dụng."
        : error.status === 403
          ? "Bạn không có quyền tạo Mentor."
          : error.status >= 500
            ? "Máy chủ chưa thể tạo Mentor. Vui lòng thử lại sau."
            : error.message;
      notify(text);
      Session.redirectIfExpired(error);
    } finally {
      saving = false;
      button.disabled = false;
      button.textContent = "Tạo Mentor";
      form.setAttribute("aria-busy", "false");
    }
  });

  initialize();
})();
