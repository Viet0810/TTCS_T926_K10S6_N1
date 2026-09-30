const form = document.getElementById("createInternForm");

const formSection = document.getElementById("internFormSection");

const openFormBtn = document.getElementById("openFormBtn");
const closeFormBtn = document.getElementById("closeFormBtn");
const cancelBtn = document.getElementById("cancelBtn");

const messageBox = document.getElementById("messageBox");

const tableBody = document.getElementById("internTableBody");
const searchInput = document.getElementById("searchInput");

const logoutBtn = document.getElementById("logoutBtn");

const INTERN_API_URL = "http://localhost:5024/api/interns";
let interns = [];
let canManageInterns = false;

async function requestInternApi(path = "", options = {}) {
  const token = localStorage.getItem("token");
  const headers = { ...options.headers };

  if (token) headers.Authorization = `Bearer ${token}`;
  if (options.body) headers["Content-Type"] = "application/json";

  const response = await fetch(`${INTERN_API_URL}${path}`, {
    ...options,
    headers,
  });
  const data = await response.json().catch(() => ({}));

  if (!response.ok) {
    throw new Error(
      data.message ||
        data.detail ||
        data.title ||
        `Lỗi HTTP ${response.status}`,
    );
  }

  return data;
}

async function loadInterns() {
  try {
    interns = await requestInternApi();
    renderInterns(interns);
    return true;
  } catch (error) {
    showMessage(`Không thể tải dữ liệu: ${error.message}`, "error");
    return false;
  }
}

/*
==================================
MỞ FORM
==================================
*/

openFormBtn.addEventListener("click", () => {
  formSection.classList.remove("hidden");

  document.getElementById("fullName").focus();
});

/*
==================================
ĐÓNG FORM
==================================
*/

function closeForm() {
  formSection.classList.add("hidden");

  form.reset();

  clearErrors();
}

closeFormBtn.addEventListener("click", closeForm);

cancelBtn.addEventListener("click", closeForm);

/*
==================================
LẤY DỮ LIỆU FORM
==================================
*/

function getFormData() {
  return {
    fullName: document.getElementById("fullName").value.trim(),

    email: document.getElementById("email").value.trim(),

    phone: document.getElementById("phone").value.trim(),

    school: document.getElementById("school").value.trim(),

    major: document.getElementById("major").value.trim(),
  };
}

/*
==================================
VALIDATE
==================================
*/

function validateIntern(intern) {
  clearErrors();

  let valid = true;

  if (!intern.fullName) {
    showFieldError("fullName", "Vui lòng nhập họ và tên.");

    valid = false;
  }

  if (!intern.email) {
    showFieldError("email", "Vui lòng nhập email.");

    valid = false;
  } else {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (!emailRegex.test(intern.email)) {
      showFieldError("email", "Email không đúng định dạng.");

      valid = false;
    }
  }

  if (!intern.phone) {
    showFieldError("phone", "Vui lòng nhập số điện thoại.");

    valid = false;
  } else {
    const phoneRegex = /^[0-9]{10,11}$/;

    if (!phoneRegex.test(intern.phone)) {
      showFieldError("phone", "Số điện thoại phải có 10 đến 11 chữ số.");

      valid = false;
    }
  }

  if (!intern.school) {
    showFieldError("school", "Vui lòng nhập tên trường.");

    valid = false;
  }

  if (!intern.major) {
    showFieldError("major", "Vui lòng nhập chuyên ngành.");

    valid = false;
  }

  return valid;
}

/*
==================================
HIỂN THỊ LỖI
==================================
*/

function showFieldError(fieldId, message) {
  const input = document.getElementById(fieldId);

  const error = document.getElementById(`${fieldId}Error`);

  input.classList.add("input-error");

  error.textContent = message;
}

/*
==================================
XÓA LỖI
==================================
*/

function clearErrors() {
  const inputs = document.querySelectorAll(".form-group input");

  inputs.forEach((input) => {
    input.classList.remove("input-error");
  });

  const errors = document.querySelectorAll(".error-message");

  errors.forEach((error) => {
    error.textContent = "";
  });
}

