window.TableUI = (() => {
  const states = new WeakMap();

  function controls(body, change) {
    if (states.has(body)) return states.get(body);
    const island = body.closest(".table-container") || body.closest("table").parentElement;
    island.classList.add("table-island");
    body.closest("table").classList.add("list-table");
    const bar = document.createElement("div");
    bar.className = "table-pagination";
    const summary = document.createElement("span");
    summary.className = "pagination-summary";
    summary.setAttribute("aria-live", "polite");
    const nav = document.createElement("nav");
    nav.setAttribute("aria-label", "Phân trang danh sách");
    bar.append(nav);
    island.after(bar);
    const state = { page: 1, pageSize: 10, total: 0, bar, summary, nav, change };
    states.set(body, state);
    return state;
  }

  function draw(state) {
    const pages = Math.ceil(state.total / state.pageSize);
    state.page = Math.max(1, Math.min(state.page, Math.max(1, pages)));
    state.summary.textContent = `Trang ${state.page}/${Math.max(1, pages)}`;
    state.nav.replaceChildren();
    function button(label, page, disabled = false) {
      const element = document.createElement("button");
      element.type = "button";
      element.textContent = label;
      element.disabled = disabled || state.busy;
      element.setAttribute("aria-label", label === "‹" ? "Trang trước" : label === "›" ? "Trang sau" : `Trang ${page}`);
      element.onclick = () => { if (page !== state.page) { state.page = page; state.change(page); } };
      state.nav.append(element);
    }
    button("‹", state.page - 1, state.page <= 1);
    state.nav.append(state.summary);
    button("›", state.page + 1, !pages || state.page >= pages);
    state.bar.hidden = !state.total;
  }

  function decorate(body) {
    for (const row of body.rows) for (const cell of row.cells) {
      if (cell.colSpan > 1 || cell.querySelector("button,a,input")) continue;
      cell.title = cell.textContent.trim();
      if (!cell.children.length) {
        const text = document.createElement("span");
        text.className = "table-cell-text";
        text.textContent = cell.textContent;
        cell.replaceChildren(text);
      }
    }
  }

  function client(body) {
    const state = controls(body, () => apply());
    function apply() {
      const rows = state.rows || [];
      state.total = rows.length;
      draw(state);
      rows.forEach((row, index) => row.hidden = index < (state.page - 1) * state.pageSize || index >= state.page * state.pageSize);
      const empty = body.querySelector(".pagination-empty");
      if (rows.length) empty?.remove();
      else if (state.all?.length && !empty) {
        const row = body.insertRow();
        row.className = "pagination-empty";
        const cell = row.insertCell();
        cell.colSpan = body.closest("table").tHead.rows[0].cells.length;
        cell.className = "empty-state";
        cell.textContent = "Không có dữ liệu phù hợp.";
      }
    }
    function resetRows() {
      state.rows = state.all.filter(row => !state.query || row.textContent.toLocaleLowerCase("vi").includes(state.query));
      state.all.forEach(row => row.hidden = true);
      state.page = 1;
      apply();
    }
    function refresh() {
      decorate(body);
      state.all = [...body.rows].filter(row => row.cells.length > 1 && !row.querySelector(".ui-skeleton"));
      resetRows();
    }
    const observer = new MutationObserver(() => { observer.disconnect(); refresh(); observer.observe(body, { childList: true }); });
    refresh();
    observer.observe(body, { childList: true });
    return { filter(text) { state.query = text.trim().toLocaleLowerCase("vi"); resetRows(); } };
  }

  function server(body, request, render) {
    body.dataset.serverPaging = "true";
    let revision = 0;
    const state = controls(body, () => load());
    async function load(reset = false) {
      if (reset) state.page = 1;
      const current = ++revision;
      state.busy = true;
      draw(state);
      const done = AppUI.loading(body, body.closest("table").tHead.rows[0].cells.length);
      try {
        const response = await request(state.page, state.pageSize);
        if (current !== revision) return;
        const data = Array.isArray(response) ? { items: response, page: 1, totalItems: response.length } : response;
        state.page = data.page;
        state.total = data.totalItems;
        render(data.items);
        decorate(body);
      } catch (error) {
        if (current !== revision) return;
        state.total = 0;
        body.replaceChildren();
        const cell = body.insertRow().insertCell();
        cell.colSpan = body.closest("table").tHead.rows[0].cells.length;
        cell.className = "empty-state";
        cell.textContent = "Không thể tải danh sách. Vui lòng thử lại.";
        throw error;
      } finally {
        if (current === revision) { state.busy = false; done(); draw(state); }
      }
    }
    state.change = () => load().catch(error => AppUI.toast(error.message, "error"));
    return { load, state };
  }

  const clients = new WeakMap();
  document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".table-container tbody,.program-table-wrapper tbody").forEach(body => {
      if (!body.dataset.serverPaging) {
        const instance = client(body);
        clients.set(body, instance);
      }
    });
  });
  return { server, filter(body, text) { clients.get(body)?.filter(text); } };
})();
