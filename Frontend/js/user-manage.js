const tableBody = document.getElementById("userTableBody");
const searchInput = document.getElementById("userSearchInput");
let users = [];

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
            ${user.role === "ADMIN" ? "—" : `<button type="button" class="btn-delete" data-user-id="${user.id}">Xóa</button>`}
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
    if (session.user.role !== "ADMIN") {
      window.location.href = "dashboard.html";
      return;
    }
    await loadUsers();
  } catch {
    localStorage.clear();
    window.location.href = "../index.html";
  }
}

document.getElementById("logoutBtn").addEventListener("click", () => {
  localStorage.clear();
  window.location.href = "../index.html";
});

searchInput.addEventListener("input", renderUsers);

tableBody.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-user-id]");
  if (!button || !confirm("Bạn có chắc muốn xóa tài khoản này?")) return;

  try {
    await API.deleteUser(button.dataset.userId);
    alert("Đã xóa tài khoản.");
    await loadUsers();
  } catch (error) {
    alert(error.message);
  }
});

initializePage();

document
  .getElementById("createUserForm")
  .addEventListener("submit", async function (event) {
    event.preventDefault();

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

    try {
      const result = await API.createUser({
        fullName,
        email,
        password,
        role,
      });

      alert(result.message);

      this.reset();

      await loadUsers();
    } catch (error) {
      alert(error.message);
    }
  });
