
"use strict";

const EVALUATION_API_URL = BASE_URL + "/FinalEvaluations";

let evaluations = [];
let filteredEvaluations = [];
let currentRole = "", evaluationSession = {};

document.addEventListener("session-permissions-updated", event => {
  evaluationSession = event.detail; currentRole = evaluationSession.user?.role || "";
  if (!Session.hasPermission(evaluationSession, "VIEW_FINAL_EVALUATIONS")) {
    evaluations = []; filteredEvaluations = []; getElement("evaluationTableBody")?.replaceChildren();
  } else if (typeof renderTable === "function") renderTable();
  if (!canEditEvaluation()) closeEvaluationForm();
});
function canEditEvaluation() {
  return ["ADMIN", "HR", "MENTOR"].includes(currentRole) && Session.hasPermission(evaluationSession, "VIEW_FINAL_EVALUATIONS") && Session.hasPermission(evaluationSession, "UPDATE_FINAL_EVALUATIONS");
}

function getElement(id) {
  return document.getElementById(id);
}

// Lấy token đăng nhập
function getToken() {
  const keys = ["authToken", "accessToken", "token"];

  for (const key of keys) {
    const token =
      localStorage.getItem(key) ||
      sessionStorage.getItem(key);

    if (token) {
      return token.replace(/^Bearer\s+/i, "");
    }
  }

  return "";
}

// Xác định vai trò từ thông tin đăng nhập
function getCurrentRole() {
  const roleKeys = ["role", "userRole", "currentRole"];
  const stores = [localStorage, sessionStorage];

  for (const store of stores) {
    for (const key of roleKeys) {
      const role = store.getItem(key);

      if (role) {
        return role.toUpperCase();
      }
    }

    for (const key of ["user", "currentUser", "authUser"]) {
      try {
        const user = JSON.parse(store.getItem(key) || "null");
        if (user?.role) {
          return String(user.role).toUpperCase();
        }
      } catch {
        // Bỏ qua dữ liệu không phải JSON
      }
    }
  }

  return "";
}

// Gọi API
async function apiRequest(path = "", method = "GET", body = null) {
  const token = getToken();

  const headers = {
    Accept: "application/json"
  };

  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  if (body !== null) {
    headers["Content-Type"] = "application/json";
  }

  const response = await fetch(`${EVALUATION_API_URL}${path}`, {
    method,
    headers,
    ...(body !== null ? { body: JSON.stringify(body) } : {})
  });

  if (!response.ok) {
    let message = "";

    try {
      const errorData = await response.json();

      message =
        typeof errorData === "string"
          ? errorData
          : errorData.message ||
            errorData.title ||
            errorData.detail ||
            "";
    } catch {
      message = await response.text().catch(() => "");
    }

    if (response.status === 401) {
      throw new Error("Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.");
    }

    if (response.status === 403) {
      throw new Error("Tài khoản không có quyền thực hiện thao tác này.");
    }

    if (response.status === 409) {
      throw new Error(message || "Thực tập sinh đã có đánh giá.");
    }

    throw new Error(message || `Lỗi API: ${response.status}`);
  }

  if (response.status === 204) {
    return null;
  }

  return response.json();
}

function apiGet(path = "") {
  return apiRequest(path);
}

// Chuẩn hóa dữ liệu
function normalizeEvaluation(item) {
  return {
    id: Number(item.internId),
    studentCode: item.studentCode ?? "",
    name: item.name ?? "",
    className: item.className ?? "",
    company: item.company ?? "",
    mentor: item.mentor ?? "",
    programName: item.programName ?? "",
    evaluationSource: item.evaluationSource ?? "",
    mentorScore: item.mentorScore,
    mentorComment: item.mentorComment ?? "",
    mentorPeriod: item.mentorPeriod ?? "",
    mentorAttendance: item.mentorAttendance,
    mentorAttitude: item.mentorAttitude,
    mentorProfessionalSkills: item.mentorProfessionalSkills,
    mentorWorkResult: item.mentorWorkResult,
    mentorClassification: item.mentorClassification ?? "",
    period: item.period ?? "",
    attendance: item.attendance,
    attitude: item.attitude,
    professionalSkills: item.professionalSkills,
    workResult: item.workResult,
    finalScore: item.finalScore,
    classification: item.classification ?? "",
    status: item.status ?? "Chưa đánh giá",
    comment: item.comment ?? ""
  };
}

