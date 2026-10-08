/**
 * attendance-report.js
 * Controller điều khiển giao diện Báo cáo Chuyên cần & Nghỉ phép (K10S6N1-92 / K10S6N1-60)
 */

let currentRecords = [];
let currentInterns = [];

document.addEventListener("DOMContentLoaded", async () => {
  await initSession();
  await initInternOptions();
  initDefaultDates();
  setupEventListeners();
  await loadReport();
});

async function initSession() {
  const roleBadge = document.getElementById("roleBadge");
  const logoutBtn = document.getElementById("logoutBtn");

  if (logoutBtn && typeof Session !== "undefined") {
    logoutBtn.addEventListener("click", Session.logout);
  }

  const token = localStorage.getItem("token");
  if (!token) {
    if (roleBadge) roleBadge.textContent = "HR";
    return;
  }

  try {
    if (typeof API !== "undefined" && typeof API.getCurrentUser === "function") {
      const session = await API.getCurrentUser();
      if (session?.user && roleBadge) {
        roleBadge.textContent = session.user.role || "HR";
      }
    }
  } catch (error) {
    if (roleBadge) roleBadge.textContent = "HR";
    if (typeof Session !== "undefined") {
      Session.redirectIfExpired(error);
    }
  }
}

async function initInternOptions() {
  const internSelect = document.getElementById("internFilter");
  if (!internSelect) return;

  currentInterns = await AttendanceService.getInterns();
  internSelect.innerHTML = `<option value="">Tất cả thực tập sinh (${currentInterns.length})</option>`;

  currentInterns.forEach((intern) => {
    const opt = document.createElement("option");
    opt.value = intern.id;
    opt.textContent = `${intern.mssv} - ${intern.name} (${intern.department})`;
    internSelect.appendChild(opt);
  });
}

function initDefaultDates() {
  // Mặc định chọn từ đầu tháng 10/2026 đến hôm nay
  const startDateInput = document.getElementById("startDate");
  const endDateInput = document.getElementById("endDate");

  const today = new Date();
  const year = today.getFullYear();
  const month = String(today.getMonth() + 1).padStart(2, "0");
  const day = String(today.getDate()).padStart(2, "0");

  const firstDayOfMonth = `${year}-${month}-01`;
  const todayStr = `${year}-${month}-${day}`;

  if (startDateInput && !startDateInput.value) {
    startDateInput.value = firstDayOfMonth;
  }
  if (endDateInput && !endDateInput.value) {
    endDateInput.value = todayStr;
  }
}

function setupEventListeners() {
  const filterForm = document.getElementById("filterForm");
  const exportBtn = document.getElementById("exportBtn");
  const keywordInput = document.getElementById("keywordInput");
  const closeDialogBtn = document.getElementById("closeDialogBtn");
  const closeDialogFooterBtn = document.getElementById("closeDialogFooterBtn");
  const attendanceDialog = document.getElementById("attendanceDialog");

  if (filterForm) {
    filterForm.addEventListener("submit", async (e) => {
      e.preventDefault();
      await loadReport();
    });

    filterForm.addEventListener("reset", () => {
      setTimeout(async () => {
        document.querySelectorAll(".preset-btn").forEach((btn) => btn.classList.remove("active"));
        const defaultPreset = document.querySelector('[data-preset="THIS_MONTH"]');
        if (defaultPreset) defaultPreset.classList.add("active");
        initDefaultDates();
        await loadReport();
      }, 50);
    });
  }

  // Quick Date Presets
  document.querySelectorAll(".preset-btn").forEach((btn) => {
    btn.addEventListener("click", async (e) => {
      document.querySelectorAll(".preset-btn").forEach((b) => b.classList.remove("active"));
      e.target.classList.add("active");
      const preset = e.target.getAttribute("data-preset");
      applyDatePreset(preset);
      await loadReport();
    });
  });

  // Tìm kiếm từ khóa với Debounce
  if (keywordInput) {
    let debounceTimer;
    keywordInput.addEventListener("input", () => {
      clearTimeout(debounceTimer);
      debounceTimer = setTimeout(async () => {
        await loadReport();
      }, 250);
    });
  }

  // Xuất file CSV
  if (exportBtn) {
    exportBtn.addEventListener("click", () => {
      if (currentRecords.length === 0) {
        alert("Không có dữ liệu báo cáo để xuất file.");
        return;
      }
      AttendanceService.exportCSV(currentRecords);
    });
  }

  // Đóng dialog chi tiết
  if (closeDialogBtn && attendanceDialog) {
    closeDialogBtn.addEventListener("click", () => attendanceDialog.close());
  }
  if (closeDialogFooterBtn && attendanceDialog) {
    closeDialogFooterBtn.addEventListener("click", () => attendanceDialog.close());
  }
}

