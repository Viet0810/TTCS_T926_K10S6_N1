let matrix, selectedRole = "ADMIN", draft = new Set(), busy = false, canSave = false;
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
function configurablePermissions() {
  const groups = new Map();
  for (const entry of matrix.catalog) {
    if (!entry.roles.includes(selectedRole) || !entry.editable) continue;
    const protectedAdmin = selectedRole === "ADMIN" && matrix.essentialAdminPermissions.includes(entry.code);
    if (!groups.has(entry.module)) groups.set(entry.module, { module: entry.module, label: entry.label, entries: [], protected: false });
    const group = groups.get(entry.module);
    group.entries.push(entry);
    group.protected ||= protectedAdmin;
    if (entry.action === "READ" || entry.action === "FEATURE") group.label = entry.label;
  }
  return [...groups.values()].filter(group => !group.protected);
}

function render() {
  document.querySelector(".dashboard-main").dataset.selectedRole = selectedRole;
  document.querySelectorAll("[data-role]").forEach(element => {
    element.setAttribute("aria-pressed", String(element.dataset.role === selectedRole));
  });
  document.getElementById("roleSelect").value = selectedRole;

  const roleLabel = document.getElementById("selectedRoleLabel");
  roleLabel.textContent = selectedRole;
  if (window.AppVisual) roleLabel.prepend(AppVisual.icon(AppVisual.roleIcons[selectedRole]));

  const keyword = document.getElementById("permissionSearch").value.trim().toLocaleLowerCase("vi");
  const groups = configurablePermissions();
  const entries = groups.filter(group =>
    `${group.module} ${group.label} ${group.entries.map(entry => entry.code).join(" ")}`.toLocaleLowerCase("vi").includes(keyword)
  );
  const enabledCount = groups.filter(group => group.entries.every(entry => draft.has(entry.code))).length;
  document.getElementById("permissionCount").textContent = `${enabledCount}/${groups.length} quyền chính đang bật`;
  document.getElementById("permissionDirtyStatus").textContent = changeCount()
    ? `${changeCount()} thay đổi chưa lưu`
    : "Chưa có thay đổi";

  const target = document.getElementById("permissionMatrix");
  target.replaceChildren();
  const list = document.createElement("div");
  list.className = "permission-card-grid";

  for (const group of entries) {
    const card = document.createElement("label");
    card.className = "permission-card";
    const checkbox = document.createElement("input");
    checkbox.type = "checkbox";
    checkbox.dataset.permission = group.module;
    checkbox.checked = group.entries.every(entry => draft.has(entry.code));
    checkbox.indeterminate = !checkbox.checked && group.entries.some(entry => draft.has(entry.code));
    checkbox.disabled = busy || !canSave;
    checkbox.setAttribute("aria-label", group.label);

    const content = document.createElement("span");
    content.className = "permission-card-content";
    const title = document.createElement("strong");
    title.textContent = group.label;
    const description = document.createElement("span");
    description.textContent = `Cho phép sử dụng chức năng ${group.label.toLocaleLowerCase("vi")}.`;
    content.append(title, description);
    card.append(checkbox, content);

    checkbox.addEventListener("change", () => {
      for (const entry of group.entries) {
        if (checkbox.checked) {
          draft.add(entry.code);
          (entry.requires || []).forEach(code => draft.add(code));
        } else draft.delete(entry.code);
      }
      message.hidden = false;
      message.textContent = "Có thay đổi chưa lưu.";
      render();
    });
    list.append(card);
  }

  if (!entries.length) {
    const empty = document.createElement("div");
    empty.className = "permission-empty";
    if (window.AppVisual) empty.append(AppVisual.illustration(keyword ? "search" : "shield"));
    empty.append(keyword ? "Không có quyền phù hợp." : "Vai trò này không có quyền chính cần cấu hình.");
    target.append(empty);
  } else {
    target.append(list);
  }
  setBusy(busy);
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