function formatValue(value) {
  return value === null || value === undefined || value === ""
    ? "Chưa cập nhật"
    : String(value);
}

function isEvaluated(item) {
  return item.status === "Đã đánh giá";
}

// Thống kê
function updateStatistics(statistics = null) {
  const evaluated = evaluations.filter(isEvaluated);

  const scores = evaluated
    .map(item => item.finalScore)
    .filter(value => value !== null && value !== undefined && value !== "")
    .map(Number)
    .filter(Number.isFinite);

  const average = scores.length
    ? scores.reduce((sum, score) => sum + score, 0) / scores.length
    : null;

  const total = statistics?.totalInterns ?? evaluations.length;
  const evaluatedCount = statistics?.evaluatedCount ?? evaluated.length;
  const pendingCount = statistics?.pendingCount ?? total - evaluatedCount;
  const averageScore = statistics?.averageScore ?? average;

  getElement("totalInterns").textContent = total;
  getElement("evaluatedCount").textContent = evaluatedCount;
  getElement("pendingCount").textContent = pendingCount;

  getElement("averageScore").textContent =
    averageScore === null || averageScore === undefined
      ? "—"
      : Number(averageScore).toFixed(2);
}

// Bộ lọc
function populateFilters() {
  const periodFilter = getElement("periodFilter");
  const classificationFilter = getElement("classificationFilter");

  const oldPeriod = periodFilter.value;
  const oldClassification = classificationFilter.value;

  periodFilter.replaceChildren(new Option("Tất cả đợt", ""));
  classificationFilter.replaceChildren(
    new Option("Tất cả xếp loại", "")
  );

  const periods = [
    ...new Set(evaluations.map(item => item.period).filter(Boolean))
  ];

  const classifications = [
    ...new Set(evaluations.map(item => item.classification).filter(Boolean))
  ];

  periods.forEach(period => {
    periodFilter.add(new Option(period, period));
  });

  classifications.forEach(classification => {
    classificationFilter.add(new Option(classification, classification));
  });

  periodFilter.value = oldPeriod;
  classificationFilter.value = oldClassification;
}

function createCell(row, value) {
  const cell = document.createElement("td");

  cell.textContent =
    value === null || value === undefined || value === ""
      ? "—"
      : String(value);

  row.appendChild(cell);
  return cell;
}

// Hiển thị danh sách
function renderTable() {
  const tableBody = getElement("evaluationTableBody");
  tableBody.replaceChildren();

  filteredEvaluations.forEach(item => {
    const row = document.createElement("tr");

    createCell(row, item.name);
    createCell(row, item.mentor);
    createCell(row, item.programName);

    const statusCell = document.createElement("td");
    const status = document.createElement("span");

    status.className = isEvaluated(item)
      ? "status status-done"
      : "status status-pending";

    status.textContent = item.status;

    statusCell.appendChild(status);
    row.appendChild(statusCell);

    const actionCell = document.createElement("td");

    const detailButton = document.createElement("button");
    detailButton.type = "button";
    detailButton.className = "detail-btn";
    detailButton.textContent = "Xem chi tiết";

    detailButton.addEventListener("click", () => {
      showDetail(item.id);
    });

    actionCell.appendChild(detailButton);

    // HR và MENTOR được thêm hoặc sửa đánh giá
    if (canEditEvaluation()) {
      const editButton = document.createElement("button");

      editButton.type = "button";
      editButton.className = "primary-btn";
      editButton.textContent = currentRole === "MENTOR"
        ? (isEvaluated(item) ? "Cập nhật" : "Đánh giá")
        : (isEvaluated(item) ? "Cập nhật tổng hợp" : "Tổng hợp");

      editButton.style.marginLeft = "8px";

      editButton.addEventListener("click", () => {
        openEvaluationForm(item.id);
      });

      actionCell.appendChild(editButton);
    }

    row.appendChild(actionCell);
    tableBody.appendChild(row);
  });

  getElement("emptyMessage").hidden =
    filteredEvaluations.length > 0;

  getElement("resultCount").textContent =
    `Hiển thị ${filteredEvaluations.length}/${evaluations.length} thực tập sinh`;
}

