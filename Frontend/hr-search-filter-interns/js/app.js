/**
 * Controller: Điều khiển sự kiện giao diện, tìm kiếm, lọc trường/ngành, modal và render
 */

const AppState = {
  filters: {
    search: "",
    school: "",
    major: "",
    status: "",
    batch: "",
    mentor: ""
  },
  viewMode: "table",
  selectedIds: new Set(),
  currentViewingId: null,
  currentPage: 1,
  pageSize: 10
};

document.addEventListener("DOMContentLoaded", () => {
  setupEventListeners();
  initDropdownOptions().then(renderApp).catch(error => showToast(error.message || "Không tải được danh mục."));
});

async function initDropdownOptions() {
  const filterSchool = document.getElementById("filter-school");
  const filterMajor = document.getElementById("filter-major");
  const filterMentor = document.getElementById("filter-mentor");
  const filterBatch = document.getElementById("filter-batch");

  // Modal thêm mới
  const modalSchool = document.getElementById("modal-school");
  const modalMajor = document.getElementById("modal-major");
  const modalMentor = document.getElementById("modal-mentor");
  const modalBatch = document.getElementById("modal-batch");

  // Modal chỉnh sửa
  const editSchool = document.getElementById("edit-school");
  const editMajor = document.getElementById("edit-major");
  const editMentor = document.getElementById("edit-mentor");

  // Xóa options cũ (nếu có) và nạp danh mục đồng bộ
  if (editSchool) editSchool.innerHTML = `<option value="">-- Chọn trường đại học --</option>`;
  if (editMajor) editMajor.innerHTML = `<option value="">-- Chọn chuyên ngành --</option>`;
  if (editMentor) editMentor.innerHTML = `<option value="">Chưa gán Mentor</option>`;

  MOCK_UNIVERSITIES.forEach(u => {
    const opt = `<option value="${u.name}">${u.name} (${u.shortName})</option>`;
    if (filterSchool) filterSchool.innerHTML += opt;
    if (modalSchool) modalSchool.innerHTML += opt;
    if (editSchool) editSchool.innerHTML += opt;
  });

  MOCK_MAJORS.forEach(m => {
    const opt = `<option value="${m}">${m}</option>`;
    if (filterMajor) filterMajor.innerHTML += opt;
    if (modalMajor) modalMajor.innerHTML += opt;
    if (editMajor) editMajor.innerHTML += opt;
  });

  const mentors = await InternService.getMentors();
  mentors.forEach(m => {
    const name = typeof m === "string" ? m : m.name;
    const opt = `<option value="${name}">${name}</option>`;
    if (filterMentor) filterMentor.innerHTML += opt;
    if (modalMentor) modalMentor.innerHTML += opt;
    if (editMentor) editMentor.innerHTML += opt;
  });

  MOCK_BATCHES.forEach(b => {
    const opt = `<option value="${b}">${b}</option>`;
    if (filterBatch) filterBatch.innerHTML += opt;
    if (modalBatch) modalBatch.innerHTML += opt;
  });
}

function setupEventListeners() {
  const searchInput = document.getElementById("search-input");
  let debounceTimeout = null;

  if (searchInput) {
    searchInput.addEventListener("input", (e) => {
      clearTimeout(debounceTimeout);
      debounceTimeout = setTimeout(() => {
        AppState.filters.search = e.target.value.trim();
        const clearBtn = document.getElementById("clear-search-btn");
        if (AppState.filters.search) {
          clearBtn.classList.remove("hidden");
        } else {
          clearBtn.classList.add("hidden");
        }
        AppState.currentPage = 1;
        renderApp();
      }, 250);
    });
  }

  document.addEventListener("keydown", (e) => {
    if ((e.ctrlKey && e.key === "k") || (e.key === "/" && document.activeElement.tagName !== "INPUT")) {
      e.preventDefault();
      if (searchInput) searchInput.focus();
    }
  });
}

async function renderApp() {
  const filteredList = await InternService.getInterns(AppState.filters);
  renderKPIs();
  renderCounters(filteredList.length);
  renderPagination(filteredList.length);
  renderActiveFilterChips();

  if (AppState.viewMode === "table") {
    renderTable(filteredList);
  } else {
    renderGrid(filteredList);
  }
}