function applyDatePreset(preset) {
  const startDateInput = document.getElementById("startDate");
  const endDateInput = document.getElementById("endDate");
  if (!startDateInput || !endDateInput) return;

  const today = new Date();
  const formatDate = (d) => {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, "0");
    const day = String(d.getDate()).padStart(2, "0");
    return `${y}-${m}-${day}`;
  };

  if (preset === "TODAY") {
    const todayStr = formatDate(today);
    startDateInput.value = todayStr;
    endDateInput.value = todayStr;
  } else if (preset === "THIS_WEEK") {
    const dayOfWeek = today.getDay(); // 0 is Sunday
    const distanceToMonday = (dayOfWeek + 6) % 7;
    const monday = new Date(today);
    monday.setDate(today.getDate() - distanceToMonday);

    startDateInput.value = formatDate(monday);
    endDateInput.value = formatDate(today);
  } else if (preset === "THIS_MONTH") {
    const firstDay = new Date(today.getFullYear(), today.getMonth(), 1);
    startDateInput.value = formatDate(firstDay);
    endDateInput.value = formatDate(today);
  } else if (preset === "LAST_MONTH") {
    const firstDayLastMonth = new Date(today.getFullYear(), today.getMonth() - 1, 1);
    const lastDayLastMonth = new Date(today.getFullYear(), today.getMonth(), 0);
    startDateInput.value = formatDate(firstDayLastMonth);
    endDateInput.value = formatDate(lastDayLastMonth);
  } else if (preset === "ALL") {
    startDateInput.value = "";
    endDateInput.value = "";
  }
}

async function loadReport() {
  const internId = document.getElementById("internFilter")?.value || "";
  const keyword = document.getElementById("keywordInput")?.value || "";
  const startDate = document.getElementById("startDate")?.value || "";
  const endDate = document.getElementById("endDate")?.value || "";
  const status = document.getElementById("statusFilter")?.value || "ALL";

  const result = await AttendanceService.getAttendanceReport({
    internId,
    keyword,
    startDate,
    endDate,
    status,
  });

  currentRecords = result.records;
  renderStats(result.stats);
  renderTable(currentRecords);
}

function renderStats(stats) {
  const totalEl = document.getElementById("totalShiftsCount");
  const rateEl = document.getElementById("attendanceRate");
  const lateEarlyEl = document.getElementById("lateEarlyCount");
  const leaveEl = document.getElementById("approvedLeaveCount");
  const leaveSubEl = document.getElementById("approvedLeaveSub");
  const resultCountEl = document.getElementById("reportCount");
  const exportBtn = document.getElementById("exportBtn");

  if (totalEl) totalEl.textContent = stats.totalShifts;
  if (rateEl) rateEl.textContent = stats.attendanceRate;
  if (lateEarlyEl) lateEarlyEl.textContent = stats.lateOrEarlyCount;
  if (leaveEl) leaveEl.textContent = stats.approvedLeaveCount;
  if (leaveSubEl) {
    leaveSubEl.textContent = `${stats.approvedLeaveCount} có phép • ${stats.absentCount} không phép`;
  }

  if (resultCountEl) {
    resultCountEl.textContent = `Hiển thị ${stats.totalShifts} bản ghi chuyên cần`;
  }

  if (exportBtn) {
    exportBtn.disabled = stats.totalShifts === 0;
  }
}

