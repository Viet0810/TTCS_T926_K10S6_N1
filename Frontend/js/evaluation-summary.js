(() => {
  const state = { page: 1, pageSize: 10, selectedId: null };
  const $ = (id) => document.getElementById(id);
  const escapeHtml = (value) => String(value ?? "").replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
  const params = () => ({ page: state.page, pageSize: state.pageSize, search: $("search").value.trim(), programId: $("program").value, mentorId: $("mentor").value, status: $("status").value });
  const compactParams = () => Object.fromEntries(Object.entries(params()).filter(([, value]) => value !== "" && value != null));

  async function load() {
    $("message").textContent = "Đang tải dữ liệu…";
    try {
      const data = await API.getEvaluationSummaries(compactParams());
      $("rows").innerHTML = data.items.length ? data.items.map(item => `<tr><td><strong>${escapeHtml(item.studentCode || "—")}</strong><br>${escapeHtml(item.fullName)}<small>${escapeHtml(item.email)}</small></td><td>${escapeHtml(item.mentorName || "Chưa phân công")}</td><td>${escapeHtml(item.programName || "—")}</td><td>${item.evaluationCount}/${item.requiredEvaluationCount}</td><td><strong>${item.averageScore ?? "—"}</strong></td><td>${escapeHtml(item.temporaryClassification)}</td><td><span class="status-badge status-${item.status.toLowerCase()}">${escapeHtml(item.statusLabel)}</span></td><td><button class="btn-secondary view-detail" data-id="${item.internId}">Chi tiết</button></td></tr>`).join("") : '<tr><td colspan="8" class="empty-state">Không có dữ liệu phù hợp.</td></tr>';
      $("message").textContent = `Hiển thị ${data.items.length}/${data.totalItems} Intern`;
      renderPager(data.page, data.totalPages);
      document.querySelectorAll(".view-detail").forEach(button => button.addEventListener("click", () => openDetail(Number(button.dataset.id))));
    } catch (error) { $("message").textContent = error.message; }
  }

  function renderPager(page, totalPages) {
    $("pager").hidden = totalPages <= 0;
    $("pager").innerHTML = totalPages > 0 ? `<nav aria-label="Phân trang"><button aria-label="Trang trước" ${page <= 1 ? "disabled" : ""} data-page="${page - 1}">‹</button><span class="pagination-summary">Trang ${page}/${totalPages}</span><button aria-label="Trang sau" ${page >= totalPages ? "disabled" : ""} data-page="${page + 1}">›</button></nav>` : "";
    $("pager").querySelectorAll("button[data-page]").forEach(button => button.addEventListener("click", () => { state.page = Number(button.dataset.page); load(); }));
  }

  async function openDetail(id) {
    state.selectedId = id;
    const detail = await API.getEvaluationSummary(id);
    const item = detail.intern;
    const locked = ["FINALIZED", "PUBLISHED"].includes(item.status);
    const evaluations = detail.evaluations.length ? detail.evaluations.map(value => `<article class="mentor-evaluation"><h4>${escapeHtml(value.mentorName)} · ${escapeHtml(value.period || "Không ghi đợt")}</h4><div class="score-grid"><span>Chuyên cần <b>${value.attendance ?? "—"}</b></span><span>Thái độ <b>${value.attitude ?? "—"}</b></span><span>Chuyên môn <b>${value.professionalSkills ?? "—"}</b></span><span>Kết quả <b>${value.workResult ?? "—"}</b></span><span>Tổng <b>${value.finalScore ?? "—"}/10</b></span></div><p>${escapeHtml(value.comment || "Không có nhận xét")}</p><small>${new Date(value.createdAt).toLocaleString("vi-VN")}</small></article>`).join("") : '<p class="empty-state">Chưa có đánh giá từ Mentor.</p>';
    const history = detail.history.length ? detail.history.map(value => `<li><b>${escapeHtml(value.action)}</b> bởi ${escapeHtml(value.actorName)} · ${new Date(value.occurredAt).toLocaleString("vi-VN")}</li>`).join("") : "<li>Chưa có lịch sử.</li>";
    const cannotFinalize = item.status === "INCOMPLETE";
    $("detailContent").innerHTML = `<header><h2>${escapeHtml(item.studentCode || "")} · ${escapeHtml(item.fullName)}</h2><p>${escapeHtml(item.email)} · ${escapeHtml(item.mentorName || "Chưa có Mentor")}</p><span class="status-badge status-${item.status.toLowerCase()}">${escapeHtml(item.statusLabel)}</span></header><h3>Đánh giá gốc của Mentor</h3>${evaluations}<div class="summary-average">Điểm trung bình hệ thống: <strong>${item.averageScore ?? "—"}/100</strong> · Xếp loại tạm thời: <strong>${escapeHtml(item.temporaryClassification)}</strong></div><form id="summaryForm"><label>Nhận xét tổng hợp<textarea id="aggregateComment" maxlength="2000" required ${locked ? "disabled" : ""}>${escapeHtml(detail.aggregateComment || "")}</textarea></label><label>Xếp loại cuối cùng<select id="finalClassification" required ${locked ? "disabled" : ""}>${["Xuất sắc","Tốt","Khá","Đạt","Chưa đạt"].map(value => `<option ${detail.finalClassification === value ? "selected" : ""}>${value}</option>`).join("")}</select></label>${cannotFinalize ? `<p class="summary-warning">Cần đủ ${item.requiredEvaluationCount} đánh giá trước khi chốt. Hiện có ${item.evaluationCount}.</p>` : ""}<div class="dialog-actions">${!locked ? `<button type="button" id="saveBtn" class="btn-secondary">Lưu nháp</button><button type="button" id="finalizeBtn" class="btn-primary" ${cannotFinalize ? "disabled" : ""}>Chốt kết quả</button>` : ""}${item.status === "FINALIZED" ? '<button type="button" id="publishBtn" class="btn-primary">Công bố kết quả</button>' : ""}</div></form><h3>Lịch sử chốt và công bố</h3><ul class="summary-history">${history}</ul>`;
    if (!$("detailDialog").open) $("detailDialog").showModal();
    $("saveBtn")?.addEventListener("click", () => mutate("save"));
    $("finalizeBtn")?.addEventListener("click", () => mutate("finalize"));
    $("publishBtn")?.addEventListener("click", () => mutate("publish"));
  }

  async function mutate(action) {
    try {
      const payload = { aggregateComment: $("aggregateComment")?.value.trim(), finalClassification: $("finalClassification")?.value };
      if (action !== "publish" && !payload.aggregateComment) return alert("Vui lòng nhập nhận xét tổng hợp.");
      if (action === "save") await API.saveEvaluationSummary(state.selectedId, payload);
      if (action === "finalize") await API.finalizeEvaluationSummary(state.selectedId, payload);
      if (action === "publish") await API.publishEvaluationSummary(state.selectedId);
      await openDetail(state.selectedId); await load();
    } catch (error) { alert(error.message); }
  }

  async function init() {
    try {
      const session = await API.getCurrentUser();
      if (!["HR", "ADMIN"].includes(session.user?.role)) return location.replace("dashboard.html");
      const filters = await API.getEvaluationSummaryFilters();
      filters.filter(x => x.type === "PROGRAM").forEach(x => $("program").insertAdjacentHTML("beforeend", `<option value="${x.id}">${escapeHtml(x.name)}</option>`));
      filters.filter(x => x.type === "MENTOR").forEach(x => $("mentor").insertAdjacentHTML("beforeend", `<option value="${x.id}">${escapeHtml(x.name)}</option>`));
      await load();
    } catch (error) { if (!Session.redirectIfExpired(error)) $("message").textContent = error.message; }
  }
  $("filters").addEventListener("submit", event => { event.preventDefault(); state.page = 1; load(); });
  $("exportBtn").addEventListener("click", async () => { try { saveDownload(await API.exportEvaluationSummaries(compactParams()), "tong-hop-danh-gia.csv"); } catch (error) { alert(error.message); } });
  $("logoutBtn").addEventListener("click", Session.logout);
  init();
})();
