const form = document.getElementById("createInternForm");

const formSection = document.getElementById("internFormSection");

const openFormBtn = document.getElementById("openFormBtn");
const closeFormBtn = document.getElementById("closeFormBtn");
const cancelBtn = document.getElementById("cancelBtn");

const messageBox = document.getElementById("messageBox");

const tableBody = document.getElementById("internTableBody");
const searchInput = document.getElementById("searchInput");

const logoutBtn = document.getElementById("logoutBtn");


/*
==================================
DỮ LIỆU TẠM THỜI

Hiện tại dùng Array để test FE.
Sau này sẽ thay bằng API Backend.
==================================
*/

let interns = [];
let currentContractIntern = null;
const contractModal = document.getElementById("contractModal");
const contractFileInput = document.getElementById("contractFile");
const contractMessage = document.getElementById("contractMessage");
const currentContract = document.getElementById("currentContract");


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

    fullName:
      document.getElementById("fullName")
        .value
        .trim(),

    email:
      document.getElementById("email")
        .value
        .trim(),

    phone:
      document.getElementById("phone")
        .value
        .trim(),

    school:
      document.getElementById("school")
        .value
        .trim(),

    major:
      document.getElementById("major")
        .value
        .trim()

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

    showFieldError(
      "fullName",
      "Vui lòng nhập họ và tên."
    );

    valid = false;

  }


  if (!intern.email) {

    showFieldError(
      "email",
      "Vui lòng nhập email."
    );

    valid = false;

  } else {

    const emailRegex =
      /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

    if (!emailRegex.test(intern.email)) {

      showFieldError(
        "email",
        "Email không đúng định dạng."
      );

      valid = false;

    }

  }


  if (!intern.phone) {

    showFieldError(
      "phone",
      "Vui lòng nhập số điện thoại."
    );

    valid = false;

  } else {

    const phoneRegex = /^[0-9]{10,11}$/;

    if (!phoneRegex.test(intern.phone)) {

      showFieldError(
        "phone",
        "Số điện thoại phải có 10 đến 11 chữ số."
      );

      valid = false;

    }

  }


  if (!intern.school) {

    showFieldError(
      "school",
      "Vui lòng nhập tên trường."
    );

    valid = false;

  }


  if (!intern.major) {

    showFieldError(
      "major",
      "Vui lòng nhập chuyên ngành."
    );

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

  const input =
    document.getElementById(fieldId);

  const error =
    document.getElementById(
      `${fieldId}Error`
    );

  input.classList.add("input-error");

  error.textContent = message;

}


/*
==================================
XÓA LỖI
==================================
*/

function clearErrors() {

  const inputs =
    document.querySelectorAll(
      ".form-group input"
    );

  inputs.forEach((input) => {

    input.classList.remove(
      "input-error"
    );

  });


  const errors =
    document.querySelectorAll(
      ".error-message"
    );

  errors.forEach((error) => {

    error.textContent = "";

  });

}


/*
==================================
SUBMIT FORM
==================================
*/

form.addEventListener(
  "submit",
  function (event) {

    event.preventDefault();


    const intern = getFormData();


    if (!validateIntern(intern)) {

      showMessage(
        "Vui lòng kiểm tra lại thông tin.",
        "error"
      );

      return;

    }


    /*
    Kiểm tra email trùng.
    Hiện tại kiểm tra trên dữ liệu FE.
    Sau này Backend sẽ kiểm tra.
    */

    const emailExists =
      interns.some(
        (item) =>
          item.email.toLowerCase() ===
          intern.email.toLowerCase()
      );


    if (emailExists) {

      showFieldError(
        "email",
        "Email này đã tồn tại."
      );

      return;

    }


    /*
    Tạo ID tạm thời.
    Sau này ID sẽ do Backend tạo.
    */

    intern.id = Date.now();


    interns.push(intern);


    renderInterns(interns);


    showMessage(
      "Thêm hồ sơ thực tập sinh thành công!",
      "success"
    );


    closeForm();

  }
);


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
          colspan="6"
          class="empty-state"
        >
          Chưa có hồ sơ thực tập sinh.
        </td>
      </tr>
    `;

    return;

  }


  data.forEach((intern) => {

    const row =
  document.createElement("tr");

row.innerHTML = `
  <td>${escapeHtml(intern.fullName)}</td>
  <td>${escapeHtml(intern.email)}</td>
  <td>${escapeHtml(intern.phone)}</td>
  <td>${escapeHtml(intern.school)}</td>
  <td>${escapeHtml(intern.major)}</td>

  <td>
    <button
      type="button"
      class="btn-edit"
      data-id="${intern.id}"
    >
      Chỉnh sửa
    </button>
  </td>
