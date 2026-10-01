const reviewStatuses = { pending: "Chờ duyệt", approved: "Đã duyệt", rejected: "Từ chối · Cần bổ sung" };
let reviewDocuments = [];
let selectedDocument = null;
let reviewing = false;
let allowed = false;
const reviewDialog = document.getElementById("reviewDialog");
const reviewMessage = document.getElementById("reviewMessage");
const utcDisplay = (value) => value ? new Date(value + (/[Z+]/.test(value) ? "" : "Z")).toLocaleString("vi-VN") : "";
function reviewErrorMessage(error) {
  if (error.status === 401) return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
  if (error.status === 403) return "Bạn không có quyền xem hoặc duyệt tài liệu.";
  if (error.status === 404) return "Không tìm thấy tài liệu. Vui lòng tải lại danh sách.";
  return error.message;
}

async function downloadReview(item, button) {
  button.disabled = true;
  try {
    const blob = await API.downloadDocumentReview(item.internId, item.kind);
    saveDownload(blob, item.fileName);
  } catch (error) {
    reviewMessage.textContent = reviewErrorMessage(error);
  } finally { button.disabled = false; }
}

function openReview(item) {
  if (reviewing) return;
  selectedDocument = item;
  document.getElementById("reviewFile").textContent = `${item.fullName} · ${item.fileName}`;
  document.getElementById("reviewComment").value = item.comment || "";
  document.getElementById("dialogMessage").textContent = "";
  setReviewBusy(false);
  reviewDialog.showModal();
}

function setReviewBusy(busy) {
  reviewing = busy;
  for (const element of document.getElementById("reviewForm").elements) {
    element.disabled = busy || (element.type === "submit" && element.value === selectedDocument?.status);
  }
}

function reviewValidation(status, comment) {
  if (status === selectedDocument.status) return "Tài liệu đã có kết quả này. Vui lòng tải lại danh sách.";
  if (!selectedDocument.version) return "Vui lòng tải lại danh sách trước khi duyệt.";
  if (status === "rejected" && !comment) return "Vui lòng nhập lý do yêu cầu bổ sung.";
  return "";
}
function renderReviews() {
  const search = document.getElementById("reviewSearch").value.trim().toLocaleLowerCase("vi"), status = document.getElementById("reviewFilter").value;
  const rows = document.getElementById("reviewRows"); rows.replaceChildren();
  const visible = reviewDocuments.filter((item) => (!status || item.status === status) && [item.fullName, item.studentCode, item.email].some((text) => (text || "").toLocaleLowerCase("vi").includes(search)));
  if (!visible.length) { const cell = rows.insertRow().insertCell(); cell.colSpan = 6; cell.textContent = "Chưa có tài liệu phù hợp."; return; }
  for (const item of visible) {
    const row = rows.insertRow();
    for (const value of [`${item.fullName}\n${item.studentCode || ""}\n${item.email}`, `${item.kind === "cv" ? "CV" : "Đơn xin thực tập"}\n${item.fileName}\n${(item.size / 1048576).toFixed(2)} MB`, utcDisplay(item.uploadedAt), reviewStatuses[item.status], `${item.comment || ""}\n${item.reviewer || ""}\n${utcDisplay(item.reviewedAt)}`]) { const cell = row.insertCell(); cell.textContent = value; cell.style.whiteSpace = "pre-wrap"; }
    const actions = row.insertCell();
    const download = document.createElement("button"); download.type = "button"; download.className = "btn-secondary btn-compact"; download.textContent = "Tải để xem";
    download.onclick = () => downloadReview(item, download);
    const review = document.createElement("button"); review.type = "button"; review.className = "btn-secondary btn-compact"; review.textContent = "Duyệt / Nhận xét";
    review.disabled = !item.version;
    review.onclick = () => openReview(item);
    actions.append(download, review);
  }
}
async function loadReviews() {
  if (!allowed) return;
  reviewDocuments = await API.getDocumentReviews();
  renderReviews();
}
document.getElementById("reviewSearch").addEventListener("input", renderReviews);
document.getElementById("reviewFilter").addEventListener("change", renderReviews);
document.getElementById("refreshBtn").onclick = async (event) => {
  if (!allowed || reviewing) return;
  const button = event.currentTarget;
  button.disabled = true;
  try { await loadReviews(); reviewMessage.textContent = "Đã tải lại danh sách."; }
  catch (error) { reviewMessage.textContent = reviewErrorMessage(error); }
  finally { button.disabled = false; }
};
document.getElementById("closeReview").onclick = () => { if (!reviewing) reviewDialog.close(); };
reviewDialog.addEventListener("cancel", (event) => { if (reviewing) event.preventDefault(); });
document.getElementById("reviewForm").addEventListener("submit", async (event) => {
  event.preventDefault(); if (!allowed || reviewing || !selectedDocument) return;
  const status = event.submitter?.value, comment = document.getElementById("reviewComment").value.trim(), message = document.getElementById("dialogMessage");
  if (!status) return;
  const validation = reviewValidation(status, comment);
  if (validation) { message.textContent = validation; return; }
  setReviewBusy(true);
  try {
    await API.reviewDocument(selectedDocument.internId, selectedDocument.kind, { status, comment, version: selectedDocument.version });
    selectedDocument.status = status; selectedDocument.comment = comment; selectedDocument.version = null;
    renderReviews();
    reviewDialog.close(); reviewMessage.textContent = "Đã lưu kết quả duyệt.";
    try { await loadReviews(); }
    catch (error) { reviewMessage.textContent = `Đã lưu kết quả duyệt, nhưng chưa tải lại được danh sách: ${reviewErrorMessage(error)}`; }
  } catch (error) { message.textContent = reviewErrorMessage(error); reviewMessage.textContent = message.textContent; }
  finally { setReviewBusy(false); }
});
document.getElementById("logoutBtn").onclick = Session.logout;
async function initializeReviews() {
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  try {
    const session = await API.getCurrentUser();
    allowed = Session.hasPermission(session, "APPROVE_DOCUMENTS");
    if (!allowed) { reviewMessage.textContent = "Bạn không có quyền duyệt tài liệu."; return; }
    await loadReviews();
  } catch (error) { reviewMessage.textContent = reviewErrorMessage(error); }
}
initializeReviews();
