const tableBody = document.getElementById("userTableBody");
const searchInput = document.getElementById("userSearchInput");
const createForm = document.getElementById("createUserForm");
const createLink = document.getElementById("createUserLink");
let users = [];
let creatingUser = false;
let canCreateUser = false;
let canDeleteUser = false;

function accountMessage(text) {
  let box = document.getElementById("accountMessage");
  if (!box) {
    box = document.createElement("p"); box.id = "accountMessage"; box.className = "inline-notice";
    box.setAttribute("role", "status"); box.setAttribute("aria-live", "polite");
    const content = document.querySelector(".dashboard-main .card"); content.append(box);
  }
  box.textContent = text;
}

function updateAccountPasswordFields() {
  if (!createForm) return;
  const automatic = document.getElementById("role").value !== "INTERN";
  for (const id of ["password", "confirmPassword"]) {
    const input = document.getElementById(id);
    input.closest(".form-group").hidden = automatic; input.required = !automatic;
    if (automatic) { input.value = ""; Validation.clearFieldError(input); }
  }
  document.getElementById("temporaryPasswordNotice").hidden = !automatic;
}
if (createForm) { document.getElementById("role").addEventListener("change", updateAccountPasswordFields); updateAccountPasswordFields(); }

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function roleClass(userRole) {
  return String(userRole).toLowerCase();
}

async function loadUsers() {
  users = await API.getUsers();
  renderUsers();
}

function renderUsers() {
  const keyword = searchInput.value.trim().toLowerCase();
  const visibleUsers = users.filter((user) =>
    `${user.fullName} ${user.email} ${user.role}`
      .toLowerCase()
      .includes(keyword),
  );

  tableBody.innerHTML = visibleUsers
    .map(
      (user) => `
        <tr>
          <td>${escapeHtml(user.fullName)}</td>
          <td>${escapeHtml(user.email)}</td>
          <td><span class="badge ${roleClass(user.role)}">${escapeHtml(user.role)}</span></td>
          <td>
            ${canCreateUser && ["HR", "MENTOR"].includes(user.role) ? `<button type="button" class="btn-secondary" data-resend-id="${user.id}">Gửi lại email đăng nhập</button>` : ""}
            ${!canDeleteUser || user.role === "ADMIN" ? "—" : `<button type="button" class="btn-delete" data-user-id="${user.id}">Xóa</button>`}
          </td>
        </tr>`,
    )
    .join("");
  if (!visibleUsers.length) {
    const cell = tableBody.insertRow().insertCell(); cell.colSpan = 4; cell.className = "empty-state";
    cell.textContent = keyword ? "Không tìm thấy tài khoản phù hợp." : "Chưa có tài khoản.";
  }
}

async function initializePage() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const session = await API.getCurrentUser();
    if (!Session.hasPermission(session, "MANAGE_USERS")) {
      window.location.href = "dashboard.html";
      return;
    }
    canCreateUser = Session.hasPermission(session, "CREATE_USER");
    canDeleteUser = Session.hasPermission(session, "DELETE_USER");
    if (createForm && !canCreateUser) {
      window.location.href = "user-manage.html";
      return;
    }
    if (createLink) createLink.hidden = !canCreateUser;
    if (tableBody) await loadUsers();
  } catch (error) {
    if (tableBody && !users.length) {
      tableBody.replaceChildren(); const cell = tableBody.insertRow().insertCell();
      cell.colSpan = 4; cell.className = "empty-state"; cell.textContent = "Chưa thể tải danh sách tài khoản.";
    }
    if (!Session.redirectIfExpired(error)) accountMessage(error.message);
  }
}


searchInput?.addEventListener("input", renderUsers);

tableBody?.addEventListener("click", async (event) => {
  const resend = event.target.closest("[data-resend-id]");
  if (resend) {
    if (!canCreateUser || resend.disabled || !confirm("Gửi lại email sẽ thay mật khẩu bằng mật khẩu tạm mới và yêu cầu người dùng đổi mật khẩu. Tiếp tục?")) return;
    resend.disabled = true;
    try { const result = await API.resendLoginEmail(resend.dataset.resendId); accountMessage(result.message); }
    catch (error) { accountMessage(error.message); Session.redirectIfExpired(error); }
    finally { resend.disabled = false; }
    return;
  }
  const button = event.target.closest("[data-user-id]");
  if (!button || button.disabled || !canDeleteUser || !confirm("Bạn có chắc muốn xóa tài khoản này?")) return;

  button.disabled = true;
  try {
    await API.deleteUser(button.dataset.userId);
    accountMessage("Đã xóa tài khoản.");
    try { await loadUsers(); }
    catch (error) { accountMessage(`Đã xóa tài khoản, nhưng chưa thể tải lại danh sách: ${error.message}`); }
  } catch (error) {
    accountMessage(error.message);
  }
  finally { button.disabled = false; }
});

initializePage();

createForm?.addEventListener("submit", async function (event) {
    event.preventDefault();
    if (creatingUser || !canCreateUser) return;

    const fullName = document.getElementById("fullName").value.trim();

    const email = document.getElementById("email").value.trim();

    const password = document.getElementById("password").value;
    const confirmPassword = document.getElementById("confirmPassword").value;

    const role = document.getElementById("role").value;

    for (const id of ["password", "confirmPassword"]) Validation.clearFieldError(document.getElementById(id));
    const passwordError = role === "INTERN" ? Validation.validatePassword(password) : "";
    if (passwordError) { Validation.showFieldError(document.getElementById("password"), passwordError); document.getElementById("password").focus(); return; }

    if (role === "INTERN" && password !== confirmPassword) {
      Validation.showFieldError(document.getElementById("confirmPassword"), "Mật khẩu xác nhận không khớp.");
      document.getElementById("confirmPassword").focus();
      return;
    }

    creatingUser = true;
    const submitButton = this.querySelector('[type="submit"]');
    const submitContent = submitButton.innerHTML;
    submitButton.textContent = "Đang tạo tài khoản…";
    this.setAttribute("aria-busy", "true");
    [...this.elements].forEach((control) => control.disabled = true);
    try {
      const result = await API.createUser({
        fullName,
        email,
        ...(role === "INTERN" ? { password } : {}),
        role,
      });

      accountMessage(result.message);

      this.reset();
      updateAccountPasswordFields();

      if (tableBody) {
        try { await loadUsers(); }
        catch (error) { accountMessage(`Đã tạo tài khoản, nhưng chưa thể tải lại danh sách: ${error.message}`); }
      }
    } catch (error) {
      accountMessage(error.message);
    }
    finally {
      creatingUser = false;
      this.setAttribute("aria-busy", "false");
      submitButton.innerHTML = submitContent;
      [...this.elements].forEach((control) => control.disabled = false);
    }
  });

if (createForm) for (const id of ["password", "confirmPassword"]) {
  const input = document.getElementById(id);
  input.addEventListener("input", () => Validation.clearFieldError(input));
}
