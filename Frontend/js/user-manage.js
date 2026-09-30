const tableBody = document.getElementById("userTableBody");
const role = localStorage.getItem("role");

if (role !== "ADMIN") {
  window.location.href = "../index.html";
}

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
  const users = await API.getUsers();
  tableBody.innerHTML = users
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

document.getElementById("logoutBtn").addEventListener("click", () => {
  localStorage.clear();
  window.location.href = "../index.html";
});

tableBody.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-user-id]");
  if (!button || !confirm("Bạn có chắc muốn xóa tài khoản này?")) return;

  try {
    const result = await API.deleteUser(button.dataset.userId);
    alert(result.message);
    await loadUsers();
  } catch (error) {
    alert(error.message);
  }
});

loadUsers().catch((error) => alert(error.message));

document
  .getElementById("createUserForm")
  .addEventListener("submit", async function (event) {
    event.preventDefault();

    const fullName = document.getElementById("fullName").value.trim();

    const email = document.getElementById("email").value.trim();

    const password = document.getElementById("password").value;

    const role = document.getElementById("role").value;

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
