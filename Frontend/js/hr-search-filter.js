let allInterns = [];
let results = [];
let revision = 0;
let debounce;
let canEdit = false;
const searchInput = document.getElementById("searchInput");
const schoolFilter = document.getElementById("schoolFilter");
const majorFilter = document.getElementById("majorFilter");
const tableBody = document.getElementById("searchTableBody");
const exportBtn = document.getElementById("exportBtn");
function message(text) { document.getElementById("searchMessage").textContent = text; }
function renderResults() {
  tableBody.replaceChildren();
  if (!results.length) {
    const cell = tableBody.insertRow().insertCell();
    cell.colSpan = 9; cell.className = "empty-state"; cell.textContent = "Không có hồ sơ phù hợp.";
  }
  for (const intern of results) {
    const row = tableBody.insertRow();
    for (const key of ["studentCode", "fullName", "email", "phone", "school", "major", "organization", "status"]) {
      const cell = row.insertCell();
      if (key === "status") cell.append(InternProfile.statusBadge(intern[key]));
      else cell.textContent = intern[key] || "—";
      if (key === "email") cell.className = "table-email";
    }
    const action = row.insertCell();
    const view = document.createElement("button"); view.type = "button"; view.className = "btn-secondary search-edit-link"; view.textContent = "Xem hồ sơ"; view.addEventListener("click", () => InternProfile.show(intern, canEdit ? (profile) => { location.href = `intern-manage.html?edit=${encodeURIComponent(profile.id)}`; } : null)); action.appendChild(view);
    if (canEdit) {
      const link = document.createElement("a");
      link.className = "btn-secondary search-edit-link";
      link.textContent = "Chỉnh sửa";
      link.href = `intern-manage.html?edit=${encodeURIComponent(intern.id)}`;
      link.setAttribute("aria-label", `Chỉnh sửa hồ sơ ${intern.fullName}`);
      action.appendChild(link);
    }
  }
  document.getElementById("resultCount").textContent = `Tìm thấy ${results.length} hồ sơ`;
  exportBtn.disabled = !results.length;
}
async function search() {
  const request = ++revision;
  exportBtn.disabled = true;
  document.getElementById("resultCount").textContent = "Đang tìm kiếm…";
  message("");
  try {
    const list = await InternService.getInterns({ search: searchInput.value.trim(), school: schoolFilter.value, major: majorFilter.value });
    if (request !== revision) return;
    results = list; renderResults();
  } catch (error) {
    if (request !== revision) return;
    results = []; renderResults();
    document.getElementById("resultCount").textContent = "Không thể tải kết quả";
    message(error.message);
  }
}
function populate(select, values) {
  for (const value of [...new Set(values.filter(Boolean))].sort((a,b) => a.localeCompare(b,"vi"))) select.add(new Option(value, value));
}
searchInput.addEventListener("input", () => {
  clearTimeout(debounce);
  ++revision;
  exportBtn.disabled = true;
  debounce = setTimeout(search, 250);
});
[schoolFilter,majorFilter].forEach((select) => select.addEventListener("change", search));
document.getElementById("filterForm").addEventListener("submit", (event) => { event.preventDefault(); clearTimeout(debounce); search(); });
document.getElementById("filterForm").addEventListener("reset", () => { clearTimeout(debounce); queueMicrotask(search); });
exportBtn.addEventListener("click", () => InternService.exportCSV(results));
async function initializeSearch() {
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  try {
    const session = await API.getCurrentUser();
    if (!Session.hasPermission(session, "SEARCH_INTERNS")) { location.href = "dashboard.html"; return; }
    canEdit = Session.hasPermission(session, "EDIT_INTERNS");
    document.getElementById("roleBadge").textContent = session.user.role;
    document.getElementById("addInternLink").hidden = !Session.hasPermission(session, "MANAGE_INTERNS");
    allInterns = await API.getInterns();
    populate(schoolFilter, allInterns.map((intern) => intern.school));
    populate(majorFilter, allInterns.map((intern) => intern.major));
    document.getElementById("totalCount").textContent = allInterns.length;
    document.getElementById("schoolCount").textContent = new Set(allInterns.map((intern) => intern.school).filter(Boolean)).size;
    document.getElementById("majorCount").textContent = new Set(allInterns.map((intern) => intern.major).filter(Boolean)).size;
    results = allInterns; renderResults();
  } catch (error) {
    results = []; renderResults();
    document.getElementById("resultCount").textContent = "Không thể tải kết quả";
    message(error.message);
  }
}
initializeSearch();
