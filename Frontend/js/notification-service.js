/**
 * notification-service.js
 * Quản lý logic gọi API và tiện ích định dạng cho hệ thống thông báo (K10S6N1-156 / K10S6N1-157)
 */

window.NotificationService = (() => {
  // Dữ liệu fallback dự phòng khi chạy offline hoặc môi trường độc lập
  let localCache = [];
  let localUnreadCount = 0;

  async function getUnreadCount() {
    try {
      if (typeof API !== "undefined" && typeof API.getNotificationUnreadCount === "function") {
        const res = await API.getNotificationUnreadCount();
        localUnreadCount = res?.unreadCount ?? 0;
        return localUnreadCount;
      }
    } catch (err) {
      if (err.status === 401 || err.status === 403) { localCache = []; localUnreadCount = 0; throw err; }
      localCache = []; localUnreadCount = 0; throw err;
    }
    throw new Error("Không tải được số thông báo.");
  }

  async function getNotifications(params = {}) {
    try {
      if (typeof API !== "undefined" && typeof API.getNotifications === "function") {
        const res = await API.getNotifications(params);
        if (res && Array.isArray(res.items)) {
          localCache = res.items;
          localUnreadCount = res.unreadCount ?? localUnreadCount;
          return res;
        }
      }
    } catch (err) {
      if (err.status === 401 || err.status === 403) { localCache = []; localUnreadCount = 0; throw err; }
      localCache = []; localUnreadCount = 0; throw err;
    }
    throw new Error("Không tải được danh sách thông báo.");
  }

  async function markAsRead(id) {
    try {
      if (typeof API !== "undefined" && typeof API.markNotificationRead === "function") {
        const res = await API.markNotificationRead(id);
        const item = localCache.find(x => x.id === Number(id));
        if (item) {
          item.isRead = true;
          item.readAt = new Date().toISOString();
        }
        if (localUnreadCount > 0) localUnreadCount--;
        return res;
      }
    } catch (err) {
      if (err.status === 401 || err.status === 403) { localCache = []; localUnreadCount = 0; throw err; }
      throw err;
    }
    throw new Error("Không thể đánh dấu thông báo.");
  }

  async function markAllAsRead() {
    try {
      if (typeof API !== "undefined" && typeof API.markAllNotificationsRead === "function") {
        const res = await API.markAllNotificationsRead();
        localCache.forEach(x => { x.isRead = true; });
        localUnreadCount = 0;
        return res;
      }
    } catch (err) {
      if (err.status === 401 || err.status === 403) { localCache = []; localUnreadCount = 0; throw err; }
      throw err;
    }
    throw new Error("Không thể đánh dấu thông báo.");
  }

  async function seedSamples() {
    try {
      if (typeof API !== "undefined" && typeof API.seedNotifications === "function") {
        return await API.seedNotifications();
      }
    } catch (err) {
      if (err.status === 401 || err.status === 403) { localCache = []; localUnreadCount = 0; throw err; }
      throw err;
    }
    throw new Error("API thông báo chưa sẵn sàng.");
  }

  function formatTimeAgo(isoString) {
    if (!isoString) return "";
    const date = new Date(isoString);
    if (isNaN(date.getTime())) return "";

    const now = new Date();
    const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffSec < 60) return "Vừa xong";
    const diffMin = Math.floor(diffSec / 60);
    if (diffMin < 60) return `${diffMin} phút trước`;
    const diffHour = Math.floor(diffMin / 60);
    if (diffHour < 24) return `${diffHour} giờ trước`;
    const diffDay = Math.floor(diffHour / 24);
    if (diffDay === 1) return "Hôm qua";
    if (diffDay < 7) return `${diffDay} ngày trước`;

    const d = String(date.getDate()).padStart(2, "0");
    const m = String(date.getMonth() + 1).padStart(2, "0");
    const y = date.getFullYear();
    return `${d}/${m}/${y}`;
  }

  function getPriorityMeta(priority) {
    switch (String(priority).toUpperCase()) {
      case "SUCCESS":
        return { className: "priority-success", icon: "✓", label: "Thành công" };
      case "WARNING":
        return { className: "priority-warning", icon: "!", label: "Cảnh báo" };
      case "DANGER":
        return { className: "priority-danger", icon: "✕", label: "Quan trọng" };
      case "INFO":
      default:
        return { className: "priority-info", icon: "ℹ", label: "Thông tin" };
    }
  }

  function getCategoryLabel(category) {
    switch (String(category).toUpperCase()) {
      case "ATTENDANCE": return "Chấm công & Điểm danh";
      case "DOCUMENT": return "Hồ sơ & Tài liệu";
      case "CONTRACT": return "Hợp đồng";
      case "ASSIGNMENT": return "Phân công Mentor";
      case "PROGRAM": return "Kỳ thực tập";
      case "SYSTEM":
      default:
        return "Hệ thống";
    }
  }

  return {
    getUnreadCount,
    getNotifications,
    markAsRead,
    markAllAsRead,
    seedSamples,
    formatTimeAgo,
    getPriorityMeta,
    getCategoryLabel
  };
})();
