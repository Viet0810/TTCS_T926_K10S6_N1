(() => {
  const user = JSON.parse(localStorage.getItem("user") || "null");
  const role = String(localStorage.getItem("role") || user?.role || "").toUpperCase();
  const token = localStorage.getItem("token");

  const accessRules = {
    ADMIN: { label: "Quản trị viên", canManageUsers: true, canManageInterns: true },
    HR: { label: "HR", canManageUsers: false, canManageInterns: true },
    MENTOR: { label: "Mentor", canManageUsers: false, canManageInterns: false },
    INTERN: { label: "Thực tập sinh", canManageUsers: false, canManageInterns: false },
  };

  const access = accessRules[role];
  if (!token || !access) {
    localStorage.clear();
    window.location.replace("../index.html");
    return;
  }

  window.CurrentAccess = { user, role, ...access };
  const requiredRoles = document.body.dataset.requiredRoles;
  if (requiredRoles && !requiredRoles.split(",").map((value) => value.trim()).includes(role)) {
    window.location.replace(document.body.dataset.deniedRedirect || "role-dashboard.html");
    return;
  }
  document.querySelectorAll("[data-allow]").forEach((element) => {
    const allowedRoles = element.dataset.allow.split(",").map((value) => value.trim());
    if (!allowedRoles.includes(role)) element.remove();
  });
  document.querySelectorAll("[data-role-label]").forEach((element) => {
    element.textContent = access.label;
  });
  document.querySelectorAll("[data-user-name]").forEach((element) => {
    element.textContent = user?.fullName || user?.username || access.label;
  });

  const apiBaseUrl = window.APP_CONFIG?.apiBaseUrl || "http://LAPTOP-JMIK4SIO:5024/api";
  fetch(`${apiBaseUrl}/auth/permissions`, {
    headers: { Authorization: `Bearer ${token}` },
  }).then(async (response) => {
    if (!response.ok) throw new Error("Phiên đăng nhập không hợp lệ.");
    const result = await response.json();
    if (result.role.toUpperCase() !== role) throw new Error("Vai trò tài khoản không đồng nhất.");
    window.CurrentAccess.permissions = result.permissions;
    document.querySelectorAll("[data-permission]").forEach((element) => {
      if (!result.permissions.includes(element.dataset.permission)) element.remove();
    });
  }).catch(() => {
    localStorage.clear();
    window.location.replace("../index.html");
  });
})();
