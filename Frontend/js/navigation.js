window.AppNavigation = (() => {
  const items = [
    { label: "Bảng điều khiển", href: "dashboard.html" },
    { label: "Quản lý tài khoản", href: "user-manage.html", permissions: ["MANAGE_USERS"] },
    { label: "Xem danh sách thực tập sinh", href: "intern-manage.html", permissions: ["VIEW_INTERNS"] },
    { label: "Tìm kiếm & Lọc thực tập sinh", href: "hr-search-filter.html", permissions: ["SEARCH_INTERNS"] },
    { label: "Phân công Mentor", href: "mentor-assignment.html", permissions: ["ASSIGN_MENTOR"] },
    { label: "Danh sách phân công", href: "mentor-assignment-list.html", permissions: ["ASSIGN_MENTOR"] },
    { label: "Hồ sơ cá nhân", href: "profile.html", permissions: ["VIEW_PROFILE", "VIEW_OWN_PROFILE"] },
    { label: "Lịch thực tập", href: "intern-schedule.html", permissions: ["VIEW_OWN_SCHEDULE"] },
    { label: "Nộp CV và đơn xin thực tập", href: "intern-upload-cv.html", permissions: ["UPLOAD_DOCUMENTS", "VIEW_DOCUMENTS"] },
    { label: "Hồ sơ thực tập", href: "internship-profile.html", permissions: ["VIEW_OWN_PROFILE"] },
    { label: "Xem và duyệt tài liệu", href: "document-reviews.html", permissions: ["DOCUMENT_REVIEW_READ"] },
    { label: "Quản lý hợp đồng", href: "contracts.html", permissions: ["MANAGE_CONTRACTS"] },
    { label: "Hợp đồng thực tập", href: "intern-contract.html", permissions: ["VIEW_OWN_CONTRACT"] },
    { label: "Chấm công", href: "intern-attendance.html", permissions: ["OWN_ATTENDANCE"] },
    { label: "Đăng ký nghỉ phép", href: "intern-leave-request.html", permissions: ["OWN_ATTENDANCE"] },
    { label: "Quản lý chương trình", href: "program-setting.html", permissions: ["MANAGE_PROGRAMS"] },
    { label: "Báo cáo chuyên cần", href: "attendance-report.html", permissions: ["VIEW_ATTENDANCE_REPORT"] },
    { label: "Thông báo", href: "notifications.html", permissions: ["VIEW_PROFILE", "VIEW_OWN_PROFILE"] },
    { label: "Ma trận phân quyền", href: "permissions.html", permissions: ["RBAC_READ"] },
  ];
  function render(session) {
    const role = session.user?.role;
    if (!["ADMIN", "HR", "MENTOR", "INTERN"].includes(role)) return;
    document.body.dataset.userRole = role;
    document.dispatchEvent(new CustomEvent('session-permissions-updated', { detail: session }));
    const nav = document.querySelector(".role-menu");
    if (session.user.mustChangePassword) nav?.remove();
    if (nav && !session.user.mustChangePassword) {
      nav.replaceChildren();
      nav.setAttribute("aria-label", "Điều hướng chính");
      const page = location.pathname.split("/").pop();
      for (const item of items) {
        if (item.permissions && !item.permissions.some(permission => Session.hasPermission(session, permission))) continue;
        const link = document.createElement("a"); link.href = item.href; link.textContent = item.label;
        if (window.AppVisual) link.prepend(AppVisual.icon(AppVisual.moduleIcon(item.href)));
        if (page === item.href || (page === "user-create.html" && item.href === "user-manage.html")
            || (page === "change-password.html" && item.href === "profile.html")) link.setAttribute("aria-current", "page");
        nav.append(link);
      }
    }
    const actions = document.querySelector(".dashboard-session");
    if (!actions) return;
    let badge = document.getElementById("roleBadge");
    if (!badge) { badge = document.createElement("span"); badge.id = "roleBadge"; actions.prepend(badge); }
    badge.textContent = role; badge.className = `badge ${role.toLowerCase()}`;
    let avatar = actions.querySelector('.session-avatar');
    if (!avatar) { avatar = document.createElement('span'); avatar.className = 'session-avatar'; actions.prepend(avatar); }
    avatar.textContent = (session.user.fullName || role).trim().split(/\s+/).slice(-2).map(part => part[0]).join('').toUpperCase();
    avatar.setAttribute('aria-label', session.user.fullName || role);
    const dashboardLinks = [...actions.querySelectorAll('a[href="dashboard.html"]')];
    dashboardLinks.slice(1).forEach(link => link.remove());
    if (!dashboardLinks.length) {
      const link = document.createElement("a"); link.href = "dashboard.html"; link.className = "btn-secondary topbar-dashboard-link";
      link.textContent = "Bảng điều khiển"; actions.insertBefore(link, document.getElementById("logoutBtn"));
    }
    let passwordLink = actions.querySelector('a[href="change-password.html"]');
    actions.querySelectorAll('a[href="change-password.html"]').forEach(link => { if (link !== passwordLink) link.remove(); });
    if (!passwordLink) passwordLink = document.createElement("a");
    passwordLink.href = "change-password.html";
    passwordLink.className = "btn-secondary topbar-password-link";
    passwordLink.textContent = "Đổi mật khẩu";
    actions.insertBefore(passwordLink, document.getElementById("logoutBtn"));
    if (window.NotificationUI) {
      window.NotificationUI.mount(actions);
    } else if (typeof document !== "undefined") {
      const s1 = document.createElement("script");
      s1.src = "../js/notification-service.js";
      s1.onload = () => {
        const s2 = document.createElement("script");
        s2.src = "../js/notification-ui.js";
        s2.onload = () => window.NotificationUI?.mount(actions);
        document.body.appendChild(s2);
      };
      document.body.appendChild(s1);
    }
  }
  document.addEventListener("click", event => { if (event.target.closest("#logoutBtn")) Session.logout(); });
  if (localStorage.getItem("token")) {
    API.getCurrentUser().then(render).catch(error => {
      Session.redirectIfExpired(error);
      const nav = document.querySelector(".role-menu");
      if (nav && error.code !== "PASSWORD_CHANGE_REQUIRED") nav.textContent = "Chưa thể tải điều hướng. Vui lòng tải lại trang.";
    });
  } else Session.logout();
  let refreshing = false;
  async function refresh() {
    if (refreshing || !localStorage.getItem('token') || document.hidden) return;
    refreshing = true;
    try { await API.getCurrentUser(); }
    catch (error) { Session.redirectIfExpired(error); }
    finally { refreshing = false; }
  }
  window.addEventListener('focus', refresh);
  document.addEventListener('visibilitychange', refresh);
  return { render };
})();
