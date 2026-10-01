const form = document.getElementById("createInternForm");

const formSection = document.getElementById("internFormSection");

const openFormBtn = document.getElementById("openFormBtn");
const closeFormBtn = document.getElementById("closeFormBtn");
const cancelBtn = document.getElementById("cancelBtn");

const messageBox = document.getElementById("messageBox");

const documentModal = document.getElementById("documentModal");
const closeDocModalBtn = document.getElementById("closeDocModalBtn");
const docTableBody = document.getElementById("documentTableBody");
let currentInternId = null;

const tableBody = document.getElementById("internTableBody");
const searchInput = document.getElementById("searchInput");

const logoutBtn = document.getElementById("logoutBtn");

const INTERN_API_URL = "http://localhost:5024/api/interns";
let interns = [];
let editingInternId = null;
let isSaving = false;
const saveInternBtn = document.getElementById("saveInternBtn");
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
<<<<<<< HEAD
    interns.forEach(updateInternOverallStatus);
    renderInterns(interns);
=======
    applySearch();
>>>>>>> origin/develop
    return true;
  } catch (error) {
    // Fake data cho danh sách thực tập sinh để test giao diện
    interns = [
      { 
        id: "1", fullName: "Nguyễn Văn A", email: "nva@gmail.com", phone: "0123456789", school: "ĐH Bách Khoa", major: "CNTT",
        documents: [
          { id: "1-1", name: "CV Thực tập sinh", url: "#", status: "Chưa nộp" },
          { id: "1-2", name: "Giấy giới thiệu", url: "#", status: "Đã duyệt" }
        ]
      },
      { 
        id: "2", fullName: "Trần Thị B", email: "ttb@gmail.com", phone: "0987654321", school: "ĐH Kinh Tế", major: "Kế toán",
        documents: [
          { id: "2-1", name: "CV Thực tập sinh", url: "#", status: "Đã duyệt" },
          { id: "2-2", name: "Giấy giới thiệu", url: "#", status: "Đã duyệt" }
        ]
      }
    ];
    interns.forEach(updateInternOverallStatus);
    renderInterns(interns);
    showMessage(`Đang dùng dữ liệu mẫu (Do lỗi API: ${error.message})`, "error");
    return false;
  }
}

/*
==================================
MỞ FORM
==================================
*/

openFormBtn.addEventListener("click", () => {
  if (!canManageInterns || isSaving) return;
  closeForm();
  formSection.classList.remove("hidden");

  document.getElementById("fullName").focus();
});

/*
==================================
ĐÓNG FORM
==================================
*/

function closeForm() {
  if (isSaving) return;
  editingInternId = null;
  document.getElementById("internFormTitle").textContent = "Thêm hồ sơ thực tập sinh";
  saveInternBtn.textContent = "Thêm hồ sơ";
  formSection.classList.add("hidden");

  form.reset();

  clearErrors();
}

closeFormBtn.addEventListener("click", closeForm);

cancelBtn.addEventListener("click", closeForm);

