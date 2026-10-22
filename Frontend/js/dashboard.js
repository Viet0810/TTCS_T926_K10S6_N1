async function initializeDashboard() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  const welcome = document.getElementById("welcomeTitle");
  const stopLoading = window.AppUI?.loading(welcome);
  try {
    const session = await API.getCurrentUser();
    const user = session.user;
    document.getElementById("roleBadge").textContent = user.role;
    if (welcome) welcome.textContent = `Xin chào, ${user.fullName}`;
    await loadDashboardMetrics(session);
  } catch (error) {
    if (welcome) welcome.textContent = "Chưa thể tải lời chào.";
    const message = document.getElementById("dashboardMessage") || document.getElementById("programDateMessage");
    if (message) {
      message.textContent = error.message;
      message.hidden = false;
    }
    Session.redirectIfExpired(error);
  }
  finally { stopLoading?.(); }
}

async function loadDashboardMetrics(session) {
  const target = document.getElementById('dashboardKpis');
  if (!target) return;
  const has = code => Session.hasPermission(session, code);
  const stopLoading = window.AppUI?.loading(target);
  let firstResult = true;
  const add = (label, value, icon, tone = '', role = '') => {
    if (firstResult) { target.replaceChildren(); firstResult = false; }
    const card = document.createElement('article'); card.className = 'kpi-card'; card.dataset.tone = tone;
    const badge = document.createElement('span'); badge.className = 'kpi-icon';
    if (role) { badge.style.color = `var(--role-${role.toLowerCase()})`; }
    if (window.AppVisual) badge.append(AppVisual.icon(icon));
    const count = document.createElement('strong'); count.textContent = String(value);
    const title = document.createElement('p'); title.textContent = label;
    card.append(badge, count, title); target.append(card);
  };
  // Each group is requested only with the same permission enforced by its API.
  const groups = [];
  if (session.user.role === 'ADMIN' && has('MANAGE_USERS')) groups.push(async () => {
    const users = await API.getUsers();
    add('Tổng tài khoản', users.length, 'users');
    for (const role of ['HR', 'MENTOR', 'INTERN']) add(role, users.filter(user => user.role === role).length, AppVisual.roleIcons[role], '', role);
  });
  if (session.user.role === 'HR') {
    if (has('VIEW_INTERNS')) groups.push(async () => { const interns = await API.getInterns(); add('Tổng thực tập sinh', interns.length, 'profile'); });
    if (has('DOCUMENT_REVIEW_READ')) groups.push(async () => {
      const documents = await API.getDocumentReviews();
      for (const [status, label] of [['pending', 'Tài liệu chờ duyệt'], ['approved', 'Tài liệu đã duyệt'], ['rejected', 'Tài liệu bị từ chối']]) add(label, documents.filter(document => document.status === status).length, 'review', status);
    });
  }
  if (session.user.role === 'INTERN') {
    if (has('VIEW_OWN_PROFILE')) groups.push(async () => { const profile = await API.getMyInternProfile(); add('Hồ sơ cá nhân', profile.fullName || session.user.fullName, 'profile'); });
    if (has('VIEW_DOCUMENTS')) groups.push(async () => {
      const documents = await API.getMyDocuments();
      add('Tài liệu đã nộp', documents.length, 'file');
      add('Tài liệu chờ duyệt', documents.filter(document => document.status === 'pending').length, 'review', 'pending');
      add('Tài liệu đã duyệt', documents.filter(document => document.status === 'approved').length, 'review', 'approved');
    });
  }
  const results = await Promise.allSettled(groups.map(group => group()));
  if (firstResult) target.replaceChildren();
  stopLoading?.();
  const failed = results.filter(result => result.status === 'rejected');
  if (failed.length) {
    const message = document.getElementById('dashboardMessage');
    message.textContent = 'Một số dữ liệu tổng quan chưa tải được. Vui lòng tải lại trang.'; message.hidden = false;
    failed.forEach(result => Session.redirectIfExpired(result.reason));
  }
  if (!target.children.length && !failed.length) {
    const empty = document.createElement('section'); empty.className = 'card';
    if (window.AppVisual) empty.append(AppVisual.illustration('dashboard'));
    const text = document.createElement('p'); text.textContent = session.user.role === 'MENTOR' ? 'Sử dụng các chức năng trong menu để theo dõi hồ sơ thực tập sinh.' : 'Chưa có dữ liệu tổng quan khả dụng cho quyền hiện tại.';
    empty.append(text); target.append(empty);
  }
}

initializeDashboard();