/*
==================================
SUBMIT FORM
==================================
*/

form.addEventListener("submit", async function (event) {
  event.preventDefault();

  if (!canManageInterns) {
    showMessage("Bạn không có quyền quản lý hồ sơ thực tập sinh.", "error");
    return;
  }

  const intern = getFormData();

  if (!validateIntern(intern)) {
    showMessage("Vui lòng kiểm tra lại thông tin.", "error");

    return;
  }

  const emailExists = interns.some(
    (item) => item.email.toLowerCase() === intern.email.toLowerCase(),
  );

  if (emailExists) {
    showFieldError("email", "Email này đã tồn tại.");

    return;
  }

  try {
    await requestInternApi("", {
      method: "POST",
      body: JSON.stringify(intern),
    });

    closeForm();
    if (await loadInterns()) {
      showMessage("Thêm hồ sơ thực tập sinh thành công!", "success");
    }
  } catch (error) {
    showMessage(error.message, "error");
  }
});

/*
==================================
HIỂN THỊ DANH SÁCH
==================================
*/

function renderInterns(data) {
  tableBody.innerHTML = "";

  if (data.length === 0) {
    tableBody.innerHTML = `
      <tr>
        <td
          colspan="5"
          class="empty-state"
        >
          Chưa có hồ sơ thực tập sinh.
        </td>
      </tr>
    `;

    return;
  }

  data.forEach((intern) => {
    const row = document.createElement("tr");

    row.innerHTML = `

      <td>${escapeHtml(intern.fullName)}</td>

      <td>${escapeHtml(intern.email)}</td>

      <td>${escapeHtml(intern.phone)}</td>

      <td>${escapeHtml(intern.school)}</td>

      <td>${escapeHtml(intern.major)}</td>

    `;

    tableBody.appendChild(row);
  });
}

/*
==================================
TÌM KIẾM
==================================
*/

searchInput.addEventListener("input", function () {
  const keyword = this.value.trim().toLowerCase();

  const filtered = interns.filter(
    (intern) =>
      intern.fullName.toLowerCase().includes(keyword) ||
      intern.email.toLowerCase().includes(keyword),
  );

  renderInterns(filtered);
});

/*
==================================
THÔNG BÁO
==================================
*/

function showMessage(message, type) {
  messageBox.textContent = message;

  messageBox.className = `message-box ${type}`;

  setTimeout(() => {
    messageBox.className = "message-box";

    messageBox.textContent = "";
  }, 3000);
}

/*
==================================
CHỐNG CHÈN HTML VÀO TABLE
==================================
*/

function escapeHtml(value) {
  const div = document.createElement("div");

  div.textContent = value;

  return div.innerHTML;
}

/*
==================================
ĐĂNG XUẤT
==================================
*/

logoutBtn.addEventListener("click", function () {
  localStorage.removeItem("token");
  localStorage.removeItem("user");

  window.location.href = "../index.html";
});

/*
==================================
KHỞI TẠO
==================================
*/

async function initializeInternPage() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const session = await API.getCurrentUser();
    const permissions = session.permissions;
    document.getElementById("userRole").textContent = session.user.role;

    if (!permissions.includes("VIEW_INTERNS")) {
      window.location.href = permissions.includes("VIEW_PROFILE")
        ? "profile.html"
        : "dashboard.html";
      return;
    }

    canManageInterns = permissions.includes("MANAGE_INTERNS");
    if (!canManageInterns) {
      openFormBtn.hidden = true;
      formSection.classList.add("hidden");
      document.querySelector(".page-heading h2").textContent =
        "Danh sách thực tập sinh";
    }

    await loadInterns();
  } catch (error) {
    showMessage(`Không thể xác thực phiên: ${error.message}`, "error");
    localStorage.clear();
    window.location.href = "../index.html";
  }
}

initializeInternPage();