if (closeDocModalBtn) {
  closeDocModalBtn.addEventListener("click", () => {
    documentModal.classList.add("hidden");
  });
}

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
    position: document.getElementById("position").value.trim(),
    mentor: document.getElementById("mentor").value.trim(),
    organization: document.getElementById("organization").value.trim(),

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

  if (!intern.position) {
    showFieldError("position", "Vui lòng nhập vị trí thực tập.");
    valid = false;
  }

  if (!intern.mentor) {
    showFieldError("mentor", "Vui lòng nhập người hướng dẫn.");
    valid = false;
  }

  if (!intern.organization) {
    showFieldError("organization", "Vui lòng nhập đơn vị thực tập.");
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

  if (isSaving) return;
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
    (item) => item.id !== editingInternId && item.email.toLowerCase() === intern.email.toLowerCase(),
  );

  if (emailExists) {
    showFieldError("email", "Email này đã tồn tại.");

    return;
  }

  const isEditing = editingInternId !== null;
  isSaving = true;
  Array.from(form.elements).forEach((element) => { element.disabled = true; });
  closeFormBtn.disabled = true;
  openFormBtn.disabled = true;
  saveInternBtn.textContent = "Đang lưu...";
  try {
    await requestInternApi(isEditing ? `/${editingInternId}` : "", {
      method: isEditing ? "PUT" : "POST",
      body: JSON.stringify(intern),
    });
    isSaving = false;
    closeForm();
    if (await loadInterns()) {
      showMessage(isEditing ? "Lưu thay đổi thành công!" : "Thêm hồ sơ thực tập sinh thành công!", "success");
    }
  } catch (error) {
    showMessage(error.message, "error");
  } finally {
    isSaving = false;
    Array.from(form.elements).forEach((element) => { element.disabled = false; });
    closeFormBtn.disabled = false;
    openFormBtn.disabled = false;
    saveInternBtn.textContent = editingInternId !== null ? "Lưu thay đổi" : "Thêm hồ sơ";
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
<<<<<<< HEAD
          colspan="7"
=======
          colspan="9"
>>>>>>> origin/develop
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

    const docStatus = intern.docStatus || 'Chưa nộp';
    let statusColor = 'orange';
    if (docStatus === 'Đã duyệt đủ') statusColor = 'green';
    else if (docStatus.includes('Thiếu') || docStatus.includes('Từ chối') || docStatus === 'Chưa có tài liệu') statusColor = 'red';

    row.innerHTML = `

      <td>${escapeHtml(intern.fullName)}</td>

      <td>${escapeHtml(intern.email)}</td>

      <td>${escapeHtml(intern.phone)}</td>

      <td>${escapeHtml(intern.school)}</td>

      <td>${escapeHtml(intern.major)}</td>
      <td>${escapeHtml(intern.position || "—")}</td>
      <td>${escapeHtml(intern.mentor || "—")}</td>
      <td>${escapeHtml(intern.organization || "—")}</td>
      <td class="intern-actions"></td>

      <td><span style="color: ${statusColor}; font-weight: bold;">${escapeHtml(docStatus)}</span></td>
      
      <td>
        <button class="btn-secondary" style="padding: 4px 8px; font-size: 12px;" onclick="openDocumentModal('${intern.id}', '${escapeHtml(intern.fullName)}')">
          Duyệt tài liệu
        </button>
      </td>
    `;

    if (canManageInterns) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = "btn-secondary";
      button.textContent = "Chỉnh sửa";
      button.setAttribute("aria-label", `Chỉnh sửa hồ sơ ${intern.fullName}`);
      button.addEventListener("click", () => openEditForm(intern));
      row.querySelector(".intern-actions").appendChild(button);
    }
    tableBody.appendChild(row);
  });
}

/*
==================================
TÌM KIẾM
==================================
*/

function applySearch() {
  const keyword = searchInput.value.trim().toLowerCase();

  const filtered = interns.filter(
    (intern) =>
      intern.fullName.toLowerCase().includes(keyword) ||
      intern.email.toLowerCase().includes(keyword),
  );

  renderInterns(filtered);
}
searchInput.addEventListener("input", applySearch);

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
QUẢN LÝ TÀI LIỆU
==================================
*/

async function openDocumentModal(internId, internName) {
  currentInternId = internId;
  document.getElementById("docInternName").textContent = internName;
  documentModal.classList.remove("hidden");
  docTableBody.innerHTML = `<tr><td colspan="4" class="empty-state">Đang tải...</td></tr>`;

  try {
    const docs = await requestInternApi(`/${internId}/documents`, { method: "GET" });
    if (!Array.isArray(docs)) throw new Error("Format error");
    renderDocuments(docs);
  } catch (error) {
    // Dùng data nội bộ từ interns
    const intern = interns.find(i => i.id === internId);
    if (intern && intern.documents) {
      renderDocuments(intern.documents);
    } else {
      renderDocuments([]);
    }
  }
}

