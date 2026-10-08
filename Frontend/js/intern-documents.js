const documentKinds = ["cv", "application"];
let documents = [];
let ready = false;
const sizeText = (size) => `${(size / 1024 / 1024).toFixed(2)} MB`;
function documentValidation(file) {
  if (!file) return "Vui lòng chọn tài liệu trước khi tải lên.";
  if (!/\.pdf$/i.test(file.name)) return "Định dạng không được hỗ trợ. Vui lòng chọn tệp PDF.";
  if (file.name.length > 255 || /[\x00-\x1f\x7f]/.test(file.name)) return "Tên tệp không hợp lệ hoặc dài quá 255 ký tự.";
  if (file.type && !["application/pdf", "application/octet-stream"].includes(file.type.toLowerCase())) return "Định dạng không được hỗ trợ. Vui lòng chọn tệp PDF.";
  if (!file.size) return "Tệp đã chọn rỗng. Vui lòng chọn tài liệu có nội dung.";
  if (file.size > 5 * 1024 * 1024) return "Tệp vượt quá dung lượng cho phép. Vui lòng chọn PDF tối đa 5 MB.";
  return "";
}
async function refreshDocuments() {
  documents = await API.getMyDocuments();
  for (const kind of documentKinds) {
    const item = documents.find((entry) => entry.kind === kind);
    const info = document.getElementById(`${kind}Info`); info.replaceChildren();
    if (!item) info.textContent = "Chưa nộp tài liệu";
    else {
      const name = document.createElement("strong"); name.className = "document-filename";
      name.textContent = name.title = item.fileName; info.append(name);
      for (const text of [`Dung lượng: ${sizeText(item.size)}`, `Ngày nộp: ${new Date(item.uploadedAt + (/[Z+]/.test(item.uploadedAt) ? "" : "Z")).toLocaleString("vi-VN")}`]) {
        const line = document.createElement("span"); line.className = "document-meta"; line.textContent = text; info.append(line);
      }
      const line = document.createElement("span"); line.className = "document-meta"; line.append("Trạng thái: ");
      const badge = document.createElement("span");
      const statuses = {pending:"Chờ duyệt",approved:"Đã duyệt",rejected:"Cần bổ sung"};
      badge.className = `status-badge ${statuses[item.status] ? item.status : ""}`;
      badge.textContent = statuses[item.status] || "Chưa xác định"; line.append(badge); info.append(line);
      if (item.comment) { const comment = document.createElement("span"); comment.className = "document-meta"; comment.textContent = `Nhận xét: ${item.comment}`; info.append(comment); }
    }
    document.getElementById(`${kind}Download`).disabled = !item;
  }
}
function bindDocumentForm(kind) {
  const form = document.getElementById(`${kind}Form`), input = document.getElementById(`${kind}File`), message = document.getElementById(`${kind}Message`);
  let busy = false;
  // Show the same clear Vietnamese feedback for missing files as for other validation errors.
  form.noValidate = true;
  input.addEventListener("change", () => {
    const file = input.files[0];
    const selected = document.getElementById(`${kind}Selected`);
    selected.textContent = file ? `${file.name} · ${sizeText(file.size)}` : "";
    selected.title = file?.name || "";
    message.textContent = file ? documentValidation(file) : "";
  });
  const picker = input.closest(".file-picker");
  picker.addEventListener("dragover", event => { event.preventDefault(); if (!input.disabled) picker.classList.add("is-dragging"); });
  picker.addEventListener("dragleave", () => picker.classList.remove("is-dragging"));
  picker.addEventListener("drop", event => {
    event.preventDefault(); picker.classList.remove("is-dragging");
    if (input.disabled || !event.dataTransfer?.files.length) return;
    if (event.dataTransfer.files.length !== 1) { message.textContent = "Vui lòng chọn một tệp PDF cho mỗi tài liệu."; return; }
    input.files = event.dataTransfer.files; input.dispatchEvent(new Event("change"));
  });
  form.addEventListener("submit", async (event) => {
    event.preventDefault(); if (!ready || busy) return;
    const file = input.files[0];
    const validation = documentValidation(file);
    if (validation) { message.textContent = validation; input.focus(); return; }
    busy = true;
    const button = form.querySelector('[type="submit"]'), original = button.textContent;
    button.disabled = input.disabled = true; button.textContent = "Đang tải lên…"; message.textContent = "Đang gửi tài liệu…";
    try {
      await API.uploadMyDocument(kind, file);
      input.value = ""; document.getElementById(`${kind}Selected`).textContent = "";
      message.textContent = "Đã lưu tài liệu thành công. Tài liệu đang chờ HR xem và duyệt.";
      try { await refreshDocuments(); }
      catch (error) { message.textContent = `Đã lưu, nhưng chưa tải lại được danh sách: ${error.message}`; }
    } catch (error) { message.textContent = error.message; }
    finally { busy = false; button.disabled = input.disabled = false; button.textContent = original; }
  });
  document.getElementById(`${kind}Download`).addEventListener("click", async () => {
    const button = document.getElementById(`${kind}Download`); button.disabled = true;
    try {
      const blob = await API.downloadMyDocument(kind);
      saveDownload(blob, documents.find((entry) => entry.kind === kind)?.fileName || `${kind}.pdf`);
    } catch (error) { message.textContent = error.message; }
    finally { button.disabled = !documents.some((entry) => entry.kind === kind); }
  });
}
documentKinds.forEach(bindDocumentForm);
async function loadProfile() {
  if (!localStorage.getItem("token")) { location.href = "../index.html"; return; }
  const status = document.getElementById("uploadMessage");
  const retry = document.getElementById("retryProfileBtn");
  if (retry.disabled) return;
  retry.disabled = true;
  retry.hidden = true;
  status.textContent = "Đang kiểm tra hồ sơ và tải tài liệu…";
  try {
    const session = await API.getCurrentUser();
    if (!Session.hasPermission(session, "UPLOAD_DOCUMENTS")) {
      status.textContent = "Chức năng nộp tài liệu dành cho tài khoản thực tập sinh."; return;
    }
    // Preserve the existing linked-profile check before enabling upload.
    const profile = await API.getMyInternProfile();
    if (!profile) { status.textContent = "Chưa có hồ sơ thực tập để nộp tài liệu."; return; }
    await refreshDocuments(); ready = true;
    document.getElementById("uploadArea").hidden = false; status.textContent = "";
  } catch (error) {
    status.textContent = error.message;
    retry.hidden = false;
  }
  finally { retry.disabled = false; }
}
document.getElementById("retryProfileBtn").addEventListener("click", loadProfile);
loadProfile();
