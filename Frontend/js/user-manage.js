const userTableBody = document.getElementById("userTableBody");

function escapeHtml(value) {
  const element = document.createElement("span");
  element.textContent = value ?? "";
  return element.innerHTML;
}

async function loadUsers() {
  try {
    const users = await API.getUsers();
    userTableBody.innerHTML = "";

    if (!users.length) {
      userTableBody.innerHTML = '<tr><td colspan="4">Chưa có tài khoản.</td></tr>';
      return;
    }

    users.forEach((user) => {
      const row = document.createElement("tr");
      row.innerHTML = `<td>${escapeHtml(user.fullName)}</td>
        <td>${escapeHtml(user.email)}</td>
        <td>${escapeHtml(user.role)}</td>
        <td><div class="user-actions">
          <button type="button" class="btn-edit" data-user-id="${Number(user.id)}">Sửa</button>
          <button type="button" class="btn-delete" data-user-id="${Number(user.id)}">Xóa</button>
        </div></td>`;
      row.querySelector(".btn-edit").addEventListener("click", () => {
        window.location.href = `user-edit.html?id=${encodeURIComponent(user.id)}`;
      });
      row.querySelector(".btn-delete").addEventListener("click", () => deleteUser(user.id));
      userTableBody.appendChild(row);
    });
  } catch (error) {
    alert(error.message);
  }
}

async function deleteUser(userId) {
  if (!window.confirm("Bạn có chắc muốn xóa tài khoản này?")) return;
  try {
    const result = await API.deleteUser(userId);
    alert(result.message);
    await loadUsers();
  } catch (error) {
    alert(error.message);
  }
}

document.getElementById("logoutBtn").addEventListener("click", () => {
  localStorage.removeItem("token");
  localStorage.removeItem("role");
  localStorage.removeItem("user");
  window.location.href = "../index.html";
});

document.getElementById("createUserForm").addEventListener("submit", async function (event) {
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

loadUsers();