function renderTable(list) {
  const tbody = document.getElementById("intern-tbody");
  const emptyState = document.getElementById("empty-state");
  if (!tbody) return;

  if (list.length === 0) {
    tbody.innerHTML = "";
    emptyState.classList.remove("hidden");
    return;
  }
  emptyState.classList.add("hidden");

  const startIndex = (AppState.currentPage - 1) * AppState.pageSize;
  const pageItems = list.slice(startIndex, startIndex + AppState.pageSize);

  tbody.innerHTML = pageItems.map((item) => {
    const isChecked = AppState.selectedIds.has(item.id) ? "checked" : "";
    const statusBadge = getStatusBadge(item.status);
    const schoolBadge = getSchoolBadge(item.schoolShort);

    return `
      <tr class="hover:bg-slate-50/80 transition-colors ${AppState.selectedIds.has(item.id) ? 'bg-indigo-50/30' : ''}">
        <td class="py-3.5 px-4 text-center">
          <input type="checkbox" ${isChecked} onchange="handleRowSelect(${item.id}, this)" class="w-4 h-4 text-indigo-600 border-slate-300 rounded focus:ring-indigo-500 cursor-pointer">
        </td>
        <td class="py-3.5 px-4">
          <div class="flex items-center space-x-3">
            <img class="w-10 h-10 rounded-full object-cover ring-1 ring-slate-200" src="${item.avatar}" alt="${item.name}">
            <div>
              <button onclick="openDetailDrawer(${item.id})" class="font-semibold text-slate-900 hover:text-indigo-600 transition-colors text-left flex items-center gap-1 group">
                ${item.name}
                <svg class="w-3.5 h-3.5 text-slate-400 group-hover:text-indigo-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/></svg>
              </button>
              <div class="text-xs text-slate-500 font-mono flex items-center gap-1.5 mt-0.5">
                <span>${item.mssv}</span>
                <span>•</span>
                <span class="truncate max-w-[140px]" title="${item.email}">${item.email}</span>
              </div>
            </div>
          </div>
        </td>
        <td class="py-3.5 px-4">
          <div class="flex items-center space-x-1.5">
            ${schoolBadge}
            <span class="text-xs font-medium text-slate-800">${item.school}</span>
          </div>
        </td>
        <td class="py-3.5 px-4">
          <span class="text-xs font-medium text-slate-800 bg-slate-100 px-2 py-1 rounded-md border border-slate-200">${item.major}</span>
        </td>
        <td class="py-3.5 px-4">
          <div>
            <p class="text-xs font-semibold text-slate-800">${item.role}</p>
            <p class="text-xs text-slate-400">${item.batch.split('(')[0].trim()}</p>
          </div>
        </td>
        <td class="py-3.5 px-4">
          <span class="text-xs text-slate-700 font-medium">${item.mentor || '<i class="text-slate-400">Chưa gán</i>'}</span>
        </td>
        <td class="py-3.5 px-4">
          ${statusBadge}
        </td>
        <td class="py-3.5 px-4 text-center">
          <div class="w-20 mx-auto">
            <div class="flex justify-between text-2xs font-semibold text-slate-500 mb-1">
              <span>${item.progress}%</span>
            </div>
            <div class="w-full bg-slate-200 rounded-full h-1.5 overflow-hidden">
              <div class="${getProgressColor(item.progress)} h-1.5 rounded-full" style="width: ${item.progress}%"></div>
            </div>
          </div>
        </td>
        <td class="py-3.5 px-4 text-right">
          <div class="flex items-center justify-end space-x-1">
            <button onclick="openDetailDrawer(${item.id})" class="p-1.5 text-slate-500 hover:text-indigo-600 hover:bg-slate-100 rounded-lg transition" title="Xem chi tiết hồ sơ">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/></svg>
            </button>
            <button onclick="openEditModal(${item.id})" class="p-1.5 text-slate-500 hover:text-amber-600 hover:bg-slate-100 rounded-lg transition" title="Chỉnh sửa thông tin">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z"/></svg>
            </button>
            <button onclick="deleteInternConfirm(${item.id})" class="p-1.5 text-slate-400 hover:text-rose-600 hover:bg-slate-100 rounded-lg transition" title="Xóa">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>
            </button>
          </div>
        </td>
      </tr>
    `;
  }).join('');
}

