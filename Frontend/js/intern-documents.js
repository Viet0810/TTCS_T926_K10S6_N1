document.getElementById("logoutBtn").addEventListener("click", Session.logout);
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
function renderProfileCreateFields(email) {
  const container = document.getElementById("profileCreateFields");
  container.replaceChildren();
  for (const field of InternProfile.fields) {
    const group = document.createElement("div");
    group.className = `form-group${["address", "organizationAddress", "internshipTopic", "notes"].includes(field.key) ? " full-width" : ""}`;
    const label = document.createElement("label");
    label.textContent = `${field.label}${field.required ? " *" : ""}`;
    let input;
    if (field.type === "textarea") {
      input = document.createElement("textarea"); input.rows = 3;
    } else if (field.type === "select") {
      input = document.createElement("select");
      const empty = document.createElement("option"); empty.value = ""; empty.textContent = "Chọn trạng thái"; input.append(empty);
      for (const status of InternProfile.statuses) { const option = document.createElement("option"); option.value = option.textContent = status; input.append(option); }
    } else {
      input = document.createElement("input"); input.type = field.type;
    }
    input.id = `create-${field.key}`; input.name = field.key;
    input.required = Boolean(field.required);
    if (field.max) input.maxLength = field.max;
    if (field.key === "email") { input.value = email; input.readOnly = true; }
    if (field.key === "phone" || field.key === "mentorPhone") input.pattern = "[0-9]{10,11}";
    const error = document.createElement("small"); error.className = "error-message";
    group.append(label, input, error); container.append(group);
  }
}

document.getElementById("profileCreateForm").addEventListener("submit", async (event) => {
  event.preventDefault();
  const form = event.currentTarget;
  if (!form.reportValidity()) return;
  const button = document.getElementById("createProfileBtn"), message = document.getElementById("profileCreateMessage");
  button.disabled = true; button.textContent = "Đang lưu hồ sơ…"; message.textContent = "";
  const values = Object.fromEntries(new FormData(form));
  const profile = Object.fromEntries(Object.entries(values).map(([key, value]) => [key, value.trim() || null]));
  try {
    const saved = await API.createMyInternProfile(profile);
    document.getElementById("profileCreateArea").hidden = true;
    document.getElementById("internName").textContent = saved.fullName;
    document.getElementById("internEmail").textContent = saved.email;
    InternProfile.details(document.getElementById("internDetails"), saved);
    try { await refreshDocuments(); }
    catch (error) {
      document.getElementById("uploadMessage").textContent = `Đã tạo hồ sơ, nhưng chưa tải được tài liệu: ${error.message}`;
      document.getElementById("retryProfileBtn").hidden = false;
      return;
    }
    ready = true;
    document.getElementById("uploadArea").hidden = false;
    document.getElementById("uploadMessage").textContent = "Đã tạo hồ sơ. Bạn có thể tải CV để HR tiếp nhận.";
  } catch (error) { message.textContent = error.message; }
  finally { button.disabled = false; button.textContent = "Lưu hồ sơ và tiếp tục"; }
});

async function refreshDocuments() {
  documents = await API.getMyDocuments();
  for (const kind of documentKinds) {
    const item = documents.find((entry) => entry.kind === kind);
    document.getElementById(`${kind}Info`).textContent = item
      ? `${item.fileName} · ${sizeText(item.size)} · ${new Date(item.uploadedAt + (/[Z+]/.test(item.uploadedAt) ? "" : "Z")).toLocaleString("vi-VN")}`
      : "Chưa nộp tài liệu";
    if (item) document.getElementById(`${kind}Info`).textContent += ` · ${{pending:"Chờ duyệt",approved:"Đã duyệt",rejected:"Cần bổ sung"}[item.status] || "Chờ duyệt"}${item.comment ? ` · ${item.comment}` : ""}`;
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
    document.getElementById(`${kind}Selected`).textContent = file ? `${file.name} · ${sizeText(file.size)}` : "";
    message.textContent = file ? documentValidation(file) : "";
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
  let session;
  try {
    session = await API.getCurrentUser();
    if (!Session.hasPermission(session, "UPLOAD_DOCUMENTS")) {
      document.getElementById("internName").textContent = "Tài liệu thực tập";
      status.textContent = "Chức năng nộp tài liệu dành cho tài khoản thực tập sinh."; return;
    }
    const profile = await API.getMyInternProfile();
    document.getElementById("profileCreateArea").hidden = true;
    document.getElementById("profileCreateMessage").textContent = "";
    document.getElementById("internName").textContent = profile.fullName;
    document.getElementById("internEmail").textContent = profile.email;
    InternProfile.details(document.getElementById("internDetails"), profile);
    await refreshDocuments(); ready = true;
    document.getElementById("uploadArea").hidden = false; status.textContent = "";
  } catch (error) {
    if (error.status === 404) {
      renderProfileCreateFields(session.user.email);
      document.getElementById("profileCreateArea").hidden = false;
      document.getElementById("uploadArea").hidden = true;
      document.getElementById("internName").textContent = "Chưa tạo hồ sơ";
      document.getElementById("internEmail").textContent = session.user.email;
      status.textContent = "Tạo hồ sơ thực tập trước khi tải CV lên.";
    } else {
      document.getElementById("internName").textContent = "Hồ sơ thực tập"; status.textContent = error.message;
      retry.hidden = false;
    }
  }
  finally { retry.disabled = false; }
}
document.getElementById("retryProfileBtn").addEventListener("click", loadProfile);
loadProfile();
