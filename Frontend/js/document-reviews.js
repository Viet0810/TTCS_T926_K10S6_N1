const reviewStatuses = { pending: "Chờ duyệt", approved: "Đã duyệt", rejected: "Từ chối" };
let reviewDocuments = [];
let selectedDocument = null;
let reviewing = false;
let allowed = false;
let detailsReady = false;
let detailsRequest = 0;
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

async function openReview(item) {
  if (reviewing || !allowed) return;
  const request = ++detailsRequest;
  detailsReady = false;
  selectedDocument = item;
  document.getElementById("reviewFile").textContent = `${item.fullName} · ${item.fileName} · ${reviewStatuses[item.status]}${item.comment ? ` · ${item.comment}` : ""}`;
  document.getElementById("reviewDetails").textContent = "Đang tải thông tin hồ sơ…";
  document.getElementById("reviewComment").value = item.comment || "";
  document.getElementById("dialogMessage").textContent = "";
  setReviewBusy(false);
  reviewDialog.showModal();
  try {
    const profile = await requestApi(`/interns/${encodeURIComponent(item.internId)}`, { headers: getAuthHeader() });
    if (request !== detailsRequest || !reviewDialog.open) return;
    InternProfile.details(document.getElementById("reviewDetails"), profile);
    detailsReady = true;
    setReviewBusy(false);
  } catch (error) {
    if (request !== detailsRequest || !reviewDialog.open) return;
    document.getElementById("reviewDetails").textContent = reviewErrorMessage(error);
  }
}

function setReviewBusy(busy) {
  reviewing = busy;
  document.getElementById("closeReviewTop").disabled = busy;
  document.getElementById("reviewForm").setAttribute("aria-busy", String(busy));
  document.getElementById("reviewCommentArea").hidden = selectedDocument?.status !== "pending";
  for (const element of document.getElementById("reviewForm").elements) {
    if (element.type === "submit") {
      element.hidden = selectedDocument?.status !== "pending";
      element.textContent = busy ? "Đang lưu…" : element.value === "approved" ? "Duyệt" : "Từ chối";
    }
    element.disabled = busy || (element.type === "submit" && (!detailsReady || !selectedDocument?.version || selectedDocument?.status !== "pending"));
  }
}

function reviewValidation(status, comment) {
  if (selectedDocument.status !== "pending") return "Tài liệu đã được xử lý, không thể xét duyệt lại.";
  if (!detailsReady) return "Vui lòng tải đầy đủ chi tiết hồ sơ trước khi xét duyệt.";
  if (!selectedDocument.version) return "Vui lòng tải lại danh sách trước khi duyệt.";
  if (status === "rejected" && !comment) return "Vui lòng nhập lý do từ chối.";
  if (comment.length > 2000) return "Nhận xét không được vượt quá 2000 ký tự.";
  return "";
}
function renderReviews() {
  const search = document.getElementById("reviewSearch").value.trim().toLocaleLowerCase("vi"), status = document.getElementById("reviewFilter").value;
  const rows = document.getElementById("reviewRows"); rows.replaceChildren();
  const visible = reviewDocuments.filter((item) => (!status || item.status === status) && [item.fullName, item.studentCode, item.email].some((text) => (text || "").toLocaleLowerCase("vi").includes(search)));
  if (!visible.length) { const cell = rows.insertRow().insertCell(); cell.colSpan = 6; cell.textContent = "Chưa có tài liệu phù hợp."; return; }
  for (const item of visible) {
    const row = rows.insertRow();
    const addLine = (cell, text, className) => {
      if (!text) return;
      const line = document.createElement("span"); line.className = className; line.textContent = text; cell.append(line);
    };
    const candidate = row.insertCell();
    addLine(candidate, item.fullName, "review-name");
    addLine(candidate, item.email, "review-contact");
    addLine(candidate, item.phone, "review-contact");
    const education = row.insertCell();
    addLine(education, item.school, "review-primary");
    addLine(education, item.major, "review-meta");
    const documentCell = row.insertCell();
    addLine(documentCell, item.kind === "cv" ? "CV" : "Đơn xin thực tập", "review-primary");
    addLine(documentCell, item.fileName, "review-filename");
    addLine(documentCell, `Đăng ký: ${utcDisplay(item.createdAt)}`, "review-meta");
    addLine(documentCell, `Nộp: ${utcDisplay(item.uploadedAt)}`, "review-meta");
    row.insertCell();
    const result = row.insertCell();
    addLine(result, item.comment, "review-comment");
    addLine(result, item.reviewer, "review-meta");
    addLine(result, utcDisplay(item.reviewedAt), "review-meta");
    const badge = document.createElement("span"); badge.className = `review-status ${item.status}`; badge.textContent = reviewStatuses[item.status]; row.cells[3].replaceChildren(badge);
    const actions = row.insertCell(); actions.className = "review-actions-cell";
    const download = document.createElement("button"); download.type = "button"; download.className = "btn-secondary btn-compact"; download.textContent = "Tải để xem";
    download.onclick = () => downloadReview(item, download);
    const review = document.createElement("button"); review.type = "button"; review.className = "btn-secondary btn-compact review-detail-button"; review.textContent = "Xem chi tiết";
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
function closeReviewDialog() { if (!reviewing) reviewDialog.close(); }
document.getElementById("closeReview").onclick = closeReviewDialog;
document.getElementById("closeReviewTop").onclick = closeReviewDialog;
reviewDialog.addEventListener("cancel", (event) => { if (reviewing) event.preventDefault(); });
document.getElementById("reviewForm").addEventListener("submit", async (event) => {
  event.preventDefault(); if (!allowed || reviewing || !selectedDocument) return;
  const status = event.submitter?.value, comment = document.getElementById("reviewComment").value.trim(), message = document.getElementById("dialogMessage");
  if (!status) return;
  const validation = reviewValidation(status, comment);
  if (validation) { message.textContent = validation; return; }
  setReviewBusy(true);
  try {
    if (!["approved", "rejected"].includes(status)) { message.textContent = "Kết quả duyệt không hợp lệ."; return; }
    const result = await requestApi(`/document-reviews/${encodeURIComponent(selectedDocument.internId)}/${encodeURIComponent(selectedDocument.kind)}/${status === "approved" ? "approve" : "reject"}`, {
      method: "PUT", headers: getAuthHeader(), body: JSON.stringify({ comment, version: selectedDocument.version }),
    });
    selectedDocument.status = status; selectedDocument.comment = comment; selectedDocument.version = null;
    renderReviews();
    reviewDialog.close(); reviewMessage.textContent = result.message || "Đã lưu kết quả duyệt.";
    try { await loadReviews(); }
    catch (error) { reviewMessage.textContent = `Đã lưu kết quả duyệt, nhưng chưa tải lại được danh sách: ${reviewErrorMessage(error)}`; }
  } catch (error) { message.textContent = reviewErrorMessage(error); reviewMessage.textContent = message.textContent; }
  finally { setReviewBusy(false); }
});
async function initializeReviews() {
  reviewMessage.textContent = "Đang tải tài liệu…";
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  try {
    const session = await API.getCurrentUser();
    allowed = session.user?.role === "HR" && Session.hasPermission(session, "APPROVE_DOCUMENTS");
    if (!allowed) { reviewMessage.textContent = "Bạn không có quyền duyệt tài liệu."; return; }
    await loadReviews();
    reviewMessage.textContent = "";
  } catch (error) { reviewMessage.textContent = reviewErrorMessage(error); }
}
initializeReviews();
