const permissionLabels = {
  MANAGE_CONTRACTS: "Quản lý và tải lên hợp đồng thực tập",
  VIEW_OWN_CONTRACT: "Xem và xác nhận hợp đồng của chính mình",
  OWN_ATTENDANCE: "Check-in / check-out của chính mình",
  ASSIGN_MENTOR: "Xem Mentor và phân công thực tập sinh",
  MANAGE_USERS: "Quản lý tài khoản",
  CREATE_USER: "Tạo tài khoản",
  DELETE_USER: "Xóa tài khoản",
  VIEW_INTERNS: "Xem thực tập sinh",
  MANAGE_INTERNS: "Tạo hồ sơ thực tập sinh",
  SEARCH_INTERNS: "Tìm kiếm và lọc hồ sơ thực tập sinh",
  EDIT_INTERNS: "Chỉnh sửa hồ sơ thực tập sinh",
  VIEW_PROFILE: "Xem hồ sơ cá nhân",
  UPLOAD_DOCUMENTS: "Nộp CV và đơn xin thực tập",
  VIEW_DOCUMENTS: "Xem tài liệu",
  APPROVE_DOCUMENTS: "Duyệt tài liệu",
  MANAGE_PERMISSIONS: "Quản lý phân quyền",
  MANAGE_PROGRAMS: "Xem, tạo, xóa và thiết lập thời gian chương trình",
  VIEW_ATTENDANCE_REPORT: "Xem báo cáo chuyên cần và nghỉ phép",
};

const permissionGroups = [
  { label: "Hợp đồng thực tập", keys: ["MANAGE_CONTRACTS", "VIEW_OWN_CONTRACT"] },
  { label: "Phân công thực tập", keys: ["ASSIGN_MENTOR"] },
  { label: "Quản lý tài khoản", keys: ["MANAGE_USERS", "CREATE_USER", "DELETE_USER"] },
  { label: "Hồ sơ thực tập sinh", keys: ["VIEW_INTERNS", "MANAGE_INTERNS", "SEARCH_INTERNS", "EDIT_INTERNS"] },
  { label: "Tài liệu", keys: ["UPLOAD_DOCUMENTS", "VIEW_DOCUMENTS", "APPROVE_DOCUMENTS"] },
  { label: "Hồ sơ cá nhân", keys: ["VIEW_PROFILE"] },
  { label: "Chương trình và lịch thực tập", keys: ["MANAGE_PROGRAMS"] },
  { label: "Chuyên cần và nghỉ phép", keys: ["VIEW_ATTENDANCE_REPORT", "OWN_ATTENDANCE"] },
  { label: "Phân quyền hệ thống", keys: ["MANAGE_PERMISSIONS"] },
];

async function loadRolePermissions() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const roles = await API.getRolePermissions();
    const body = document.getElementById("rolePermissionCards");
    body.replaceChildren();

    Object.entries(roles).forEach(([role, permissions]) => {
      const card = document.createElement("article");
      card.className = "permission-role-card";
      const header = document.createElement("header");
      header.className = "permission-role-header";
      const title = document.createElement("h3");
      title.className = "permission-role-badge";
      if (["ADMIN", "HR", "MENTOR", "INTERN"].includes(role)) title.classList.add(role.toLowerCase());
      title.textContent = role;
      const count = document.createElement("span");
      count.className = "permission-count";
      count.textContent = `${permissions.length} quyền`;
      header.append(title, count);
      card.appendChild(header);
      const groups = new Map();
      for (const permission of permissions) {
        const label = permissionGroups.find((group) => group.keys.includes(permission))?.label || "Quyền khác";
        if (!groups.has(label)) groups.set(label, []);
        groups.get(label).push(permission);
      }
      for (const [label, entries] of groups) {
        const section = document.createElement("section");
        section.className = "permission-group";
        const heading = document.createElement("h4");
        heading.textContent = label;
        const list = document.createElement("ul");
        for (const permission of entries) {
          const item = document.createElement("li");
          const check = document.createElement("span");
          check.className = "permission-check";
          check.setAttribute("aria-hidden", "true");
          check.textContent = "✓";
          const text = document.createElement("span");
          text.textContent = role === "INTERN" && permission === "VIEW_PROFILE"
            ? "Xem hồ sơ và lịch thực tập cá nhân" : permissionLabels[permission] || permission;
          item.append(check, text);
          list.appendChild(item);
        }
        section.append(heading, list);
        card.appendChild(section);
      }
      if (!permissions.length) {
        const empty = document.createElement("p");
        empty.className = "permission-count";
        empty.textContent = "Chưa được cấp quyền.";
        card.appendChild(empty);
      }
      body.appendChild(card);
    });
  } catch (error) {
    document.getElementById("permissionsMessage").textContent = error.message;
    if (error.status === 403) {
      window.location.href = "dashboard.html";
    } else Session.redirectIfExpired(error);
  }
}


loadRolePermissions();
