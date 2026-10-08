(() => {
  const shell = document.getElementById("adminShell");
  const sidebarToggle = document.getElementById("sidebarToggle");
  const mobileMenuButton = document.getElementById("mobileMenuButton");
  const backdrop = document.getElementById("sidebarBackdrop");
  const navItems = [...document.querySelectorAll("[data-admin-panel]")];
  const panels = [...document.querySelectorAll("[data-admin-panel-view]")];
  const title = document.getElementById("pageTitle");
  const description = document.getElementById("pageDescription");
  const breadcrumb = document.getElementById("breadcrumbTitle");
  const mobileQuery = window.matchMedia("(max-width: 820px)");
  const excelModal = document.getElementById("excelImportModal");
  const excelOpenButton = document.getElementById("openExcelImport");
  const excelCloseButton = document.getElementById("closeExcelImport");
  let previousFocus = null;

  function setCollapsed(collapsed) {
    shell.classList.toggle("sidebar-collapsed", collapsed);
    sidebarToggle.setAttribute("aria-expanded", String(!collapsed));
    sidebarToggle.setAttribute("aria-label", collapsed ? "Mở rộng thanh công cụ" : "Thu gọn thanh công cụ");
    sidebarToggle.title = collapsed ? "Mở rộng thanh công cụ" : "Thu gọn thanh công cụ";
    try { localStorage.setItem("adminSidebarCollapsed", String(collapsed)); } catch {}
  }

  function closeMobileSidebar() {
    shell.classList.remove("sidebar-open");
    mobileMenuButton.setAttribute("aria-expanded", "false");
  }

  function openMobileSidebar() {
    shell.classList.add("sidebar-open");
    mobileMenuButton.setAttribute("aria-expanded", "true");
  }

  function showPanel(name, button) {
    panels.forEach((panel) => { panel.hidden = panel.dataset.adminPanelView !== name; });
    navItems.forEach((item) => {
      const active = item === button;
      item.classList.toggle("is-active", active);
      if (active) item.setAttribute("aria-current", "page");
      else item.removeAttribute("aria-current");
    });
    const heading = button.dataset.title || "Quản trị hệ thống";
    const subheading = button.dataset.description || "";
    title.textContent = heading;
    breadcrumb.textContent = heading;
    description.textContent = subheading;
    document.title = heading + " | InternFlow Admin";
    if (mobileQuery.matches) closeMobileSidebar();
  }

  let savedCollapsed = false;
  try { savedCollapsed = localStorage.getItem("adminSidebarCollapsed") === "true"; } catch {}
  setCollapsed(savedCollapsed);

  navItems.forEach((button) => button.addEventListener("click", () => showPanel(button.dataset.adminPanel, button)));
  sidebarToggle.addEventListener("click", () => setCollapsed(!shell.classList.contains("sidebar-collapsed")));
  mobileMenuButton.addEventListener("click", () => shell.classList.contains("sidebar-open") ? closeMobileSidebar() : openMobileSidebar());
  backdrop.addEventListener("click", closeMobileSidebar);

  function closeExcelModal() {
    excelModal.hidden = true;
    if (previousFocus) previousFocus.focus();
  }
  excelOpenButton.addEventListener("click", () => {
    previousFocus = document.activeElement;
    excelModal.hidden = false;
    document.getElementById("internExcelFile").focus();
  });
  excelCloseButton.addEventListener("click", closeExcelModal);
  excelModal.querySelectorAll("[data-close-excel-modal]").forEach((button) => button.addEventListener("click", closeExcelModal));
  window.addEventListener("keydown", (event) => {
    if (event.key === "Escape") { closeMobileSidebar(); if (!excelModal.hidden) closeExcelModal(); }
  });
  mobileQuery.addEventListener("change", () => closeMobileSidebar());
})();