// Tìm kiếm và lọc
function filterEvaluations() {
  const keyword = getElement("searchInput")
    .value.trim().toLocaleLowerCase("vi");

  const period = getElement("periodFilter").value;
  const classification = getElement("classificationFilter").value;
  const status = getElement("statusFilter").value;

  filteredEvaluations = evaluations.filter(item => {
    const matchesSearch =
      item.name.toLocaleLowerCase("vi").includes(keyword) ||
      item.studentCode.toLocaleLowerCase("vi").includes(keyword);

    return (
      matchesSearch &&
      (!period || item.period === period) &&
      (!classification || item.classification === classification) &&
      (!status || item.status === status)
    );
  });

  getElement("detailSection").hidden = true;
  renderTable();
}

// Hiển thị chi tiết
function createInfoItem(label, value) {
  const item = document.createElement("div");
  item.className = "info-item";

  const title = document.createElement("strong");
  title.textContent = label;

  const content = document.createElement("span");
  content.textContent = formatValue(value);

  item.append(title, content);
  return item;
}

function renderInformation(containerId, entries) {
  const container = getElement(containerId);
  container.replaceChildren();

  entries.forEach(([label, value]) => {
    container.appendChild(createInfoItem(label, value));
  });
}

function showDetail(id) {
  const intern = evaluations.find(item => item.id === id);

  if (!intern) {
    AppUI.toast("Không tìm thấy thực tập sinh.", "error");
    return;
  }

  renderInformation("internInfo", [
    ["Họ và tên", intern.name],
    ["Mã sinh viên", intern.studentCode],
    ["Lớp", intern.className],
    ["Đơn vị thực tập", intern.company],
    ["Người hướng dẫn", intern.mentor],
    ["Chương trình", intern.programName]
  ]);

  renderInformation("mentorEvaluationResult", [
    ["Đợt đánh giá", intern.mentorPeriod],
    ["Điểm chuyên cần", intern.mentorAttendance],
    ["Điểm thái độ", intern.mentorAttitude],
    ["Điểm chuyên môn", intern.mentorProfessionalSkills],
    ["Kết quả công việc", intern.mentorWorkResult],
    ["Điểm tổng kết", intern.mentorScore],
    ["Xếp loại", intern.mentorClassification]
  ]);

  renderInformation("evaluationResult", [
    ["Điểm chuyên cần", intern.attendance],
    ["Điểm thái độ", intern.attitude],
    ["Điểm chuyên môn", intern.professionalSkills],
    ["Kết quả công việc", intern.workResult],
    ["Điểm tổng kết", intern.finalScore],
    ["Xếp loại", intern.classification],
    ["Trạng thái", intern.status]
  ]);

  getElement("mentorComment").textContent =
    intern.mentorComment || "Chưa có đánh giá của Mentor.";

  getElement("hrComment").textContent =
    intern.evaluationSource === "HR" && intern.comment
      ? intern.comment
      : "HR chưa tổng hợp đánh giá.";
  getElement("hrEvaluationPanel").hidden = currentRole === "MENTOR";

  getElement("evaluationFormSection").hidden = true;

  const section = getElement("detailSection");
  section.hidden = false;

  section.scrollIntoView({
    behavior: "smooth",
    block: "start"
  });
}

