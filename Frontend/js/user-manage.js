const tableBody = document.getElementById("userTableBody");
const userViewToggle = document.getElementById("userViewToggle");
const usersCard = document.querySelector(".users-card");

function setUsersView(view) {
  const grid = view === "grid";
  usersCard.classList.toggle("view-grid", grid);
  userViewToggle.classList.toggle("is-grid", grid);
  userViewToggle.setAttribute("aria-pressed", String(grid));
  userViewToggle.setAttribute("aria-label", grid ? "Chuyển sang dạng bảng" : "Chuyển sang dạng ô");
  userViewToggle.title = grid ? "Chuyển sang dạng bảng" : "Chuyển sang dạng ô";
  try { localStorage.setItem("adminUsersView", grid ? "grid" : "table"); } catch {}
}

userViewToggle.addEventListener("click", () => {
  setUsersView(usersCard.classList.contains("view-grid") ? "table" : "grid");
});
setUsersView(localStorage.getItem("adminUsersView") || "table");
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
          <td>${user.role === "ADMIN" ? `<span class="badge ${roleClass(user.role)}">${escapeHtml(user.role)}</span>` : `
            <select class="role-select" data-user-role-id="${user.id}" aria-label="Vai trò ${escapeHtml(user.email)}">
              <option value="HR" ${user.role === "HR" ? "selected" : ""}>HR</option>
              <option value="MENTOR" ${user.role === "MENTOR" ? "selected" : ""}>Mentor</option>
              <option value="INTERN" ${user.role === "INTERN" ? "selected" : ""}>Thực tập sinh</option>
            </select>`}</td>
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

tableBody.addEventListener("change", async (event) => {
  const select = event.target.closest("[data-user-role-id]");
  if (!select) return;
  select.disabled = true;
  try {
    const result = await API.updateUserRole(select.dataset.userRoleId, select.value);
    alert(result.message);
    await loadUsers();
  } catch (error) {
    alert(error.message);
    await loadUsers();
  }
});

loadUsers().catch((error) => alert(error.message));

const importForm = document.getElementById("internImportForm");
const importMessage = document.getElementById("internImportMessage");
const importResults = document.getElementById("internImportResults");
const csvCell = (value) => `"${String(value ?? "").replaceAll('"', '""')}"`;
const downloadCsv = (name, rows) => {
  const content = "\uFEFF" + rows.map((row) => row.map(csvCell).join(",")).join("\r\n");
  const url = URL.createObjectURL(new Blob([content], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
};

document.getElementById("downloadImportTemplate").addEventListener("click", () => {
  downloadCsv("mau-import-thuc-tap-sinh.csv", [[
    "Họ tên", "MSSV", "Email", "Số điện thoại", "Trường", "Chuyên ngành", "Vị trí",
    "Trạng thái", "Phòng ban", "Ngày bắt đầu", "Ngày kết thúc", "Email Mentor",
  ], ["Nguyễn Văn A", "SV001", "sinhvien@example.com", "0900000000", "Tên trường", "Công nghệ thông tin", "Thực tập sinh", "Đang thực tập", "Kỹ thuật", "2026-10-01", "2027-01-01", "mentor@example.com"]]);
});

importForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const file = document.getElementById("internExcelFile").files[0];
  if (!file) return;
  const button = importForm.querySelector("button[type=submit]");
  button.disabled = true;
  importMessage.textContent = "Đang đọc tệp và tạo tài khoản...";
  importResults.replaceChildren();
  try {
    const body = new FormData();
    body.append("file", file);
    const response = await fetch(`${BASE_URL}/interns/import-excel`, {
      method: "POST",
      headers: { Authorization: `Bearer ${localStorage.getItem("token") || ""}` },
      body,
    });
    const result = await response.json().catch(() => ({}));
    if (response.status === 405) {
      throw new Error("Backend đang chạy chưa có API nhập Excel. Hãy khởi động lại Backend từ thư mục Backend/InternManagement của bản mã nguồn hiện tại rồi thử lại.");
    }
    if (!response.ok) throw new Error(result.message || `Lỗi HTTP ${response.status}`);
    importMessage.textContent = result.message;
    if (result.created?.length) {
      const download = document.createElement("button");
      download.type = "button";
      download.className = "btn-primary";
      download.textContent = "Tải danh sách tài khoản và mật khẩu khởi tạo";
      download.addEventListener("click", () => downloadCsv("tai-khoan-thuc-tap-sinh-moi.csv", [
        ["Họ tên", "MSSV", "Email đăng nhập", "Mật khẩu khởi tạo", "Mentor"],
        ...result.created.map((user) => [user.name, user.mssv, user.email, user.password, user.mentor || ""]),
      ]));
      importResults.append(download);
      const note = document.createElement("p");
      note.textContent = "Tệp tải xuống chứa mật khẩu khởi tạo chỉ được cung cấp trong lần nhập này. Hãy chuyển an toàn cho đúng thực tập sinh.";
      importResults.append(note);
      await loadUsers();
    }
    if (result.rejected?.length) {
      const list = document.createElement("ul");
      list.innerHTML = result.rejected.map((row) => `<li>Dòng ${Number(row.row)} (${escapeHtml(row.email || row.mssv || "không rõ")}): ${escapeHtml(row.reason)}</li>`).join("");
      importResults.append(list);
    }
  } catch (error) {
    importMessage.textContent = error.message;
  } finally {
    button.disabled = false;
  }
});

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
