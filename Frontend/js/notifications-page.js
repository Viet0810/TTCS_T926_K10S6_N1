/**
 * notifications-page.js
 * Controller trang Trung tâm thông báo (Frontend - K10S6N1-157)
 */

let allNotifications = [], notificationPageSession = {};
const notificationPermission = code => Session.hasPermission(notificationPageSession, code);
function applyNotificationPermissions(session) {
  notificationPageSession = session;
  const read = notificationPermission("NOTIFICATION_READ");
  document.getElementById("pageMarkAllReadBtn").hidden = !read || !notificationPermission("NOTIFICATION_UPDATE");
  if (document.getElementById("pageSeedBtn")) document.getElementById("pageSeedBtn").hidden = !read || !notificationPermission("NOTIFICATION_CREATE");
  if (!read) { allNotifications = []; updateStats([]); }
  applyFilters();
}
document.addEventListener("session-permissions-updated", event => applyNotificationPermissions(event.detail));

document.addEventListener("DOMContentLoaded", async () => {
  setupPageEventListeners();
  try { applyNotificationPermissions(await API.getCurrentUser()); await loadPageNotifications(); }
  catch (error) { AppUI.toast(error.message, "error"); Session.redirectIfExpired(error); }
});

function setupPageEventListeners() {
  const categoryFilter = document.getElementById("filterCategory");
  const statusFilter = document.getElementById("filterStatus");
  const searchInput = document.getElementById("filterSearch");
  const markAllBtn = document.getElementById("pageMarkAllReadBtn");
  const refreshBtn = document.getElementById("pageRefreshBtn");
  const seedBtn = document.getElementById("pageSeedBtn");

  if (categoryFilter) categoryFilter.addEventListener("change", applyFilters);
  if (statusFilter) statusFilter.addEventListener("change", applyFilters);
  if (searchInput) searchInput.addEventListener("input", applyFilters);

  if (markAllBtn) {
    markAllBtn.addEventListener("click", async () => {
      if (!notificationPermission("NOTIFICATION_UPDATE")) return;
      try { await NotificationService.markAllAsRead(); } catch (error) { AppUI.toast(error.message, "error"); return; }
      allNotifications.forEach(x => { x.isRead = true; });
      updateStats(allNotifications);
      applyFilters();
      if (window.NotificationUI) NotificationUI.refreshBadge();
    });
  }

  if (refreshBtn) {
    refreshBtn.addEventListener("click", async () => {
      await loadPageNotifications();
    });
  }

  if (seedBtn) {
    seedBtn.addEventListener("click", async () => {
      if (!notificationPermission("NOTIFICATION_CREATE")) return;
      try { await NotificationService.seedSamples(); } catch (error) { AppUI.toast(error.message, "error"); return; }
      await loadPageNotifications();
      if (window.NotificationUI) NotificationUI.refreshBadge();
    });
  }
}

async function loadPageNotifications() {
  if (!notificationPermission("NOTIFICATION_READ")) return;
  const infoEl = document.getElementById("notifsLoadedInfo");
  if (infoEl) infoEl.textContent = "Đang tải dữ liệu…";

  try {
    const res = await NotificationService.getNotifications({ page: 1, pageSize: 50 });
    allNotifications = res.items || [];
    updateStats(allNotifications);
    applyFilters();
    if (infoEl) infoEl.textContent = `Hiển thị ${allNotifications.length} thông báo gần nhất`;
    if (window.NotificationUI) NotificationUI.refreshBadge();
  } catch (err) {
    allNotifications = []; updateStats([]); applyFilters();
    AppUI.toast(err.message, "error");
    if (infoEl) infoEl.textContent = "Lỗi tải thông báo";
  }
}

function updateStats(items) {
  const totalEl = document.getElementById("totalNotifsCount");
  const unreadEl = document.getElementById("unreadNotifsCount");
  const readEl = document.getElementById("readNotifsCount");

  const total = items.length;
  const unread = items.filter(x => !x.isRead).length;
  const read = total - unread;

  if (totalEl) totalEl.textContent = String(total);
  if (unreadEl) unreadEl.textContent = String(unread);
  if (readEl) readEl.textContent = String(read);
}

