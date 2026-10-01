const permissionLabels = {
  MANAGE_USERS: "Quản lý tài khoản",
  CREATE_USER: "Tạo tài khoản",
  DELETE_USER: "Xóa tài khoản",
  VIEW_INTERNS: "Xem thực tập sinh",
  MANAGE_INTERNS: "Quản lý thực tập sinh",
  VIEW_PROFILE: "Xem hồ sơ cá nhân",
  VIEW_DOCUMENTS: "Xem tài liệu",
  APPROVE_DOCUMENTS: "Duyệt tài liệu",
  MANAGE_PERMISSIONS: "Quản lý phân quyền",
};

async function loadRolePermissions() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const roles = await API.getRolePermissions();
    const body = document.getElementById("rolePermissionTable");
    body.replaceChildren();

    Object.entries(roles).forEach(([role, permissions]) => {
      const row = document.createElement("tr");
      const roleCell = document.createElement("td");
      const permissionsCell = document.createElement("td");
      roleCell.textContent = role;
      permissionsCell.textContent = permissions
        .map((permission) => permissionLabels[permission] || permission)
        .join(", ");
      row.append(roleCell, permissionsCell);
      body.appendChild(row);
    });
  } catch (error) {
    document.getElementById("permissionsMessage").textContent = error.message;
    if (error.message.includes("không có quyền")) {
      window.location.href = "dashboard.html";
    } else if (
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

loadRolePermissions();
