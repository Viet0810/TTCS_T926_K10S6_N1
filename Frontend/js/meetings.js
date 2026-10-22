document.addEventListener('DOMContentLoaded', () => {
  const byId = id => document.getElementById(id);
  const form = byId('meetingForm'), save = byId('meetingSave'), reload = byId('meetingReload');
  let grants = [], participants = [], selected = new Set(), rows = [], loading = false, saving = false, initialized = false;
  let pendingRequest = null;
  const has = code => grants.includes(code);
  const call = (path, options = {}) => requestApi(path, { ...options, headers: getAuthHeader() });
  const notify = (message, type = 'error') => AppUI.toast(message, type, 'meeting-feedback');
  function controls() {
    save.disabled = saving || loading || !has('MEETING_CREATE') || !participants.length;
    save.textContent = saving ? 'Đang tạo cuộc họp…' : 'Tạo cuộc họp';
    reload.disabled = loading || saving;
    [...form.elements].forEach(element => { if (element !== save) element.disabled = saving; });
  }
  function renderParticipants() {
    const query = byId('participantSearch').value.trim().toLocaleLowerCase('vi');
    const container = byId('participantOptions'); container.replaceChildren();
    const filtered = participants.filter(p => `${p.fullName} ${p.email} ${p.role}`.toLocaleLowerCase('vi').includes(query));
    for (const person of filtered) {
      const label = document.createElement('label'), check = document.createElement('input');
      label.className = 'meeting-person'; label.classList.toggle('is-selected', selected.has(person.id));
      check.type = 'checkbox'; check.value = person.id; check.checked = selected.has(person.id);
      check.onchange = () => { check.checked ? selected.add(person.id) : selected.delete(person.id); label.classList.toggle('is-selected', check.checked); byId('participantCount').textContent = `Đã chọn ${selected.size} người`; };
      const avatar = document.createElement('span'); avatar.className = 'meeting-person-avatar'; avatar.setAttribute('aria-hidden', 'true');
      avatar.textContent = (person.fullName || '?').trim().split(/\s+/).slice(-2).map(part => part[0]).join('').toLocaleUpperCase('vi');
      const info = document.createElement('span'); info.className = 'meeting-person-info';
      const heading = document.createElement('span'); heading.className = 'meeting-person-heading';
      const name = document.createElement('strong'); name.textContent = person.fullName;
      const role = document.createElement('span'); role.className = 'meeting-person-role'; role.dataset.role = person.role; role.textContent = person.role;
      const email = document.createElement('span'); email.className = 'meeting-person-email'; email.textContent = person.email;
      heading.append(name, role); info.append(heading, email); label.append(check, avatar, info); container.append(label);
    }
    if (!filtered.length) container.textContent = participants.length ? 'Không có kết quả phù hợp.' : 'Chưa có người dùng để chọn.';
  }
  function renderRows() {
    const body = byId('meetingRows'); body.replaceChildren();
    byId('meetingState').textContent = rows.length ? `${rows.length} cuộc họp` : 'Chưa có cuộc họp nào.';
    for (const meeting of rows) {
      const row = body.insertRow();
      const title = row.insertCell(); title.className = 'meeting-title-cell';
      const titleText = document.createElement('strong'); titleText.textContent = meeting.title; title.append(titleText);
      if (meeting.description) { const description = document.createElement('small'); description.textContent = meeting.description; title.append(description); }

      const start = new Date(meeting.startTime), end = new Date(meeting.endTime);
      const timeCell = row.insertCell(); timeCell.className = 'meeting-time-cell';
      const dateLine = document.createElement('strong');
      dateLine.textContent = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', weekday: 'short', day: '2-digit', month: '2-digit', year: 'numeric' }).format(start);
      const timeLine = document.createElement('span');
      const timeFormat = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', hour: '2-digit', minute: '2-digit', hour12: false });
      timeLine.textContent = `${timeFormat.format(start)} – ${timeFormat.format(end)}`;
      timeCell.append(dateLine, timeLine);

      const locationCell = row.insertCell(); locationCell.className = 'meeting-location-cell';
      if (/^https?:\/\//i.test(meeting.locationOrLink || '')) {
        const link = document.createElement('a'); link.href = meeting.locationOrLink; link.target = '_blank'; link.rel = 'noopener'; link.className = 'meeting-join-link'; link.textContent = 'Mở link họp ↗'; locationCell.append(link);
      } else locationCell.textContent = meeting.locationOrLink || 'Chưa cập nhật';

      const peopleCell = row.insertCell(); peopleCell.className = 'meeting-attendee-cell';
      const attendees = meeting.participants || [];
      if (!attendees.length) peopleCell.textContent = 'Chưa có người tham gia';
      else {
        const stack = document.createElement('div'); stack.className = 'meeting-attendee-stack';
        attendees.slice(0, 3).forEach(person => {
          const chip = document.createElement('span'); chip.className = 'meeting-attendee-chip'; chip.title = `${person.fullName} · ${person.email}`;
          const avatar = document.createElement('i'); avatar.textContent = person.fullName.trim().split(/\s+/).slice(-2).map(part => part[0]).join('').toLocaleUpperCase('vi');
          const name = document.createElement('span'); name.textContent = person.fullName; chip.append(avatar, name); stack.append(chip);
        });
        if (attendees.length > 3) { const more = document.createElement('span'); more.className = 'meeting-attendee-more'; more.textContent = `+${attendees.length - 3} người`; more.title = attendees.slice(3).map(person => person.fullName).join(', '); stack.append(more); }
        peopleCell.append(stack);
      }

      const emailCell = row.insertCell();
      const delivered = meeting.emailsSent === meeting.participantUserIds.length;
      const emailBadge = document.createElement('span'); emailBadge.className = `meeting-email-badge ${delivered ? 'is-complete' : 'is-pending'}`;
      emailBadge.textContent = delivered ? `Đã gửi ${meeting.emailsSent}/${meeting.participantUserIds.length}` : `Đã gửi ${meeting.emailsSent}/${meeting.participantUserIds.length}`;
      emailCell.append(emailBadge);
    }
  }
  async function load() {
    if (loading || !has('MEETING_READ')) return;
    loading = true; controls(); byId('meetingState').textContent = 'Đang tải lịch họp…';
    try {
      [participants, rows] = await Promise.all([has('MEETING_CREATE') ? call('/meetings/participants') : Promise.resolve([]), call('/meetings')]);
      renderParticipants(); renderRows();
    } catch (error) { byId('meetingState').textContent = 'Chưa tải được lịch họp. Hãy thử tải lại.'; notify(error.message); Session.redirectIfExpired(error); }
    finally { loading = false; controls(); }
  }
  async function sessionChanged(session) {
    const couldCreate = has('MEETING_CREATE');
    grants = session.permissions || []; const allowed = has('MEETING_READ');
    byId('meetingAccess').hidden = allowed; byId('meetingAccess').textContent = 'Bạn không có quyền xem lịch họp.';
    byId('meetingList').hidden = !allowed; byId('meetingCreate').hidden = !allowed || !has('MEETING_CREATE'); controls();
    if (!allowed) { initialized = false; participants = []; rows = []; renderParticipants(); renderRows(); return; }
    if (!initialized || couldCreate !== has('MEETING_CREATE')) { initialized = true; await load(); }
  }
  byId('participantSearch').addEventListener('input', renderParticipants);
  reload.addEventListener('click', load);
  form.addEventListener('reset', () => {
    selected.clear(); pendingRequest = null; byId('participantCount').textContent = 'Đã chọn 0 người';
    requestAnimationFrame(renderParticipants);
  });
  form.addEventListener('submit', async event => {
    event.preventDefault(); if (saving || loading || !has('MEETING_CREATE') || !form.reportValidity()) return;
    const values = form.elements;
    const start = new Date(values.start.value + '+07:00'), end = new Date(values.end.value + '+07:00');
    if (!(start < end)) { notify('Thời gian bắt đầu phải trước thời gian kết thúc.'); return; }
    if (!selected.size) { notify('Hãy chọn ít nhất một người tham gia.'); return; }
    const payload = { title: values.title.value.trim(), startTime: start.toISOString(), endTime: end.toISOString(), locationOrLink: values.location.value, description: values.description.value, participantUserIds: [...selected].sort((a,b) => a-b) };
    const fingerprint = JSON.stringify(payload);
    if (!pendingRequest || pendingRequest.fingerprint !== fingerprint) pendingRequest = { fingerprint, id: crypto.randomUUID() };
    saving = true; controls();
    try {
      const result = await call('/meetings', { method: 'POST', body: JSON.stringify({ ...payload, clientRequestId: pendingRequest.id }) });
      pendingRequest = null; form.reset();
      notify(result.emailsFailed ? `Đã lưu cuộc họp. ${result.emailsFailed} email chưa gửi được; vui lòng kiểm tra dịch vụ email.` : 'Đã lưu cuộc họp và gửi email thông báo.', result.emailsFailed ? 'warning' : 'success');
      await load();
    } catch (error) { notify(error.message); Session.redirectIfExpired(error); }
    finally { saving = false; controls(); }
  });
  document.addEventListener('session-permissions-updated', event => sessionChanged(event.detail));
  API.getCurrentUser().then(sessionChanged).catch(error => { byId('meetingAccess').textContent = 'Chưa xác thực được phiên đăng nhập.'; notify(error.message); Session.redirectIfExpired(error); });
});