function applyFilters() {
  const category = document.getElementById("filterCategory")?.value || "ALL";
  const status = document.getElementById("filterStatus")?.value || "ALL";
  const query = (document.getElementById("filterSearch")?.value || "").trim().toLowerCase();

  let filtered = [...allNotifications];

  if (category !== "ALL") {
    filtered = filtered.filter(x => String(x.category).toUpperCase() === category.toUpperCase());
  }

  if (status === "UNREAD") {
    filtered = filtered.filter(x => !x.isRead);
  } else if (status === "READ") {
    filtered = filtered.filter(x => x.isRead);
  }

  if (query) {
    filtered = filtered.filter(x =>
      (x.title && x.title.toLowerCase().includes(query)) ||
      (x.message && x.message.toLowerCase().includes(query))
    );
  }

  renderPageList(filtered);
}

function renderPageList(items) {
  const listEl = document.getElementById("fullNotificationList");
  if (!listEl) return;

  if (!items || items.length === 0) {
    listEl.innerHTML = `
      <li class="notification-empty" style="padding: 48px 16px;">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
          <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
        </svg>
        <p style="font-size: 14px; font-weight: 600;">Không có thông báo phù hợp.</p>
        <p style="font-size: 12px; color: var(--muted); margin-top: 4px;">Thử thay đổi bộ lọc hoặc bấm "Tạo thông báo mẫu" ở phía trên.</p>
      </li>
    `;
    return;
  }

  listEl.innerHTML = items.map(item => {
    const priorityMeta = NotificationService.getPriorityMeta(item.priority);
    const categoryLabel = NotificationService.getCategoryLabel(item.category);
    const timeAgo = NotificationService.formatTimeAgo(item.createdAt);
    const unreadClass = !item.isRead ? "unread" : "";

    return `
      <li class="notification-item ${unreadClass}" data-id="${item.id}" style="padding: 16px; margin-bottom: 8px; border: 1px solid var(--line, #e2e8f0);">
        <div class="notif-icon-box ${priorityMeta.className}" style="width: 44px; height: 44px; font-size: 18px;">
          <span>${priorityMeta.icon}</span>
        </div>
        <div class="notif-body">
          <div class="notif-title" style="font-size: 15px;">
            <span>${escapeHtml(item.title)}</span>
            <div style="display: flex; align-items: center; gap: 8px;">
              <span class="badge ${item.isRead ? 'secondary' : 'primary'}" style="font-size: 11px;">
                ${item.isRead ? 'Đã đọc' : 'Mới'}
              </span>
              ${!item.isRead ? '<span class="notif-unread-dot" title="Chưa đọc"></span>' : ''}
            </div>
          </div>
          <div class="notif-desc" style="font-size: 13px; margin: 6px 0;">${escapeHtml(item.message)}</div>
          <div class="notif-meta" style="justify-content: space-between; margin-top: 8px;">
            <div style="display: flex; gap: 10px;">
              <span>🕒 ${timeAgo}</span>
              <span>🏷️ ${escapeHtml(categoryLabel)}</span>
            </div>
            <div style="display: flex; gap: 8px;">
              ${!item.isRead && notificationPermission("NOTIFICATION_UPDATE") ? `<button type="button" class="btn-secondary mark-single-btn" data-id="${item.id}" style="padding: 2px 10px; font-size: 12px;">Đánh dấu đã đọc</button>` : ''}
              ${sanitizeUrl(item.actionUrl) ? `<a href="${escapeHtml(sanitizeUrl(item.actionUrl))}" class="btn-primary" style="padding: 2px 12px; font-size: 12px; text-decoration: none;">Xem chi tiết →</a>` : ''}
            </div>
          </div>
        </div>
      </li>
    `;
  }).join("");

  listEl.querySelectorAll(".mark-single-btn").forEach(btn => {
    btn.addEventListener("click", async (e) => {
      e.stopPropagation();
      const id = btn.getAttribute("data-id");
      if (id) {
        if (!notificationPermission("NOTIFICATION_UPDATE")) return;
        try { await NotificationService.markAsRead(id); } catch (error) { AppUI.toast(error.message, "error"); return; }
        const item = allNotifications.find(x => x.id === Number(id));
        if (item) item.isRead = true;
        updateStats(allNotifications);
        applyFilters();
        if (window.NotificationUI) NotificationUI.refreshBadge();
      }
    });
  });
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
