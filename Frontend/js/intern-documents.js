const documentKinds = ["cv", "application"];
let documents = [];
let ready = false;
let canUpload = false;
document.addEventListener('session-permissions-updated',event=>{
  canUpload=Session.hasPermission(event.detail,'UPLOAD_DOCUMENTS');
  for(const kind of documentKinds){const input=document.getElementById(kind+'File');input.disabled=!ready||!canUpload;input.closest('.file-picker').hidden=!canUpload;document.getElementById(kind+'Form').querySelector('[type=submit]').hidden=!canUpload;}
  if(!Session.hasPermission(event.detail,'VIEW_DOCUMENTS')){ready=false;document.getElementById('uploadArea').hidden=true;}
});
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
  document.getElementById("uploadArea").setAttribute("aria-busy", "true");
  try { documents = await API.getMyDocuments(); }
  finally { document.getElementById("uploadArea").setAttribute("aria-busy", "false"); }
  for (const kind of documentKinds) {
    const item = documents.find((entry) => entry.kind === kind);
    const info = document.getElementById(`${kind}Info`); info.replaceChildren();
    if (!item) { if (window.AppVisual) info.append(AppVisual.illustration('file')); info.append("Chưa nộp tài liệu"); }
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
    message.hidden = false; message.textContent = file ? documentValidation(file) : "";
  });
  const picker = input.closest(".file-picker");
  picker.addEventListener("dragover", event => { event.preventDefault(); if (!input.disabled) picker.classList.add("is-dragging"); });
  picker.addEventListener("dragleave", () => picker.classList.remove("is-dragging"));
  picker.addEventListener("drop", event => {
    event.preventDefault(); picker.classList.remove("is-dragging");
    if (input.disabled || !event.dataTransfer?.files.length) return;
    if (event.dataTransfer.files.length !== 1) { message.hidden = false; message.textContent = "Vui lòng chọn một tệp PDF cho mỗi tài liệu."; return; }
    input.files = event.dataTransfer.files; input.dispatchEvent(new Event("change"));
  });
  form.addEventListener("submit", async (event) => {
    event.preventDefault(); if (!ready || !canUpload || busy) return;
    const file = input.files[0];
    const validation = documentValidation(file);
    if (validation) { message.hidden = false; message.textContent = validation; input.focus(); return; }
    busy = true;
    const button = form.querySelector('[type="submit"]'), original = button.textContent;
    button.disabled = input.disabled = true; button.textContent = "Đang tải lên…"; message.textContent = "Đang gửi tài liệu…";
    try {
      await API.uploadMyDocument(kind, file);
      input.value = ""; document.getElementById(`${kind}Selected`).textContent = "";
      AppUI.feedback(message, "Đã lưu tài liệu thành công. Tài liệu đang chờ HR xem và duyệt.");
      try { await refreshDocuments(); }
      catch (error) { AppUI.feedback(message, `Đã lưu, nhưng chưa tải lại được danh sách: ${error.message}`, "warning"); }
    } catch (error) { AppUI.feedback(message, error.message, "error"); }
    finally { busy = false; button.disabled = input.disabled = false; button.textContent = original; }
  });
  document.getElementById(`${kind}Download`).addEventListener("click", async () => {
    const button = document.getElementById(`${kind}Download`); button.disabled = true;
    try {
      const blob = await API.downloadMyDocument(kind);
      saveDownload(blob, documents.find((entry) => entry.kind === kind)?.fileName || `${kind}.pdf`);
    } catch (error) { AppUI.feedback(message, error.message, "error"); }
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
  const stopLoading = window.AppUI?.loading(status);
  try {
    const session = await API.getCurrentUser();
    canUpload = Session.hasPermission(session, "UPLOAD_DOCUMENTS");
    if (!Session.hasPermission(session, "VIEW_DOCUMENTS")) {
      status.textContent = "Bạn không có quyền xem tài liệu cá nhân."; return;
    }
    // The document API checks the signed-in owner's linked profile itself.
    await refreshDocuments(); ready = true;
    for (const kind of documentKinds) {
      document.getElementById(`${kind}File`).disabled = !canUpload;
      document.getElementById(`${kind}Form`).querySelector('[type="submit"]').hidden = !canUpload;
      document.getElementById(`${kind}File`).closest(".file-picker").hidden = !canUpload;
    }
    document.getElementById("uploadArea").hidden = false; status.textContent = "";
  } catch (error) {
    status.textContent = error.message;
    retry.hidden = false;
  }
  finally { retry.disabled = false; stopLoading?.(); }
}
document.getElementById("retryProfileBtn").addEventListener("click", loadProfile);
loadProfile();
