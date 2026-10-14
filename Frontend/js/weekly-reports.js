let reports = [];
let selectedReport = null;
let busy = false;
let allowed = false;
const reportList = document.getElementById("reportList");
const reportMessage = document.getElementById("reportMessage");
const dialog = document.getElementById("feedbackDialog");
const dateOnly = (value) => value ? new Date(`${value.slice(0, 10)}T00:00:00`).toLocaleDateString("vi-VN") : "";

function appendSection(container, title, value) {
  const section = document.createElement("section");
  section.className = "weekly-report-section";
  const heading = document.createElement("h4"); heading.textContent = title;
  const text = document.createElement("p"); text.textContent = value?.trim() || "Chưa cập nhật.";
  section.append(heading, text); container.append(section);
}

function renderReports() {
  const search = document.getElementById("reportSearch").value.trim().toLocaleLowerCase("vi");
  const week = document.getElementById("weekFilter").value;
  const status = document.getElementById("feedbackFilter").value;
  const visible = reports.filter((report) => {
    const identity = `${report.internName} ${report.studentCode || ""}`.toLocaleLowerCase("vi");
    return (!search || identity.includes(search)) && (!week || report.weekStart === week)
      && (!status || (status === "done") === Boolean(report.mentorFeedback));
  });
  reportList.replaceChildren();
  if (!visible.length) {
    const empty = document.createElement("div"); empty.className = "weekly-report-empty";
    empty.textContent = reports.length ? "Không có báo cáo phù hợp với bộ lọc." : "Chưa có báo cáo tuần nào được gửi.";
    reportList.append(empty); return;
  }
  for (const report of visible) {
    const card = document.createElement("article"); card.className = "weekly-report-card";
    const header = document.createElement("div"); header.className = "weekly-report-card-header";
    const identity = document.createElement("div");
    const name = document.createElement("h3"); name.textContent = report.internName;
    const subtitle = document.createElement("p"); subtitle.className = "weekly-report-subtitle";
    subtitle.textContent = [report.studentCode, report.organization].filter(Boolean).join(" · ") || "Thực tập sinh";
    const dates = document.createElement("p"); dates.className = "weekly-report-date";
    dates.textContent = `Tuần ${dateOnly(report.weekStart)} – ${dateOnly(report.weekEnd)} · Gửi lúc ${new Date(report.submittedAt).toLocaleString("vi-VN")}`;
    identity.append(name, subtitle, dates);
    const badge = document.createElement("span"); badge.className = `weekly-report-badge${report.mentorFeedback ? " is-done" : ""}`;
    badge.textContent = report.mentorFeedback ? "Đã phản hồi" : "Chờ phản hồi";
    header.append(identity, badge); card.append(header);
    const sections = document.createElement("div"); sections.className = "weekly-report-sections";
    appendSection(sections, "Tổng kết tuần", report.summary);
    appendSection(sections, "Công việc đã hoàn thành", report.accomplishments);
    appendSection(sections, "Khó khăn", report.challenges);
    appendSection(sections, "Kế hoạch tuần tới", report.nextWeekPlan);
    card.append(sections);
    if (report.mentorFeedback) {
      const feedback = document.createElement("section"); feedback.className = "weekly-report-feedback";
      const title = document.createElement("h4"); title.textContent = `Phản hồi của bạn${report.feedbackAt ? ` · ${new Date(report.feedbackAt).toLocaleString("vi-VN")}` : ""}`;
      const text = document.createElement("p"); text.textContent = report.mentorFeedback;
      feedback.append(title, text); card.append(feedback);
    }
    const actions = document.createElement("div"); actions.className = "weekly-report-card-actions";
    const button = document.createElement("button"); button.type = "button"; button.className = "btn-primary";
    button.textContent = report.mentorFeedback ? "Cập nhật phản hồi" : "Gửi phản hồi";
    button.addEventListener("click", () => openFeedback(report)); actions.append(button); card.append(actions);
    reportList.append(card);
  }
}

function updateWeeks() {
  const select = document.getElementById("weekFilter");
  const selected = select.value;
  select.replaceChildren(new Option("Tất cả các tuần", ""));
  const weeks = [...new Set(reports.map((report) => report.weekStart))].sort().reverse();
  for (const value of weeks) select.add(new Option(`Tuần bắt đầu ${dateOnly(value)}`, value));
  select.value = weeks.includes(selected) ? selected : "";
}

async function loadReports() {
  if (!allowed || busy) return;
  const button = document.getElementById("refreshBtn"); button.disabled = true;
  try {
    reports = await API.getWeeklyReports();
    updateWeeks(); renderReports(); reportMessage.textContent = `${reports.length} báo cáo tuần.`;
  } catch (error) {
    reportMessage.textContent = error.message;
    Session.redirectIfExpired(error);
  } finally { button.disabled = false; }
}

function openFeedback(report) {
  selectedReport = report;
  document.getElementById("feedbackIntern").textContent = `${report.internName} · Tuần ${dateOnly(report.weekStart)} – ${dateOnly(report.weekEnd)}`;
  document.getElementById("feedbackText").value = report.mentorFeedback || "";
  document.getElementById("feedbackDialogMessage").textContent = "";
  dialog.showModal();
}

document.getElementById("feedbackForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!allowed || busy || !selectedReport) return;
  const feedback = document.getElementById("feedbackText").value.trim();
  const message = document.getElementById("feedbackDialogMessage");
  if (!feedback) { message.textContent = "Vui lòng nhập phản hồi."; return; }
  busy = true;
  const save = document.getElementById("saveFeedbackBtn"); save.disabled = true; save.textContent = "Đang lưu…";
  try {
    const result = await API.saveWeeklyReportFeedback(selectedReport.id, feedback);
    selectedReport.mentorFeedback = feedback; selectedReport.feedbackAt = new Date().toISOString();
    dialog.close(); renderReports();
    reportMessage.textContent = result.notificationCreated
      ? "Đã lưu phản hồi và thông báo cho thực tập sinh."
      : "Đã lưu phản hồi, nhưng chưa tìm thấy tài khoản INTERN để gửi thông báo.";
  } catch (error) {
    message.textContent = error.message; Session.redirectIfExpired(error);
  } finally { busy = false; save.disabled = false; save.textContent = "Lưu phản hồi"; }
});

document.getElementById("reportSearch").addEventListener("input", renderReports);
document.getElementById("weekFilter").addEventListener("change", renderReports);
document.getElementById("feedbackFilter").addEventListener("change", renderReports);
document.getElementById("refreshBtn").addEventListener("click", loadReports);
document.getElementById("closeFeedbackBtn").addEventListener("click", () => { if (!busy) dialog.close(); });
dialog.addEventListener("cancel", (event) => { if (busy) event.preventDefault(); });
document.getElementById("logoutBtn").addEventListener("click", Session.logout);

async function initializeWeeklyReports() {
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  try {
    const session = await API.getCurrentUser();
    if (!Session.hasPermission(session, "VIEW_WEEKLY_REPORTS")) { location.href = "dashboard.html"; return; }
    allowed = true; await loadReports();
  } catch (error) { reportMessage.textContent = error.message; Session.redirectIfExpired(error); }
}
initializeWeeklyReports();
