(() => {
  const message = document.getElementById("profileMessage");
  const retry = document.getElementById("retryProfileBtn");
  async function load() {
    if (retry.disabled || !localStorage.getItem("token")) return;
    retry.disabled = true; retry.hidden = true; message.textContent = "Đang tải hồ sơ…";
    try {
      const session = await API.getCurrentUser();
      if (!Session.hasPermission(session, "VIEW_OWN_PROFILE")) {
        message.textContent = "Bạn không có quyền xem hồ sơ thực tập cá nhân."; return;
      }
      const profile = await API.getMyInternProfile();
      if (!profile) { message.textContent = "Chưa có hồ sơ thực tập."; return; }
      document.getElementById("internName").textContent = profile.fullName;
      document.getElementById("internEmail").textContent = profile.email;
      InternProfile.details(document.getElementById("internDetails"), profile);
      message.textContent = "";
    } catch (error) {
      message.textContent = error.message; retry.hidden = false; Session.redirectIfExpired(error);
    } finally { retry.disabled = false; }
  }
  retry.addEventListener("click", load);
  load();
})();
