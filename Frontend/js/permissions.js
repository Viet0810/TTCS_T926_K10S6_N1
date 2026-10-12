let matrix, selectedRole = "ADMIN", draft = new Set(), busy = false, canSave = false;
const actions = { READ: "Xem", CREATE: "Tạo / nộp", UPDATE: "Sửa", APPROVE: "Duyệt", REJECT: "Từ chối", DELETE: "Xóa", CHECK_IN: "Check-in", CHECK_OUT: "Check-out", FEATURE: "Chức năng" };
const message = document.getElementById("permissionsMessage");
const save = document.getElementById("savePermissionsBtn"), reset = document.getElementById("resetPermissionsBtn");
const cancel = document.getElementById("cancelPermissionsBtn"), retry = document.getElementById("retryPermissionsBtn");
function changeCount() { const stored = new Set(matrix?.roles[selectedRole] || []); return [...new Set([...draft, ...stored])].filter(code => draft.has(code) !== stored.has(code)).length; }
function dirty() { return JSON.stringify([...draft].sort()) !== JSON.stringify([...(matrix?.roles[selectedRole] || [])].sort()); }
function setBusy(value) {
  busy = value; save.disabled = value || !canSave || !dirty(); reset.disabled = value || !canSave;
  cancel.disabled = value || !dirty(); retry.disabled = value;
  save.textContent = value ? "Đang xử lý…" : "Lưu thay đổi";
  document.getElementById("permissionMatrix").setAttribute("aria-busy", String(value));
  document.querySelectorAll("[data-role], #roleSelect").forEach(el => el.disabled = value);
  if (value) document.querySelectorAll("[data-permission]").forEach(el => el.disabled = true);
}
function render() {
  document.querySelector('.dashboard-main').dataset.selectedRole = selectedRole;
  const target = document.getElementById("permissionMatrix"); target.replaceChildren();
  document.querySelectorAll("[data-role]").forEach(el => el.setAttribute("aria-pressed", String(el.dataset.role === selectedRole)));
  document.getElementById("roleSelect").value = selectedRole;
  document.getElementById("selectedRoleLabel").textContent = selectedRole;
  if (window.AppVisual) document.getElementById('selectedRoleLabel').prepend(AppVisual.icon(AppVisual.roleIcons[selectedRole]));
  document.getElementById("permissionCount").textContent = `${draft.size} quyền đang bật`;
  document.getElementById("permissionDirtyStatus").textContent = changeCount() ? `${changeCount()} thay đổi chưa lưu` : "Chưa có thay đổi";
  const table = document.createElement("table"); table.className = "permission-matrix-table";
  const head = table.createTHead().insertRow();
  for (const title of ["Chức năng", ...Object.values(actions)]) { const cell = document.createElement("th"); cell.scope = "col"; cell.textContent = title; head.append(cell); }
  const body = table.createTBody();
  const keyword = document.getElementById("permissionSearch").value.trim().toLocaleLowerCase("vi");
  for (const module of [...new Set(matrix.catalog.filter(item => item.label.toLocaleLowerCase("vi").includes(keyword)).map(item => item.module))]) {
    const entries = matrix.catalog.filter(item => item.module === module), row = body.insertRow();
    const title = document.createElement("th"); title.scope = "row"; title.textContent = entries[0].label; row.append(title);
    for (const [action, label] of Object.entries(actions)) {
      const cell = row.insertCell(); cell.dataset.label = label;
      const entry = entries.find(item => item.action === action && item.roles.includes(selectedRole));
      if (!entry) { cell.textContent = "—"; cell.setAttribute("aria-label", label + ": không áp dụng"); continue; }
      const field = document.createElement("label"), checkbox = document.createElement("input"); checkbox.type = "checkbox";
      checkbox.dataset.permission = entry.code; checkbox.checked = draft.has(entry.code);
      const protectedAdmin = selectedRole === "ADMIN" && matrix.essentialAdminPermissions.includes(entry.code);
      checkbox.disabled = busy || !canSave || !entry.editable || protectedAdmin;
      checkbox.setAttribute("aria-label", entry.label + ": " + label);
      checkbox.addEventListener("change", () => {
        if (checkbox.checked) draft.add(entry.code); else draft.delete(entry.code);
        if (checkbox.checked) (entry.requires || []).forEach(code => draft.add(code));
        if (!checkbox.checked) matrix.catalog.filter(child => child.requires?.includes(entry.code)).forEach(child => draft.delete(child.code));
        message.hidden = false; message.textContent = "Có thay đổi chưa lưu."; render();
      });
      field.append(checkbox);
      if (protectedAdmin || !entry.editable) { const hint = document.createElement("span"); hint.className = "permission-lock"; hint.textContent = protectedAdmin ? "Bảo vệ" : "Cố định"; field.append(hint); }
      cell.append(field);
    }
  }
  if (!body.rows.length) { const cell = body.insertRow().insertCell(); cell.colSpan = Object.keys(actions).length + 1; cell.className = "permission-empty"; if (window.AppVisual) cell.append(AppVisual.illustration(keyword ? 'search' : 'shield')); cell.append(keyword ? "Không có chức năng phù hợp." : "Chưa có quyền được cấu hình."); }
  target.append(table); setBusy(busy);
}
async function switchRole(role) {
  if (busy || !matrix) return;
  if (role === selectedRole) return;
  if (dirty() && !await AppUI.confirm("Bạn có thay đổi chưa lưu. Bạn có muốn bỏ thay đổi?", "Thay đổi chưa lưu", "Bỏ thay đổi", "Ở lại")) { document.getElementById("roleSelect").value = selectedRole; return; }
  selectedRole = role; draft = new Set(matrix.roles[role]); message.textContent = ""; render();
}
document.querySelectorAll("[data-role]").forEach(el => el.onclick = () => switchRole(el.dataset.role));
document.getElementById("roleSelect").onchange = event => switchRole(event.target.value);
reset.onclick = async () => {
  if (busy || !canSave || !await AppUI.confirm("Khôi phục quyền mặc định của " + selectedRole + "? Bạn cần bấm Lưu quyền để áp dụng.", "Khôi phục mặc định", "Xem quyền mặc định")) return;
  setBusy(true);
  try { const defaults = await API.getPermissionDefaults(selectedRole); draft = new Set(defaults.permissions); AppUI.feedback(message, "Đã chọn quyền mặc định. Bấm Lưu quyền để áp dụng.", "info"); }
  catch (error) { AppUI.feedback(message, error.message, "error"); Session.redirectIfExpired(error); }
  finally { setBusy(false); render(); }
};
save.onclick = async () => {
  if (busy || !canSave || !dirty()) return;
  setBusy(true);
  let persisted = false;
  try {
    const result = await API.savePermissions(selectedRole, [...draft]);
    persisted = true;
    matrix.roles[selectedRole] = result.permissions; draft = new Set(result.permissions);
    matrix = await API.getPermissionMatrix(); draft = new Set(matrix.roles[selectedRole]);
    await API.getCurrentUser();
    AppUI.feedback(message, "Lưu phân quyền thành công"); retry.hidden = true;
  } catch (error) { AppUI.feedback(message, persisted ? `Đã lưu quyền nhưng chưa tải lại đầy đủ cấu hình: ${error.message}` : error.message, "error"); retry.hidden = false; Session.redirectIfExpired(error); }
  finally { setBusy(false); render(); }
};
cancel.onclick = () => { if (busy || !matrix) return; draft = new Set(matrix.roles[selectedRole]); AppUI.feedback(message, "Đã hủy các thay đổi chưa lưu.", "info"); render(); };
document.getElementById("permissionSearch").addEventListener("input", () => { if (matrix) render(); });
retry.onclick = async () => { if (dirty() && !await AppUI.confirm("Bỏ thay đổi chưa lưu để tải lại cấu hình?")) return; await loadRolePermissions(); };
window.addEventListener("beforeunload", event => { if (matrix && dirty()) { event.preventDefault(); event.returnValue = ""; } });
async function loadRolePermissions() {
  if (busy) return;
  setBusy(true); message.textContent = "Đang tải cấu hình quyền…";
  const stopLoading = AppUI.loading(document.getElementById("permissionMatrix"));
  try {
    const session = await API.getCurrentUser();
    if (session.user?.role !== "ADMIN" || !Session.hasPermission(session, "RBAC_READ")) { location.href = "dashboard.html"; return; }
    canSave = Session.hasPermission(session, "MANAGE_PERMISSIONS");
    matrix = await API.getPermissionMatrix(); draft = new Set(matrix.roles[selectedRole]); message.textContent = ""; retry.hidden = true;
  } catch (error) { message.textContent = error.message; retry.hidden = false; document.getElementById("permissionMatrix").textContent = "Chưa thể tải cấu hình quyền."; Session.redirectIfExpired(error); }
  finally { stopLoading(); setBusy(false); if (matrix) render(); }
}
loadRolePermissions();
