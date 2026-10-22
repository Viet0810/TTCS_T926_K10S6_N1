(() => {
  const $ = id => document.getElementById(id);
  const api = (path = '', options = {}) => requestApi('/work-schedules' + path, { ...options, headers: getAuthHeader() });
  const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const labels = { WORK: 'Làm việc', MEETING: 'Họp', TRAINING: 'Đào tạo', OTHER: 'Khác', ACTIVE: 'Hoạt động', PAUSED: 'Tạm dừng', CANCELLED: 'Đã hủy', NONE: 'Không lặp', WEEKLY: 'Hằng tuần', CUSTOM: 'Tùy chỉnh' };
  const dayNames = ['Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7', 'Chủ nhật'];
  const dayStart = value => { const result = new Date(value); result.setHours(0, 0, 0, 0); return result; };
  const addDays = (value, count) => { const result = new Date(value); result.setDate(result.getDate() + count); return result; };
  const monday = value => addDays(dayStart(value), -((value.getDay() + 6) % 7));
  const iso = value => `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, '0')}-${String(value.getDate()).padStart(2, '0')}`;
  const parse = value => new Date(value + 'T00:00:00');
  const date = value => parse(value).toLocaleDateString('vi-VN');
  const time = value => value.slice(0, 5);
  const monthRange = value => ({ from: new Date(value.getFullYear(), value.getMonth(), 1), to: new Date(value.getFullYear(), value.getMonth() + 1, 0) });
  let session, rows = [], editing = null, view = 'month', cursor = dayStart(new Date()), page = 1;
  const pageSize = 10;
  const manager = () => ['HR', 'ADMIN'].includes(session?.user?.role);

  function currentRange() {
    if (view === 'month') return monthRange(cursor);
    if (view === 'week') { const from = monday(cursor); return { from, to: addDays(from, 6) }; }
    if (view === 'day') return { from: cursor, to: cursor };
    return { from: parse($('filterFrom').value), to: parse($('filterTo').value) };
  }
  function query() {
    const period = currentRange();
    return new URLSearchParams(Object.fromEntries(Object.entries({ from: iso(period.from), to: iso(period.to), mentorId: $('filterMentor').value, internId: $('filterIntern').value, type: $('filterType').value, status: $('filterStatus').value }).filter(([, value]) => value)));
  }
  function installNavigation() {
    const navigation = document.createElement('div');
    navigation.className = 'schedule-period';
    navigation.innerHTML = '<div class="period-actions"><button id="previousPeriod" type="button" class="btn-secondary">‹</button><button id="todayPeriod" type="button" class="btn-secondary">Hôm nay</button><button id="nextPeriod" type="button" class="btn-secondary">›</button></div><h2 id="periodTitle"></h2>';
    $('filters').before(navigation);
    const pager = document.createElement('div');
    pager.id = 'schedulePager'; pager.className = 'schedule-pager'; pager.style.justifyContent = 'flex-end';
    pager.innerHTML = '<div><button id="previousPage" type="button" class="btn-secondary">‹</button><span id="pageNumbers"></span><button id="nextPage" type="button" class="btn-secondary">›</button></div>';
    $('calendar').after(pager);
    $('previousPeriod').onclick = () => move(-1); $('nextPeriod').onclick = () => move(1);
    $('todayPeriod').onclick = () => { cursor = dayStart(new Date()); load(); };
    $('previousPage').onclick = () => { if (page > 1) { page--; render(); } };
    $('nextPage').onclick = () => { if (page < Math.ceil(rows.length / pageSize)) { page++; render(); } };
  }
  function move(direction) {
    if (view === 'month') cursor = new Date(cursor.getFullYear(), cursor.getMonth() + direction, 1);
    else if (view === 'week') cursor = addDays(cursor, direction * 7);
    else if (view === 'day') cursor = addDays(cursor, direction);
    page = 1; load();
  }
  function updateHeading() {
    const period = currentRange();
    $('periodTitle').textContent = view === 'month' ? `Tháng ${cursor.getMonth() + 1}/${cursor.getFullYear()}` : view === 'week' ? `${date(iso(period.from))} – ${date(iso(period.to))}` : view === 'day' ? cursor.toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' }) : 'Danh sách lịch làm việc';
    $('todayPeriod').textContent = view === 'month' ? 'Tháng này' : view === 'week' ? 'Tuần này' : 'Hôm nay';
    ['previousPeriod', 'nextPeriod', 'todayPeriod'].forEach(id => $(id).hidden = view === 'list');
    $('filterFrom').style.display = $('filterTo').style.display = view === 'list' ? '' : 'none';
  }
  async function load() {
    try { updateHeading(); rows = await api('?' + query()); page = 1; render(); }
    catch (error) { AppUI.feedback($('scheduleMessage'), error.message, 'error'); }
  }
  const eventHtml = (item, index, compact = false) => `<article class="schedule-event type-${item.scheduleType}" data-index="${index}"><div><b>${compact ? time(item.startTime) : date(item.date)}</b><small>${time(item.startTime)}–${time(item.endTime)}</small></div><div><strong>${esc(item.title)}</strong><small>${esc(item.mentorName)} · ${labels[item.scheduleType]} · ${labels[item.status]}</small></div><button class="btn-secondary">Chi tiết</button></article>`;
  function bind(box) { box.querySelectorAll('.schedule-event').forEach(element => element.onclick = () => detail(rows[Number(element.dataset.index)])); }
  function renderMonth(box) {
    const first = monday(new Date(cursor.getFullYear(), cursor.getMonth(), 1));
    box.innerHTML = `<div class="calendar-weekdays">${dayNames.map(x => `<b>${x}</b>`).join('')}</div><div class="month-grid">${Array.from({ length: 42 }, (_, offset) => { const day = addDays(first, offset), key = iso(day); const events = rows.map((item, index) => ({ item, index })).filter(x => x.item.date === key); return `<section class="calendar-day${day.getMonth() !== cursor.getMonth() ? ' outside' : ''}${key === iso(new Date()) ? ' today' : ''}"><header>${day.getDate()}</header>${events.map(x => eventHtml(x.item, x.index, true)).join('')}</section>`; }).join('')}</div>`;
  }
  function renderWeek(box) {
    const first = monday(cursor);
    box.innerHTML = `<div class="week-grid">${Array.from({ length: 7 }, (_, offset) => { const day = addDays(first, offset), key = iso(day); const events = rows.map((item, index) => ({ item, index })).filter(x => x.item.date === key); return `<section class="week-column"><header><b>${dayNames[offset]}</b><span>${date(key)}</span></header>${events.length ? events.map(x => eventHtml(x.item, x.index, true)).join('') : '<p class="empty-day">Không có lịch</p>'}</section>`; }).join('')}</div>`;
  }
  function renderList(box) {
    const pages = Math.max(1, Math.ceil(rows.length / pageSize)); page = Math.min(page, pages); const start = (page - 1) * pageSize, items = rows.slice(start, start + pageSize);
    box.innerHTML = items.length ? items.map((item, offset) => eventHtml(item, start + offset)).join('') : '<p class="empty-state">Không có lịch trong khoảng đã chọn.</p>';
    $('schedulePager').hidden = !rows.length; $('pageNumbers').textContent = `Trang ${page}/${pages}`; $('previousPage').disabled = page === 1; $('nextPage').disabled = page === pages;
  }
  function render() {
    const box = $('calendar'); box.dataset.view = view; $('schedulePager').hidden = view !== 'list';
    if (view === 'month') renderMonth(box); else if (view === 'week') renderWeek(box); else if (view === 'day') box.innerHTML = rows.length ? rows.map((item, index) => eventHtml(item, index)).join('') : '<p class="empty-state">Không có lịch trong ngày này.</p>'; else renderList(box);
    bind(box);
  }
  async function recipients(id) { const data = await api(`/mentors/${id}/interns`); $('internRecipients').innerHTML = data.length ? data.map(x => `<span>${esc(x.fullName)}</span>`).join(', ') : 'Mentor chưa có Intern.'; return data; }
  function payload() { return { mentorId: Number($('mentorId').value), title: $('title').value.trim(), scheduleType: $('scheduleType').value, startDate: $('startDate').value, endDate: $('endDate').value, startTime: $('startTime').value + ':00', endTime: $('endTime').value + ':00', recurrenceType: $('recurrence').value, weekDays: [...document.querySelectorAll('[name=weekday]:checked')].map(x => Number(x.value)), note: $('note').value.trim(), status: $('status').value }; }
  function openForm(row = null) { editing = row; $('scheduleForm').reset(); $('formTitle').textContent = row ? 'Chỉnh sửa lịch' : 'Tạo lịch theo Mentor'; $('scopeField').hidden = !row; if (row) { $('mentorId').value = row.mentorId; $('title').value = row.title; $('scheduleType').value = row.scheduleType; $('startDate').value = row.seriesStartDate; $('endDate').value = row.seriesEndDate; $('startTime').value = time(row.startTime); $('endTime').value = time(row.endTime); $('recurrence').value = row.recurrenceType; $('note').value = row.note || ''; $('status').value = row.status; document.querySelectorAll('[name=weekday]').forEach(x => x.checked = row.weekDays.includes(Number(x.value))); } toggleDays(); recipients($('mentorId').value); $('editor').showModal(); }
  async function save(event) { event.preventDefault(); const data = payload(); if (data.startTime >= data.endTime) return AppUI.feedback($('scheduleMessage'), 'Giờ kết thúc phải sau giờ bắt đầu.', 'error'); try { if (editing) { const scope = document.querySelector('[name=scope]:checked').value; await api('/' + editing.seriesId, { method: 'PUT', body: JSON.stringify({ scope, occurrenceDate: editing.date, schedule: data }) }); } else await api('', { method: 'POST', body: JSON.stringify(data) }); $('editor').close(); await load(); AppUI.feedback($('scheduleMessage'), 'Đã lưu lịch làm việc.', 'success'); } catch (error) { AppUI.feedback($('scheduleMessage'), error.message, 'error'); } }
  function detail(item) { $('detailBody').innerHTML = `<h2>${esc(item.title)}</h2><p><b>Mentor:</b> ${esc(item.mentorName)}</p><p><b>Intern nhận lịch:</b> ${item.interns.map(x => esc(x.fullName)).join(', ')}</p><p><b>Thời gian:</b> ${date(item.date)}, ${time(item.startTime)}–${time(item.endTime)}</p><p><b>Lặp lại:</b> ${labels[item.recurrenceType]}</p><p><b>Ghi chú:</b> ${esc(item.note || 'Không có')}</p><p><b>Trạng thái:</b> ${labels[item.status]}</p>${manager() ? '<div class="dialog-actions"><button id="editEvent" class="btn-secondary">Chỉnh sửa</button><button id="deleteEvent" class="btn-danger">Xóa/Hủy lịch</button></div>' : ''}`; $('details').showModal(); $('editEvent')?.addEventListener('click', () => { $('details').close(); openForm(item); }); $('deleteEvent')?.addEventListener('click', async () => { const scope = prompt('Nhập phạm vi: THIS, FUTURE hoặc ALL', 'ALL'); if (!scope) return; try { await api(`/${item.seriesId}?scope=${encodeURIComponent(scope)}&occurrenceDate=${item.date}`, { method: 'DELETE' }); $('details').close(); load(); } catch (error) { alert(error.message); } }); }
  function toggleDays() { $('weekdays').hidden = $('recurrence').value === 'NONE'; }
  async function init() { session = await API.getCurrentUser(); document.querySelectorAll('[data-view]').forEach(x => x.classList.toggle('active', x.dataset.view === view)); $('createBtn').hidden = !manager(); $('readonlyHint').textContent = session.user.role === 'INTERN' ? 'Lịch do Mentor đang quản lý bạn phụ trách.' : session.user.role === 'MENTOR' ? 'Lịch của bạn và danh sách Intern nhận lịch.' : ''; const now = new Date(), later = new Date(); later.setMonth(later.getMonth() + 2); $('filterFrom').value = iso(now); $('filterTo').value = iso(later); if (manager()) for (const mentor of await api('/mentors')) { $('mentorId').add(new Option(`${mentor.fullName} (${mentor.internCount} Intern)`, mentor.id)); $('filterMentor').add(new Option(mentor.fullName, mentor.id)); } installNavigation(); await load(); }
  dayNames.forEach((label, index) => document.querySelector('.weekday-grid').insertAdjacentHTML('beforeend', `<label><input type="checkbox" name="weekday" value="${index + 1}"> ${label}</label>`));
  $('mentorId').onchange = () => recipients($('mentorId').value); $('filterMentor').onchange = async () => { const id = $('filterMentor').value; $('filterIntern').innerHTML = '<option value="">Tất cả Intern</option>'; if (id) (await api(`/mentors/${id}/interns`)).forEach(x => $('filterIntern').add(new Option(x.fullName, x.id))); }; $('recurrence').onchange = toggleDays; $('filters').onsubmit = event => { event.preventDefault(); load(); }; $('scheduleForm').onsubmit = save; $('createBtn').onclick = () => openForm(); document.querySelectorAll('[data-close]').forEach(x => x.onclick = () => $('editor').close()); document.querySelector('[data-detail-close]').onclick = () => $('details').close(); document.querySelectorAll('[data-view]').forEach(button => button.onclick = () => { document.querySelectorAll('[data-view]').forEach(x => x.classList.remove('active')); button.classList.add('active'); view = button.dataset.view; page = 1; load(); }); $('logoutBtn').onclick = Session.logout;
  init().catch(error => AppUI.feedback($('scheduleMessage'), error.message, 'error'));
})();
