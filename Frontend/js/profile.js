(async function loadProfile() {
  const form = document.getElementById("ownProfileForm"), edit = document.getElementById("editProfileBtn"), message = document.getElementById("profileMessage");
  form.className = "own-profile-form";
  const save = document.getElementById("saveProfileBtn"), cancel = document.getElementById("cancelProfileBtn");
  const fields = { fullName: ["ownFullName", "Họ và tên"], phone: ["ownPhone", "Số điện thoại"], school: ["ownSchool", "Trường"], major: ["ownMajor", "Chuyên ngành"], address: ["ownAddress", "Địa chỉ"] };
  let profile, busy = false;
  function render() {
    const details = document.getElementById("ownProfileDetails"); details.replaceChildren();
    for (const [key, [, title]] of Object.entries(fields)) {
      const row = document.createElement("div"), label = document.createElement("dt"), value = document.createElement("dd");
      label.textContent = title; value.textContent = profile[key] || "Chưa cập nhật"; row.append(label, value); details.append(row);
    }
    document.getElementById("profileName").textContent = profile.fullName;
  }
  function reset() {
    for (const [key, [id]] of Object.entries(fields)) { const input = document.getElementById(id); input.value = profile[key] || ""; Validation.clearFieldError(input); }
  }
  function validate(key) {
    const input = document.getElementById(fields[key][0]), value = input.value.trim();
    const error = key === "phone" ? Validation.validatePhone(value) : !value && key !== "address" ? "Vui lòng nhập thông tin này." : value.length > (key === "address" ? 500 : 200) ? "Thông tin vượt quá độ dài cho phép." : "";
    Validation.showFieldError(input, error); return !error;
  }
  for (const [key, [id]] of Object.entries(fields)) {
    const input = document.getElementById(id); input.addEventListener("input", () => Validation.clearFieldError(input)); input.addEventListener("blur", () => validate(key));
  }
  edit.addEventListener("click", () => { reset(); form.hidden = false; edit.hidden = true; message.textContent = ""; document.getElementById("ownFullName").focus(); });
  cancel.addEventListener("click", () => { if (busy) return; reset(); form.hidden = true; edit.hidden = false; message.textContent = ""; });
  form.addEventListener("submit", async event => {
    event.preventDefault(); if (busy) return;
    if (Object.keys(fields).map(validate).includes(false)) { form.querySelector('[aria-invalid="true"]').focus(); return; }
    const payload = Object.fromEntries(Object.entries(fields).map(([key, [id]]) => [key, document.getElementById(id).value.trim()]));
    busy = true; save.disabled = cancel.disabled = true; save.textContent = "Đang lưu..."; form.setAttribute("aria-busy", "true"); message.textContent = "";
    Object.values(fields).forEach(([id]) => document.getElementById(id).disabled = true);
    try { profile = await API.updateMyInternProfile(payload); render(); form.hidden = true; edit.hidden = false; message.textContent = "Cập nhật thông tin cá nhân thành công."; }
    catch (error) { Session.redirectIfExpired(error); message.textContent = Validation.requestMessage(error, "Không thể cập nhật thông tin. Vui lòng kiểm tra và thử lại."); }
    finally { busy = false; save.disabled = cancel.disabled = false; save.textContent = "Lưu thay đổi"; form.removeAttribute("aria-busy"); Object.values(fields).forEach(([id]) => document.getElementById(id).disabled = false); }
  });
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const session = await API.getCurrentUser();
    const user = session.user;
    document.getElementById("profileName").textContent = user.fullName;
    document.getElementById("profileEmail").textContent = user.email;
    document.getElementById("profileUsername").textContent = user.username;
    document.getElementById("profileRole").textContent = user.role;
    document.getElementById("profileRoleName").textContent = user.role;
    if (user.role === "INTERN" && Session.hasPermission(session, "VIEW_PROFILE")) {
      profile = await API.getMyInternProfile(); render(); document.getElementById("ownProfile").hidden = false;
    }
  } catch (error) {
    message.textContent = error.status === 404 ? "Chưa có hồ sơ thực tập sinh gắn với tài khoản này." : Validation.requestMessage(error, "Không thể tải hồ sơ. Vui lòng tải lại trang.");
    Session.redirectIfExpired(error);
  }
})();
