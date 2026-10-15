
"use strict";

// Dữ liệu mẫu để kiểm thử giao diện HR.
// Khi tích hợp Backend, thay mảng này bằng dữ liệu từ API.
const evaluations = [
  {
    id: 1,
    studentCode: "DTC245200001",
    name: "Nguyễn Văn A",
    className: "CNTT K22A",
    company: "Công ty Công nghệ ABC",
    mentor: "Trần Văn B",
    period: "Đợt 1 - 2026",
    attendance: 9,
    attitude: 8.5,
    professionalSkills: 8,
    workResult: 9,
    finalScore: 8.6,
    classification: "Giỏi",
    status: "Đã đánh giá",
    comment: "Có trách nhiệm, hoàn thành tốt công việc."
  },
  {
    id: 2,
    studentCode: "DTC245200002",
    name: "Trần Thị B",
    className: "CNTT K22A",
    company: "Công ty Phần mềm XYZ",
    mentor: "Nguyễn Văn C",
    period: "Đợt 1 - 2026",
    attendance: 9.5,
    attitude: 9,
    professionalSkills: 9,
    workResult: 9,
    finalScore: 9.1,
    classification: "Xuất sắc",
    status: "Đã đánh giá",
    comment: "Chủ động, có kỹ năng chuyên môn tốt."
  },
  {
    id: 3,
    studentCode: "DTC245200003",
    name: "Lê Văn C",
    className: "CNTT K22B",
    company: "Công ty Công nghệ ABC",
    mentor: "Trần Văn B",
    period: "Đợt 1 - 2026",
    attendance: null,
    attitude: null,
    professionalSkills: null,
    workResult: null,
    finalScore: null,
    classification: "",
    status: "Chưa đánh giá",
    comment: ""
  },
  {
    id: 4,
    studentCode: "DTC245200004",
    name: "Phạm Thị D",
    className: "CNTT K22B",
    company: "Công ty Giải pháp DEF",
    mentor: "Lê Văn H",
    period: "Đợt 2 - 2026",
    attendance: 8,
    attitude: 8,
    professionalSkills: 7.5,
    workResult: 8,
    finalScore: 7.9,
    classification: "Khá",
    status: "Đã đánh giá",
    comment: "Hoàn thành nhiệm vụ, cần cải thiện kỹ năng chuyên môn."
  },
  {
    id: 5,
    studentCode: "DTC245200005",
    name: "Hoàng Văn E",
    className: "CNTT K22C",
    company: "Công ty Phần mềm XYZ",
    mentor: "Nguyễn Văn C",
    period: "Đợt 2 - 2026",
    attendance: null,
    attitude: null,
    professionalSkills: null,
    workResult: null,
    finalScore: null,
    classification: "",
    status: "Chưa đánh giá",
    comment: ""
  }
];

let filteredEvaluations = [...evaluations];

function getElement(id) {
  return document.getElementById(id);
}

function formatValue(value) {
  return value === null || value === undefined || value === ""
    ? "Chưa cập nhật"
    : String(value);
}

// Thống kê theo toàn bộ dữ liệu
function updateStatistics() {
  const total = evaluations.length;

  const evaluated = evaluations.filter(
    item => item.status === "Đã đánh giá"
  );

  const pending = total - evaluated.length;

  const scores = evaluated
    .map(item => item.finalScore)
    .filter(score => typeof score === "number" && Number.isFinite(score));

  const average = scores.length
    ? scores.reduce((sum, score) => sum + score, 0) / scores.length
    : null;

  getElement("totalInterns").textContent = total;
  getElement("evaluatedCount").textContent = evaluated.length;
  getElement("pendingCount").textContent = pending;
  getElement("averageScore").textContent =
    average === null ? "—" : average.toFixed(2);
}

// Tạo các lựa chọn lọc từ dữ liệu thực tế
function populateFilters() {
  const periods = [...new Set(evaluations.map(item => item.period))];
  const classifications = [
    ...new Set(evaluations.map(item => item.classification).filter(Boolean))
  ];

  const periodFilter = getElement("periodFilter");
  const classificationFilter = getElement("classificationFilter");

  periods.forEach(period => {
    const option = document.createElement("option");
    option.value = period;
    option.textContent = period;
    periodFilter.appendChild(option);
  });

  classifications.forEach(classification => {
    const option = document.createElement("option");
    option.value = classification;
    option.textContent = classification;
    classificationFilter.appendChild(option);
  });
}

