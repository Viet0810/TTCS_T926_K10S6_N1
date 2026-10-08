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

function showMessage(message, type = "error") {
  const box = document.getElementById("messageBox");
  box.className = `message-box ${type}`;
  box.textContent = message;
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
  const visible = interns.filter((item) => [item.fullName, item.email, item.studentCode || "", item.organization || ""].some((text) => text.toLocaleLowerCase("vi").includes(keyword)));
  tableBody.replaceChildren();
  if (!visible.length) {
    const row = tableBody.insertRow();
    const cell = row.insertCell();
    cell.colSpan = 9; cell.className = "empty-state";
    cell.textContent = keyword ? "Không tìm thấy hồ sơ phù hợp." : "Chưa có hồ sơ thực tập sinh.";
    return;
  }
  for (const intern of visible) {
    const row = tableBody.insertRow();
    for (const field of tableFields) row.insertCell().textContent = intern[field] || "—";
    const cell = row.insertCell();
    const view = document.createElement("button"); view.type = "button"; view.className = "btn-secondary btn-compact"; view.textContent = "Xem hồ sơ"; view.addEventListener("click", () => InternProfile.show(intern, canEdit ? openForm : null)); cell.appendChild(view);
    if (canEdit) {
      const button = document.createElement("button");
      button.type = "button"; button.className = "btn-secondary btn-compact";
      button.textContent = "Chỉnh sửa";
      button.addEventListener("click", () => openForm(intern));
      cell.appendChild(button);
    }
  }
}
async function loadInterns() {
  interns = await API.getInterns();
  renderInterns();
}
openFormBtn.addEventListener("click", () => openForm());
document.getElementById("closeFormBtn").addEventListener("click", closeForm);
document.getElementById("cancelBtn").addEventListener("click", closeForm);
document.getElementById("logoutBtn").addEventListener("click", Session.logout);
searchInput.addEventListener("input", renderInterns);
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
    document.getElementById("reviewProfilesLink").hidden = false;
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
    document.getElementById("userRole").textContent = session.user.role;
    if (!Session.hasPermission(session, "VIEW_INTERNS")) { location.href = "dashboard.html"; return; }
    canCreate = Session.hasPermission(session, "MANAGE_INTERNS");
    canEdit = Session.hasPermission(session, "EDIT_INTERNS");
    openFormBtn.hidden = !canCreate;
    document.getElementById("reviewDocumentsLink").hidden = !Session.hasPermission(session, "APPROVE_DOCUMENTS");
    document.getElementById("reviewProfilesLink").hidden = !Session.hasPermission(session, "APPROVE_DOCUMENTS");
    document.getElementById("searchInternLink").hidden = !Session.hasPermission(session, "SEARCH_INTERNS");
  } catch (error) { showMessage(`Không thể xác thực phiên: ${error.message}`); return; }
  try {
    await loadInterns();
    const targetId = new URLSearchParams(location.search).get("edit");
    if (targetId) {
      if (!canEdit) { showMessage("Bạn không có quyền chỉnh sửa hồ sơ."); return; }
      const target = /^[1-9][0-9]*$/.test(targetId)
        ? interns.find((intern) => String(intern.id) === targetId) : null;
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
