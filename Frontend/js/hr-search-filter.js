let results = [];
let debounce;
let canEdit = false;
const searchInput = document.getElementById("searchInput");
const schoolFilter = document.getElementById("schoolFilter");
const majorFilter = document.getElementById("majorFilter");
const statusFilter = document.getElementById("statusFilter");
const tableBody = document.getElementById("searchTableBody");
const exportBtn = document.getElementById("exportBtn");
document.addEventListener('session-permissions-updated',event=>{
  const session=event.detail;canEdit=Session.hasPermission(session,'EDIT_INTERNS');
  document.getElementById('addInternLink').hidden=!Session.hasPermission(session,'MANAGE_INTERNS');
  if(!Session.hasPermission(session,'SEARCH_INTERNS')){tableBody.replaceChildren();return;}
  renderResults();
});
function message(text) { AppUI.feedback("searchMessage", text, "error"); }
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
  }
  document.getElementById("resultCount").textContent = `Tìm thấy ${results.length} hồ sơ`;
  exportBtn.disabled = !results.length;
}
const searchPager=TableUI.server(tableBody,
  (page,pageSize)=>InternService.getInterns({page,pageSize,search:searchInput.value.trim(),school:schoolFilter.value,major:majorFilter.value,status:statusFilter.value}),
  items=>{results=items;renderResults();document.getElementById("resultCount").textContent=`Tìm thấy ${searchPager.state.total} hồ sơ`;});
async function search(){
  message("");
  try{await searchPager.load(true);}catch(error){message(error.message);}
}

function populate(select, values) {
  for (const value of [...new Set(values.filter(Boolean))].sort((a,b) => a.localeCompare(b,"vi"))) select.add(new Option(value, value));
}
searchInput.addEventListener("input", () => {
  clearTimeout(debounce);
  exportBtn.disabled = true;
  debounce = setTimeout(search, 250);
});
[schoolFilter,majorFilter,statusFilter].forEach((select) => select.addEventListener("change", search));
document.getElementById("refreshSearchBtn").onclick = async event => { const button = event.currentTarget; if (button.disabled) return; button.disabled = true; clearTimeout(debounce); try { await search(); } finally { button.disabled = false; } };
document.getElementById("filterForm").addEventListener("submit", (event) => { event.preventDefault(); clearTimeout(debounce); search(); });
document.getElementById("filterForm").addEventListener("reset", () => { clearTimeout(debounce); queueMicrotask(search); });
exportBtn.addEventListener("click", async () => {exportBtn.disabled=true;try{const list=await InternService.getInterns({search:searchInput.value.trim(),school:schoolFilter.value,major:majorFilter.value,status:statusFilter.value});InternService.exportCSV(list);}catch(error){message(error.message);}finally{exportBtn.disabled=!results.length;}});
async function initializeSearch() {
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  const stopLoading = window.AppUI?.loading(tableBody, 9);
  try {
    const session = await API.getCurrentUser();
    if (!Session.hasPermission(session, "SEARCH_INTERNS")) { location.href = "dashboard.html"; return; }
    canEdit = Session.hasPermission(session, "EDIT_INTERNS");
    document.getElementById("roleBadge").textContent = session.user.role;
    document.getElementById("addInternLink").hidden = !Session.hasPermission(session, "MANAGE_INTERNS");
    const options=await requestApi('/interns/search/options',{headers:getAuthHeader()});
    populate(schoolFilter,options.schools||[]);populate(majorFilter,options.majors||[]);
    document.getElementById("totalCount").textContent=options.totalItems;
    document.getElementById("schoolCount").textContent=options.schoolCount;
    document.getElementById("majorCount").textContent=options.majorCount;
    await search();
  } catch (error) {
    results = []; renderResults();
    document.getElementById("resultCount").textContent = "Không thể tải kết quả";
    message(error.message);
  }
  finally { stopLoading?.(); }
}
initializeSearch();
