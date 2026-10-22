const form = document.getElementById("createInternForm");
const formSection = document.getElementById("internFormSection");
const tableBody = document.getElementById("internTableBody");
const searchInput = document.getElementById("searchInput");
const openFormBtn = document.getElementById("openFormBtn");
let interns = [];
let editingId = null;
let canCreate = false;
let canEdit = false;
let saving = false;
const fields = InternProfile.fields.map((field) => field.key);
const tableFields = ["studentCode", "fullName", "email", "phone", "school", "major", "organization", "status"];
document.addEventListener('session-permissions-updated',event=>{
  const session=event.detail;canCreate=Session.hasPermission(session,'MANAGE_INTERNS');canEdit=Session.hasPermission(session,'EDIT_INTERNS');
  openFormBtn.hidden=!canCreate;document.getElementById('searchInternLink').hidden=!Session.hasPermission(session,'SEARCH_INTERNS');
  if(!Session.hasPermission(session,'VIEW_INTERNS')){tableBody.replaceChildren();formSection.classList.add('hidden');openFormBtn.hidden=true;return;}
  if((editingId!==null&&!canEdit)||(editingId===null&&!canCreate))closeForm();
  if(!saving)renderInterns();
});

function showMessage(message, type = "error") {
  const box = document.getElementById("messageBox");
  AppUI.feedback(box, message, type);
}
function closeForm() {
  if (saving) return;
  formSection.classList.add("hidden");
  form.reset();
  editingId = null;
  clearErrors();
}
function openForm(intern = null) {
  if (saving || (intern ? !canEdit : !canCreate)) return;
  closeForm();
  editingId = intern?.id ?? null;
  if (intern) fields.forEach((field) => document.getElementById(field).value = intern[field] || "");
  document.getElementById("internFormTitle").textContent = intern ? "Chỉnh sửa hồ sơ thực tập sinh" : "Thêm hồ sơ thực tập sinh";
  document.getElementById("saveInternBtn").textContent = intern ? "Lưu thay đổi" : "Thêm hồ sơ";
  formSection.classList.remove("hidden");
  formSection.scrollIntoView({ behavior: "smooth", block: "start" });
  document.getElementById("fullName").focus({ preventScroll: true });
}
function clearErrors() {
  document.querySelectorAll(".input-error").forEach((input) => input.classList.remove("input-error"));
  document.querySelectorAll(".error-message").forEach((error) => error.textContent = "");
}
function readInternForm() {
  return Object.fromEntries(fields.map((key) => [key, document.getElementById(key).value.trim() || null]));
}

function validateInternForm(intern) {
  if (!/^[0-9]{10,11}$/.test(intern.phone)) {
    document.getElementById("phoneError").textContent = "Số điện thoại phải có 10 đến 11 chữ số.";
    document.getElementById("phone").classList.add("input-error");
    return false;
  }
  if (intern.startDate && intern.endDate && intern.endDate < intern.startDate) {
    document.getElementById("endDateError").textContent = "Ngày kết thúc phải từ ngày bắt đầu trở đi.";
    return false;
  }
  return true;
}

function setSaving(busy) {
  saving = busy;
  [...form.elements].forEach((control) => control.disabled = busy);
  document.getElementById("closeFormBtn").disabled = busy;
  openFormBtn.disabled = busy;
  document.getElementById("saveInternBtn").textContent = busy ? "Đang lưu…"
    : editingId !== null ? "Lưu thay đổi" : "Thêm hồ sơ";
}
function renderInterns() {
  const keyword = searchInput.value.trim().toLocaleLowerCase("vi");
  tableBody.replaceChildren();
  if (!interns.length) {
    const row = tableBody.insertRow();
    const cell = row.insertCell();
    cell.colSpan = 9; cell.className = "empty-state";
    cell.textContent = keyword ? "Không tìm thấy hồ sơ phù hợp." : "Chưa có hồ sơ thực tập sinh.";
    if (window.AppVisual) cell.prepend(AppVisual.illustration(keyword ? 'search' : 'profile'));
    return;
  }
  for (const intern of interns) {
    const row = tableBody.insertRow();
    for (const field of tableFields) {
      const cell = row.insertCell();
      if (field === "status") cell.append(InternProfile.statusBadge(intern[field]));
      else cell.textContent = intern[field] || "—";
      if (field === "email") cell.className = "table-email";
    }
    const cell = row.insertCell();
    const view = document.createElement("button"); view.type = "button"; view.className = "btn-secondary btn-compact"; view.textContent = "Xem hồ sơ"; view.addEventListener("click", () => InternProfile.show(intern, canEdit ? openForm : null)); cell.appendChild(view);
  }
}
const internPager = TableUI.server(tableBody,
  (page,pageSize) => API.getInterns({page,pageSize,search:searchInput.value.trim()}),
  items => { interns=items;renderInterns(); });
async function loadInterns() { return internPager.load(); }

openFormBtn.addEventListener("click", () => openForm());
document.getElementById("closeFormBtn").addEventListener("click", closeForm);
document.getElementById("cancelBtn").addEventListener("click", closeForm);
let internSearchTimer;searchInput.addEventListener("input",()=>{clearTimeout(internSearchTimer);internSearchTimer=setTimeout(()=>internPager.load(true).catch(error=>showMessage(error.message)),250);});
form.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (saving || (editingId !== null ? !canEdit : !canCreate)) return;
  clearErrors();
  const intern = readInternForm();
  if (!validateInternForm(intern)) return;
  setSaving(true);
  try {
    if (editingId !== null) await API.updateIntern(editingId, intern);
    else await API.createIntern(intern);
    saving = false;
    closeForm();
    showMessage("Đã lưu hồ sơ vào hệ thống.", "success");
    try { await loadInterns(); }
    catch (error) { showMessage(`Đã lưu hồ sơ, nhưng chưa thể tải lại danh sách: ${error.message}`); }
  } catch (error) { showMessage(error.message); }
  finally { setSaving(false); }
});
async function initializeInternPage() {
  openFormBtn.hidden = true;
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  try {
    const session = await API.getCurrentUser();
    const roleBadge = document.getElementById("roleBadge");
    if (roleBadge) {
      roleBadge.textContent = session.user.role;
      roleBadge.className = `badge ${session.user.role.toLowerCase()}`;
    }
    if (!Session.hasPermission(session, "VIEW_INTERNS")) { location.href = "dashboard.html"; return; }
    canCreate = Session.hasPermission(session, "MANAGE_INTERNS");
    canEdit = Session.hasPermission(session, "EDIT_INTERNS");
    openFormBtn.hidden = !canCreate;
    document.getElementById("searchInternLink").hidden = !Session.hasPermission(session, "SEARCH_INTERNS");
  } catch (error) { showMessage(`Không thể xác thực phiên: ${error.message}`); return; }
  try {
    await loadInterns();
    const targetId = new URLSearchParams(location.search).get("edit");
    if (targetId) {
      if (!canEdit) { showMessage("Bạn không có quyền chỉnh sửa hồ sơ."); return; }
      const target = /^[1-9][0-9]*$/.test(targetId)
        ? await requestApi(`/interns/${encodeURIComponent(targetId)}`,{headers:getAuthHeader()}) : null;
      if (target) openForm(target);
      else showMessage("Không tìm thấy hồ sơ cần chỉnh sửa.");
    }
  }
  catch (error) {
    interns = [];
    renderInterns();
    showMessage(`Không thể tải danh sách: ${error.message}`);
  }
}
initializeInternPage();