// Mở form thêm/sửa
function openEvaluationForm(id) {
  if (!canEditEvaluation()) {
    AppUI.toast("Bạn không có quyền thêm hoặc sửa đánh giá.", "error");
    return;
  }

  const intern = evaluations.find(item => item.id === id);

  if (!intern) {
    AppUI.toast("Không tìm thấy thực tập sinh.", "error");
    return;
  }

  const evaluated = isEvaluated(intern);

  getElement("evaluationForm").reset();

  getElement("evaluationInternId").value = intern.id;
  getElement("evaluationMode").value =
    evaluated ? "update" : "create";

  getElement("evaluationFormTitle").textContent =
    currentRole === "MENTOR"
      ? (evaluated ? "Cập nhật đánh giá Intern" : "Đánh giá Intern")
      : (evaluated ? "Cập nhật tổng hợp của HR" : "Tạo tổng hợp của HR");

  getElement("evaluationInternName").textContent =
    `${intern.name} - ${intern.studentCode}`;

  renderInformation("evaluationFormInternInfo", [
    ["Họ và tên", intern.name],
    ["Mã sinh viên", intern.studentCode],
    ["Mentor", intern.mentor],
    ["Chương trình", intern.programName]
  ]);

  getElement("evaluationPeriod").value = intern.period || "";

  if (evaluated) {
    const fields = {
      evaluationAttendance: intern.attendance,
      evaluationAttitude: intern.attitude,
      evaluationProfessionalSkills: intern.professionalSkills,
      evaluationWorkResult: intern.workResult,
      evaluationFinalScore: intern.finalScore,
      evaluationClassification: intern.classification,
      evaluationComment: intern.comment
    };

    Object.entries(fields).forEach(([id, value]) => {
      getElement(id).value = value ?? "";
    });
  }

  getElement("evaluationFormMessage").textContent = "";
  clearEvaluationErrors();
  getElement("detailSection").hidden = true;

  const section = getElement("evaluationFormSection");
  section.hidden = false;

  section.scrollIntoView({
    behavior: "smooth",
    block: "start"
  });
}

// Đóng form
function closeEvaluationForm() {
  getElement("evaluationFormSection").hidden = true;
  getElement("evaluationForm").reset();
  getElement("evaluationFormMessage").textContent = "";
  clearEvaluationErrors();
}

const evaluationFields = [
  "evaluationPeriod", "evaluationAttendance", "evaluationAttitude",
  "evaluationProfessionalSkills", "evaluationWorkResult",
  "evaluationFinalScore", "evaluationClassification", "evaluationComment"
];

function clearEvaluationErrors() {
  evaluationFields.forEach(id => {
    const field = getElement(id);
    const error = getElement(`${id}Error`);
    field?.removeAttribute("aria-invalid");
    if (error) error.textContent = "";
  });
}

function validateEvaluationForm() {
  clearEvaluationErrors();
  let firstInvalid = null;
  evaluationFields.forEach(id => {
    const field = getElement(id);
    if (!field || field.checkValidity()) return;
    field.setAttribute("aria-invalid", "true");
    const error = getElement(`${id}Error`);
    if (error) error.textContent = field.validity.valueMissing
      ? "Vui lòng nhập trường này."
      : "Giá trị phải nằm trong khoảng từ 0 đến 10.";
    firstInvalid ||= field;
  });
  firstInvalid?.focus();
  return !firstInvalid;
}

// Đọc dữ liệu nhập
function getEvaluationPayload() {
  return {
    period: getElement("evaluationPeriod").value.trim(),
    attendance: Number(getElement("evaluationAttendance").value),
    attitude: Number(getElement("evaluationAttitude").value),
    professionalSkills: Number(
      getElement("evaluationProfessionalSkills").value
    ),
    workResult: Number(getElement("evaluationWorkResult").value),
    finalScore: Number(getElement("evaluationFinalScore").value),
    classification: getElement("evaluationClassification").value,
    comment: getElement("evaluationComment").value.trim()
  };
}

