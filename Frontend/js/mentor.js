(() => {
  const API_BASE = window.APP_CONFIG?.apiBaseUrl || "http://localhost:5024/api";
  const token = localStorage.getItem("token");
  const $ = (selector, root = document) => root.querySelector(selector);
  const content = $("#mentorContent");
  const dialog = $("#mentorDialog");
  const dialogContent = $("#dialogContent");
  const sections = {
    dashboard: "Dashboard", interns: "Thực tập sinh của tôi", tasks: "Nhiệm vụ", progress: "Tiến độ",
    reports: "Báo cáo", evaluations: "Đánh giá", schedules: "Lịch mentoring", feedback: "Feedback",
    notifications: "Thông báo", profile: "Hồ sơ cá nhân"
  };
  const criteria = ["Thái độ", "Kỷ luật", "Khả năng học hỏi", "Kiến thức chuyên môn", "Kỹ năng chuyên môn", "Làm việc nhóm", "Giải quyết vấn đề", "Tiến độ công việc", "Tính chủ động", "Đánh giá tổng thể"];
  let currentPage = "dashboard";
  let internsCache = [];
  let toastTimer;

  const esc = value => String(value ?? "").replace(/[&<>"']/g, char => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&#39;" }[char]));
  const date = value => value ? new Date(value).toLocaleDateString("vi-VN") : "—";
  const dateTime = value => value ? new Date(value).toLocaleString("vi-VN", { dateStyle: "medium", timeStyle: "short" }) : "—";
  const statusLabel = value => ({ NotStarted:"Chưa bắt đầu", InProgress:"Đang thực hiện", PendingReview:"Chờ đánh giá", Completed:"Hoàn thành", Submitted:"Đã nộp", InReview:"Đang xem xét", RevisionRequested:"Yêu cầu chỉnh sửa", Approved:"Đã duyệt", Rejected:"Từ chối", Scheduled:"Đã lên lịch", Cancelled:"Đã hủy" })[value] || value || "—";

  async function api(path, options = {}) {
    let response;
    try {
      response = await fetch(`${API_BASE}${path}`, {
        ...options,
        headers: { "Content-Type": "application/json", Authorization: token ? `Bearer ${token}` : "", ...options.headers }
      });
    } catch { throw new Error("Không kết nối được Backend. Kiểm tra backend và địa chỉ API."); }
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      const message = response.status === 401 ? "Phiên đăng nhập hết hạn. Hãy đăng nhập lại." : response.status === 403 ? "Tài khoản không có quyền thực hiện thao tác này." : result.message || `Lỗi máy chủ HTTP ${response.status}.`;
      throw new Error(message);
    }
    return result;
  }

  function toast(message) {
    const el = $("#mentorToast"); el.textContent = message; el.classList.add("show");
    clearTimeout(toastTimer); toastTimer = setTimeout(() => el.classList.remove("show"), 3000);
  }
  function empty(message = "Chưa có dữ liệu.") { return `<div class="empty-state">${esc(message)}</div>`; }
  function heading(title, description, action = "") {
    return `<div class="page-heading"><div><p class="eyebrow">MENTOR WORKSPACE</p><h1>${title}</h1><p>${description}</p></div>${action}</div>`;
  }
  function panel(title, body, action = "") {
    return `<section class="panel"><div class="panel-head"><h2>${title}</h2>${action}</div><div class="panel-body">${body}</div></section>`;
  }
  function stat(label, value, note = "Theo hồ sơ được phân công") {
    return `<article class="stat-card"><div class="stat-label">${label}</div><div class="stat-value">${value ?? 0}</div><div class="stat-note">${note}</div></article>`;
  }
  function table(headers, rows, emptyText) {
    if (!rows.length) return empty(emptyText);
    return `<div class="table-wrap"><table><thead><tr>${headers.map(item => `<th>${item}</th>`).join("")}</tr></thead><tbody>${rows.join("")}</tbody></table></div>`;
  }
  function badge(value) {
    const cls = ["Completed", "Approved"].includes(value) ? "green" : ["PendingReview", "Submitted", "InReview", "RevisionRequested"].includes(value) ? "amber" : ["Rejected", "Cancelled"].includes(value) ? "red" : "blue";
    return `<span class="badge ${cls}">${esc(statusLabel(value))}</span>`;
  }
  function setPage(page) {
    if (!sections[page]) return;
    currentPage = page;
    $("#currentSection").textContent = sections[page];
    document.querySelectorAll(".nav-item").forEach(button => button.classList.toggle("active", button.dataset.page === page));
    $("#mentorSidebar").classList.remove("open");
    content.innerHTML = `<div class="loading-state">Đang tải ${esc(sections[page].toLowerCase())}...</div>`;
    const renderer = renderers[page];
    renderer().catch(error => { content.innerHTML = `<div class="error-state">${esc(error.message)}<br><button class="button" data-action="retry" style="margin-top:12px">Thử lại</button></div>`; });
  }
  async function loadInterns() {
    const result = await api("/mentor/interns?page=1&pageSize=1000");
    internsCache = result.items || [];
    return internsCache;
  }
  function internOptions(selected) {
    return `<option value="">Chọn thực tập sinh</option>${internsCache.map(i => `<option value="${i.Id}" ${Number(selected) === i.Id ? "selected" : ""}>${esc(i.Name)} · ${esc(i.Mssv)}</option>`).join("")}`;
  }
  async function renderDashboard() {
    const data = await api("/mentor/dashboard");
    const mentor = data.mentor || {};
    updateIdentity(mentor);
    const s = data.stats || {};
    const interns = data.recentInterns || [], tasks = data.pendingTasks || [], reports = data.recentReports || [], schedules = data.upcomingSchedules || [];
    const internRows = interns.map(i => `<div class="list-row"><div class="list-main"><strong>${esc(i.Name)}</strong><small>${esc(i.Position)} · ${esc(i.Mssv)} · ${esc(i.Email)}</small></div><div class="progress-cell"><div class="progress-track"><span style="width:${Math.max(0,Math.min(100,Number(i.Progress)||0))}%"></span></div><small>${Number(i.Progress)||0}%</small></div><button class="button" data-action="intern-detail" data-id="${i.Id}">Chi tiết</button></div>`);
    const taskRows = tasks.map(t => `<div class="list-row"><div class="list-main"><strong>${esc(t.Title)}</strong><small>${esc(t.InternName)} · Hạn ${date(t.Deadline)}</small></div>${badge(t.Status)}</div>`);
    const reportRows = reports.map(r => `<div class="list-row"><div class="list-main"><strong>${esc(r.Title)}</strong><small>${esc(r.InternName)} · ${dateTime(r.SubmittedAt)}</small></div><button class="button" data-action="review-report" data-id="${r.Id}">Xem</button></div>`);
    const scheduleRows = schedules.map(x => `<div class="list-row"><div class="list-main"><strong>${esc(x.Title)} · ${esc(x.InternName)}</strong><small>${dateTime(x.StartsAt)} – ${dateTime(x.EndsAt)} · ${esc(x.Location)}</small></div></div>`);
    content.innerHTML = `${heading(`Xin chào, ${esc(mentor.FullName || "Mentor")}`, `${esc(mentor.Email || "")} · ${esc(mentor.Position || "Mentor")}`, `<button class="button primary" data-goto="tasks">＋ Giao nhiệm vụ</button>`)}
      <div class="stats-grid">${stat("Thực tập sinh phụ trách",s.assignedInterns)}${stat("Đang hoạt động",s.activeInterns)}${stat("Nhiệm vụ cần xử lý",s.pendingTasks)}${stat("Nhiệm vụ hoàn thành",s.completedTasks)}${stat("Báo cáo cần xem",s.reportsToReview)}${stat("Feedback 7 ngày gần đây",s.recentFeedback,`${s.unreadNotifications||0} thông báo chưa đọc`)}</div>
      <div class="two-col">${panel("Thực tập sinh gần đây",`<div class="list">${internRows.join("")||empty("Chưa có thực tập sinh được phân công.")}</div>`,`<button class="button" data-goto="interns">Tất cả</button>`)}${panel("Nhiệm vụ cần xử lý",`<div class="list">${taskRows.join("")||empty("Chưa có nhiệm vụ cần xử lý.")}</div>`,`<button class="button" data-goto="tasks">Mở nhiệm vụ</button>`)}</div>
      <div class="two-col">${panel("Báo cáo gần đây",`<div class="list">${reportRows.join("")||empty("Chưa có báo cáo cần xem.")}</div>`,`<button class="button" data-goto="reports">Mở báo cáo</button>`)}${panel("Lịch mentoring sắp tới",`<div class="list">${scheduleRows.join("")||empty("Chưa có lịch sắp tới.")}</div>`,`<button class="button" data-goto="schedules">Mở lịch</button>`)}</div>`;
    updateNotificationBadge(s.unreadNotifications || 0);
  }
  function updateIdentity(user) {
    const initials = String(user.FullName || "Mentor").split(/\s+/).slice(-2).map(x => x[0]).join("").toUpperCase();
    document.querySelectorAll("[data-name]").forEach(x => x.textContent = user.FullName || "Mentor");
    document.querySelectorAll("[data-email]").forEach(x => x.textContent = user.Email || "");
    document.querySelectorAll("[data-avatar]").forEach(x => { x.textContent = initials || "ME"; if(user.AvatarUrl) x.style.backgroundImage=`url('${esc(user.AvatarUrl)}')`; });
  }
  function updateNotificationBadge(count) {
    const badge = $("#unreadBadge"), dot = $("#notificationDot"); badge.hidden = !count; badge.textContent = count; dot.hidden = !count;
  }
  async function renderInterns() {
    await loadInterns();
    const rows = internsCache.map(i => `<tr><td class="table-name">${esc(i.Name)}</td><td>${esc(i.Mssv)}</td><td>${esc(i.Email)}</td><td>${esc(i.Phone||"—")}</td><td>${esc(i.Position||"—")}</td><td>${esc(i.Department||"—")}</td><td>${date(i.StartDate)} – ${date(i.EndDate)}</td><td>${badge(i.Status)}</td><td><div class="progress-cell"><div class="progress-track"><span style="width:${Math.max(0,Math.min(100,Number(i.Progress)||0))}%"></span></div><small>${Number(i.Progress)||0}%</small></div></td><td><button class="button" data-action="intern-detail" data-id="${i.Id}">Xem chi tiết</button></td></tr>`);
    content.innerHTML = `${heading("Thực tập sinh của tôi","Chỉ hiển thị hồ sơ được phân công cho tài khoản Mentor này.")}<div class="filters"><input id="internSearch" placeholder="Tìm tên, MSSV hoặc email"><select id="internStatus"><option value="">Mọi trạng thái</option><option>Đang thực tập</option><option>Chờ tiếp nhận</option><option>Đã hoàn thành</option></select><select id="internProgress"><option value="">Mọi tiến độ</option><option value="Low">Dưới 40%</option><option value="Medium">40–79%</option><option value="High">Từ 80%</option></select></div><div id="internTable">${table(["Họ tên","Mã SV","Email","Điện thoại","Vị trí","Phòng ban","Thời gian","Trạng thái","Tiến độ",""],rows,"Chưa có hồ sơ được phân công.")}</div>`;
  }
  async function filterInterns() {
    const params = new URLSearchParams({ page:"1", pageSize:"50" });
    if($("#internSearch")) params.set("search",$("#internSearch").value.trim());
    if($("#internStatus")?.value) params.set("status",$("#internStatus").value);
    if($("#internProgress")?.value) params.set("progress",$("#internProgress").value);
    const result = await api(`/mentor/interns?${params}`);
    const rows=(result.items||[]).map(i=>`<tr><td class="table-name">${esc(i.Name)}</td><td>${esc(i.Mssv)}</td><td>${esc(i.Email)}</td><td>${esc(i.Phone||"—")}</td><td>${esc(i.Position||"—")}</td><td>${esc(i.Department||"—")}</td><td>${date(i.StartDate)} – ${date(i.EndDate)}</td><td>${badge(i.Status)}</td><td><div class="progress-cell"><div class="progress-track"><span style="width:${i.Progress||0}%"></span></div><small>${i.Progress||0}%</small></div></td><td><button class="button" data-action="intern-detail" data-id="${i.Id}">Xem chi tiết</button></td></tr>`);
    $("#internTable").innerHTML=table(["Họ tên","Mã SV","Email","Điện thoại","Vị trí","Phòng ban","Thời gian","Trạng thái","Tiến độ",""],rows,"Không có kết quả phù hợp.");
  }
  async function showInternDetail(id) {
    const data = await api(`/mentor/interns/${id}`), i=data.intern;
    const fields=[["Họ tên",i.Name],["Mã thực tập sinh",i.Mssv],["Email",i.Email],["Điện thoại",i.Phone],["Trường",i.School],["Chuyên ngành",i.Major],["Vị trí",i.Position],["Phòng ban",i.Department],["Thời gian",`${date(i.StartDate)} – ${date(i.EndDate)}`],["Trạng thái",statusLabel(i.Status)],["Tiến độ",`${i.Progress||0}%`]];
    openDialog(`<h2 class="dialog-title">Chi tiết thực tập sinh · ${esc(i.Name)}</h2><div class="detail-grid">${fields.map(([label,value])=>`<div class="detail-item"><small>${label}</small><strong>${esc(value||"—")}</strong></div>`).join("")}</div><h3>Nhiệm vụ (${data.tasks.length})</h3>${data.tasks.map(t=>`<div class="list-row"><div class="list-main"><strong>${esc(t.Title)}</strong><small>${date(t.Deadline)}</small></div>${badge(t.Status)}</div>`).join("")||empty("Chưa có nhiệm vụ.")}<h3>Báo cáo (${data.reports.length})</h3>${data.reports.map(r=>`<div class="list-row"><strong>${esc(r.Title)}</strong>${badge(r.Status)}</div>`).join("")||empty("Chưa có báo cáo.")}<h3>Feedback gần đây</h3>${data.feedback.map(f=>`<div class="list-row"><div class="list-main"><strong>${esc(f.Type)} · ${esc(f.Content)}</strong><small>${dateTime(f.CreatedAt)}</small></div></div>`).join("")||empty("Chưa có feedback.")}`);
  }
  async function renderTasks() {
    await loadInterns(); const tasks=await api("/mentor/tasks");
    const rows=tasks.map(t=>`<tr><td class="table-name">${esc(t.Title)}</td><td>${esc(t.InternName)}</td><td>${date(t.AssignedAt)}</td><td>${date(t.Deadline)}</td><td>${badge(t.Priority)}</td><td>${badge(t.Status)}</td><td><div class="toolbar"><button class="button" data-action="edit-task" data-id="${t.Id}">Sửa</button>${t.Status==="PendingReview"?`<button class="button primary" data-action="complete-task" data-id="${t.Id}">Duyệt hoàn thành</button>`:""}<button class="button danger" data-action="delete-task" data-id="${t.Id}">Xóa</button></div></td></tr>`);
    content.innerHTML=`${heading("Quản lý nhiệm vụ","Giao việc và theo dõi nhiệm vụ của các Intern được phân công.",`<button class="button primary" data-action="new-task">＋ Tạo nhiệm vụ</button>`)}${table(["Tiêu đề","Thực tập sinh","Ngày giao","Deadline","Ưu tiên","Trạng thái","Thao tác"],rows,"Chưa có nhiệm vụ.")}`;
  }
  async function taskForm(task) {
    await loadInterns();
    openDialog(`<h2 class="dialog-title">${task?"Sửa nhiệm vụ":"Tạo nhiệm vụ"}</h2><form id="taskForm" class="form-grid"><input type="hidden" name="id" value="${task?.Id||""}"><div class="field wide" id="taskInternTarget"><label>Thực tập sinh</label><select name="internId" ${task?"required":""}>${internOptions(task?.InternId)}</select></div>${task?"":`<div class="field wide"><label><input type="checkbox" name="assignToAll" id="assignToAll"> Giao nhiệm vụ này cho toàn bộ ${internsCache.length} thực tập sinh thuộc nhóm của tôi</label></div>`}<div class="field wide"><label>Tiêu đề</label><input name="title" maxlength="200" required value="${esc(task?.Title)}"></div><div class="field wide"><label>Mô tả</label><textarea name="description" maxlength="2000">${esc(task?.Description)}</textarea></div><div class="field"><label>Deadline</label><input type="date" name="deadline" value="${task?.Deadline?new Date(task.Deadline).toISOString().slice(0,10):""}"></div><div class="field"><label>Mức ưu tiên</label><select name="priority"><option value="Low" ${task?.Priority==="Low"?"selected":""}>Thấp</option><option value="Medium" ${!task||task.Priority==="Medium"?"selected":""}>Trung bình</option><option value="High" ${task?.Priority==="High"?"selected":""}>Cao</option></select></div><div class="dialog-actions wide"><button type="button" class="button" data-action="close-dialog">Hủy</button><button class="button primary" type="submit">Lưu nhiệm vụ</button></div></form>`);
  }
  async function renderProgress() {
    await loadInterns(); const tasks=await api("/mentor/tasks");
    const rows=internsCache.map(i=>{const mine=tasks.filter(t=>t.InternId===i.Id),done=mine.filter(t=>t.Status==="Completed").length,active=mine.filter(t=>t.Status==="InProgress").length,overdue=mine.filter(t=>t.Deadline&&new Date(t.Deadline)<new Date()&&t.Status!=="Completed").length;return `<tr><td class="table-name">${esc(i.Name)}</td><td>${mine.length}</td><td>${done}</td><td>${active}</td><td>${overdue}</td><td>${i.Progress||0}%</td><td><div class="progress-track"><span style="width:${Math.max(0,Math.min(100,Number(i.Progress)||0))}%"></span></div></td></tr>`;});
    content.innerHTML=`${heading("Tiến độ thực tập","Tổng hợp theo hồ sơ và nhiệm vụ lưu trong hệ thống.")}${table(["Thực tập sinh","Tổng nhiệm vụ","Hoàn thành","Đang làm","Quá hạn","Tiến độ hồ sơ",""],rows,"Chưa có thực tập sinh được phân công.")}`;
  }
  async function renderReports() {
    const reports=await api("/mentor/reports");
    const rows=reports.map(r=>`<tr><td class="table-name">${esc(r.Title)}</td><td>${esc(r.InternName)}</td><td>${dateTime(r.SubmittedAt)}</td><td>${badge(r.Status)}</td><td><button class="button primary" data-action="review-report" data-id="${r.Id}">Xem / nhận xét</button></td></tr>`);
    content.innerHTML=`${heading("Báo cáo thực tập","Đọc, nhận xét, yêu cầu chỉnh sửa, duyệt hoặc từ chối báo cáo được phân công.")}<div class="filters"><select id="reportStatus"><option value="">Mọi trạng thái</option><option value="Submitted">Đã nộp</option><option value="InReview">Đang xem xét</option><option value="RevisionRequested">Yêu cầu chỉnh sửa</option><option value="Approved">Đã duyệt</option></select></div><div id="reportTable">${table(["Báo cáo","Thực tập sinh","Thời gian nộp","Trạng thái",""],rows,"Chưa có báo cáo.")}</div>`;
  }
  async function filterReports() {
    const status=$("#reportStatus").value; const reports=await api(`/mentor/reports${status?`?status=${encodeURIComponent(status)}`:""}`);
    const rows=reports.map(r=>`<tr><td class="table-name">${esc(r.Title)}</td><td>${esc(r.InternName)}</td><td>${dateTime(r.SubmittedAt)}</td><td>${badge(r.Status)}</td><td><button class="button primary" data-action="review-report" data-id="${r.Id}">Xem / nhận xét</button></td></tr>`);
    $("#reportTable").innerHTML=table(["Báo cáo","Thực tập sinh","Thời gian nộp","Trạng thái",""],rows,"Không có báo cáo phù hợp.");
  }
  async function reviewReport(id) {
    const result=await api(`/mentor/reports/${id}`), r=result.report;
    openDialog(`<h2 class="dialog-title">${esc(r.Title)} · ${esc(r.InternName)}</h2><p class="badge">${esc(statusLabel(r.Status))} · ${dateTime(r.SubmittedAt)}</p><div class="panel-body" style="white-space:pre-wrap;line-height:1.7">${esc(r.Content)}</div>${r.AttachmentUrl?`<p><a href="${esc(r.AttachmentUrl)}" target="_blank" rel="noopener">Mở tệp đính kèm</a></p>`:""}<h3>Lịch sử nhận xét</h3>${result.reviewHistory.map(h=>`<div class="list-row"><div class="list-main"><strong>${esc(statusLabel(h.Status))} · ${esc(h.Score??"—")}/10</strong><small>${esc(h.Feedback)} · ${dateTime(h.ReviewedAt)}</small></div></div>`).join("")||empty("Chưa có nhận xét.")}<form id="reviewForm" class="form-grid"><input type="hidden" name="id" value="${r.Id}"><div class="field"><label>Kết quả</label><select name="status"><option value="Approved">Duyệt</option><option value="RevisionRequested">Yêu cầu chỉnh sửa</option><option value="Rejected">Từ chối</option></select></div><div class="field"><label>Điểm (0–10, tùy chọn)</label><input type="number" name="score" min="0" max="10" step="0.1"></div><div class="field wide"><label>Nhận xét${" · lý do bắt buộc nếu yêu cầu chỉnh sửa"}</label><textarea name="feedback" maxlength="2000" required></textarea></div><div class="dialog-actions wide"><button type="button" class="button" data-action="close-dialog">Đóng</button><button class="button primary" type="submit">Lưu nhận xét</button></div></form>`);
  }
  async function renderEvaluations() {
    await loadInterns(); const evaluations=await api("/mentor/evaluations");
    const rows=evaluations.map(e=>`<tr><td class="table-name">${esc(e.InternName)}</td><td>${e.IsSubmitted?badge("Approved"):badge("InProgress")}</td><td>${dateTime(e.UpdatedAt)}</td><td>${esc(e.Comments||"—")}</td><td><button class="button" data-action="edit-evaluation" data-id="${e.InternId}">Mở đánh giá</button></td></tr>`);
    content.innerHTML=`${heading("Đánh giá thực tập sinh","Chấm điểm theo tiêu chí, lưu bản nháp hoặc gửi đánh giá cho Intern.",`<button class="button primary" data-action="new-evaluation">＋ Tạo đánh giá</button>`)}${table(["Thực tập sinh","Trạng thái","Cập nhật","Nhận xét",""],rows,"Chưa có đánh giá.")}`;
  }
  async function evaluationForm(internId) {
    await loadInterns(); if(!internId&&internsCache.length===1)internId=internsCache[0].Id;
    const list=internId?await api(`/mentor/evaluations?internId=${internId}`):[]; const ev=list[0];
    const scoreMap=Object.fromEntries((ev?.scores||[]).map(x=>[x.Criterion,x.Score]));
    openDialog(`<h2 class="dialog-title">Đánh giá thực tập sinh</h2><form id="evaluationForm" class="form-grid"><div class="field wide"><label>Thực tập sinh</label><select name="internId" required>${internOptions(internId)}</select></div>${criteria.map((c,n)=>`<div class="field"><label>${esc(c)}</label><input type="number" name="score_${n}" data-criterion="${esc(c)}" min="0" max="10" step="0.1" value="${scoreMap[c]??""}" required></div>`).join("")}<div class="field wide"><label>Nhận xét</label><textarea name="comments" maxlength="3000">${esc(ev?.Comments)}</textarea></div><div class="field wide"><label>Đề xuất</label><textarea name="recommendation" maxlength="1000">${esc(ev?.Recommendation)}</textarea></div><div class="dialog-actions wide"><button type="button" class="button" data-action="close-dialog">Hủy</button><button class="button" type="submit" data-submit="false">Lưu nháp</button><button class="button primary" type="submit" data-submit="true">Gửi đánh giá</button></div></form>`);
  }
  async function renderSchedules() {
    await loadInterns();const items=await api("/mentor/schedules");
    const rows=items.map(s=>`<tr><td class="table-name">${esc(s.Title)}</td><td>${esc(s.InternName)}</td><td>${dateTime(s.StartsAt)}</td><td>${dateTime(s.EndsAt)}</td><td>${esc(s.Location||"—")}</td><td>${badge(s.Status)}</td><td><div class="toolbar"><button class="button" data-action="edit-schedule" data-id="${s.Id}">Sửa</button><button class="button danger" data-action="cancel-schedule" data-id="${s.Id}">Hủy lịch</button></div></td></tr>`);
    content.innerHTML=`${heading("Lịch mentoring","Tạo, cập nhật và hủy các buổi gặp với Intern được phân công.",`<button class="button primary" data-action="new-schedule">＋ Tạo lịch</button>`)}${table(["Tiêu đề","Thực tập sinh","Bắt đầu","Kết thúc","Địa điểm / link","Trạng thái",""],rows,"Chưa có lịch mentoring.")}`;
  }
  async function scheduleForm(item) {
    await loadInterns();
    openDialog(`<h2 class="dialog-title">${item?"Cập nhật lịch mentoring":"Tạo lịch mentoring"}</h2><form id="scheduleForm" class="form-grid"><input type="hidden" name="id" value="${item?.Id||""}"><div class="field wide"><label>Thực tập sinh</label><select name="internId" required>${internOptions(item?.InternId)}</select></div><div class="field wide"><label>Tiêu đề</label><input name="title" maxlength="200" required value="${esc(item?.Title)}"></div><div class="field wide"><label>Nội dung</label><textarea name="content">${esc(item?.Content)}</textarea></div><div class="field"><label>Bắt đầu</label><input name="startsAt" type="datetime-local" required value="${localDateTime(item?.StartsAt)}"></div><div class="field"><label>Kết thúc</label><input name="endsAt" type="datetime-local" required value="${localDateTime(item?.EndsAt)}"></div><div class="field wide"><label>Địa điểm / link</label><input name="location" maxlength="500" value="${esc(item?.Location)}"></div><div class="field wide"><label>Ghi chú</label><textarea name="notes" maxlength="1000">${esc(item?.Notes)}</textarea></div><div class="dialog-actions wide"><button type="button" class="button" data-action="close-dialog">Hủy</button><button class="button primary">Lưu lịch</button></div></form>`);
  }
  function localDateTime(value){if(!value)return"";const d=new Date(value);d.setMinutes(d.getMinutes()-d.getTimezoneOffset());return d.toISOString().slice(0,16);}
  async function renderFeedback() {
    await loadInterns();const items=await api("/mentor/feedback");
    const rows=items.map(f=>`<tr><td>${dateTime(f.CreatedAt)}</td><td class="table-name">${esc(f.InternName)}</td><td>${esc(f.Type)}</td><td>${badge(f.Severity)}</td><td>${esc(f.Content)}</td></tr>`);
    content.innerHTML=`${heading("Feedback","Gửi góp ý, lời khen hoặc cảnh báo; feedback được lưu và hiển thị cho Intern.",`<button class="button primary" data-action="new-feedback">＋ Gửi feedback</button>`)}${table(["Ngày","Thực tập sinh","Loại","Mức độ","Nội dung"],rows,"Chưa có feedback.")}`;
  }
  async function feedbackForm() {
    await loadInterns();openDialog(`<h2 class="dialog-title">Gửi feedback cho Intern</h2><form id="feedbackForm" class="form-grid"><div class="field wide"><label>Thực tập sinh</label><select name="internId" required>${internOptions()}</select></div><div class="field"><label>Loại</label><select name="type"><option value="Praise">Khen ngợi</option><option value="Suggestion">Góp ý</option><option value="Warning">Cảnh báo</option><option value="Improvement">Đề xuất cải thiện</option></select></div><div class="field"><label>Mức độ</label><select name="severity"><option value="Low">Thấp</option><option value="Normal" selected>Bình thường</option><option value="High">Cao</option></select></div><div class="field wide"><label>Nội dung</label><textarea name="content" maxlength="2000" required></textarea></div><div class="dialog-actions wide"><button type="button" class="button" data-action="close-dialog">Hủy</button><button class="button primary">Gửi feedback</button></div></form>`);
  }
  async function renderNotifications() {
    const items=await api("/mentor/notifications");const unread=items.filter(i=>!i.ReadAt).length;updateNotificationBadge(unread);
    const rows=items.map(n=>`<tr><td>${dateTime(n.CreatedAt)}</td><td class="table-name">${esc(n.Title)}</td><td>${esc(n.Message)}</td><td>${n.ReadAt?badge("Completed"):'<span class="badge amber">Chưa đọc</span>'}</td><td>${n.ReadAt?"":`<button class="button" data-action="read-notification" data-id="${n.Id}">Đánh dấu đã đọc</button>`}</td></tr>`);
    content.innerHTML=`${heading("Thông báo","Sự kiện liên quan đến hồ sơ được phân công.",`<button class="button" data-action="read-all">Đánh dấu tất cả đã đọc</button>`)}${table(["Thời gian","Tiêu đề","Nội dung","Trạng thái",""],rows,"Bạn chưa có thông báo.")}`;
  }
  async function renderProfile() {
    const p=await api("/mentor/profile");updateIdentity(p);
    content.innerHTML=`${heading("Hồ sơ Mentor","Chỉnh sửa thông tin cá nhân được phép. Vai trò, quyền và email tài khoản bị khóa.")}<section class="panel"><div class="panel-body"><form id="profileForm" class="form-grid"><div class="field"><label>Họ tên</label><input value="${esc(p.FullName)}" disabled></div><div class="field"><label>Email đăng nhập</label><input value="${esc(p.Email)}" disabled></div><div class="field"><label>Vai trò</label><input value="${esc(p.Role)}" disabled></div><div class="field"><label>Điện thoại</label><input name="phone" maxlength="30" value="${esc(p.Phone)}"></div><div class="field"><label>Chức vụ</label><input name="position" maxlength="150" value="${esc(p.Position)}"></div><div class="field"><label>Phòng ban</label><input name="department" maxlength="150" value="${esc(p.Department)}"></div><div class="field wide"><label>Avatar URL</label><input name="avatarUrl" maxlength="1000" type="url" value="${esc(p.AvatarUrl)}"></div><div class="field wide"><label>Kỹ năng</label><textarea name="skills" maxlength="2000">${esc(p.Skills)}</textarea></div><div class="field wide"><label>Kinh nghiệm</label><textarea name="experience" maxlength="4000">${esc(p.Experience)}</textarea></div><div class="wide"><button class="button primary">Lưu hồ sơ</button></div></form></div></section>`;
  }

  const renderers={dashboard:renderDashboard,interns:renderInterns,tasks:renderTasks,progress:renderProgress,reports:renderReports,evaluations:renderEvaluations,schedules:renderSchedules,feedback:renderFeedback,notifications:renderNotifications,profile:renderProfile};
  function openDialog(html){dialogContent.innerHTML=html;dialog.showModal();}
  const closeDialog=()=>dialog.close();
  const byId=(arr,id)=>arr.find(item=>item.Id===Number(id));

  document.addEventListener("click", async event => {
    const nav=event.target.closest("[data-page]");if(nav){setPage(nav.dataset.page);return;}
    const goto=event.target.closest("[data-goto]");if(goto){setPage(goto.dataset.goto);return;}
    const action=event.target.closest("[data-action]");if(!action)return;
    const id=action.dataset.id;action.disabled=true;
    try {
      switch(action.dataset.action){
        case "retry": setPage(currentPage);break;
        case "close-dialog": closeDialog();break;
        case "intern-detail": await showInternDetail(id);break;
        case "new-task": await taskForm();break;
        case "edit-task": await loadInterns();await taskForm(byId(await api("/mentor/tasks"),id));break;
        case "delete-task": if(confirm("Xóa nhiệm vụ này?")){await api(`/mentor/tasks/${id}`,{method:"DELETE"});toast("Đã xóa nhiệm vụ.");await renderTasks();}break;
        case "complete-task": if(confirm("Xác nhận duyệt hoàn thành nhiệm vụ?")){await api(`/mentor/tasks/${id}/review`,{method:"PUT",body:JSON.stringify({status:"Completed"})});toast("Đã duyệt nhiệm vụ.");await renderTasks();}break;
        case "review-report": await reviewReport(id);break;
        case "new-evaluation": await evaluationForm();break;
        case "edit-evaluation": await evaluationForm(id);break;
        case "new-schedule": await scheduleForm();break;
        case "edit-schedule": await loadInterns();await scheduleForm(byId(await api("/mentor/schedules"),id));break;
        case "cancel-schedule": if(confirm("Hủy lịch mentoring này?")){await api(`/mentor/schedules/${id}`,{method:"DELETE"});toast("Đã hủy lịch.");await renderSchedules();}break;
        case "new-feedback": await feedbackForm();break;
        case "read-notification": await api(`/mentor/notifications/${id}/read`,{method:"PUT"});await renderNotifications();break;
        case "read-all": await api("/mentor/notifications/read-all",{method:"PUT"});await renderNotifications();toast("Đã đánh dấu đã đọc.");break;
      }
    } catch(error){toast(error.message);} finally {action.disabled=false;}
  });

  document.addEventListener("submit", async event => {
    const form=event.target;if(!form.matches("#taskForm,#reviewForm,#evaluationForm,#scheduleForm,#feedbackForm,#profileForm"))return;
    event.preventDefault();const button=event.submitter||form.querySelector("button[type=submit]");if(button)button.disabled=true;
    const data=new FormData(form), id=data.get("id");
    try {
      if(form.id==="taskForm"){
        const payload={internId:Number(data.get("internId")),title:data.get("title"),description:data.get("description"),deadline:data.get("deadline")||null,priority:data.get("priority")};
        if(!id && data.get("assignToAll")){
          await api("/mentor/tasks/group",{method:"POST",body:JSON.stringify({internIds:internsCache.map(intern=>intern.Id),title:payload.title,description:payload.description,deadline:payload.deadline,priority:payload.priority})});
        }else{
          if(!payload.internId)throw new Error("Chọn thực tập sinh hoặc bật tùy chọn giao cho cả nhóm.");
          await api(id?`/mentor/tasks/${id}`:"/mentor/tasks",{method:id?"PUT":"POST",body:JSON.stringify(payload)});
        }
        closeDialog();toast("Đã lưu nhiệm vụ.");await renderTasks();
      }else if(form.id==="reviewForm"){
        await api(`/mentor/reports/${id}/review`,{method:"PUT",body:JSON.stringify({status:data.get("status"),feedback:data.get("feedback"),score:data.get("score")?Number(data.get("score")):null})});
        closeDialog();toast("Đã lưu nhận xét báo cáo.");await renderReports();
      }else if(form.id==="evaluationForm"){
        const scores={};form.querySelectorAll("[data-criterion]").forEach(input=>scores[input.dataset.criterion]=Number(input.value));
        await api(`/mentor/evaluations/${data.get("internId")}`,{method:"PUT",body:JSON.stringify({scores,comments:data.get("comments"),recommendation:data.get("recommendation"),submit:event.submitter?.dataset.submit==="true"})});
        closeDialog();toast("Đã lưu đánh giá.");await renderEvaluations();
      }else if(form.id==="scheduleForm"){
        const payload={internId:Number(data.get("internId")),title:data.get("title"),content:data.get("content"),startsAt:new Date(data.get("startsAt")).toISOString(),endsAt:new Date(data.get("endsAt")).toISOString(),location:data.get("location"),notes:data.get("notes")};
        await api(id?`/mentor/schedules/${id}`:"/mentor/schedules",{method:id?"PUT":"POST",body:JSON.stringify(payload)});
        closeDialog();toast("Đã lưu lịch mentoring.");await renderSchedules();
      }else if(form.id==="feedbackForm"){
        await api("/mentor/feedback",{method:"POST",body:JSON.stringify({internId:Number(data.get("internId")),content:data.get("content"),type:data.get("type"),severity:data.get("severity")})});
        closeDialog();toast("Đã gửi feedback.");await renderFeedback();
      }else if(form.id==="profileForm"){
        await api("/mentor/profile",{method:"PUT",body:JSON.stringify({phone:data.get("phone"),avatarUrl:data.get("avatarUrl"),position:data.get("position"),department:data.get("department"),skills:data.get("skills"),experience:data.get("experience")})});
        toast("Đã cập nhật hồ sơ.");await renderProfile();
      }
    }catch(error){toast(error.message);}finally{if(button)button.disabled=false;}
  });

  document.addEventListener("input",event=>{if(event.target.id==="internSearch")filterInterns().catch(error=>toast(error.message));});
  document.addEventListener("change",event=>{if(["internStatus","internProgress"].includes(event.target.id))filterInterns().catch(error=>toast(error.message));if(event.target.id==="reportStatus")filterReports().catch(error=>toast(error.message));if(event.target.id==="assignToAll"){const target=$("#taskInternTarget");const select=target.querySelector("select");target.hidden=event.target.checked;select.required=!event.target.checked;}});
  $("#mobileMenu").addEventListener("click",()=>$("#mentorSidebar").classList.toggle("open"));
  $("#logoutBtn").addEventListener("click",()=>{localStorage.clear();location.replace("../index.html");});
  setPage("dashboard");
})();
