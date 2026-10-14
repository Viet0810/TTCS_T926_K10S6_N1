let canSubmit = false;
let submitting = false;
const reportForm = document.getElementById("weeklyReportForm");
const submitMessage = document.getElementById("submitMessage");
const myReportList = document.getElementById("myReportList");
const dateLabel = (value) => value ? new Date(`${value.slice(0, 10)}T00:00:00`).toLocaleDateString("vi-VN") : "";

function addReportSection(card, title, value) {
  if (!value) return;
  const section = document.createElement("section"); section.className = "weekly-report-section";
  const heading = document.createElement("h4"); heading.textContent = title;
  const text = document.createElement("p"); text.textContent = value;
  section.append(heading, text); card.append(section);
}

function renderMyReports(items) {
  myReportList.replaceChildren();
  if (!items.length) {
    const empty = document.createElement("div"); empty.className = "weekly-report-empty";
    empty.textContent = "Bạn chưa gửi báo cáo tuần nào."; myReportList.append(empty); return;
  }
  for (const item of items) {
    const card = document.createElement("article"); card.className = "weekly-report-card";
    const header = document.createElement("div"); header.className = "weekly-report-card-header";
    const identity = document.createElement("div");
    const title = document.createElement("h3"); title.textContent = `Tuần ${dateLabel(item.weekStart)} – ${dateLabel(item.weekEnd)}`;
    const submitted = document.createElement("p"); submitted.className = "weekly-report-subtitle";
    submitted.textContent = `Đã gửi ${new Date(item.submittedAt).toLocaleString("vi-VN")}`;
    identity.append(title, submitted);
    const status = document.createElement("span"); status.className = `weekly-report-badge${item.mentorFeedback ? " is-done" : ""}`;
    status.textContent = item.mentorFeedback ? "Đã có phản hồi" : "Đang chờ Mentor";
    header.append(identity, status); card.append(header);
    const sections = document.createElement("div"); sections.className = "weekly-report-sections";
    addReportSection(sections, "Tổng kết tuần", item.summary);
    addReportSection(sections, "Công việc đã hoàn thành", item.accomplishments);
    addReportSection(sections, "Khó khăn", item.challenges);
    addReportSection(sections, "Kế hoạch tuần tới", item.nextWeekPlan);
    card.append(sections);
    if (item.mentorFeedback) {
      const feedback = document.createElement("section"); feedback.className = "weekly-report-feedback";
      const heading = document.createElement("h4"); heading.textContent = `Phản hồi Mentor${item.feedbackAt ? ` · ${new Date(item.feedbackAt).toLocaleString("vi-VN")}` : ""}`;
      const text = document.createElement("p"); text.textContent = item.mentorFeedback;
      feedback.append(heading, text); card.append(feedback);
    }
    myReportList.append(card);
  }
}

async function loadMyReports() {
  if (!canSubmit) return;
  const reports = await API.getMyWeeklyReports();
  renderMyReports(reports);
}

reportForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (!canSubmit || submitting) return;
  if (!reportForm.reportValidity()) return;
  const data = Object.fromEntries(new FormData(reportForm));
  const start = new Date(`${data.weekStart}T00:00:00`), end = new Date(`${data.weekEnd}T00:00:00`);
  const days = (end - start) / 86400000;
  if (days < 0 || days > 6) { submitMessage.textContent = "Khoảng ngày báo cáo cần nằm trong cùng một tuần (tối đa 7 ngày)."; return; }
  if (!data.summary.trim()) { submitMessage.textContent = "Vui lòng nhập phần tổng kết tuần."; return; }
  submitting = true;
  const button = document.getElementById("submitReportBtn"); button.disabled = true; button.textContent = "Đang gửi…";
  submitMessage.textContent = "";
  try {
    const result = await API.submitMyWeeklyReport({
      weekStart: data.weekStart, weekEnd: data.weekEnd, summary: data.summary.trim(),
      accomplishments: data.accomplishments.trim() || null,
      challenges: data.challenges.trim() || null,
      nextWeekPlan: data.nextWeekPlan.trim() || null,
    });
    submitMessage.textContent = result.message || "Đã gửi báo cáo tuần.";
    reportForm.reset();
    try { await loadMyReports(); }
    catch (error) { submitMessage.textContent += ` Báo cáo đã được lưu nhưng chưa tải lại lịch sử: ${error.message}`; }
  } catch (error) { submitMessage.textContent = error.message; Session.redirectIfExpired(error); }
  finally { submitting = false; button.disabled = false; button.textContent = "Gửi / cập nhật báo cáo"; }
});

document.getElementById("refreshMyReports").addEventListener("click", async (event) => {
  const button = event.currentTarget; button.disabled = true;
  try { await loadMyReports(); }
  catch (error) { submitMessage.textContent = error.message; Session.redirectIfExpired(error); }
  finally { button.disabled = false; }
});
document.getElementById("logoutBtn").addEventListener("click", Session.logout);

async function initializeSubmitPage() {
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  try {
    const session = await API.getCurrentUser();
    if (!Session.hasPermission(session, "SUBMIT_WEEKLY_REPORTS")) { location.href = "dashboard.html"; return; }
    canSubmit = true;
    const profile = await API.getMyInternProfile();
    if (profile.mentorEmail) submitMessage.textContent = `Báo cáo sẽ được gửi đến Mentor ${profile.mentor || profile.mentorEmail}.`;
    await loadMyReports();
  } catch (error) {
    submitMessage.textContent = error.status === 404
      ? "Tài khoản chưa có hồ sơ thực tập sinh. Vui lòng hoàn thiện hồ sơ trước khi gửi báo cáo."
      : error.message;
    if (error.status === 401) Session.redirectIfExpired(error);
    reportForm.querySelectorAll("input, textarea, button[type=submit]").forEach((control) => { control.disabled = true; });
  }
}
initializeSubmitPage();