function renderDocuments(docs) {
  docTableBody.innerHTML = "";
  if (!docs || docs.length === 0) {
    docTableBody.innerHTML = `<tr><td colspan="4" class="empty-state">Chưa có tài liệu nào.</td></tr>`;
    return;
  }

  docs.forEach(doc => {
    const row = document.createElement("tr");
    row.innerHTML = `
      <td>${escapeHtml(doc.name)}</td>
      <td><a href="${doc.url}" target="_blank">Xem</a></td>
      <td>
        <select id="select-status-${doc.id}" style="padding: 4px;">
            <option value="Chưa nộp" ${doc.status === 'Chưa nộp' ? 'selected' : ''}>Chưa nộp</option>
            <option value="Chờ duyệt" ${doc.status === 'Chờ duyệt' ? 'selected' : ''}>Chờ duyệt</option>
            <option value="Đã duyệt" ${doc.status === 'Đã duyệt' ? 'selected' : ''}>Đã duyệt</option>
            <option value="Từ chối" ${doc.status === 'Từ chối' ? 'selected' : ''}>Từ chối</option>
        </select>
      </td>
      <td>
        <button class="btn-primary" style="padding: 4px 8px; font-size: 12px;" onclick="saveDocumentStatus('${doc.id}')">Lưu trạng thái</button>
      </td>
    `;
    docTableBody.appendChild(row);
  });
}

async function saveDocumentStatus(documentId) {
  const newStatus = document.getElementById(`select-status-${documentId}`).value;
  try {
    // Gọi API thật khi backend sẵn sàng:
    // await requestInternApi(`/${currentInternId}/documents/${documentId}/status`, { 
    //   method: "PUT",
    //   body: JSON.stringify({ status: newStatus })
    // });
    
    // Cập nhật dữ liệu nội bộ
    const internIndex = interns.findIndex(i => i.id === currentInternId);
    if (internIndex !== -1) {
      const intern = interns[internIndex];
      const doc = intern.documents.find(d => d.id === documentId);
      if (doc) doc.status = newStatus;
      
      updateInternOverallStatus(intern);
      renderInterns(interns); // Render lại bảng danh sách bên ngoài
    }

    showMessage(`Đã lưu trạng thái thành công!`, "success");
    
  } catch (error) {
    showMessage("Lỗi khi lưu trạng thái: " + error.message, "error");
  }
}

/*
==================================
TÍNH TOÁN TRẠNG THÁI TÀI LIỆU
==================================
*/
function updateInternOverallStatus(intern) {
  if (!intern.documents || intern.documents.length === 0) {
    intern.docStatus = "Chưa có tài liệu";
    return;
  }
  
  const statuses = intern.documents.map(d => d.status);
  
  if (statuses.includes("Từ chối")) {
    intern.docStatus = "Có tài liệu bị từ chối";
  } else if (statuses.includes("Chưa nộp")) {
    const missingDocs = intern.documents.filter(d => d.status === "Chưa nộp").map(d => d.name);
    intern.docStatus = `Thiếu: ${missingDocs.join(', ')}`;
  } else if (statuses.includes("Chờ duyệt")) {
    intern.docStatus = "Chờ duyệt";
  } else {
    intern.docStatus = "Đã duyệt đủ";
  }
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
  /* Tạm thời comment đoạn check token để test giao diện
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }
  */

  try {
    // Tạm thời comment API check user
    // const session = await API.getCurrentUser();
    // const permissions = session.permissions;
    // document.getElementById("userRole").textContent = session.user.role;

    // Giả lập quyền để test:
    const permissions = ["VIEW_INTERNS", "MANAGE_INTERNS"];
    document.getElementById("userRole").textContent = "HR (Demo)";

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

function openEditForm(intern) {
  if (!canManageInterns || isSaving) return;
  closeForm();
  editingInternId = intern.id;
  Object.keys(getFormData()).forEach((key) => {
    document.getElementById(key).value = intern[key] || "";
  });
  document.getElementById("internFormTitle").textContent = "Chỉnh sửa hồ sơ thực tập sinh";
  saveInternBtn.textContent = "Lưu thay đổi";
  formSection.classList.remove("hidden");
  formSection.scrollIntoView({ behavior: "smooth", block: "start" });
  document.getElementById("fullName").focus();
}
