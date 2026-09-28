document.addEventListener("DOMContentLoaded", () => {
  const token = localStorage.getItem("token");
  if (!token) {
    alert("Vui lòng đăng nhập trước!");
    window.location.href = "../index.html";
    return;
  }
  loadUsers();
});

async function loadUsers() {
  try {
    const users = await API.getUsers();
    const tbody = document.getElementById("userTableBody");
    tbody.innerHTML = "";

    users.forEach((user) => {
      const tr = document.createElement("tr");
      tr.innerHTML = `
                <td>${user.fullName}</td>
                <td>${user.email}</td>
                <td><span class="badge ${user.role.toLowerCase()}">${user.role}</span></td>
                <td>
                    <button onclick="handleDelete('${user.id}')" class="btn-delete">Xóa</button>
                </td>
            `;
      tbody.appendChild(tr);
    });
  } catch (err) {
    console.error("Lỗi lấy danh sách tài khoản:", err);
  }
}

document
  .getElementById("createUserForm")
  .addEventListener("submit", async (e) => {
    e.preventDefault();

    const fullName = document.getElementById("fullName").value;
    const email = document.getElementById("email").value;
    const role = document.getElementById("role").value;

    try {
      const res = await API.createUser({ fullName, email, role });
      if (res.success) {
        alert("Tạo tài khoản thành công!");
        document.getElementById("createUserForm").reset();
        loadUsers();
      } else {
        alert(res.message || "Tạo tài khoản thất bại!");
      }
    } catch (err) {
      console.error("Lỗi kết nối:", err);
    }
  });

async function handleDelete(id) {
  if (confirm("Bạn có chắc chắn muốn xóa tài khoản này?")) {
    try {
      await API.deleteUser(id);
      alert("Đã xóa!");
      loadUsers();
    } catch (err) {
      console.error("Lỗi xóa tài khoản:", err);
    }
  }
}

document.getElementById("logoutBtn").addEventListener("click", () => {
  localStorage.clear();
  window.location.href = "../index.html";
});
