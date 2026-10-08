const menu = document.getElementById("roleMenu");
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
    renderMenu(session.permissions.filter(permission => permission !== "APPROVE_DOCUMENTS" || user.role === "HR"));
  } catch (error) {
    document.getElementById("dashboardMessage").textContent = error.message;
    document.getElementById("dashboardMessage").hidden = false;
    Session.redirectIfExpired(error);
  }
}

document.getElementById("logoutBtn").addEventListener("click", Session.logout);

initializeDashboard();