function renderGrid(list) {
  const gridContainer = document.getElementById("grid-container");
  const emptyState = document.getElementById("empty-state");
  if (!gridContainer) return;

  if (list.length === 0) {
    gridContainer.innerHTML = "";
    emptyState.classList.remove("hidden");
    return;
  }
  emptyState.classList.add("hidden");

  gridContainer.innerHTML = list.map(item => `
    <div class="bg-white rounded-xl border border-slate-200 p-5 shadow-xs hover:shadow-md hover:border-slate-300 transition-all flex flex-col justify-between">
      <div>
        <div class="flex items-start justify-between">
          <div class="flex items-center space-x-3">
            <img class="w-12 h-12 rounded-full object-cover ring-2 ring-indigo-50" src="${item.avatar}" alt="${item.name}">
            <div>
              <h4 onclick="openDetailDrawer(${item.id})" class="font-bold text-slate-900 hover:text-indigo-600 cursor-pointer transition">${item.name}</h4>
              <p class="text-xs text-slate-500 font-mono">${item.mssv}</p>
            </div>
          </div>
          ${getStatusBadge(item.status)}
        </div>

        <div class="mt-4 pt-3 border-t border-slate-100 space-y-2 text-xs">
          <div class="flex items-center justify-between text-slate-600">
            <span class="text-slate-400">Trường:</span>
            <span class="font-medium text-slate-800 text-right">${item.schoolShort || item.school}</span>
          </div>
          <div class="flex items-center justify-between text-slate-600">
            <span class="text-slate-400">Ngành:</span>
            <span class="font-medium text-slate-800 text-right">${item.major}</span>
          </div>
          <div class="flex items-center justify-between text-slate-600">
            <span class="text-slate-400">Vị trí:</span>
            <span class="font-semibold text-indigo-600">${item.role}</span>
          </div>
          <div class="flex items-center justify-between text-slate-600">
            <span class="text-slate-400">Mentor:</span>
            <span class="font-medium text-slate-700">${item.mentor || 'Chưa gán'}</span>
          </div>
        </div>
      </div>

      <div class="mt-4 pt-3 border-t border-slate-100">
        <div class="flex justify-between text-xs text-slate-500 mb-1">
          <span>Tiến độ thực tập</span>
          <span class="font-bold text-slate-800">${item.progress}%</span>
        </div>
        <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
          <div class="${getProgressColor(item.progress)} h-1.5 rounded-full" style="width: ${item.progress}%"></div>
        </div>
        <div class="mt-3 flex gap-2">
          <button onclick="openDetailDrawer(${item.id})" class="flex-1 py-1.5 text-xs font-semibold text-slate-700 bg-slate-50 hover:bg-slate-100 border border-slate-200 rounded-lg transition text-center">
            Xem hồ sơ
          </button>
          <a href="tel:${item.phone}" class="px-3 py-1.5 text-xs font-medium text-indigo-600 bg-indigo-50 hover:bg-indigo-100 rounded-lg transition flex items-center justify-center">
            📞
          </a>
        </div>
      </div>
    </div>
  `).join('');
}

function renderKPIs() {
  const stats = InternService.getKPIStats();
  document.getElementById("kpi-total").textContent = stats.total;
  document.getElementById("kpi-active").textContent = stats.active;
  document.getElementById("kpi-pending").textContent = stats.pending;
  document.getElementById("kpi-completed").textContent = stats.completed;
}

function renderCounters(totalCount) {
  document.getElementById("total-badge").textContent = `${totalCount} hồ sơ`;
  document.getElementById("result-count").textContent = totalCount;
  document.getElementById("page-total").textContent = totalCount;

  if (totalCount === 0) {
    document.getElementById("page-start").textContent = 0;
    document.getElementById("page-end").textContent = 0;
  } else {
    const start = (AppState.currentPage - 1) * AppState.pageSize + 1;
    const end = Math.min(AppState.currentPage * AppState.pageSize, totalCount);
    document.getElementById("page-start").textContent = start;
    document.getElementById("page-end").textContent = end;
  }
}

/**
 * Điều khiển phân trang động (Dynamic Pagination)
 */
