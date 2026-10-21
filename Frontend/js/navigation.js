
window.AppNavigation = (() => {
  const adminMenu = new Set([
    "dashboard.html",
    "user-manage.html",
    "permissions.html",
    "profile.html"
  ]);
  const items = [
    { label: "Lịch làm việc", href: "work-schedules.html", permissions: ["WORK_SCHEDULE_READ"] },
    { label: "Lịch họp", href: "meetings.html", permissions: ["MEETING_READ"] },
    { label: "Bảng điều khiển", href: "dashboard.html" },
    { label: "Quản lý tài khoản", href: "user-manage.html", permissions: ["MANAGE_USERS"] },
    { label: "Phân quyền RBAC", href: "permissions.html", permissions: ["RBAC_READ"] },
    { label: "Xem danh sách thực tập sinh", href: "intern-manage.html", permissions: ["VIEW_INTERNS"] },
    { label: "Tìm kiếm & Lọc thực tập sinh", href: "hr-search-filter.html", permissions: ["SEARCH_INTERNS"] },
    { label: "Phân công Mentor", href: "mentor-assignment.html", permissions: ["ASSIGN_MENTOR"] },
    { label: "Danh sách phân công", href: "mentor-assignment-list.html", permissions: ["ASSIGN_MENTOR", "MENTOR_WORKLOAD_READ"] },
    { label: "Nhiệm vụ", href: "tasks.html", permissions: ["MENTOR_TASK_READ", "OWN_TASK_READ"], roles: ["MENTOR", "INTERN"] },
    { label: "Báo cáo tuần", href: "weekly-reports.html", permissions: ["OWN_WEEKLY_REPORT_READ", "MENTOR_WEEKLY_REPORT_READ"], roles: ["INTERN", "MENTOR"] },
    { label: "Đánh giá cuối kỳ", href: "final-evaluation.html", permissions: ["VIEW_FINAL_EVALUATIONS"] },
    { label: "Hồ sơ cá nhân", href: "profile.html", permissions: ["VIEW_PROFILE", "VIEW_OWN_PROFILE"] },
    { label: "Lịch thực tập", href: "intern-schedule.html", permissions: ["VIEW_OWN_SCHEDULE"] },
    { label: "Nộp CV và đơn xin thực tập", href: "intern-upload-cv.html", permissions: ["UPLOAD_DOCUMENTS", "VIEW_DOCUMENTS"] },
    { label: "Hồ sơ thực tập", href: "internship-profile.html", permissions: ["VIEW_OWN_PROFILE"] },
    { label: "Xem và duyệt tài liệu", href: "document-reviews.html", permissions: ["DOCUMENT_REVIEW_READ"] },

    { label: "Quản lý hợp đồng", href: "contracts.html", permissions: ["MANAGE_CONTRACTS"] },

    { label: "Hợp đồng thực tập", href: "intern-contract.html", permissions: ["VIEW_OWN_CONTRACT"] },
    { label: "Chấm công", href: "intern-attendance.html", permissions: ["OWN_ATTENDANCE"] },
    { label: "Đăng ký nghỉ phép", href: "intern-leave-request.html", permissions: ["OWN_LEAVE_REQUEST_READ"], roles: ["INTERN"] },
    { label: "Duyệt đơn nghỉ", href: "mentor-leave-requests.html", permissions: ["MENTOR_LEAVE_REQUEST_READ"], roles: ["MENTOR"] },
    { label: "Quản lý chương trình", href: "program-setting.html", permissions: ["MANAGE_PROGRAMS"] },
    { label: "Báo cáo chuyên cần", href: "attendance-report.html", permissions: ["VIEW_ATTENDANCE_REPORT"] },
    { label: "Thông báo", href: "notifications.html", permissions: ["NOTIFICATION_READ"] }
  ];
  const groups = [
    { id: "overview", label: "Tổng quan" },
    { id: "calendar", label: "Lịch & công việc" },
    { id: "internship", label: "Quản lý thực tập" },
    { id: "administration", label: "Quản trị hệ thống" },
    { id: "account", label: "Cá nhân" }
  ];
  const groupByPage = new Map([
    ["dashboard.html", "overview"],
    ["work-schedules.html", "calendar"], ["meetings.html", "calendar"], ["tasks.html", "calendar"], ["weekly-reports.html", "calendar"],
    ["intern-manage.html", "internship"], ["hr-search-filter.html", "internship"], ["mentor-assignment.html", "internship"],
    ["mentor-assignment-list.html", "internship"], ["final-evaluation.html", "internship"], ["intern-schedule.html", "internship"],
    ["intern-upload-cv.html", "internship"], ["internship-profile.html", "internship"], ["document-reviews.html", "internship"],
    ["contracts.html", "internship"], ["intern-contract.html", "internship"], ["intern-attendance.html", "internship"],
    ["intern-leave-request.html", "internship"], ["mentor-leave-requests.html", "internship"], ["attendance-report.html", "internship"],
    ["user-manage.html", "administration"], ["permissions.html", "administration"], ["program-setting.html", "administration"],
    ["profile.html", "account"], ["notifications.html", "account"]
  ]);

  function render(session) {
    const role = session.user?.role;

    if (!["ADMIN", "HR", "MENTOR", "INTERN"].includes(role)) return;

    document.body.dataset.userRole = role;

    document.dispatchEvent(
      new CustomEvent("session-permissions-updated", {
        detail: session
      })
    );

    const nav = document.querySelector(".role-menu");

    if (session.user.mustChangePassword) nav?.remove();

    if (nav && !session.user.mustChangePassword) {
      nav.replaceChildren();
      nav.setAttribute("aria-label", "Điều hướng chính");

      const page = location.pathname.split("/").pop();

      const visibleGroups = new Map(groups.map(group => [group.id, []]));
      for (const item of items) {
        if (role === "ADMIN" && !adminMenu.has(item.href)) continue;
        if (item.roles && !item.roles.includes(role)) continue;
        if (
          item.permissions &&
          !item.permissions.some(permission =>
            Session.hasPermission(session, permission)
          )
        ) {
          continue;
        }

        const link = document.createElement("a");

        link.href = item.href;
        link.textContent = item.href === "final-evaluation.html" && role === "MENTOR" ? "Đánh giá thực tập sinh" : item.href === "user-manage.html" && role === "HR" ? "Quản lý Mentor" : item.label;

        if (window.AppVisual) {
          link.prepend(
            AppVisual.icon(AppVisual.moduleIcon(item.href))
          );
        }

        if (
          page === item.href ||
          (page === "user-create.html" && item.href === "user-manage.html") ||
          (page === "change-password.html" && item.href === "profile.html")
        ) {
          link.setAttribute("aria-current", "page");
        }

        visibleGroups.get(groupByPage.get(item.href) || "internship").push(link);
      }
      const storageKey = `sidebar-groups:${role}`;
      let savedOpen = [];
      try { savedOpen = JSON.parse(localStorage.getItem(storageKey) || "[]"); } catch {}
      for (const group of groups) {
        const links = visibleGroups.get(group.id);
        if (!links.length) continue;
        const section = document.createElement("section");
        section.className = "role-menu-group"; section.dataset.group = group.id;
        const containsCurrent = links.some(link => link.hasAttribute("aria-current"));
        const isOpen = containsCurrent || savedOpen.includes(group.id) || (!savedOpen.length && group.id === "overview");
        section.classList.toggle("is-open", isOpen);
        const heading = document.createElement("button"); heading.type = "button"; heading.className = "role-menu-group-title";
        heading.setAttribute("aria-expanded", String(isOpen));
        const label = document.createElement("span"); label.textContent = group.label;
        heading.append(label);
        const content = document.createElement("div"); content.className = "role-menu-group-links"; content.hidden = !isOpen; content.append(...links);
        section.append(heading,content);
        heading.addEventListener("click", () => {
          const nextOpen = !section.classList.contains("is-open");
          section.classList.toggle("is-open", nextOpen); content.hidden = !nextOpen;
          heading.setAttribute("aria-expanded", String(nextOpen));
          const open = [...nav.querySelectorAll(".role-menu-group.is-open")].map(item => item.dataset.group);
          localStorage.setItem(storageKey, JSON.stringify(open));
        });
        nav.append(section);
      }
    }

    const actions = document.querySelector(".dashboard-session");

    if (!actions) return;

    let badge = document.getElementById("roleBadge");

    if (!badge) {
      badge = document.createElement("span");
      badge.id = "roleBadge";
      actions.prepend(badge);
    }

    badge.textContent = role;
    badge.className = `badge ${role.toLowerCase()}`;

    let avatar = actions.querySelector(".session-avatar");

    if (!avatar) {
      avatar = document.createElement("span");
      avatar.className = "session-avatar";
      actions.prepend(avatar);
    }

    avatar.textContent = (session.user.fullName || role)
      .trim()
      .split(/\s+/)
      .slice(-2)
      .map(part => part[0])
      .join("")
      .toUpperCase();

    avatar.setAttribute(
      "aria-label",
      session.user.fullName || role
    );

    const dashboardLinks = [
      ...actions.querySelectorAll('a[href="dashboard.html"]')
    ];

    dashboardLinks.slice(1).forEach(link => link.remove());

    if (!dashboardLinks.length) {
      const link = document.createElement("a");

      link.href = "dashboard.html";
      link.className = "btn-secondary topbar-dashboard-link";
      link.textContent = "Bảng điều khiển";

      actions.insertBefore(
        link,
        document.getElementById("logoutBtn")
      );
    }

    let passwordLink = actions.querySelector(
      'a[href="change-password.html"]'
    );

    actions
      .querySelectorAll('a[href="change-password.html"]')
      .forEach(link => {
        if (link !== passwordLink) link.remove();
      });

    if (!passwordLink) {
      passwordLink = document.createElement("a");
    }

    passwordLink.href = "change-password.html";
    passwordLink.className = "btn-secondary topbar-password-link";
    passwordLink.textContent = "Đổi mật khẩu";

    actions.insertBefore(
      passwordLink,
      document.getElementById("logoutBtn")
    );

    if (window.NotificationUI) {
      window.NotificationUI.mount(actions, session);
    } else if (typeof document !== "undefined") {
      const s1 = document.createElement("script");

      s1.src = "../js/notification-service.js";

      s1.onload = () => {
        const s2 = document.createElement("script");

        s2.src = "../js/notification-ui.js";

        s2.onload = () =>
          window.NotificationUI?.mount(actions, session);

        document.body.appendChild(s2);
      };

      document.body.appendChild(s1);
    }
  }

  document.addEventListener("click", event => {
    if (event.target.closest("#logoutBtn")) {
      Session.logout();
    }
  });

  if (localStorage.getItem("token")) {
    API.getCurrentUser()
      .then(render)
      .catch(error => {
        Session.redirectIfExpired(error);

        const nav = document.querySelector(".role-menu");

        if (nav && error.code !== "PASSWORD_CHANGE_REQUIRED") {
          nav.textContent =
            "Chưa thể tải điều hướng. Vui lòng tải lại trang.";
        }
      });
  } else {
    Session.logout();
  }

  let refreshing = false;

  async function refresh() {
    if (
      refreshing ||
      !localStorage.getItem("token") ||
      document.hidden
    ) {
      return;
    }

    refreshing = true;

    try {
      await API.getCurrentUser();
    } catch (error) {
      Session.redirectIfExpired(error);
    } finally {
      refreshing = false;
    }
  }

  window.addEventListener("focus", refresh);
  document.addEventListener("visibilitychange", refresh);

  return { render };
})();
