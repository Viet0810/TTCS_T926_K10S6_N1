/**
 * notification-ui.js
 * Quản lý giao diện Khu vực thông báo và số chưa đọc (K10S6N1-157)
 */

window.NotificationUI = (() => {
  let isMounted = false, notificationSession = {};
  const canMarkRead = () => Session.hasPermission(notificationSession, "NOTIFICATION_UPDATE");
  let currentTab = "all"; // 'all' | 'unread'
  let cachedNotifications = [];

  function ensureStyles() {
    if (!document.getElementById("notifications-css")) {
      const link = document.createElement("link");
      link.id = "notifications-css";
      link.rel = "stylesheet";
      link.href = "../css/notifications.css";
      document.head.appendChild(link);
    }
  }

  function mount(targetContainer, session = {}) {
    notificationSession = session;
    if (!Session.hasPermission(session, "NOTIFICATION_READ")) {
      document.getElementById("notificationWrapper")?.remove(); cachedNotifications = []; isMounted = false; return;
    }
    const markButton = document.getElementById("markAllReadBtn");
    if (markButton) markButton.hidden = !canMarkRead();
    if (!targetContainer || document.getElementById("notificationWrapper")) return;
    ensureStyles();

    const wrapper = document.createElement("div");
    wrapper.id = "notificationWrapper";
    wrapper.className = "notification-wrapper";

    wrapper.innerHTML = `
      <button id="notificationBellBtn" type="button" class="notification-bell-btn" aria-label="Xem thông báo" aria-expanded="false" title="Thông báo">
        <svg class="notification-bell-icon" viewBox="0 0 24 24">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
          <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
        </svg>
        <span id="notificationBadge" class="notification-badge hidden" data-count="0">0</span>
      </button>

      <div id="notificationPopover" class="notification-popover" role="dialog" aria-modal="false" aria-label="Hộp thư thông báo">
        <div class="notification-popover-header">
          <h3 class="notification-popover-title">
            <span>Thông báo</span>
            <span id="notifCountPill" class="notif-count-pill">0</span>
          </h3>
          <button id="markAllReadBtn" type="button" class="mark-all-read-btn">Đánh dấu tất cả đã đọc</button>
        </div>

        <div class="notification-tabs">
          <button id="tabAllNotifs" type="button" class="notification-tab-btn active" data-tab="all">Tất cả</button>
          <button id="tabUnreadNotifs" type="button" class="notification-tab-btn" data-tab="unread">Chưa đọc</button>
        </div>

        <ul id="notificationList" class="notification-list" role="list">
          <li class="notification-empty">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
              <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
            </svg>
            <p>Đang tải thông báo…</p>
          </li>
        </ul>

        <div class="notification-popover-footer">
          <a href="notifications.html" id="viewAllNotifsLink">Xem tất cả thông báo →</a>
        </div>
      </div>
    `;

    // Chèn trước nút Đổi mật khẩu hoặc Đăng xuất
    const ref = targetContainer.querySelector("#logoutBtn") || targetContainer.firstChild;
    targetContainer.insertBefore(wrapper, ref);

    isMounted = true;
    setupEvents();
    refreshBadge();
  }

  function setupEvents() {
    const bellBtn = document.getElementById("notificationBellBtn");
    const popover = document.getElementById("notificationPopover");
    const markAllBtn = document.getElementById("markAllReadBtn");
    if (markAllBtn) markAllBtn.hidden = !canMarkRead();
    const tabAll = document.getElementById("tabAllNotifs");
    const tabUnread = document.getElementById("tabUnreadNotifs");

    if (bellBtn && popover) {
      bellBtn.addEventListener("click", async (e) => {
        e.stopPropagation();
        const isOpen = popover.classList.contains("open");
        if (!isOpen) {
          popover.classList.add("open");
          bellBtn.setAttribute("aria-expanded", "true");
          await loadPopoverNotifications();
        } else {
          closePopover();
        }
      });
    }

    if (markAllBtn) {
      markAllBtn.addEventListener("click", async (e) => {
        e.stopPropagation();
        try { await markAllAsRead(); } catch (error) { AppUI.toast(error.message, "error"); }
      });
    }

    if (tabAll && tabUnread) {
      tabAll.addEventListener("click", () => switchTab("all"));
      tabUnread.addEventListener("click", () => switchTab("unread"));
    }

    // Đóng khi click ngoài hoặc ấn ESC
    document.addEventListener("click", (e) => {
      const wrapper = document.getElementById("notificationWrapper");
      if (wrapper && !wrapper.contains(e.target)) {
        closePopover();
      }
    });

    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape") closePopover();
    });
  }

  function closePopover() {
    const popover = document.getElementById("notificationPopover");
    const bellBtn = document.getElementById("notificationBellBtn");
    if (popover) popover.classList.remove("open");
    if (bellBtn) bellBtn.setAttribute("aria-expanded", "false");
  }

  async function refreshBadge() {
    const badge = document.getElementById("notificationBadge");
    const pill = document.getElementById("notifCountPill");
    if (!badge) return;

    try {
      const count = await NotificationService.getUnreadCount();
      badge.textContent = count > 99 ? "99+" : String(count);
      badge.setAttribute("data-count", String(count));
      badge.classList.toggle("hidden", count <= 0);

      if (pill) {
        pill.textContent = count > 0 ? `${count} mới` : "0";
      }
    } catch (err) {
      badge.textContent = "?"; badge.classList.remove("hidden"); badge.title = "Không tải được số thông báo";
    }
  }

  async function loadPopoverNotifications() {
    const listEl = document.getElementById("notificationList");
    if (!listEl) return;

    try {
      const res = await NotificationService.getNotifications({ page: 1, pageSize: 15 });
      cachedNotifications = res.items || [];
      renderList(cachedNotifications);
      refreshBadge();
    } catch (err) {
      listEl.innerHTML = `
        <li class="notification-empty">
          <p>Không thể tải thông báo. Vui lòng thử lại sau.</p>
        </li>
      `;
    }
  }

  function switchTab(tab) {
    currentTab = tab;
    const tabAll = document.getElementById("tabAllNotifs");
    const tabUnread = document.getElementById("tabUnreadNotifs");

    if (tabAll) tabAll.classList.toggle("active", tab === "all");
    if (tabUnread) tabUnread.classList.toggle("active", tab === "unread");

    renderList(cachedNotifications);
  }

  function renderList(items) {
    const listEl = document.getElementById("notificationList");
    if (!listEl) return;

    const filtered = currentTab === "unread" ? items.filter(x => !x.isRead) : items;

    if (!filtered || filtered.length === 0) {
      listEl.innerHTML = `
        <li class="notification-empty">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
            <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
          </svg>
          <p>${currentTab === "unread" ? "Không có thông báo chưa đọc nào." : "Hiện tại bạn không có thông báo nào."}</p>
        </li>
      `;
      return;
    }

    listEl.innerHTML = filtered.map(item => {
      const priorityMeta = NotificationService.getPriorityMeta(item.priority);
      const timeAgo = NotificationService.formatTimeAgo(item.createdAt);
      const unreadClass = !item.isRead ? "unread" : "";

      return `
        <li class="notification-item ${unreadClass}" data-id="${item.id}" data-url="${escapeHtml(item.actionUrl || '')}" role="button" tabindex="0">
          <div class="notif-icon-box ${priorityMeta.className}">
            <span>${priorityMeta.icon}</span>
          </div>
          <div class="notif-body">
            <div class="notif-title">
              <span>${escapeHtml(item.title)}</span>
              ${!item.isRead ? '<span class="notif-unread-dot" title="Chưa đọc"></span>' : ''}
            </div>
            <div class="notif-desc">${escapeHtml(item.message)}</div>
            <div class="notif-meta">
              <span>${timeAgo}</span>
              ${item.category ? `<span>• ${NotificationService.getCategoryLabel(item.category)}</span>` : ''}
            </div>
          </div>
        </li>
      `;
    }).join("");

    // Bắt sự kiện click vào từng thông báo
    listEl.querySelectorAll(".notification-item").forEach(itemEl => {
      itemEl.addEventListener("click", async () => {
        const id = itemEl.getAttribute("data-id");
        const actionUrl = itemEl.getAttribute("data-url");

        if (id && canMarkRead()) {
          try { await NotificationService.markAsRead(id); }
          catch (error) { AppUI.toast(error.message, "error"); return; }
          const found = cachedNotifications.find(x => x.id === Number(id));
          if (found) found.isRead = true;
          itemEl.classList.remove("unread");
          const dot = itemEl.querySelector(".notif-unread-dot");
          if (dot) dot.remove();
          await refreshBadge();
        }

        const safeUrl = sanitizeUrl(actionUrl);
        if (safeUrl) {
          window.location.href = safeUrl;
        }
      });
    });
  }

  async function markAllAsRead() {
    if (!canMarkRead()) return;
    await NotificationService.markAllAsRead();
    cachedNotifications.forEach(x => { x.isRead = true; });
    renderList(cachedNotifications);
    await refreshBadge();
  }

  function sanitizeUrl(url) {
    if (!url) return "";
    const trimmed = String(url).trim();
    if (/^(https?:\/\/|\/|\.\/|\.\.\/|[a-zA-Z0-9_\-]+\.html)/i.test(trimmed) && !trimmed.toLowerCase().startsWith("javascript:")) {
      return trimmed;
    }
    return "";
  }

  function escapeHtml(text) {
    const div = document.createElement("div");
    div.textContent = text == null ? "" : String(text);
    return div.innerHTML;
  }

  return {
    mount,
    refreshBadge,
    closePopover
  };
})();