`;

const editBtn = row.querySelector(".btn-edit");

editBtn.addEventListener("click", function () {
  openEditIntern(intern.id);
});

const contractBtn = document.createElement("button");
contractBtn.type = "button";
contractBtn.className = "btn-contract";
contractBtn.textContent = "Hợp đồng";
contractBtn.addEventListener("click", function () { openContractDialog(intern); });
row.querySelector("td:last-child").appendChild(contractBtn);

tableBody.appendChild(row);

  });

}


/*
==================================
TÌM KIẾM
==================================
*/

searchInput.addEventListener(
  "input",
  function () {

    const keyword =
      this.value
        .trim()
        .toLowerCase();


    const filtered =
      interns.filter((intern) =>

        intern.fullName
          .toLowerCase()
          .includes(keyword)

        ||

        intern.email
          .toLowerCase()
          .includes(keyword)

      );


    renderInterns(filtered);

  }
);


/*
==================================
THÔNG BÁO
==================================
*/

function showMessage(message, type) {

  messageBox.textContent = message;

  messageBox.className =
    `message-box ${type}`;


  setTimeout(() => {

    messageBox.className =
      "message-box";

    messageBox.textContent = "";

  }, 3000);

}


/*
==================================
CHỐNG CHÈN HTML VÀO TABLE
==================================
*/

function escapeHtml(value) {

  const div =
    document.createElement("div");

  div.textContent = value;

  return div.innerHTML;

}


/*
==================================
ĐĂNG XUẤT
==================================
*/

logoutBtn.addEventListener(
  "click",
  function () {

    localStorage.removeItem("token");
    localStorage.removeItem("user");
    localStorage.removeItem("role");

    window.location.href =
      "../index.html";

  }
);


/*
==================================
KHỞI TẠO
==================================
*/

loadInterns();
// ========================================
// CHỈNH SỬA HỒ SƠ THỰC TẬP SINH
// ========================================

function openEditIntern(id) {
    // Tìm thực tập sinh theo id
    const intern = interns.find(function (item) {
        return item.id == id;
    });

    if (!intern) {
        alert("Không tìm thấy hồ sơ thực tập sinh.");
        return;
    }

    // Đưa dữ liệu hiện tại vào form chỉnh sửa
    document.getElementById("editInternId").value = intern.id;
    document.getElementById("editFullName").value = intern.fullName;
    document.getElementById("editEmail").value = intern.email;
    document.getElementById("editPhone").value = intern.phone;
    document.getElementById("editSchool").value = intern.school;
    document.getElementById("editMajor").value = intern.major;

    // Hiển thị modal
    const modal = document.getElementById("editInternModal");

    if (modal) {
        modal.classList.remove("hidden");
    }
}
// ========================================
// LƯU THAY ĐỔI HỒ SƠ THỰC TẬP SINH
// ========================================

const editInternForm = document.getElementById("editInternForm");

if (editInternForm) {
    editInternForm.addEventListener("submit", function (event) {
        event.preventDefault();

        // Lấy ID thực tập sinh đang chỉnh sửa
        const id = document.getElementById("editInternId").value;

        // Tìm vị trí thực tập sinh trong mảng
        const index = interns.findIndex(function (item) {
            return item.id == id;
        });

        if (index === -1) {
            alert("Không tìm thấy hồ sơ thực tập sinh.");
            return;
        }

        // Cập nhật thông tin
        interns[index].fullName =
            document.getElementById("editFullName").value.trim();

        interns[index].email =
            document.getElementById("editEmail").value.trim();

        interns[index].phone =
            document.getElementById("editPhone").value.trim();

        interns[index].school =
            document.getElementById("editSchool").value.trim();

        interns[index].major =
            document.getElementById("editMajor").value.trim();

        // Render lại bảng
        renderInterns(interns);

        // Đóng modal
        const modal = document.getElementById("editInternModal");

        if (modal) {
            modal.classList.add("hidden");
        }

        alert("Cập nhật hồ sơ thành công!");
    });
}

async function loadInterns() {
  try {
    var rows = await API.getInterns();
    interns = rows.map(function (row) { return { id: row.id, fullName: row.name, email: row.email, phone: row.phone || "", school: row.school, major: row.major, mssv: row.mssv, role: row.role, status: row.status, mentor: row.mentor, progress: row.progress, gpa: row.gpa }; });
    renderInterns(interns);
  } catch (error) { renderInterns([]); showMessage("Không thể tải danh sách thực tập sinh: " + error.message, "error"); }
}
function formatFileSize(bytes) { if (bytes < 1024) return bytes + " B"; if (bytes < 1048576) return (bytes / 1024).toFixed(1) + " KiB"; return (bytes / 1048576).toFixed(2) + " MiB"; }
function showContractMessage(message, type) { contractMessage.textContent = message; contractMessage.className = "message-box " + (type || "error"); }
function openContractDialog(intern) {
  currentContractIntern = intern;
  document.getElementById("contractInternName").textContent = intern.fullName + " · MSSV " + (intern.mssv || "—");
  contractFileInput.value = ""; document.getElementById("contractFileInfo").textContent = "Chưa chọn tệp.";
  contractMessage.textContent = ""; contractMessage.className = "message-box";
  currentContract.innerHTML = '<p class="contract-muted">Đang tải hợp đồng…</p>';
  contractModal.classList.remove("hidden"); refreshContract();
}
async function refreshContract() {
  if (!currentContractIntern) return;
  try {
    var contract = await API.getInternContract(currentContractIntern.id);
    if (!contract) { currentContract.innerHTML = '<p class="contract-empty">Thực tập sinh chưa có hợp đồng.</p>'; return; }
    currentContract.innerHTML = '<div class="contract-summary"><div><strong>' + escapeHtml(contract.fileName) + '</strong><p>' + formatFileSize(contract.fileSize) + ' · Tải lên ' + new Date(contract.uploadedAt).toLocaleString("vi-VN") + '</p></div><div class="contract-actions"><button type="button" class="btn-secondary" data-view-contract="' + contract.contractId + '">Xem</button><button type="button" class="btn-primary" data-download-contract="' + contract.contractId + '">Tải xuống</button></div></div>';
  } catch (error) { currentContract.innerHTML = '<p class="contract-error">' + escapeHtml(error.message) + '</p>'; }
}
contractFileInput.addEventListener("change", function () { var file = contractFileInput.files && contractFileInput.files[0]; document.getElementById("contractFileInfo").textContent = file ? file.name + " · " + formatFileSize(file.size) : "Chưa chọn tệp."; });
document.getElementById("uploadContractBtn").addEventListener("click", async function (event) {
  var file = contractFileInput.files && contractFileInput.files[0];
  if (!file) return showContractMessage("Vui lòng chọn tệp PDF trước khi tải lên.");
  if (!file.name.toLowerCase().endsWith(".pdf") || (file.type && !["application/pdf", "application/octet-stream"].includes(file.type))) return showContractMessage("Chỉ chấp nhận tệp PDF.");
  if (file.size === 0) return showContractMessage("Tệp đang trống.");
  if (file.size > 10 * 1024 * 1024) return showContractMessage("Tệp vượt quá giới hạn 10 MiB.");
  var button = event.currentTarget; button.disabled = true; button.textContent = "Đang tải lên…";
  try { await API.uploadInternContract(currentContractIntern.id, file); showContractMessage("Tải hợp đồng lên thành công.", "success"); contractFileInput.value = ""; document.getElementById("contractFileInfo").textContent = "Chưa chọn tệp."; await refreshContract(); }
  catch (error) { showContractMessage(error.message); }
  finally { button.disabled = false; button.textContent = "Tải hợp đồng lên"; }
});
contractModal.querySelectorAll("[data-contract-close]").forEach(function (element) { element.addEventListener("click", function () { contractModal.classList.add("hidden"); }); });
contractModal.addEventListener("click", async function (event) {
  var viewButton = event.target.closest("[data-view-contract]"), downloadButton = event.target.closest("[data-download-contract]");
  if (!viewButton && !downloadButton) return;
  var view = Boolean(viewButton), id = view ? viewButton.dataset.viewContract : downloadButton.dataset.downloadContract;
  var tab = view ? window.open("about:blank", "_blank") : null;
  try {
    var result = await API.getContractFile(id, view), url = URL.createObjectURL(result.blob);
    if (view && tab) tab.location.href = url;
    else { var link = document.createElement("a"); link.href = url; link.download = result.fileName || "hop-dong.pdf"; document.body.appendChild(link); link.click(); link.remove(); }
    window.setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
    if (view && !tab) showContractMessage("Trình duyệt đã chặn tab xem; tệp đã được tải xuống.");
  } catch (error) { if (tab) tab.close(); showContractMessage(error.message); }
});
