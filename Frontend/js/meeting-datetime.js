// Shared Vietnamese datetime picker. The original ISO value remains the submitted form value.
document.addEventListener('DOMContentLoaded', () => {
  const pad = value => String(value).padStart(2, '0');
  const dayValue = date => `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
  const parse = value => {
    const match = /^(\d{2})\/(\d{2})\/(\d{4}) (\d{2}):(\d{2})$/.exec(value.trim());
    if (!match) return null;
    const [, day, month, year, hour, minute] = match.map(Number);
    const date = new Date(year, month - 1, day, hour, minute);
    return year >= 1000 && date.getFullYear() === year && date.getMonth() === month - 1 && date.getDate() === day
      && hour < 24 && minute < 60 ? `${year}-${pad(month)}-${pad(day)}T${pad(hour)}:${pad(minute)}` : null;
  };
  let closeActive = () => {};
  for (const original of document.querySelectorAll('.meeting-datetime-field input[type="datetime-local"]')) {
    const field = original.parentElement, label = field.querySelector('label').textContent;
    const wrapper = document.createElement('div'); wrapper.className = 'meeting-datetime';
    const input = document.createElement('input'); input.type = 'text'; input.id = original.id + 'Display';
    input.placeholder = 'dd/MM/yyyy HH:mm'; input.required = original.required; input.autocomplete = 'off'; input.maxLength = 16;
    input.setAttribute('aria-label', `${label}, định dạng ngày/tháng/năm giờ:phút`);
    field.querySelector('label').htmlFor = input.id;
    const trigger = document.createElement('button'); trigger.type = 'button'; trigger.className = 'meeting-datetime-trigger';
    trigger.setAttribute('aria-label', `Chọn ngày giờ ${label.toLocaleLowerCase('vi')}`); trigger.setAttribute('aria-haspopup', 'dialog'); trigger.setAttribute('aria-expanded', 'false');
    trigger.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M7 3v4m10-4v4M4 10h16M5 5h14a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1Z"/><circle cx="12" cy="15" r="3"/><path d="M12 13v2l1 1"/></svg>';
    const panel = document.createElement('div'); panel.className = 'meeting-datetime-panel'; panel.hidden = true; panel.id = original.id + 'Picker';
    panel.setAttribute('role', 'dialog'); panel.setAttribute('aria-label', `Chọn ${label.toLocaleLowerCase('vi')}`); trigger.setAttribute('aria-controls', panel.id);
    panel.innerHTML = '<div class="meeting-calendar-heading"><button type="button" data-prev aria-label="Tháng trước">‹</button><strong aria-live="polite"></strong><button type="button" data-next aria-label="Tháng sau">›</button></div><div class="meeting-calendar-week" aria-hidden="true"><span>T2</span><span>T3</span><span>T4</span><span>T5</span><span>T6</span><span>T7</span><span>CN</span></div><div class="meeting-calendar-days" role="group" aria-label="Ngày trong tháng"></div><div class="meeting-time-row"><span>Giờ (24h)</span><input type="number" data-hours aria-label="Giờ" min="0" max="23" step="1" inputmode="numeric" value="08" required><span>:</span><input type="number" data-minutes aria-label="Phút" min="0" max="59" step="1" inputmode="numeric" value="00" required></div><div class="meeting-calendar-actions"><button type="button" data-today>Hôm nay</button><button type="button" data-apply>Xong</button></div>';
    const hours = panel.querySelector('[data-hours]'), minutes = panel.querySelector('[data-minutes]');
    original.type = 'hidden'; original.required = false;
    const clock = document.createElement('button'); clock.type = 'button'; clock.className = 'meeting-datetime-clock';
    clock.setAttribute('aria-label', `Chỉnh giờ ${label.toLocaleLowerCase('vi')}`); clock.setAttribute('aria-haspopup', 'dialog'); clock.setAttribute('aria-controls', panel.id); clock.setAttribute('aria-expanded', 'false');
    clock.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7"><circle cx="12" cy="12" r="8"/><path d="M12 7v5l3 2"/></svg>';
    original.before(wrapper); wrapper.append(clock, input, trigger, original, panel);
    let selected, month;
    const sync = () => {
      const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(original.value);
      input.value = match ? `${match[3]}/${match[2]}/${match[1]} ${match[4]}:${match[5]}` : '';
      input.setCustomValidity('');
    };
    function close(restoreFocus = false) {
      // Discard an incomplete time edit, not the last valid datetime, when leaving the popup.
      const time = original.value.slice(11, 16) || '08:00';
      [hours.value, minutes.value] = time.split(':');
      panel.hidden = true; trigger.setAttribute('aria-expanded', 'false'); clock.setAttribute('aria-expanded', 'false');
      if (restoreFocus) trigger.focus();
    }
    function commit() {
      if (!hours.checkValidity() || !minutes.checkValidity()) return false;
      original.value = `${dayValue(selected)}T${pad(Number(hours.value))}:${pad(Number(minutes.value))}`;
      sync(); original.dispatchEvent(new Event('change', { bubbles: true }));
      return true;
    }
    function render(focusDay = null) {
      panel.querySelector('strong').textContent = `Tháng ${month.getMonth() + 1}, ${month.getFullYear()}`;
      const days = panel.querySelector('.meeting-calendar-days'); days.replaceChildren();
      const first = new Date(month.getFullYear(), month.getMonth(), 1);
      for (let i = 0; i < (first.getDay() + 6) % 7; i++) days.append(document.createElement('span'));
      const count = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate();
      for (let day = 1; day <= count; day++) {
        const date = new Date(month.getFullYear(), month.getMonth(), day), button = document.createElement('button');
        button.type = 'button'; button.textContent = day; button.dataset.day = day;
        button.setAttribute('aria-label', `Ngày ${day} tháng ${month.getMonth() + 1} năm ${month.getFullYear()}`);
        button.setAttribute('aria-pressed', String(dayValue(date) === dayValue(selected)));
        if (dayValue(date) === dayValue(new Date())) button.setAttribute('aria-current', 'date');
        button.tabIndex = day === (focusDay || (selected.getMonth() === month.getMonth() && selected.getFullYear() === month.getFullYear() ? selected.getDate() : 1)) ? 0 : -1;
        button.onclick = () => { selected = date; commit(); render(day); days.querySelector(`[data-day="${day}"]`).focus(); };
        button.onkeydown = event => {
          const offset = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[event.key];
          if (offset === undefined) return;
          event.preventDefault(); const next = new Date(month.getFullYear(), month.getMonth(), day + offset);
          month = new Date(next.getFullYear(), next.getMonth(), 1); render(next.getDate());
          days.querySelector(`[data-day="${next.getDate()}"]`).focus();
        };
        days.append(button);
      }
    }
    function open(focusTime = false, moveFocus = true) {
      if (input.disabled) return;
      if (!panel.hidden) {
        if (moveFocus) {
          if (focusTime) { hours.focus(); hours.select(); }
          else panel.querySelector('.meeting-calendar-days [tabindex="0"]').focus();
        }
        return;
      }
      closeActive(); closeActive = close;
      const value = original.value || `${dayValue(new Date())}T08:00`;
      const [date, time] = value.split('T'), [year, monthNumber, day] = date.split('-').map(Number);
      selected = new Date(year, monthNumber - 1, day); month = new Date(year, monthNumber - 1, 1);
      [hours.value, minutes.value] = time.slice(0, 5).split(':');
      render(); panel.hidden = false; trigger.setAttribute('aria-expanded', 'true'); clock.setAttribute('aria-expanded', 'true');
      if (moveFocus) {
        if (focusTime) { hours.focus(); hours.select(); }
        else panel.querySelector('.meeting-calendar-days [tabindex="0"]').focus();
      }
    }
    trigger.onclick = () => open();
    clock.onclick = () => open(true);
    input.addEventListener('click', () => {
      open(false, false);
      const caret = input.selectionStart;
      if (input.value.length !== 16 || input.selectionStart !== input.selectionEnd) return;
      const [start, end] = caret >= 14 ? [14, 16] : caret >= 11 ? [11, 13] : caret >= 6 ? [6, 10] : caret >= 3 ? [3, 5] : [0, 2];
      input.setSelectionRange(start, end);
    });
    input.oninput = () => {
      const value = parse(input.value); original.value = value || '';
      input.setCustomValidity(input.value && !value ? 'Nhập ngày giờ hợp lệ theo dd/MM/yyyy HH:mm.' : '');
      if (value && !panel.hidden) {
        const [date, time] = value.split('T'), [year, monthNumber, day] = date.split('-').map(Number);
        selected = new Date(year, monthNumber - 1, day); month = new Date(year, monthNumber - 1, 1);
        [hours.value, minutes.value] = time.split(':'); render();
      }
    };
    input.onkeydown = event => { if (event.key === 'ArrowDown' && event.altKey) { event.preventDefault(); open(); } };
    panel.querySelector('[data-prev]').onclick = () => { month.setMonth(month.getMonth() - 1); render(); };
    panel.querySelector('[data-next]').onclick = () => { month.setMonth(month.getMonth() + 1); render(); };
    panel.querySelector('[data-today]').onclick = () => { selected = new Date(); month = new Date(selected.getFullYear(), selected.getMonth(), 1); commit(); render(); };
    panel.querySelector('[data-apply]').onclick = () => {
      if (!hours.reportValidity() || !minutes.reportValidity()) return;
      if (commit()) close(true);
    };
    hours.oninput = minutes.oninput = commit;
    hours.onchange = minutes.onchange = () => { if (commit()) { hours.value = pad(Number(hours.value)); minutes.value = pad(Number(minutes.value)); } };
    wrapper.addEventListener('keydown', event => { if (event.key === 'Escape' && !panel.hidden) { event.preventDefault(); close(true); } });
    // Focus can temporarily leave an input during pointer interaction or calendar rendering.
    // Only an actual outside pointer event, Done, or Escape dismisses the popup.
    document.addEventListener('pointerdown', event => { if (!wrapper.contains(event.target)) close(); });
    original.addEventListener('change', sync);
    original.form.addEventListener('reset', () => requestAnimationFrame(() => { sync(); close(); }));
    sync();
  }
});