// Lưu đánh giá xuống SQL Server thông qua Backend
async function saveEvaluation(event) {
  event.preventDefault();

  if (!canEditEvaluation()) {
    AppUI.toast("Bạn không có quyền lưu đánh giá.", "error");
    return;
  }

  const form = getElement("evaluationForm");
  if (getElement("saveEvaluationBtn").disabled) return;

  if (!validateEvaluationForm()) {
    return;
  }

  const internId = Number(getElement("evaluationInternId").value);
  const mode = getElement("evaluationMode").value;

  if (!Number.isInteger(internId) || internId <= 0) {
    AppUI.toast("ID thực tập sinh không hợp lệ.", "error");
    return;
  }

  const payload = getEvaluationPayload();
  const method = mode === "update" ? "PUT" : "POST";

  const saveButton = getElement("saveEvaluationBtn");
  const message = getElement("evaluationFormMessage");

  saveButton.disabled = true;
  saveButton.textContent = "Đang lưu...";
  message.textContent = "";

  try {
    await apiRequest(`/${internId}`, method, payload);

    closeEvaluationForm();
    await loadEvaluations();

    getElement("evaluationMessage").textContent =
      "Đã lưu đánh giá cuối kỳ thành công.";
    AppUI.toast("Đã lưu đánh giá.", "success");

    window.scrollTo({
      top: 0,
      behavior: "smooth"
    });
  } catch (error) {
    console.error("Lỗi lưu đánh giá:", error);
    message.textContent = error.message;
    AppUI.toast(error.message, "error");
  } finally {
    saveButton.disabled = false;
    saveButton.textContent = "Lưu đánh giá";
  }
}

// Xuất CSV
function csvCell(value) {
  const text = value === null || value === undefined
    ? ""
    : String(value);

  const safeText = /^\s*[=+\-@]/.test(text)
    ? "'" + text
    : text;

  return `"${safeText.replace(/"/g, '""')}"`;
}