// Tạo ô trong bảng, tránh chèn HTML từ dữ liệu
function createCell(row, value) {
  const cell = document.createElement("td");
  cell.textContent = value;
  row.appendChild(cell);
  return cell;
}

// Hiển thị danh sách đánh giá
function renderTable() {
  const tableBody = getElement("evaluationTableBody");
  tableBody.replaceChildren();

  filteredEvaluations.forEach(item => {
    const row = document.createElement("tr");

    createCell(row, item.studentCode);
    createCell(row, item.name);
    createCell(row, item.company);
    createCell(row, item.period);
    createCell(row, item.finalScore ?? "—");
    createCell(row, item.classification || "—");

    const statusCell = document.createElement("td");
    const status = document.createElement("span");

    status.className = item.status === "Đã đánh giá"
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
    row.appendChild(actionCell);

    tableBody.appendChild(row);
  });

  getElement("emptyMessage").hidden = filteredEvaluations.length > 0;

  getElement("resultCount").textContent =
    `Hiển thị ${filteredEvaluations.length}/${evaluations.length} thực tập sinh`;
}

// Tìm kiếm và lọc danh sách
function filterEvaluations() {
  const keyword = getElement("searchInput").value
    .trim()
    .toLocaleLowerCase("vi");

  const period = getElement("periodFilter").value;
  const classification = getElement("classificationFilter").value;
  const status = getElement("statusFilter").value;

  filteredEvaluations = evaluations.filter(item => {
    const matchesSearch =
      item.name.toLocaleLowerCase("vi").includes(keyword) ||
      item.studentCode.toLocaleLowerCase("vi").includes(keyword);

    const matchesPeriod = !period || item.period === period;

    const matchesClassification =
      !classification || item.classification === classification;

    const matchesStatus = !status || item.status === status;

    return (
      matchesSearch &&
      matchesPeriod &&
      matchesClassification &&
      matchesStatus
    );
  });

  // Đóng chi tiết cũ khi thay đổi bộ lọc
  getElement("detailSection").hidden = true;

  renderTable();
}

// Tạo ô hiển thị thông tin chi tiết
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

// Xem chi tiết đánh giá của một Intern
function showDetail(id) {
  const intern = evaluations.find(item => item.id === id);

  if (!intern) return;

  renderInformation("internInfo", [
    ["Họ và tên", intern.name],
    ["Mã sinh viên", intern.studentCode],
    ["Lớp", intern.className],
    ["Đơn vị thực tập", intern.company],
    ["Người hướng dẫn", intern.mentor],
    ["Đợt thực tập", intern.period]
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
    intern.comment || "Chưa có nhận xét.";

  const detailSection = getElement("detailSection");
  detailSection.hidden = false;

  detailSection.scrollIntoView({
    behavior: "smooth",
    block: "start"
  });
}

// Chuyển dữ liệu thành nội dung CSV
function csvCell(value) {
  const text = value === null || value === undefined
    ? ""
    : String(value);

  // Ngăn công thức CSV bị thực thi khi mở bằng Excel
  const safeText = /^[\s]*[=+\-@]/.test(text)
    ? "'" + text
    : text;

  return `"${safeText.replace(/"/g, '""')}"`;
}

// Xuất danh sách hiện đang được lọc
function exportCSV() {
  if (filteredEvaluations.length === 0) {
    alert("Không có dữ liệu để xuất báo cáo.");
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

  // BOM giúp Excel nhận diện tiếng Việt UTF-8
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

// Khởi tạo trang
document.addEventListener("DOMContentLoaded", () => {
  updateStatistics();
  populateFilters();
  renderTable();

  getElement("searchInput").addEventListener("input", filterEvaluations);

  getElement("periodFilter").addEventListener("change", filterEvaluations);

  getElement("classificationFilter").addEventListener(
    "change",
    filterEvaluations
  );

  getElement("statusFilter").addEventListener("change", filterEvaluations);

  getElement("exportBtn").addEventListener("click", exportCSV);

  getElement("closeDetailBtn").addEventListener("click", () => {
    getElement("detailSection").hidden = true;
  });

  getElement("printBtn").addEventListener("click", () => {
    window.print();
  });
});
