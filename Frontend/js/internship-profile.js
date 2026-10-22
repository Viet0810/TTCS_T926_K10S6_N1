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
      const [profile, schedule] = await Promise.all([API.getMyInternProfile(), API.getMySchedule()]);
      if (!profile) { message.textContent = "Chưa có hồ sơ thực tập."; return; }
      if (schedule) {
        profile.department = schedule.departmentName || profile.department;
        profile.mentor = schedule.mentorName || profile.mentor;
        profile.mentorEmail = schedule.mentorEmail || profile.mentorEmail;
        profile.startDate = schedule.startDate || profile.startDate;
        profile.endDate = schedule.endDate || profile.endDate;
        profile.status = ({ UPCOMING: "Chờ tiếp nhận", ACTIVE: "Đang thực tập", ENDED: "Đã hoàn thành" })[schedule.status] || profile.status;
      }
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