function renderPagination(totalCount) {
  const container = document.getElementById("pagination-controls");
  if (!container) return;

  const totalPages = Math.ceil(totalCount / AppState.pageSize) || 1;
  let html = "";

  // Nút Trước
  const prevDisabled = AppState.currentPage <= 1;
  html += `
    <button onclick="goToPage(${AppState.currentPage - 1})" ${prevDisabled ? 'disabled' : ''} class="px-2.5 py-1.5 rounded border border-slate-200 ${prevDisabled ? 'bg-slate-50 text-slate-400 cursor-not-allowed' : 'bg-white text-slate-700 hover:bg-slate-100 cursor-pointer'} transition">
      Trước
    </button>
  `;

  // Các nút số trang
  for (let i = 1; i <= totalPages; i++) {
    const isActive = i === AppState.currentPage;
    html += `
      <button onclick="goToPage(${i})" class="px-3 py-1.5 rounded border ${isActive ? 'border-indigo-600 bg-indigo-600 text-white font-medium' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-100'} transition cursor-pointer">
        ${i}
      </button>
    `;
  }

  // Nút Sau
  const nextDisabled = AppState.currentPage >= totalPages;
  html += `
    <button onclick="goToPage(${AppState.currentPage + 1})" ${nextDisabled ? 'disabled' : ''} class="px-2.5 py-1.5 rounded border border-slate-200 ${nextDisabled ? 'bg-slate-50 text-slate-400 cursor-not-allowed' : 'bg-white text-slate-700 hover:bg-slate-100 cursor-pointer'} transition">
      Sau
    </button>
  `;

  container.innerHTML = html;
}

function goToPage(page) {
  AppState.currentPage = page;
  renderApp();
}

function renderActiveFilterChips() {
  const wrapper = document.getElementById("chips-wrapper");
  if (!wrapper) return;

  const chips = [];
  if (AppState.filters.search) chips.push({ key: 'search', label: `Tìm: "${AppState.filters.search}"` });
  if (AppState.filters.school) chips.push({ key: 'school', label: `Trường: ${AppState.filters.school}` });
  if (AppState.filters.major) chips.push({ key: 'major', label: `Ngành: ${AppState.filters.major}` });
  if (AppState.filters.status) chips.push({ key: 'status', label: `Trạng thái: ${AppState.filters.status}` });
  if (AppState.filters.batch) chips.push({ key: 'batch', label: `Đợt: ${AppState.filters.batch.split('(')[0]}` });
  if (AppState.filters.mentor) chips.push({ key: 'mentor', label: `Mentor: ${AppState.filters.mentor}` });

  if (chips.length === 0) {
    wrapper.innerHTML = `<span class="text-slate-400 italic">Chưa chọn bộ lọc nào (Hiển thị toàn bộ)</span>`;
  } else {
    wrapper.innerHTML = chips.map(c => `
      <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-indigo-50 text-indigo-700 border border-indigo-200">
        ${c.label}
        <button onclick="removeFilter('${c.key}')" class="ml-1.5 text-indigo-400 hover:text-indigo-700 focus:outline-hidden">
          <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
        </button>
      </span>
    `).join('') + `
      <button onclick="resetAllFilters()" class="text-xs text-rose-600 hover:underline font-medium ml-1">Xóa tất cả</button>
    `;
  }
}

function handleFilterChange() {
  AppState.filters.school = document.getElementById("filter-school").value;
  AppState.filters.major = document.getElementById("filter-major").value;
  AppState.filters.status = document.getElementById("filter-status").value;
  AppState.filters.batch = document.getElementById("filter-batch").value;
  AppState.filters.mentor = document.getElementById("filter-mentor").value;
  AppState.currentPage = 1;
  renderApp();
}

function removeFilter(key) {
  if (key === 'search') {
    clearSearch();
  } else {
    AppState.filters[key] = "";
    const el = document.getElementById(`filter-${key}`);
    if (el) el.value = "";
    renderApp();
  }
}

function resetAllFilters() {
  document.getElementById("search-input").value = "";
  document.getElementById("filter-school").value = "";
  document.getElementById("filter-major").value = "";
  document.getElementById("filter-status").value = "";
  document.getElementById("filter-batch").value = "";
  document.getElementById("filter-mentor").value = "";
  document.getElementById("clear-search-btn").classList.add("hidden");

  AppState.filters = { search: "", school: "", major: "", status: "", batch: "", mentor: "" };
  AppState.currentPage = 1;
  renderApp();
  showToast("Đã đặt lại toàn bộ bộ lọc!");
}

