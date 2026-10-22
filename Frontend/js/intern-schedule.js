(() => {
  const message = document.getElementById("dashboardMessage");
  async function load() {
    if (!localStorage.getItem("token")) return;
    try {
      const session = await API.getCurrentUser();
      if (!Session.hasPermission(session, "VIEW_OWN_SCHEDULE")) {
        message.textContent = "Bạn không có quyền xem lịch thực tập cá nhân."; return;
      }
      const schedule = await API.getMySchedule();
      if (!schedule?.startDate || !schedule?.endDate) {
        message.textContent = "Lịch thực tập của bạn chưa được thiết lập."; return;
      }
      const date = value => new Date(`${value.slice(0, 10)}T00:00:00`).toLocaleDateString("vi-VN");
      document.getElementById("scheduleStart").textContent = date(schedule.startDate);
      document.getElementById("scheduleProgram").textContent = schedule.programName || "Chương trình thực tập";
      document.getElementById("scheduleDuration").textContent = schedule.durationDays ? `${schedule.durationDays} ngày` : "—";
      document.getElementById("scheduleEnd").textContent = date(schedule.endDate);
      const status = document.getElementById("scheduleStatus");
      status.textContent = { UPCOMING: "Sắp bắt đầu", ACTIVE: "Đang diễn ra", ENDED: "Đã kết thúc" }[schedule.status] || "—";
      status.className = `schedule-status is-${String(schedule.status || "unknown").toLowerCase()}`;
      const start = new Date(`${schedule.startDate.slice(0, 10)}T00:00:00`);
      const end = new Date(`${schedule.endDate.slice(0, 10)}T23:59:59`);
      const now = new Date();
      const total = Math.max(1, end - start);
      const progress = Math.max(0, Math.min(100, Math.round(((now - start) / total) * 100)));
      document.getElementById("scheduleProgressText").textContent = `${progress}%`;
      document.getElementById("scheduleProgressBar").style.width = `${progress}%`;
      const progressElement = document.querySelector(".schedule-progress");
      progressElement.setAttribute("aria-valuenow", String(progress));
      const daysLeft = Math.max(0, Math.ceil((end - now) / 86400000));
      document.getElementById("scheduleProgressNote").textContent = schedule.status === "ENDED"
        ? "Chương trình thực tập đã hoàn thành."
        : schedule.status === "UPCOMING" ? "Chương trình chưa bắt đầu." : `Còn ${daysLeft} ngày đến khi kết thúc chương trình.`;
      for (const [field, value] of [["Department", schedule.departmentName], ["Mentor", schedule.mentorName]]) {
        document.getElementById(`schedule${field}Field`).hidden = !value;
        if (value) document.getElementById(`schedule${field}`).textContent = value;
      }
      document.getElementById("scheduleDetails").hidden = false; message.hidden = true;
    } catch (error) { message.textContent = error.message; message.hidden = false; Session.redirectIfExpired(error); }
  }
  load();
  document.addEventListener('session-permissions-updated', event=>{
    if(!Session.hasPermission(event.detail,'VIEW_OWN_SCHEDULE')){
      document.getElementById('scheduleDetails').hidden=true;message.hidden=false;message.textContent='Bạn không còn quyền xem lịch thực tập.';
    }
  });
})();
