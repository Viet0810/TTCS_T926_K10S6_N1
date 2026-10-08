const menu = document.getElementById("roleMenu");
const attendanceCard = document.getElementById("attendanceCard");
const attendanceStatus = document.getElementById("attendanceStatus");
const attendanceCheckIn = document.getElementById("attendanceCheckIn");
const attendanceCheckOut = document.getElementById("attendanceCheckOut");
const attendanceMessage = document.getElementById("attendanceMessage");
const checkInBtn = document.getElementById("checkInBtn");
const checkOutBtn = document.getElementById("checkOutBtn");
let todayAttendance = null;

const featureLabels = {
  MANAGE_USERS: { label: "Quản lý tài khoản", href: "user-manage.html" },
  CREATE_USER: { label: "Tạo tài khoản thành viên" },
  DELETE_USER: { label: "Xóa tài khoản thành viên" },
  VIEW_INTERNS: {
    label: "Xem danh sách thực tập sinh",
    href: "intern-manage.html",
  },
  MANAGE_INTERNS: {
    label: "Tạo hồ sơ thực tập sinh",
    href: "intern-manage.html",
  },
  SEARCH_INTERNS: {
    label: "Tìm kiếm & Lọc thực tập sinh",
    href: "hr-search-filter.html",
  },
  EDIT_INTERNS: {
    label: "Chỉnh sửa hồ sơ thực tập sinh",
    href: "intern-manage.html",
  },
  VIEW_PROFILE: { label: "Hồ sơ cá nhân", href: "profile.html" },
  UPLOAD_DOCUMENTS: { label: "Nộp CV và đơn xin thực tập", href: "intern-upload-cv.html" },
  VIEW_DOCUMENTS: { label: "Hồ sơ và tài liệu", href: "intern-upload-cv.html" },
  APPROVE_DOCUMENTS: {
    label: "Xem và duyệt tài liệu", href: "document-reviews.html",
  },
  MANAGE_PERMISSIONS: { label: "Ma trận phân quyền", href: "permissions.html" },
};

function addMenuItem(label, href) {
  const item = href
    ? document.createElement("a")
    : document.createElement("span");
  item.textContent = label;
  if (href) item.href = href;
  else item.className = "role-menu-unavailable";
  menu.appendChild(item);
}

function formatAttendanceTime(value) {
  if (!value) return "—";
  return new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).format(new Date(value));
}

function renderAttendance(record) {
  todayAttendance = record;
  attendanceStatus.textContent = !record
    ? "Chưa check-in"
    : record.checkOutAt
      ? "Đã hoàn tất"
      : "Đang làm việc";
  attendanceCheckIn.textContent = formatAttendanceTime(record?.checkInAt);
  attendanceCheckOut.textContent = formatAttendanceTime(record?.checkOutAt);
  checkInBtn.disabled = Boolean(record);
  checkOutBtn.disabled = !record || Boolean(record.checkOutAt);
}

function showAttendanceMessage(message) {
  attendanceMessage.textContent = message;
  attendanceMessage.hidden = !message;
}

async function loadTodayAttendance() {
  checkInBtn.disabled = true;
  checkOutBtn.disabled = true;
  attendanceStatus.textContent = "Đang tải…";

  try {
    renderAttendance(await API.getMyTodayAttendance());
  } catch (error) {
    if (Session.redirectIfExpired(error)) return;
    attendanceStatus.textContent = "Không tải được dữ liệu";
    showAttendanceMessage(error.message);
  }
}

async function submitAttendance(action) {
  checkInBtn.disabled = true;
  checkOutBtn.disabled = true;
  showAttendanceMessage("");

  try {
    const record = action === "check-in"
      ? await API.checkInToday()
      : await API.checkOutToday();
    renderAttendance(record);
    showAttendanceMessage(action === "check-in"
      ? "Check-in thành công."
      : "Check-out thành công.");
  } catch (error) {
    if (Session.redirectIfExpired(error)) return;
    renderAttendance(todayAttendance);
    showAttendanceMessage(error.status === 409
      ? action === "check-in"
        ? "Bạn đã check-in hôm nay."
        : "Chưa check-in hôm nay hoặc bạn đã check-out."
      : error.message);
  }
}

function renderMenu(permissions) {
  const menuPermissions = permissions.filter(
    (permission) =>
      !["CREATE_USER", "DELETE_USER"].includes(permission),
  );
  const items = [
    { label: "Bảng điều khiển", href: "dashboard.html" },
    ...menuPermissions
      .map((permission) => featureLabels[permission])
      .filter(Boolean),
  ];

  menu.replaceChildren();
  const linkedPages = new Set();
  items.forEach((item) => {
    if (item.href && linkedPages.has(item.href)) return;
    if (item.href) linkedPages.add(item.href);
    addMenuItem(item.label, item.href);
  });
}

async function initializeDashboard() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const session = await API.getCurrentUser();
    const user = session.user;
    document.getElementById("roleBadge").textContent = user.role;
    document.getElementById("welcomeTitle").textContent =
      `Xin chào, ${user.fullName}`;
    renderMenu(session.permissions);
    if (user.role === "INTERN") {
      attendanceCard.hidden = false;
      await loadTodayAttendance();
    }
  } catch (error) {
    document.getElementById("dashboardMessage").textContent = error.message;
    document.getElementById("dashboardMessage").hidden = false;
    Session.redirectIfExpired(error);
  }
}

document.getElementById("logoutBtn").addEventListener("click", Session.logout);
checkInBtn.addEventListener("click", () => submitAttendance("check-in"));
checkOutBtn.addEventListener("click", () => submitAttendance("check-out"));

initializeDashboard();
