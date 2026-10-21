window.AppUI = (() => {
  let confirmationOpen = false;
  function confirm(message, title = "Xác nhận thay đổi", accept = "Xác nhận", cancelLabel = "Hủy") {
    if (confirmationOpen) return Promise.resolve(false);
    confirmationOpen = true;
    const previousFocus = document.activeElement;
    const dialog = document.createElement("dialog"); dialog.className = "shared-confirm";
    dialog.setAttribute("aria-labelledby", "confirmationTitle");
    dialog.setAttribute("aria-describedby", "confirmationDescription");
    dialog.innerHTML = '<form method="dialog"><h2 id="confirmationTitle"></h2><p id="confirmationDescription"></p><div class="form-actions"><button class="btn-secondary" value="cancel">Hủy</button><button class="btn-primary" value="confirm">Xác nhận</button></div></form>';
    dialog.querySelector("h2").textContent = title;
    dialog.querySelector("p").textContent = message;
    dialog.querySelector('[value="confirm"]').textContent = accept;
    dialog.querySelector('[value="cancel"]').textContent = cancelLabel;
    document.body.append(dialog);
    return new Promise(resolve => {
      dialog.addEventListener("close", () => {
        const accepted = dialog.returnValue === "confirm";
        dialog.remove(); confirmationOpen = false; previousFocus?.focus(); resolve(accepted);
      }, { once: true });
      dialog.showModal(); dialog.querySelector('[value="cancel"]').focus();
    });
  }
  const toastTimers = new Map();
  const toastDialogs = new WeakSet();
  function toast(message, type = "success", key = "") {
    if (!message) return;
    if (!["success", "error", "warning", "info"].includes(type)) type = "info";
    let region = document.getElementById("toastRegion");
    if (!region) {
      region = document.createElement("div"); region.id = "toastRegion"; region.className = "toast-region";
      if (typeof region.showPopover === "function") region.setAttribute("popover", "manual");
      document.body.append(region);

    }
    function remove(item, animate = false) {
      clearTimeout(toastTimers.get(item)); toastTimers.delete(item);
      if (!animate) { item.remove(); return; }
      item.classList.add("is-leaving");
      toastTimers.set(item, setTimeout(() => { item.remove(); toastTimers.delete(item); }, 160));
    }
    [...region.children].filter(item => (key && item.dataset.feedbackKey === key) || item.querySelector(".ui-toast-message")?.textContent === String(message)).forEach(item => remove(item));
    const toast = document.createElement("div"); toast.className = `ui-toast is-${type}`;
    toast.setAttribute("role", type === "error" ? "alert" : "status");
    toast.setAttribute("aria-live", type === "error" ? "assertive" : "polite");
    toast.setAttribute("aria-atomic", "true");
    toast.dataset.feedbackKey = key;
    const icon = document.createElement("span"); icon.className = "ui-toast-icon"; icon.setAttribute("aria-hidden", "true");
    icon.textContent = {success: "✓", error: "!", warning: "!", info: "i"}[type];
    const content = document.createElement("span"); content.className = "ui-toast-text";
    const title = document.createElement("strong"); title.className = "ui-toast-title";
    title.textContent = {success: "Thành công", error: "Thất bại", warning: "Cảnh báo", info: "Thông tin"}[type];
    const text = document.createElement("span"); text.textContent = message; text.className = "ui-toast-message";
    content.append(title,text);
    const close = document.createElement("button"); close.type = "button"; close.textContent = "×"; close.setAttribute("aria-label", "Đóng thông báo");
    close.onclick = () => remove(toast, true); toast.append(icon, content, close); region.prepend(toast);
    while (region.children.length > 3) remove(region.lastElementChild);
    // Promote above native dialogs without changing their placement or behavior.
    if (region.showPopover && region.matches(":popover-open")) region.hidePopover();
    const activeDialog = [...document.querySelectorAll("dialog[open]")].at(-1);
    // A sibling of a modal is inert even in the top layer. Keep the close button usable.
    (activeDialog || document.body).append(region);
    if (activeDialog && !toastDialogs.has(activeDialog)) {
      toastDialogs.add(activeDialog);
      activeDialog.addEventListener("close", () => {
        if (region.parentElement !== activeDialog) return;
        if (region.showPopover && region.matches(":popover-open")) region.hidePopover();
        document.body.append(region);
        if (region.showPopover) region.showPopover();
      });
    }
    if (region.showPopover) region.showPopover();
    toastTimers.set(toast, setTimeout(() => remove(toast, true), type === "error" ? 5000 : 4000));
    return toast;
  }
  function loading(container, columns = 1) {
    if (!container) return () => {};
    container.setAttribute("aria-busy", "true"); container.replaceChildren();
    for (let i = 0; i < 3; i++) {
      const row = container.tagName === "TBODY" ? container.insertRow() : container.appendChild(document.createElement("div"));
      const cell = container.tagName === "TBODY" ? row.insertCell() : row;
      if (container.tagName === "TBODY") cell.colSpan = columns;
      const bar = document.createElement("span"); bar.className = "ui-skeleton"; bar.setAttribute("aria-label", "Đang tải dữ liệu"); cell.append(bar);
    }
    return () => container.setAttribute("aria-busy", "false");
  }
  function feedback(element, message, type = "success") {
    if (typeof element === "string") element = document.getElementById(element);
    const key = element?.id || "";
    if (element) { element.textContent = ""; element.hidden = true; }
    return toast(message, type, key);
  }
  function flash(message, type = "success") {
    sessionStorage.setItem("app-toast-flash", JSON.stringify({ message, type }));
  }

  const obsoleteHelperSelector = [
    ".form-hint",
    ".meeting-optional",
    ".meeting-action-note",
    ".meeting-guide",
    ".form-helper > span:not(.char-counter)"
  ].join(", ");

  function findControlLabel(control) {
    const wrappingLabel = control.closest("label");
    if (wrappingLabel || !control.id) return wrappingLabel;
    return document.querySelector(`label[for="${CSS.escape(control.id)}"]`);
  }

  function syncRequiredFieldMarkers(root = document) {
    const controls = root.querySelectorAll?.("input, select, textarea") || [];

    for (const control of controls) {
      const label = findControlLabel(control);
      if (!label) continue;

      const existingMarker = label.querySelector(":scope > .required-marker, .file-picker-title > .required-marker");
      if (!control.required) {
        existingMarker?.remove();
        continue;
      }
      if (existingMarker || label.textContent.trim().endsWith("*")) continue;

      const marker = document.createElement("span");
      marker.className = "required-marker";
      marker.textContent = " *";
      marker.setAttribute("aria-hidden", "true");

      const fileTitle = label.querySelector(".file-picker-title");
      if (fileTitle) {
        fileTitle.append(marker);
      } else if (label.contains(control)) {
        let directChild = control;
        while (directChild.parentElement !== label) directChild = directChild.parentElement;
        label.insertBefore(marker, directChild);
      } else {
        label.append(marker);
      }
    }
  }

  function initializeFormLabels() {
    document.querySelectorAll(obsoleteHelperSelector).forEach(element => element.remove());
    document.querySelectorAll("summary, label").forEach(element => {
      for (const node of element.childNodes) {
        if (node.nodeType === Node.TEXT_NODE) {
          node.textContent = node.textContent.replace(/\s*\(không bắt buộc\)/gi, "");
        }
      }
    });

    syncRequiredFieldMarkers();
    new MutationObserver(mutations => {
      for (const mutation of mutations) {
        if (mutation.type === "attributes") {
          syncRequiredFieldMarkers(mutation.target.parentElement || document);
        }
        for (const node of mutation.addedNodes) {
          if (node.nodeType !== Node.ELEMENT_NODE) continue;
          const root = node.matches("input, select, textarea") ? node.parentElement : node;
          syncRequiredFieldMarkers(root);
        }
      }
    }).observe(document.body, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ["required"]
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initializeFormLabels);
  } else {
    initializeFormLabels();
  }

  const pending = sessionStorage.getItem("app-toast-flash");
  sessionStorage.removeItem("app-toast-flash");
  if (pending) { try { const item = JSON.parse(pending); setTimeout(() => toast(item.message, item.type), 0); } catch {} }
  return { confirm, toast, feedback, flash, loading };
})();
