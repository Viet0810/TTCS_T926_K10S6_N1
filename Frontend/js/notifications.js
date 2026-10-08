(() => {
  const header = document.querySelector(".dashboard-header");
  if (!header) return;

  let session = header.querySelector(".dashboard-session");
  if (!session) {
    session = document.createElement("div");
    session.className = "dashboard-session";
    [...header.children].filter((child) => !child.classList.contains("dashboard-brand"))
      .forEach((child) => session.appendChild(child));
    header.appendChild(session);
  }

  const center = document.createElement("div");
  center.className = "notification-center";
  center.innerHTML = `
    <button class="notification-trigger" type="button" aria-label="Thông báo của tôi" aria-expanded="false" aria-controls="notificationPanel">
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4"/></svg>
      <span class="notification-count" hidden></span>
    </button>
    <section class="notification-panel" id="notificationPanel" aria-label="Thông báo của tôi" hidden>
      <div class="notification-panel-heading"><div><strong>Thông báo của tôi</strong><span class="notification-subtitle">Chỉ hiển thị thông báo dành cho tài khoản đang đăng nhập</span></div><div class="notification-panel-actions"><button class="notification-push-toggle" type="button">Bật thông báo trên thiết bị</button><button class="notification-clear" type="button">Xóa thông báo của tôi</button></div></div>
      <ul class="notification-list"></ul>
      <p class="notification-empty">Chưa có thông báo nào.</p>
    </section>
    <dialog class="notification-detail-dialog" aria-labelledby="notificationDetailTitle">
      <div class="notification-detail-heading"><h2 id="notificationDetailTitle"></h2><button class="notification-detail-close btn-secondary" type="button">Đóng</button></div>
      <p class="notification-detail-time"></p>
      <p class="notification-detail-message"></p>
      <a class="notification-detail-action btn-primary" hidden></a>
    </dialog>`;
  session.insertBefore(center, session.querySelector("#logoutBtn") || null);

  const trigger = center.querySelector(".notification-trigger");
  const panel = center.querySelector(".notification-panel");
  const list = center.querySelector(".notification-list");
  const empty = center.querySelector(".notification-empty");
  const count = center.querySelector(".notification-count");
  const detail = center.querySelector(".notification-detail-dialog");
  const detailTitle = center.querySelector("#notificationDetailTitle");
  const detailTime = center.querySelector(".notification-detail-time");
  const detailMessage = center.querySelector(".notification-detail-message");
  const detailAction = center.querySelector(".notification-detail-action");
  const pushToggle = center.querySelector(".notification-push-toggle");
  let notices = [];
  let loading = false;
  let pushRegistration = null;
  const pushReady = "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;

  function base64UrlToBytes(value) {
    const padded = value.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(value.length / 4) * 4, "=");
    return Uint8Array.from(atob(padded), (character) => character.charCodeAt(0));
  }

  async function refreshPushButton() {
    if (!pushReady) {
      pushToggle.disabled = true;
      pushToggle.textContent = "Trình duyệt không hỗ trợ thông báo đẩy";
      return;
    }
    try {
      pushRegistration = await navigator.serviceWorker.register("../service-worker.js");
      const subscription = await pushRegistration.pushManager.getSubscription();
      pushToggle.textContent = subscription ? "Tắt thông báo trên thiết bị này" : "Bật thông báo trên thiết bị";
      pushToggle.disabled = Notification.permission === "denied" && !subscription;
      if (pushToggle.disabled) pushToggle.title = "Hãy cho phép thông báo trong cài đặt trình duyệt.";
    } catch {
      pushToggle.disabled = true;
      pushToggle.textContent = "Không thể bật thông báo trên thiết bị";
    }
  }

  pushToggle.addEventListener("click", async () => {
    if (!pushReady || !localStorage.getItem("token")) return;
    pushToggle.disabled = true;
    try {
      pushRegistration ||= await navigator.serviceWorker.register("../service-worker.js");
      const current = await pushRegistration.pushManager.getSubscription();
      if (current) {
        await API.removePushSubscription(current.endpoint);
        await current.unsubscribe();
        pushToggle.textContent = "Bật thông báo trên thiết bị";
      } else {
        const permission = await Notification.requestPermission();
        if (permission !== "granted") throw new Error("Bạn chưa cho phép trình duyệt gửi thông báo.");
        const { publicKey } = await API.getPushPublicKey();
        const subscription = await pushRegistration.pushManager.subscribe({
          userVisibleOnly: true, applicationServerKey: base64UrlToBytes(publicKey),
        });
        await API.savePushSubscription(subscription);
        pushToggle.textContent = "Tắt thông báo trên thiết bị này";
      }
    } catch (error) {
      alert(error.message || "Không thể cấu hình thông báo đẩy. Vui lòng kiểm tra kết nối và cấu hình máy chủ.");
    } finally {
      pushToggle.disabled = false;
      await refreshPushButton();
    }
  });

  function render() {
    const unread = notices.filter((notice) => !notice.readAt).length;
    count.hidden = unread === 0;
    count.textContent = unread > 9 ? "9+" : String(unread);
    empty.hidden = notices.length > 0;
    list.replaceChildren(...notices.map((notice) => {
      const item = document.createElement("li");
      item.className = `notification-item${notice.readAt ? " is-read" : ""}`;
      const open = document.createElement("button");
      open.type = "button"; open.className = "notification-entry";
      const title = document.createElement("strong"); title.textContent = notice.title;
      const message = document.createElement("p"); message.textContent = notice.message;
      const time = document.createElement("time");
      time.dateTime = notice.createdAt;
      time.textContent = new Intl.DateTimeFormat("vi-VN", { dateStyle: "short", timeStyle: "short" }).format(new Date(notice.createdAt));
      open.append(title, message, time);
      open.addEventListener("click", () => openDetail(notice));
      item.append(open);
      return item;
    }));
  }

  async function openDetail(notice) {
    detailTitle.textContent = notice.title;
    detailMessage.textContent = notice.message;
    detailTime.textContent = new Intl.DateTimeFormat("vi-VN", { dateStyle: "full", timeStyle: "short" }).format(new Date(notice.createdAt));
    const destination = ["document-reviews.html", "intern-upload-cv.html"].includes(notice.actionUrl) ? notice.actionUrl : "";
    detailAction.hidden = !destination;
    if (destination) {
      detailAction.href = destination;
      detailAction.textContent = destination === "document-reviews.html" ? "Mở danh sách duyệt hồ sơ" : "Mở hồ sơ và tài liệu của tôi";
    }
    detail.showModal();
    if (!notice.readAt) {
      notice.readAt = new Date().toISOString();
      render();
      try { await API.readNotification(notice.id); }
      catch { await load(); }
    }
  }

  async function load() {
    if (loading || !localStorage.getItem("token")) return;
    loading = true;
    try { notices = await API.getNotifications(); render(); }
    catch (error) { if (error.status === 401) Session.redirectIfExpired(error); }
    finally { loading = false; }
  }

  trigger.addEventListener("click", async () => {
    const opening = panel.hidden;
    panel.hidden = !opening;
    trigger.setAttribute("aria-expanded", String(opening));
    if (!opening) return;
    await load();
  });

  center.querySelector(".notification-detail-close").addEventListener("click", () => detail.close());
  detail.addEventListener("click", (event) => {
    if (event.target === detail) detail.close();
  });

  center.querySelector(".notification-clear").addEventListener("click", async () => {
    try { await API.clearNotifications(); notices = []; render(); }
    catch (error) { if (error.status === 401) Session.redirectIfExpired(error); }
  });
  document.addEventListener("click", (event) => {
    if (!center.contains(event.target)) {
      panel.hidden = true;
      trigger.setAttribute("aria-expanded", "false");
    }
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") {
      panel.hidden = true;
      trigger.setAttribute("aria-expanded", "false");
      trigger.focus();
    }
  });

  render();
  refreshPushButton();
  load();
  window.addEventListener("focus", load);
  window.setInterval(() => { if (!document.hidden) load(); }, 30000);
})();
