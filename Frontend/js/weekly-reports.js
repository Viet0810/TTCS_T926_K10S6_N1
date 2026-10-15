(() => {
  const message = document.getElementById('reportsMessage');
  const form = document.getElementById('reportForm');
  const list = document.getElementById('reportList');
  const reload = document.getElementById('reloadReports');
  const internFilter = document.getElementById('reportInternFilter');
  internFilter.addEventListener('change', render);
  let session, reports = [], busy = false, loading = false, version = 0;
  const has = code => Session.hasPermission(session || {}, code);
  const mentor = () => session?.user?.role === 'MENTOR';
  const canRead = () => mentor() ? has('MENTOR_WEEKLY_REPORT_READ') : session?.user?.role === 'INTERN' && has('OWN_WEEKLY_REPORT_READ');
  const canCreate = () => canRead() && session.user.role === 'INTERN' && has('WEEKLY_REPORT_CREATE');
  const canFeedback = () => canRead() && mentor() && has('WEEKLY_REPORT_FEEDBACK');
  const api = (path = '', options = {}) => requestApi('/weekly-reports' + path, { ...options, headers: getAuthHeader() });
  const date = value => new Date(value.length === 10 ? value + 'T00:00:00' : value).toLocaleDateString('vi-VN');
  function element(tag, text, className) {
    const node = document.createElement(tag); node.textContent = text;
    if (className) node.className = className;
    return node;
  }
  function state(text) { list.replaceChildren(element('p', text, 'report-state')); }
  function permissions() {
    document.getElementById('createReportSection').hidden = !canCreate();
    document.getElementById('reportListSection').hidden = !canRead();
    document.getElementById('submitReport').disabled = busy || loading || !canCreate();
    reload.disabled = busy || loading;
    document.getElementById('reportInternFilterGroup').hidden = !mentor() || !canRead();
    internFilter.disabled = busy || loading;
    list.querySelectorAll('.feedback-form').forEach(item => { item.hidden = !canFeedback(); });
    list.querySelectorAll('button,textarea').forEach(item => { item.disabled = busy || loading || !canFeedback(); });
    if (!canRead()) { version++; reports = []; list.replaceChildren(); }
  }
  function render() {
    list.replaceChildren();
    if (!reports.length) { state('Chưa có báo cáo tuần.'); return; }
    const visible = reports.filter(report => !mentor() || !internFilter.value || String(report.internId) === internFilter.value);
    if (!visible.length) { state('Intern này chưa có báo cáo.'); return; }
    visible.forEach(report => {
      const article = element('article', '', 'weekly-report'); article.dataset.reportId = report.id;
      article.append(element('h3', `${report.internName} · ${date(report.weekStartDate)} – ${date(report.weekEndDate)}`));
      article.append(element('p', `${report.programName || 'Chưa có chương trình'} · Gửi ngày ${date(report.createdAt)}`, 'report-meta'));
      article.append(element('span', report.feedback.length ? 'Đã phản hồi' : 'Chưa phản hồi',
        'report-feedback-status ' + (report.feedback.length ? 'is-reviewed' : 'is-pending')));
      article.append(element('p', report.content, 'report-text'));
      article.append(element('h4', 'Phản hồi từ Mentor'));
      if (!report.feedback.length) article.append(element('p', 'Chưa có phản hồi.', 'report-meta'));
      report.feedback.forEach(feedback => {
        const box = element('div', '', 'report-feedback');
        box.append(element('strong', feedback.mentorName + ' · ' + date(feedback.createdAt)), element('p', feedback.content, 'report-text'));
        article.append(box);
      });
      if (canFeedback()) {
        const feedbackForm = element('form', '', 'feedback-form');
        const fieldId = 'feedback-' + report.id;
        const label = element('label', 'Nội dung phản hồi'); label.htmlFor = fieldId;
        const input = document.createElement('textarea'); input.id = fieldId; input.required = true; input.maxLength = 2000; input.rows = 3;
        const button = element('button', 'Gửi phản hồi', 'btn-primary'); button.type = 'submit';
        feedbackForm.append(label, input, button);
        feedbackForm.addEventListener('submit', event => {
          event.preventDefault();
          if (!canFeedback() || !feedbackForm.reportValidity()) return;
          mutate('/' + report.id + '/feedback', { content: input.value.trim() }, 'Đã gửi phản hồi.');
        });
        article.append(feedbackForm);
      }
      list.append(article);
    });
    permissions();
  }
  async function load() {
    if (loading || !canRead()) return;
    loading = true; const current = ++version; permissions(); list.setAttribute('aria-busy', 'true'); state('Đang tải báo cáo...');
    try {
      const result = await api();
      if (current !== version || !canRead()) return;
      reports = result;
      const selected = internFilter.value;
      internFilter.replaceChildren(new Option('Tất cả Intern', ''));
      const interns = new Map(reports.map(report => [String(report.internId), report.internName]));
      interns.forEach((name, id) => internFilter.add(new Option(name, id)));
      internFilter.value = interns.has(selected) ? selected : '';
      render(); message.hidden = true;
    } catch (error) { if (current === version && canRead()) state('Không tải được báo cáo. Nhấn Tải lại để thử lại.'); throw error; }
    finally { loading = false; list.setAttribute('aria-busy', 'false'); permissions(); }
  }
  async function mutate(path, body, success) {
    if (busy || loading || !canRead()) return;
    if (!body.content) { AppUI.feedback(message, 'Nội dung không được để trống.', 'error'); return; }
    busy = true; permissions(); let persisted = false;
    [...form.elements].forEach(field => field.disabled = true);
    try {
      await api(path, { method: 'POST', body: JSON.stringify(body) });
      persisted = true; if (!path) form.reset(); await load();
      AppUI.feedback(message, success, 'success');
    } catch (error) {
      AppUI.feedback(message, persisted ? 'Dữ liệu đã lưu. Hãy tải lại danh sách.' : error.message, persisted ? 'warning' : 'error');
      Session.redirectIfExpired(error);
    } finally { busy = false; [...form.elements].forEach(field => field.disabled = false); permissions(); }
  }
  form.addEventListener('submit', event => {
    event.preventDefault();
    if (!canCreate() || !form.reportValidity()) return;
    const sections = [['Công việc đã làm', 'reportContent'], ['Kết quả', 'reportResults'], ['Khó khăn', 'reportDifficulties'], ['Kế hoạch tiếp theo', 'reportNextPlan']];
    if (sections.some(([, id]) => !document.getElementById(id).value.trim())) {
      AppUI.feedback(message, 'Điền đầy đủ các mục báo cáo (có thể ghi Không có).', 'error'); return;
    }
    const reportContent = sections.map(([label, id]) => label + ':\n' + document.getElementById(id).value.trim()).join('\n\n');
    if (reportContent.length > 4000) { AppUI.feedback(message, 'Tổng nội dung báo cáo tối đa 4000 ký tự.', 'error'); return; }
    mutate('', { weekStartDate: document.getElementById('reportWeek').value, content: reportContent, workDone: document.getElementById("reportContent").value.trim(), results: document.getElementById("reportResults").value.trim(), difficulties: document.getElementById("reportDifficulties").value.trim(), nextPlan: document.getElementById("reportNextPlan").value.trim() }, 'Đã gửi báo cáo tuần.');
  });
  reload.addEventListener('click', () => load().catch(error => { AppUI.feedback(message, error.message, 'error'); Session.redirectIfExpired(error); }));
  document.addEventListener('session-permissions-updated', event => { session = event.detail; permissions(); });
  (async () => {
    if (!localStorage.getItem('token')) return;
    try {
      session = await API.getCurrentUser(); permissions();
      if (canRead()) await load(); else AppUI.feedback(message, 'Bạn không có quyền xem báo cáo tuần.', 'error');
    } catch (error) { AppUI.feedback(message, error.message, 'error'); Session.redirectIfExpired(error); }
  })();
})();
