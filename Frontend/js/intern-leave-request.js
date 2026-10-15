// intern-leave-request.js - Xử lý nghiệp vụ đăng ký và quản lý đơn xin nghỉ (K10S6N1-137)

document.addEventListener("DOMContentLoaded", () => {
  // 1. Kiểm tra session đăng nhập
  const token = localStorage.getItem("token");
  if (!token) {
    window.location.href = "login.html";
    return;
  }

  // 2. DOM Elements
  const form = document.getElementById("leaveRequestForm");
  const leaveTypeSelect = document.getElementById("leaveType");
  const durationTypeSelect = document.getElementById("durationType");
  const startDateInput = document.getElementById("startDate");
  const endDateInput = document.getElementById("endDate");
  const reasonInput = document.getElementById("reason");
  const charCounter = document.getElementById("charCounter");
  const previewTotalDays = document.getElementById("previewTotalDays");
  const previewDateRangeNote = document.getElementById("previewDateRangeNote");
  const submitBtn = document.getElementById("submitLeaveBtn");
  const resetBtn = document.getElementById("resetFormBtn");

  const totalCountEl = document.getElementById("totalLeaveCount");
  const pendingCountEl = document.getElementById("pendingLeaveCount");
  const approvedCountEl = document.getElementById("approvedLeaveCount");
  const rejectedCountEl = document.getElementById("rejectedLeaveCount");

  const filterTabs = document.querySelectorAll(".filter-tab");
  const tableBody = document.getElementById("leaveRequestsTableBody");
  const refreshBtn = document.getElementById("refreshListBtn");

  const cancelModal = document.getElementById("cancelModal");
  const cancelTargetIdEl = document.getElementById("cancelTargetId");
  const closeCancelModalBtn = document.getElementById("closeCancelModalBtn");
  const confirmCancelBtn = document.getElementById("confirmCancelBtn");

  // State
  let currentFilter = "ALL";
  let targetCancelId = null;
  let isSubmitting = false;

  // 3. Khởi tạo giá trị ngày mặc định (Hôm nay)
  function initDefaultDates() {
    const today = new Date();
    const yyyy = today.getFullYear();
    const mm = String(today.getMonth() + 1).padStart(2, "0");
    const dd = String(today.getDate()).padStart(2, "0");
    const todayStr = `${yyyy}-${mm}-${dd}`;

    startDateInput.value = todayStr;
    endDateInput.value = todayStr;
    updateDurationAndPreview();
  }

  // 4. Cập nhật preview thời gian nghỉ & trạng thái input EndDate
  function updateDurationAndPreview() {
    const durationType = durationTypeSelect.value;
    const startVal = startDateInput.value;

    if (durationType === "MULTI_DAY") {
      endDateInput.disabled = false;
      if (startVal) {
        endDateInput.min = startVal;
      }
      if (!endDateInput.value || endDateInput.value < startVal) {
        endDateInput.value = startVal;
      }

      if (startVal && endDateInput.value) {
        const [y1, m1, d1] = startVal.split("-").map(Number);
        const [y2, m2, d2] = endDateInput.value.split("-").map(Number);
        const dtStart = new Date(y1, m1 - 1, d1);
        const dtEnd = new Date(y2, m2 - 1, d2);
        if (!isNaN(dtStart.getTime()) && !isNaN(dtEnd.getTime()) && dtEnd >= dtStart) {
          const diffDays = Math.round((dtEnd - dtStart) / (1000 * 60 * 60 * 24)) + 1;
          previewTotalDays.textContent = `${diffDays.toFixed(1)} ngày công`;
          previewDateRangeNote.textContent = `(Từ ${startVal} đến ${endDateInput.value})`;
        } else {
          previewTotalDays.textContent = "—";
          previewDateRangeNote.textContent = "(Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu)";
        }
      }
    } else {
      endDateInput.disabled = true;
      endDateInput.removeAttribute("min");
      endDateInput.value = startVal;

      if (durationType === "FULL_DAY") {
        previewTotalDays.textContent = "1.0 ngày công";
        previewDateRangeNote.textContent = `(Cả ngày: ${startVal})`;
      } else if (durationType === "MORNING") {
        previewTotalDays.textContent = "0.5 ngày công";
        previewDateRangeNote.textContent = `(Ca sáng 08:00 - 12:00: ${startVal})`;
      } else if (durationType === "AFTERNOON") {
        previewTotalDays.textContent = "0.5 ngày công";
        previewDateRangeNote.textContent = `(Ca chiều 13:30 - 17:30: ${startVal})`;
      }
    }
  }

  // 5. Cập nhật bộ đếm ký tự cho lý do
  function updateCharCount() {
    const length = reasonInput.value.trim().length;
    charCounter.textContent = `${length} / 1000 ký tự (tối thiểu 10)`;

    if (length < 10) {
      charCounter.classList.remove("limit-near", "limit-reached");
      charCounter.style.color = "#64748b";
    } else if (length >= 950) {
      charCounter.classList.add("limit-reached");
      charCounter.classList.remove("limit-near");
      charCounter.style.color = "#ef4444";
    } else if (length >= 800) {
      charCounter.classList.add("limit-near");
      charCounter.classList.remove("limit-reached");
      charCounter.style.color = "#f59e0b";
    } else {
      charCounter.classList.remove("limit-near", "limit-reached");
      charCounter.style.color = "#10b981";
    }
  }

  // 6. Xử lý sự kiện form
  durationTypeSelect.addEventListener("change", updateDurationAndPreview);
  startDateInput.addEventListener("change", updateDurationAndPreview);
  endDateInput.addEventListener("change", updateDurationAndPreview);
  reasonInput.addEventListener("input", updateCharCount);

  resetBtn.addEventListener("click", () => {
    form.reset();
    initDefaultDates();
    updateCharCount();
  });

  // 7. Gửi đơn xin nghỉ phép
  form.addEventListener("submit", async (e) => {
    e.preventDefault();
    if (isSubmitting) return;

    const leaveType = leaveTypeSelect.value;
    const durationType = durationTypeSelect.value;
    const startDate = startDateInput.value;
    const endDate = endDateInput.value;
    const reason = reasonInput.value.trim();

    // Client-side validation
    if (!startDate) {
      showAlert("Vui lòng chọn ngày bắt đầu nghỉ.", "warning");
      startDateInput.focus();
      return;
    }

    if (durationType === "MULTI_DAY") {
      if (!endDate) {
        showAlert("Vui lòng chọn ngày kết thúc nghỉ.", "warning");
        endDateInput.focus();
        return;
      }
      if (endDate < startDate) {
        showAlert("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.", "warning");
        endDateInput.focus();
        return;
      }
    }

    if (reason.length < 10) {
      showAlert("Lý do xin nghỉ bắt buộc phải có ít nhất 10 ký tự.", "warning");
      reasonInput.focus();
      return;
    }

    if (reason.length > 1000) {
      showAlert("Lý do xin nghỉ không được vượt quá 1000 ký tự.", "warning");
      reasonInput.focus();
      return;
    }

    // Submit API
    try {
      setSubmitLoading(true);
      const payload = {
        leaveType,
        durationType,
        startDate,
        endDate,
        reason
      };

      const result = await API.createLeaveRequest(payload);
      showAlert("Đã gửi đơn xin nghỉ phép thành công! Đơn của bạn đang chờ phê duyệt.", "success");

      // Reset form & tự động chuyển về tab 'Tất cả' để hiển thị ngay đơn vừa tạo
      form.reset();
      initDefaultDates();
      updateCharCount();

      filterTabs.forEach((t) => t.classList.remove("active"));
      const allTab = document.querySelector('.filter-tab[data-status="ALL"]');
      if (allTab) allTab.classList.add("active");
      currentFilter = "ALL";

      await loadLeaveRequests();
    } catch (error) {
      showAlert(error.message || "Không thể tạo đơn xin nghỉ phép. Vui lòng kiểm tra lại thông tin.", "danger");
    } finally {
      setSubmitLoading(false);
    }
  });

  function setSubmitLoading(loading) {
    isSubmitting = loading;
    submitBtn.disabled = loading;
    const btnText = submitBtn.querySelector(".btn-text");
    if (btnText) {
      btnText.textContent = loading ? "Đang gửi đơn…" : "Gửi đơn xin nghỉ phép";
    }
  }

  // 8. Tải danh sách đơn xin nghỉ phép
  async function loadLeaveRequests() {
    try {
      tableBody.innerHTML = `
        <tr>
          <td colspan="8" style="text-align: center; padding: 24px; color: #64748b;">
            Đang tải dữ liệu đơn nghỉ phép…
          </td>
        </tr>
      `;

      const params = {
        status: currentFilter,
        page: 1,
        pageSize: 50
      };

      const response = await API.getLeaveRequests(params);

      // Cập nhật thẻ thống kê
      updateStats(response);

      // Render danh sách bảng
      renderTable(response.items || []);
    } catch (error) {
      tableBody.innerHTML = `
        <tr>
          <td colspan="8" style="text-align: center; padding: 24px; color: #ef4444;">
            Không thể tải danh sách đơn nghỉ phép: ${escapeHtml(error.message || "Lỗi máy chủ")}
          </td>
        </tr>
      `;
    }
  }

  function updateStats(data) {
    const pending = data.pendingCount || 0;
    const approved = data.approvedCount || 0;
    const rejected = data.rejectedCount || 0;
    const cancelled = data.cancelledCount || 0;
    const total = pending + approved + rejected + cancelled;

    totalCountEl.textContent = total;
    pendingCountEl.textContent = pending;
    approvedCountEl.textContent = approved;
    rejectedCountEl.textContent = rejected;
  }

  function renderTable(items) {
    if (!items || items.length === 0) {
      tableBody.innerHTML = `
        <tr>
          <td colspan="8">
            <div class="leave-empty-state">
              <div class="leave-empty-icon">📭</div>
              <div class="leave-empty-title">Chưa có đơn xin nghỉ phép nào</div>
              <p>Bạn chưa nộp đơn nào hoặc không có đơn nào phù hợp với bộ lọc hiện tại.</p>
            </div>
          </td>
        </tr>
      `;
      return;
    }

    tableBody.innerHTML = items.map((item) => {
      const statusBadge = getStatusBadge(item.status, item.statusLabel);
      const isPending = item.status === "PENDING";
      const actionHtml = isPending
        ? `<button type="button" class="btn-action-cancel" data-id="${item.id}" title="Hủy đơn xin nghỉ này">Hủy đơn</button>`
        : `<span style="color: #94a3b8; font-size: 0.85rem;">—</span>`;

      let reviewerHtml = "—";
      if (item.reviewerName) {
        reviewerHtml = `<strong>${escapeHtml(item.reviewerName)}</strong>`;
      }
      if (item.reviewerNote) {
        reviewerHtml += `<div class="reviewer-feedback" title="Nhận xét của người duyệt">💬 ${escapeHtml(item.reviewerNote)}</div>`;
      }

      const dateRangeText = item.startDate === item.endDate
        ? item.startDate
        : `${item.startDate} → ${item.endDate}`;

      return `
        <tr>
          <td><span style="font-family: monospace; font-weight: 600; color: #475569;">#${item.id}</span></td>
          <td><strong>${escapeHtml(item.leaveTypeLabel || item.leaveType)}</strong></td>
          <td>
            <div>${dateRangeText}</div>
            <small style="color: #64748b;">${escapeHtml(item.durationTypeLabel || item.durationType)}</small>
          </td>
          <td><span style="font-weight: 700; color: #2563eb;">${Number(item.totalDays ?? 0).toFixed(1)}</span> ngày</td>
          <td style="max-width: 250px; word-break: break-word;">${escapeHtml(item.reason)}</td>
          <td>${statusBadge}</td>
          <td>${reviewerHtml}</td>
          <td style="text-align: right;">${actionHtml}</td>
        </tr>
      `;
    }).join("");

    // Gắn sự kiện hủy đơn cho các nút trên bảng
    tableBody.querySelectorAll(".btn-action-cancel").forEach((btn) => {
      btn.addEventListener("click", () => {
        const id = btn.getAttribute("data-id");
        openCancelModal(id);
      });
    });
  }

  function getStatusBadge(status, label) {
    const text = label || status;
    switch (status) {
      case "PENDING":
        return `<span class="leave-badge badge-pending">⏳ ${escapeHtml(text)}</span>`;
      case "APPROVED":
        return `<span class="leave-badge badge-approved">✅ ${escapeHtml(text)}</span>`;
      case "REJECTED":
        return `<span class="leave-badge badge-rejected">❌ ${escapeHtml(text)}</span>`;
      case "CANCELLED":
        return `<span class="leave-badge badge-cancelled">⚪ ${escapeHtml(text)}</span>`;
      default:
        return `<span class="leave-badge">${escapeHtml(text)}</span>`;
    }
  }

  // 9. Bộ lọc trạng thái
  filterTabs.forEach((tab) => {
    tab.addEventListener("click", () => {
      filterTabs.forEach((t) => t.classList.remove("active"));
      tab.classList.add("active");
      currentFilter = tab.getAttribute("data-status");
      loadLeaveRequests();
    });
  });

  refreshBtn.addEventListener("click", loadLeaveRequests);

  // 10. Modal hủy đơn
  function openCancelModal(id) {
    targetCancelId = id;
    cancelTargetIdEl.textContent = `#${id}`;
    cancelModal.style.display = "flex";
  }

  function closeCancelModal() {
    targetCancelId = null;
    cancelModal.style.display = "none";
  }

  closeCancelModalBtn.addEventListener("click", closeCancelModal);
  cancelModal.addEventListener("click", (e) => {
    if (e.target === cancelModal) closeCancelModal();
  });
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && cancelModal.style.display === "flex") {
      closeCancelModal();
    }
  });

  confirmCancelBtn.addEventListener("click", async () => {
    if (!targetCancelId) return;

    try {
      confirmCancelBtn.disabled = true;
      confirmCancelBtn.textContent = "Đang hủy…";
      await API.cancelLeaveRequest(targetCancelId);
      closeCancelModal();
      showAlert("Đã hủy đơn xin nghỉ phép thành công.", "success");
      await loadLeaveRequests();
    } catch (error) {
      showAlert(error.message || "Không thể hủy đơn. Vui lòng thử lại.", "danger");
    } finally {
      confirmCancelBtn.disabled = false;
      confirmCancelBtn.textContent = "Hủy đơn ngay";
    }
  });

  // 11. Hàm tiện ích hiển thị Alert / Toast
  function showAlert(message, type = "info") {
    const toastType = type === "danger" ? "error" : type;
    if (window.AppUI && typeof AppUI.toast === "function") {
      AppUI.toast(message, toastType);
      return;
    }
    if (window.AppUI && typeof AppUI.notify === "function") {
      AppUI.notify(message, toastType);
      return;
    }
    // Fallback: alert
    alert(message);
  }

  function escapeHtml(str) {
    if (!str) return "";
    return String(str)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;")
      .replace(/'/g, "&#039;");
  }

  // 12. Chạy khởi tạo ban đầu
  initDefaultDates();
  updateCharCount();
  loadLeaveRequests();
});