function clearSearch() {
  document.getElementById("search-input").value = "";
  AppState.filters.search = "";
  document.getElementById("clear-search-btn").classList.add("hidden");
  renderApp();
}

function toggleAdvancedFilter() {
  const el = document.getElementById("advanced-filters");
  const btn = document.getElementById("btn-advanced");
  if (el.classList.contains("hidden")) {
    el.classList.remove("hidden");
    btn.classList.add("bg-indigo-50", "text-indigo-700", "border-indigo-200");
  } else {
    el.classList.add("hidden");
    btn.classList.remove("bg-indigo-50", "text-indigo-700", "border-indigo-200");
  }
}

function setViewMode(mode) {
  AppState.viewMode = mode;
  const tableBox = document.getElementById("table-container");
  const gridBox = document.getElementById("grid-container");
  const btnTable = document.getElementById("btn-view-table");
  const btnGrid = document.getElementById("btn-view-grid");

  if (mode === "table") {
    tableBox.classList.remove("hidden");
    gridBox.classList.add("hidden");
    btnTable.className = "p-1.5 rounded-md bg-white text-indigo-600 shadow-xs cursor-pointer";
    btnGrid.className = "p-1.5 rounded-md text-slate-500 hover:text-slate-900 cursor-pointer";
  } else {
    tableBox.classList.add("hidden");
    gridBox.classList.remove("hidden");
    btnGrid.className = "p-1.5 rounded-md bg-white text-indigo-600 shadow-xs cursor-pointer";
    btnTable.className = "p-1.5 rounded-md text-slate-500 hover:text-slate-900 cursor-pointer";
  }
  renderApp();
}

function handleRowSelect(id, checkbox) {
  if (checkbox.checked) {
    AppState.selectedIds.add(id);
  } else {
    AppState.selectedIds.delete(id);
  }
  updateSelectionUI();
}

async function toggleSelectAll(masterCheckbox) {
  const list = await InternService.getInterns(AppState.filters);
  if (masterCheckbox.checked) {
    list.forEach(item => AppState.selectedIds.add(item.id));
  } else {
    AppState.selectedIds.clear();
  }
  renderApp();
  updateSelectionUI();
}

function updateSelectionUI() {
  const count = AppState.selectedIds.size;
  document.getElementById("selected-count").textContent = count;
  const batchBox = document.getElementById("batch-actions-box");
  const masterCheck = document.getElementById("select-all-checkbox");

  if (count > 0) {
    batchBox.classList.remove("hidden");
    batchBox.classList.add("flex");
  } else {
    batchBox.classList.add("hidden");
    batchBox.classList.remove("flex");
    if (masterCheck) masterCheck.checked = false;
  }
}

async function handleBatchAction(action) {
  if (AppState.selectedIds.size === 0) return;
  const ids = Array.from(AppState.selectedIds);

  if (action === 'status') {
    await InternService.bulkUpdateStatus(ids, "Đang thực tập");
    showToast(`Đã duyệt thực tập cho ${ids.length} sinh viên được chọn!`);
    AppState.selectedIds.clear();
    updateSelectionUI();
    renderApp();
  } else if (action === 'email') {
    showToast(`Đang mở giao diện gửi email tới ${ids.length} sinh viên...`);
  } else if (action === 'mentor') {
    const mentors = await InternService.getMentors();
    if (!mentors.length) {
      showToast("Chưa có tài khoản Mentor. Hãy nhờ Admin tạo tài khoản Mentor trước.");
      return;
    }
    const promptText = mentors.map((mentor, index) => `${index + 1}. ${mentor.name} · ${mentor.email}`).join("\n");
    const selection = prompt(`Nhập số thứ tự Mentor muốn phân công:\n${promptText}`);
    if (selection) {
      const mentor = mentors[Number(selection) - 1];
      if (!mentor) {
        showToast("Số thứ tự Mentor không hợp lệ.");
        return;
      }
      const result = await InternService.bulkAssignMentor(ids, mentor.id);
      showToast(result.message || `Đã gán ${ids.length} thực tập sinh cho ${mentor.name}.`);
      AppState.selectedIds.clear();
      updateSelectionUI();
      await renderApp();
    }
  }
}