function renderTable(records) {
  const tbody = document.getElementById("reportTableBody");
  if (!tbody) return;

  tbody.innerHTML = "";

  if (records.length === 0) {
    tbody.innerHTML = `
      <tr>
        <td colspan="9" class="empty-state">
          Không tìm thấy bản ghi điểm danh phù hợp với bộ lọc đã chọn.
        </td>
      </tr>
    `;
    return;
  }

  records.forEach((r, idx) => {
    const statusMeta = AttendanceService.getStatusLabel(r.status);
    const tr = document.createElement("tr");

    // Format ngày hiển thị dạng DD/MM/YYYY
    const dateParts = r.date.split("-");
    const displayDate = dateParts.length === 3 ? `${dateParts[2]}/${dateParts[1]}/${dateParts[0]}` : r.date;

    tr.innerHTML = `
      <td><strong>${displayDate}</strong></td>
      <td><code>${escapeHtml(r.mssv)}</code></td>
      <td>
        <strong>${escapeHtml(r.name)}</strong>
        <div class="intern-meta">${escapeHtml(r.department)}</div>
      </td>
      <td>${escapeHtml(r.checkIn)}</td>
      <td>${escapeHtml(r.checkOut)}</td>
      <td class="work-hours">${r.hours > 0 ? `${r.hours}h` : "—"}</td>
      <td>
        <span class="status-badge ${statusMeta.className}">
          ${statusMeta.label}
        </span>
      </td>
      <td>
        <span title="${escapeHtml(r.note || "")}">
          ${escapeHtml(truncate(r.note || "Bình thường", 32))}
        </span>
      </td>
      <td>
        <button type="button" class="btn-detail" onclick="openDetailModal(${r.id})">
          Chi tiết
        </button>
      </td>
    `;

    tbody.appendChild(tr);
  });
}

window.openDetailModal = function (recordId) {
  const record = currentRecords.find((r) => r.id === recordId);
  const dialog = document.getElementById("attendanceDialog");
  if (!record || !dialog) return;

  const statusMeta = AttendanceService.getStatusLabel(record.status);
  const dateParts = record.date.split("-");
  const displayDate = dateParts.length === 3 ? `${dateParts[2]}/${dateParts[1]}/${dateParts[0]}` : record.date;

  document.getElementById("modalInternName").textContent = record.name;
  document.getElementById("modalMssv").textContent = record.mssv;
  document.getElementById("modalDepartment").textContent = record.department;
  document.getElementById("modalDate").textContent = displayDate;
  document.getElementById("modalCheckIn").textContent = record.checkIn;
  document.getElementById("modalCheckOut").textContent = record.checkOut;
  document.getElementById("modalHours").textContent = record.hours > 0 ? `${record.hours} giờ` : "0 giờ";

  const modalStatusEl = document.getElementById("modalStatus");
  if (modalStatusEl) {
    modalStatusEl.className = `status-badge ${statusMeta.className}`;
    modalStatusEl.textContent = statusMeta.label;
  }

  document.getElementById("modalNote").textContent = record.note || "Không có ghi chú thêm.";
  document.getElementById("modalApprover").textContent = record.approver || "Hệ thống tự động ghi nhận";

  dialog.showModal();
};

function escapeHtml(text) {
  const div = document.createElement("div");
  div.textContent = text == null ? "" : String(text);
  return div.innerHTML;
}

function truncate(str, maxLen) {
  if (!str) return "";
  return str.length > maxLen ? str.slice(0, maxLen) + "…" : str;
}
