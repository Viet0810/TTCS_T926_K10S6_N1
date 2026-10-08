const tableBody = document.getElementById("userTableBody");
const searchInput = document.getElementById("userSearchInput");
const createForm = document.getElementById("createUserForm");
const createLink = document.getElementById("createUserLink");
let users = [];
let creatingUser = false;
let canCreateUser = false;
let canDeleteUser = false;

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
            ${!canDeleteUser || user.role === "ADMIN" ? "—" : `<button type="button" class="btn-delete" data-user-id="${user.id}">Xóa</button>`}
          </td>
        </tr>`,
    )
    .join("");
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
    if (!Session.redirectIfExpired(error)) alert(error.message);
  }
}

document.getElementById("logoutBtn").addEventListener("click", Session.logout);

searchInput?.addEventListener("input", renderUsers);

tableBody?.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-user-id]");
  if (!button || button.disabled || !canDeleteUser || !confirm("Bạn có chắc muốn xóa tài khoản này?")) return;

  button.disabled = true;
  try {
    await API.deleteUser(button.dataset.userId);
    alert("Đã xóa tài khoản.");
    try { await loadUsers(); }
    catch (error) { alert(`Đã xóa tài khoản, nhưng chưa thể tải lại danh sách: ${error.message}`); }
  } catch (error) {
    alert(error.message);
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

    if (password !== confirmPassword) {
      alert("Mật khẩu xác nhận không khớp.");
      document.getElementById("confirmPassword").focus();
      return;
    }

    creatingUser = true;
    [...this.elements].forEach((control) => control.disabled = true);
    try {
      const result = await API.createUser({
        fullName,
        email,
        password,
        role,
      });

      alert(result.message);

      this.reset();

      if (tableBody) {
        try { await loadUsers(); }
        catch (error) { alert(`Đã tạo tài khoản, nhưng chưa thể tải lại danh sách: ${error.message}`); }
      }
    } catch (error) {
      alert(error.message);
    }
    finally {
      creatingUser = false;
      [...this.elements].forEach((control) => control.disabled = false);
    }
  });
