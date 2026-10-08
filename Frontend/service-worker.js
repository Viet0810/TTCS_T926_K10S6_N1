/* Web Push worker. It is scoped to /Frontend/ and contains no account-specific data. */
self.addEventListener("push", (event) => {
  let payload = {};
  try { payload = event.data ? event.data.json() : {}; }
  catch { payload = { body: event.data?.text() || "Bạn có thông báo mới." }; }
  const title = payload.title || "Thông báo thực tập";
  const options = {
    body: payload.body || "Bạn có thông báo mới.",
    icon: "./assets/codegym-logo.webp",
    badge: "./assets/codegym-logo.webp",
    data: { url: payload.url ? (payload.url.includes("/") ? payload.url : `pages/${payload.url}`) : "pages/dashboard.html" },
    tag: "intern-management-notification",
    renotify: true,
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const destination = new URL(event.notification.data?.url || "./pages/dashboard.html", self.registration.scope).href;
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
    for (const client of windows) {
      if (client.url === destination && "focus" in client) return client.focus();
    }
    return self.clients.openWindow(destination);
  })());
});
