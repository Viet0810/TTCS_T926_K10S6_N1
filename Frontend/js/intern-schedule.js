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
      document.getElementById("scheduleStatus").textContent = { UPCOMING: "Sắp bắt đầu", ACTIVE: "Đang diễn ra", ENDED: "Đã kết thúc" }[schedule.status] || "—";
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