async function openDetailDrawer(id) {
  AppState.currentViewingId = id;
  const intern = await InternService.getInternById(id);
  if (!intern) return;

  document.getElementById("drawer-name").textContent = intern.name;
  document.getElementById("drawer-full-name").textContent = intern.name;
  document.getElementById("drawer-mssv").textContent = `MSSV: ${intern.mssv}`;
  document.getElementById("drawer-avatar").src = intern.avatar;
  document.getElementById("drawer-school").textContent = intern.school;
  document.getElementById("drawer-major").textContent = intern.major;
  document.getElementById("drawer-gpa").textContent = intern.gpa;
  document.getElementById("drawer-year").textContent = intern.year || "Năm 4";
  document.getElementById("drawer-role").textContent = intern.role;
  document.getElementById("drawer-batch").textContent = intern.batch;
  document.getElementById("drawer-mentor").textContent = intern.mentor || "Chưa phân công";
  document.getElementById("drawer-progress-val").textContent = `${intern.progress}%`;
  document.getElementById("drawer-email").textContent = intern.email;
  document.getElementById("drawer-phone").textContent = intern.phone;
  document.getElementById("drawer-notes").textContent = intern.notes || "Chưa có ghi chú.";

  const badgeEl = document.getElementById("drawer-status-badge");
  if (badgeEl) {
    badgeEl.innerHTML = getStatusBadge(intern.status);
  }

  document.getElementById("detail-drawer").classList.remove("hidden");
}

function closeDetailDrawer() {
  document.getElementById("detail-drawer").classList.add("hidden");
  AppState.currentViewingId = null;
}

function openAddModal() {
  const form = document.getElementById("add-intern-form");
  if (form) form.reset();
  document.getElementById("add-modal").classList.remove("hidden");
}

function closeAddModal() {
  document.getElementById("add-modal").classList.add("hidden");
}

async function handleAddInternSubmit(event) {
  event.preventDefault();
  const newIntern = {
    name: document.getElementById("modal-name").value.trim(),
    mssv: document.getElementById("modal-mssv").value.trim(),
    email: document.getElementById("modal-email").value.trim(),
    phone: document.getElementById("modal-phone").value.trim(),
    school: document.getElementById("modal-school").value,
    major: document.getElementById("modal-major").value,
    role: document.getElementById("modal-role").value.trim() || "Thực tập sinh",
    batch: document.getElementById("modal-batch").value,
    mentor: document.getElementById("modal-mentor").value,
    status: document.getElementById("modal-status").value,
    gpa: document.getElementById("modal-gpa").value.trim() || "3.5 / 4.0",
    progress: Number(document.getElementById("modal-progress").value) || 0,
    notes: document.getElementById("modal-notes").value.trim()
  };

  if (!newIntern.name || !newIntern.mssv || !newIntern.school || !newIntern.major) {
    alert("Vui lòng điền các trường bắt buộc (Tên, MSSV, Trường, Ngành)!");
    return;
  }

  await InternService.addIntern(newIntern);
  closeAddModal();
  showToast(`Đã tiếp nhận thành công thực tập sinh: ${newIntern.name}`);
  renderApp();
}

async function openEditModal(id) {
  let intern;
  try {
    intern = await InternService.getInternById(id);
  } catch (error) {
    showToast(error.message || "Không tải được hồ sơ.");
    return;
  }
  if (!intern) return;

  document.getElementById("edit-id").value = intern.id;
  document.getElementById("edit-name").value = intern.name;
  document.getElementById("edit-mssv").value = intern.mssv;
  document.getElementById("edit-email").value = intern.email;
  document.getElementById("edit-phone").value = intern.phone;
  document.getElementById("edit-school").value = intern.school;
  document.getElementById("edit-major").value = intern.major;
  document.getElementById("edit-role").value = intern.role;
  document.getElementById("edit-status").value = intern.status;
  document.getElementById("edit-mentor").value = intern.mentor;
  document.getElementById("edit-progress").value = intern.progress;
  document.getElementById("edit-gpa").value = intern.gpa;

  document.getElementById("edit-modal").classList.remove("hidden");
}

function closeEditModal() {
  document.getElementById("edit-modal").classList.add("hidden");
}

