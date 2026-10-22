document.addEventListener("DOMContentLoaded", async () => {
  const body = document.getElementById("mentorLeaveBody");
  const modal = document.getElementById("reviewModal");
  const detail = document.getElementById("reviewDetail");
  const note = document.getElementById("reviewNote");
  const approve = document.getElementById("approveLeaveBtn");
  const reject = document.getElementById("rejectLeaveBtn");
  let selectedId = null;
  let canReview = false;
  let status = "ALL";
  const escape = value => String(value ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;");
  const toast = (message, type = "success") => AppUI.toast(message, type);
  const badge = item => `<span class="leave-badge badge-${item.status.toLowerCase()}">${escape(item.statusLabel)}</span>`;

  async function load() {
    body.innerHTML = '<tr><td colspan="5" class="empty-state">Đang tải đơn nghỉ…</td></tr>';
    try {
      const items = await API.getMentorLeaveRequests(status);
      if (!items.length) { body.innerHTML = '<tr><td colspan="5" class="empty-state">Không có đơn nghỉ phù hợp.</td></tr>'; return; }
      body.innerHTML = items.map(item => `<tr>
        <td><strong>${escape(item.internName)}</strong><small class="mentor-leave-email">${escape(item.internEmail)}</small></td>
        <td>${escape(item.startDate)}${item.startDate === item.endDate ? "" : ` → ${escape(item.endDate)}`}<small>${escape(item.durationTypeLabel)}</small></td>
        <td class="mentor-leave-reason">${escape(item.reason)}</td><td>${badge(item)}</td>
        <td><button type="button" class="btn-secondary" data-detail="${item.id}">Xem chi tiết</button></td></tr>`).join("");
    } catch (error) {
      body.innerHTML = '<tr><td colspan="5" class="empty-state">Không thể tải danh sách đơn nghỉ.</td></tr>';
      toast(error.message, "error"); Session.redirectIfExpired(error);
    }
  }

  async function openDetail(id) {
    try {
      const item = await API.getMentorLeaveRequest(id); selectedId = item.id; note.value = item.reviewerNote || "";
      detail.innerHTML = `<dl><dt>Intern</dt><dd>${escape(item.internName)} — ${escape(item.internEmail)}</dd><dt>Loại nghỉ</dt><dd>${escape(item.leaveTypeLabel)}</dd><dt>Thời gian</dt><dd>${escape(item.startDate)} → ${escape(item.endDate)} (${item.totalDays} ngày)</dd><dt>Lý do</dt><dd>${escape(item.reason)}</dd><dt>Trạng thái</dt><dd>${badge(item)}</dd></dl>`;
      const pending = item.status === "PENDING" && canReview; approve.hidden = !pending; reject.hidden = !pending; note.disabled = !pending;
      modal.hidden = false; document.getElementById("closeReviewBtn").focus();
    } catch (error) { toast(error.message, "error"); Session.redirectIfExpired(error); }
  }

  async function review(decision) {
    if (!selectedId || !canReview || approve.disabled || reject.disabled) return;
    approve.disabled = reject.disabled = true;
    try { const result = await API.reviewLeaveRequest(selectedId, decision, note.value.trim()); toast(result.message); modal.hidden = true; await load(); }
    catch (error) { toast(error.message, "error"); Session.redirectIfExpired(error); }
    finally { approve.disabled = reject.disabled = false; }
  }

  body.addEventListener("click", event => { const button = event.target.closest("[data-detail]"); if (button) openDetail(button.dataset.detail); });
  document.querySelectorAll(".filter-tab").forEach(tab => tab.addEventListener("click", () => { document.querySelectorAll(".filter-tab").forEach(x => x.classList.remove("active")); tab.classList.add("active"); status = tab.dataset.status; load(); }));
  document.getElementById("refreshLeaveBtn").addEventListener("click", load);
  document.getElementById("closeReviewBtn").addEventListener("click", () => { modal.hidden = true; });
  modal.addEventListener("click", event => { if (event.target === modal) modal.hidden = true; });
  approve.addEventListener("click", () => review("APPROVED")); reject.addEventListener("click", () => review("REJECTED"));

  try {
    const session = await API.getCurrentUser();
    if (session.user?.role !== "MENTOR" || !Session.hasPermission(session, "MENTOR_LEAVE_REQUEST_READ")) { location.href = "dashboard.html"; return; }
    canReview = Session.hasPermission(session, "LEAVE_REQUEST_REVIEW"); await load();
  } catch (error) { toast(error.message, "error"); Session.redirectIfExpired(error); }
});