function exportCSV() {
  if (filteredEvaluations.length === 0) {
    AppUI.toast("Không có dữ liệu để xuất báo cáo.", "error");
    return;
  }

  const headers = [
    "Mã sinh viên",
    "Họ và tên",
    "Lớp",
    "Đơn vị thực tập",
    "Người hướng dẫn",
    "Đợt thực tập",
    "Điểm tổng kết",
    "Xếp loại",
    "Trạng thái",
    "Nhận xét"
  ];

  const rows = filteredEvaluations.map(item => [
    item.studentCode,
    item.name,
    item.className,
    item.company,
    item.mentor,
    item.period,
    item.finalScore,
    item.classification,
    item.status,
    item.comment
  ]);

  const csvContent = [headers, ...rows]
    .map(row => row.map(csvCell).join(","))
    .join("\r\n");

  const blob = new Blob(["\uFEFF", csvContent], {
    type: "text/csv;charset=utf-8;"
  });

  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");

  link.href = url;
  link.download = "tong-hop-danh-gia-cuoi-ky.csv";

  document.body.appendChild(link);
  link.click();
  link.remove();

  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// Tải dữ liệu từ Backend
async function loadEvaluations() {
  if (!Session.hasPermission(evaluationSession, "VIEW_FINAL_EVALUATIONS")) { getElement("evaluationTableBody").replaceChildren(); return; }
  const tableBody = getElement("evaluationTableBody");

  tableBody.replaceChildren();

  const loadingRow = document.createElement("tr");
  const loadingCell = document.createElement("td");

  loadingCell.colSpan = 5;
  loadingCell.textContent = "Đang tải dữ liệu đánh giá...";

  loadingRow.appendChild(loadingCell);
  tableBody.appendChild(loadingRow);

  try {
    const data = await apiGet();

    if (!Array.isArray(data)) {
      throw new Error("Dữ liệu API trả về không đúng định dạng.");
    }

    evaluations = data.map(normalizeEvaluation);

    populateFilters();

    let statistics = null;

    try {
      statistics = await apiGet("/statistics");
    } catch (error) {
      console.warn("Không tải được thống kê:", error);
    }

    updateStatistics(statistics);
    filterEvaluations();

    return true;
  } catch (error) {
    console.error("Lỗi tải đánh giá:", error);

    evaluations = [];
    filteredEvaluations = [];

    updateStatistics();

    tableBody.replaceChildren();

    const errorRow = document.createElement("tr");
    const errorCell = document.createElement("td");

    errorCell.colSpan = 5;
    errorCell.textContent = error.message;

    errorRow.appendChild(errorCell);
    tableBody.appendChild(errorRow);

    getElement("emptyMessage").hidden = true;
    getElement("resultCount").textContent =
      "Không thể tải dữ liệu từ Backend";
    AppUI.toast(error.message, "error");

    return false;
  }
}

// Khởi tạo
document.addEventListener("DOMContentLoaded", () => {
  currentRole = getCurrentRole();

  const roleBadge = getElement("roleBadge");

  if (roleBadge) {
    roleBadge.textContent = currentRole || "Người dùng";
  }

  getElement("searchInput").addEventListener(
    "input",
    filterEvaluations
  );

  getElement("periodFilter").addEventListener(
    "change",
    filterEvaluations
  );

  getElement("classificationFilter").addEventListener(
    "change",
    filterEvaluations
  );

  getElement("statusFilter").addEventListener(
    "change",
    filterEvaluations
  );

  getElement("exportBtn").addEventListener(
    "click",
    exportCSV
  );

  getElement("closeDetailBtn").addEventListener("click", () => {
    getElement("detailSection").hidden = true;
  });

  getElement("printBtn").addEventListener("click", () => {
    window.print();
  });

  getElement("evaluationForm").addEventListener(
    "submit",
    saveEvaluation
  );
  evaluationFields.forEach(id => getElement(id)?.addEventListener("input", () => {
    const error = getElement(`${id}Error`);
    if (error) error.textContent = "";
    getElement(id).removeAttribute("aria-invalid");
  }));

  getElement("closeEvaluationFormBtn").addEventListener(
    "click",
    closeEvaluationForm
  );

  getElement("cancelEvaluationBtn").addEventListener(
    "click",
    closeEvaluationForm
  );

  // Ưu tiên vai trò được Backend xác nhận thay vì dữ liệu lưu cũ trong trình duyệt.
  async function initializeEvaluations() {
    try {
      if (typeof API !== "undefined" && API.getCurrentUser) {
        const session = await API.getCurrentUser();
        evaluationSession = session;
        const serverRole = session?.user?.role;
        if (serverRole) {
          currentRole = String(serverRole).toUpperCase();
          if (roleBadge) roleBadge.textContent = currentRole;
        }
        const mentorView = currentRole === "MENTOR";
        const topbarTitle = document.querySelector(".dashboard-title h1");
        if (topbarTitle) topbarTitle.textContent = mentorView ? "Đánh giá thực tập sinh" : "Đánh giá cuối kỳ";
        getElement("evaluationPageTitle").textContent = mentorView ? "Đánh giá thực tập sinh" : "Tổng hợp đánh giá cuối kỳ";
        getElement("evaluationPageDescription").textContent = mentorView
          ? "Đánh giá kỹ năng, thái độ và kết quả của Intern bạn đang hướng dẫn."
          : "Xem đánh giá của Mentor và lưu phần tổng hợp cuối kỳ của HR riêng biệt.";
        getElement("evaluationListTitle").textContent = mentorView ? "Intern đang hướng dẫn" : "Danh sách Intern cuối kỳ";
        getElement("evaluationListDescription").textContent = mentorView
          ? "Chọn Intern để tạo hoặc cập nhật đánh giá."
          : "Tra cứu đánh giá Mentor và phần tổng hợp của HR.";
        getElement("exportBtn").hidden = mentorView;
      }
    } catch (error) {
      console.warn("Không thể xác nhận vai trò từ Backend:", error);
    }

    await loadEvaluations();
  }

  initializeEvaluations();
});