async function handleEditInternSubmit(event) {
  event.preventDefault();
  const form = event.currentTarget;
  if (!form.reportValidity()) return;
  const id = document.getElementById("edit-id").value;
  const updatedFields = {
    name: document.getElementById("edit-name").value.trim(),
    mssv: document.getElementById("edit-mssv").value.trim(),
    email: document.getElementById("edit-email").value.trim(),
    phone: document.getElementById("edit-phone").value.trim(),
    school: document.getElementById("edit-school").value,
    major: document.getElementById("edit-major").value,
    role: document.getElementById("edit-role").value.trim(),
    status: document.getElementById("edit-status").value,
    mentor: document.getElementById("edit-mentor").value,
    progress: Number(document.getElementById("edit-progress").value),
    gpa: document.getElementById("edit-gpa").value.trim()
  };

  const saveButton = form.querySelector('button[type="submit"]');
  const originalText = saveButton.textContent;
  saveButton.disabled = true;
  saveButton.textContent = "Đang lưu...";
  try {
    await InternService.updateIntern(id, updatedFields);
    closeEditModal();
    showToast("Cập nhật hồ sơ thành công.");
    await renderApp();
  } catch (error) {
    Object.entries(error.details || {}).forEach(([field, message]) => {
      const input = document.getElementById(`edit-${field}`);
      if (!input) return;
      let errorNode = document.getElementById(`edit-${field}-error`);
      if (!errorNode) {
        errorNode = document.createElement("small");
        errorNode.id = `edit-${field}-error`;
        errorNode.className = "mt-1 block text-rose-600";
        input.insertAdjacentElement("afterend", errorNode);
      }
      errorNode.textContent = message;
    });
    if (!Object.keys(error.details || {}).length) showToast(error.message || "Không thể cập nhật hồ sơ.");
  } finally {
    saveButton.disabled = false;
    saveButton.textContent = originalText;
  }
}

async function deleteInternConfirm(id) {
  const intern = await InternService.getInternById(id);
  if (!intern) return;

  if (confirm(`Bạn có chắc chắn muốn xóa hồ sơ: "${intern.name}" (${intern.mssv})?`)) {
    await InternService.deleteIntern(id);
    showToast(`Đã xóa hồ sơ: ${intern.name}`);
    AppState.selectedIds.delete(id);
    updateSelectionUI();
    renderApp();
  }
}

async function handleExportExcel() {
  const list = await InternService.getInterns(AppState.filters);
  if (list.length === 0) {
    showToast("Không có thực tập sinh nào phù hợp để xuất file!");
    return;
  }
  InternService.exportExcel(list);
  showToast(`Đã xuất file Excel (${list.length} thực tập sinh) định dạng bảng đẹp!`);
}

function getStatusBadge(status) {
  switch (status) {
    case "Đang thực tập":
      return `<span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-50 text-emerald-700 border border-emerald-200"><span class="w-1.5 h-1.5 rounded-full bg-emerald-500 mr-1.5 animate-pulse"></span>Đang thực tập</span>`;
    case "Chờ tiếp nhận":
      return `<span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-blue-50 text-blue-700 border border-blue-200">Chờ tiếp nhận</span>`;
    case "Đang phỏng vấn":
      return `<span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-50 text-amber-700 border border-amber-200">Đang phỏng vấn</span>`;
    case "Đã hoàn thành":
      return `<span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-700 border border-slate-200">Đã hoàn thành</span>`;
    default:
      return `<span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-slate-100 text-slate-700">${status}</span>`;
  }
}

function getSchoolBadge(shortName) {
  const match = MOCK_UNIVERSITIES.find(u => u.shortName === shortName || u.name === shortName);
  const colorClass = match ? match.color : "bg-slate-100 text-slate-700 border-slate-200";
  return `<span class="px-1.5 py-0.5 text-2xs font-bold rounded ${colorClass} border uppercase">${shortName || 'DH'}</span>`;
}

function getProgressColor(val) {
  if (val >= 80) return "bg-emerald-500";
  if (val >= 50) return "bg-indigo-500";
  if (val >= 25) return "bg-amber-500";
  return "bg-slate-400";
}

function showToast(msg) {
  const toast = document.getElementById("toast");
  const msgEl = document.getElementById("toast-message");
  if (!toast || !msgEl) return;
  msgEl.textContent = msg;
  toast.classList.remove("translate-y-16", "opacity-0");
  setTimeout(() => {
    toast.classList.add("translate-y-16", "opacity-0");
  }, 3200);
}
