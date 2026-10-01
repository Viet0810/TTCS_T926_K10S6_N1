const menu = document.getElementById("roleMenu");
const permissionList = document.getElementById("permissionList");
const featureLabels = {
  MANAGE_USERS: { label: "Quản lý tài khoản", href: "user-manage.html" },
  CREATE_USER: { label: "Tạo tài khoản thành viên" },
  DELETE_USER: { label: "Xóa tài khoản thành viên" },
  VIEW_INTERNS: {
    label: "Xem danh sách thực tập sinh",
    href: "intern-manage.html",
  },
  MANAGE_INTERNS: { label: "Quản lý hồ sơ thực tập sinh" },
  VIEW_PROFILE: { label: "Hồ sơ cá nhân", href: "profile.html" },
  VIEW_DOCUMENTS: { label: "Tài liệu (module giao diện chưa triển khai)" },
  APPROVE_DOCUMENTS: {
    label: "Duyệt tài liệu (module giao diện chưa triển khai)",
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

function renderPermissions(permissions) {
  const menuPermissions = permissions.filter(
    (permission) =>
      !["CREATE_USER", "DELETE_USER", "MANAGE_INTERNS"].includes(permission),
  );
  const items = [
    { label: "Bảng điều khiển", href: "dashboard.html" },
    ...menuPermissions
      .map((permission) => featureLabels[permission])
      .filter(Boolean),
  ];

  items.forEach((item) => addMenuItem(item.label, item.href));
  permissionList.replaceChildren();

  permissions.forEach((permission) => {
    const item = featureLabels[permission];
    if (!item) return;
    const row = document.createElement("li");
    row.textContent = item.label;
    permissionList.appendChild(row);
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
    document.getElementById("welcomeDescription").textContent =
      `Đăng nhập với vai trò ${user.role}.`;
    renderPermissions(session.permissions);
  } catch (error) {
    document.getElementById("dashboardMessage").textContent = error.message;
    if (
      error.message.includes("đăng nhập") ||
      error.message.includes("phiên")
    ) {
      localStorage.clear();
      window.location.href = "../index.html";
    }
  }
}

document.getElementById("logoutBtn").addEventListener("click", () => {
  localStorage.clear();
  window.location.href = "../index.html";
});

initializeDashboard();
