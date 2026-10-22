(() => {
  const message = document.getElementById('dashboardMessage');
  const form = document.getElementById('taskForm');
  const intern = document.getElementById('taskIntern');
  const content = document.getElementById('taskContent');
  const deadline = document.getElementById('taskDeadline');
  const save = document.getElementById('saveTask');
  const refresh = document.getElementById('refreshTasks');
  const progressForm = document.getElementById('progressForm');
  const progressTask = document.getElementById('progressTask');
  const progress = document.getElementById('taskProgress');
  const progressNote = document.getElementById('progressNote');
  const progressStatus = document.getElementById('progressStatus');
  function syncProgressStatus() {
    const value = Number(progress.value);
    progressStatus.value = progress.value === '' || !progress.validity.valid ? '—' : value === 100 ? 'Hoàn thành' : value === 0 ? 'Đã giao' : 'Đang thực hiện';
  }
  progress.addEventListener('input', syncProgressStatus);
  let loadedTasks = [];
  let session, saving = false, loading = false, version = 0;
  const dateFormatter = new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
  function formatDeadline(value) {
    const date = new Date(value);
    if (!Number.isFinite(date.getTime())) return '—';
    const parts = Object.fromEntries(dateFormatter.formatToParts(date).map(part => [part.type, part.value]));
    return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
  }
  function tableState(text) {
    const row = document.createElement('tr'), cell = document.createElement('td');
    cell.colSpan = 5; cell.className = 'task-state'; cell.textContent = text; row.append(cell);
    document.getElementById('taskRows').replaceChildren(row);
  }
  const has = permission => Session.hasPermission(session || {}, permission);
  const canRead = () => session?.user?.role === 'MENTOR' ? has('MENTOR_TASK_READ') : session?.user?.role === 'INTERN' && has('OWN_TASK_READ');
  const canCreate = () => session?.user?.role === 'MENTOR' && canRead() && has('TASK_CREATE');
  const canUpdate = () => session?.user?.role === 'INTERN' && canRead() && has('OWN_TASK_UPDATE');
  const api = (path = '', options = {}) => requestApi('/tasks' + path, { ...options, headers: getAuthHeader() });
  function permissions() {
    document.getElementById('createTaskSection').hidden = !canCreate();
    document.getElementById('taskListSection').hidden = !canRead();
    document.getElementById('updateProgressSection').hidden = !canUpdate();
    document.getElementById('saveProgress').disabled = saving || loading || !canUpdate() || !loadedTasks.length;
    save.disabled = saving || !canCreate() || intern.options.length <= 1;
    refresh.disabled = loading || saving;
    document.querySelectorAll('.task-update-action').forEach(button => {
      button.hidden = !canUpdate(); button.disabled = saving || loading || !canUpdate();
    });
    if (!canRead()) {
      version++;
      document.getElementById('taskRows').replaceChildren();
      AppUI.feedback(message, 'Bạn không có quyền xem nhiệm vụ.', 'error');
    }
  }
  document.addEventListener('session-permissions-updated', event => { session = event.detail; permissions(); });
  async function load() {
    if (loading) return;
    const currentVersion = ++version;
    if (!canRead()) return;
    loading = true; refresh.textContent = 'Đang tải…'; permissions();
    document.getElementById('taskRows').setAttribute('aria-busy', 'true');
    tableState('Đang tải nhiệm vụ…');
    try {
    const [tasks, interns] = await Promise.all([api(), canCreate() ? api('/interns') : Promise.resolve([])]);
    if (currentVersion !== version || !canRead()) return;
    intern.replaceChildren(new Option('Chọn thực tập sinh', ''));
    interns.forEach(item => intern.add(new Option(`${item.fullName} (${item.email})`, item.internId)));
    const rows = document.getElementById('taskRows'); rows.replaceChildren();
    loadedTasks = tasks;
    const selectedTask = progressTask.value;
    progressTask.replaceChildren(new Option('Chọn nhiệm vụ', ''));
    tasks.forEach(task => progressTask.add(new Option(task.content, task.id)));
    progressTask.value = selectedTask;
    tasks.forEach(task => {
      const row = document.createElement('tr');
      [task.internName, task.mentorName, task.content, formatDeadline(task.deadline),
        ({ ASSIGNED: 'Đã giao', IN_PROGRESS: 'Đang thực hiện', COMPLETED: 'Hoàn thành' })[task.status] || task.status].forEach((value, index) => {
        const cell = document.createElement('td');
        if (index === 0 || index === 1) {
          const person = document.createElement('span'); person.className = 'task-person';
          const avatar = document.createElement('span'); avatar.className = 'task-person-avatar';
          avatar.textContent = String(value || '?').trim().split(/\s+/).slice(-2).map(part => part[0]).join('').toUpperCase();
          const name = document.createElement('strong'); name.textContent = value;
          person.append(avatar, name); cell.append(person);
        } else if (index === 2 || index === 4) {
          const text = document.createElement('span'); text.textContent = value;
          text.className = index === 2 ? 'task-content-preview' : 'task-status';
          if (index === 4) text.classList.add('is-' + String(task.status || 'assigned').toLowerCase().replace('_', '-'));
          if (index === 2) text.title = value;
          cell.append(text);
          if (index === 4) {
            const percentage = document.createElement('small'); percentage.className = 'task-progress-label';
            percentage.textContent = `${task.progress ?? 0}%`; cell.append(percentage);
            const bar = document.createElement('progress'); bar.className = 'task-progress-bar';
            bar.max = 100; bar.value = task.progress ?? 0;
            bar.setAttribute('aria-label', 'Tiến độ: ' + task.content); cell.append(bar);
            if (session.user.role === 'INTERN') {
              const action = document.createElement('button'); action.type = 'button';
              action.className = 'btn-secondary task-update-action'; action.textContent = 'Cập nhật tiến độ';
              action.addEventListener('click', () => {
                if (!canUpdate() || saving || loading) return;
                progressTask.value = String(task.id); progressTask.dispatchEvent(new Event('change'));
                document.getElementById('updateProgressSection').scrollIntoView({ block: 'center' });
                progress.focus({ preventScroll: true });
              });
              cell.append(action);
            }
          }
          if (index === 2 && task.progressNote) {
            const note = document.createElement('small'); note.className = 'task-content-preview task-progress-label';
            note.textContent = 'Tiến độ: ' + task.progressNote; cell.append(note);
          }
        } else {
          cell.textContent = value;
          if (index === 3) {
            cell.className = 'task-deadline';
            const deadline = new Date(task.deadline);
            if (task.status !== 'COMPLETED' && !Number.isNaN(deadline.getTime()) && deadline < new Date()) {
              cell.classList.add('is-overdue'); cell.title = 'Nhiệm vụ đã quá hạn';
            }
          }
        }
        row.append(cell);
      });
      rows.append(row);
    });
    if (!tasks.length) {
      tableState('Chưa có nhiệm vụ.');
    }
    document.getElementById('taskListTitle').textContent = session.user.role === 'MENTOR' ? 'Nhiệm vụ đã giao' : 'Nhiệm vụ của tôi';
    permissions();
    if (canCreate() && !interns.length) AppUI.feedback(message, 'Bạn chưa được phân công hướng dẫn Intern nào.', 'info');
    else message.hidden = true;
    } catch (error) {
      if (currentVersion === version && canRead()) tableState('Không tải được nhiệm vụ. Nhấn Tải lại để thử lại.');
      throw error;
    } finally {
      loading = false; refresh.textContent = 'Tải lại';
      document.getElementById('taskRows').setAttribute('aria-busy', 'false'); permissions();
    }
  }
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (saving || !canCreate() || !form.reportValidity()) return;
    const due = new Date(deadline.value + ':00+07:00');
    if (!content.value.trim() || !Number.isFinite(due.getTime()) || due <= new Date()) {
      AppUI.feedback(message, 'Nhập nội dung và deadline ở tương lai.', 'error'); return;
    }
    saving = true; permissions();
    const controls = [...form.querySelectorAll('input,select,textarea')]; controls.forEach(control => control.disabled = true);
    let saved = false;
    try {
      if (!await AppUI.confirm('Giao nhiệm vụ cho ' + intern.selectedOptions[0].text + '?')) return;
      if (!canCreate()) return;
      await api('', { method: 'POST', body: JSON.stringify({ internId: Number(intern.value), content: content.value.trim(), deadline: due.toISOString() }) });
      saved = true; form.reset(); await load();
      AppUI.feedback(message, 'Đã giao nhiệm vụ.', 'success');
    } catch (error) {
      AppUI.feedback(message, saved ? 'Nhiệm vụ đã lưu. Hãy tải lại danh sách.' : error.message, saved ? 'warning' : 'error');
      Session.redirectIfExpired(error);
    } finally {
      saving = false; controls.forEach(control => control.disabled = false); permissions();
    }
  });
  progressTask.addEventListener('change', () => {
    const task = loadedTasks.find(item => String(item.id) === progressTask.value);
    progress.value = task?.progress ?? 0;
    progressNote.value = task?.progressNote ?? '';
    syncProgressStatus();
  });
  progressForm.addEventListener('submit', async event => {
    event.preventDefault();
    if (saving || loading || !canUpdate() || !progressForm.reportValidity()) return;
    saving = true; permissions();
    let persisted = false;
    const fields = [...progressForm.querySelectorAll('input,select,textarea')];
    fields.forEach(field => field.disabled = true);
    try {
      await api('/' + progressTask.value + '/progress', { method: 'PATCH', body: JSON.stringify({ progress: Number(progress.value), progressNote: progressNote.value.trim() }) });
      persisted = true; await load();
      AppUI.feedback(message, 'Đã cập nhật tiến độ.', 'success');
    } catch (error) {
      AppUI.feedback(message, persisted ? 'Tiến độ đã lưu. Hãy tải lại danh sách.' : error.message, persisted ? 'warning' : 'error');
      Session.redirectIfExpired(error);
    } finally { saving = false; fields.forEach(field => field.disabled = false); permissions(); }
  });
  document.getElementById('refreshTasks').addEventListener('click', () => load().catch(error => { AppUI.feedback(message, error.message, 'error'); Session.redirectIfExpired(error); }));
  (async () => {
    if (!localStorage.getItem('token')) return;
    try { session = await API.getCurrentUser(); permissions(); await load(); }
    catch (error) { AppUI.feedback(message, error.message, 'error'); Session.redirectIfExpired(error); }
  })();
})();
