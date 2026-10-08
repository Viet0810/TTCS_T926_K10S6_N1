(() => {
  const form = document.getElementById("assignmentForm");
  const internSelect = document.getElementById("internId");
  const mentorSelect = document.getElementById("mentorUserId");
  const programSelect = document.getElementById("internshipProgramId");
  const button = document.getElementById("assignBtn");
  const message = document.getElementById("dashboardMessage");
  let interns = [];
  let saving = false;
  const programLabel = item => `${item.name || `#${item.id}`}${item.department ? ` (${item.department})` : ""}: ${new Date(item.startDate).toLocaleDateString("vi-VN")} – ${new Date(item.endDate).toLocaleDateString("vi-VN")}`;

  function notify(text) { message.textContent = text; message.hidden = false; }
  function options(select, items, label) {
    select.replaceChildren(new Option(select.options[0].text, ""));
    items.forEach(item => select.add(new Option(label(item), item.id ?? item.internId)));
  }
  function selectIntern(id) {
    const intern = interns.find(item => item.internId === Number(id));
    internSelect.value = intern ? String(intern.internId) : "";
    mentorSelect.value = intern?.mentorUserId ? String(intern.mentorUserId) : "";
    programSelect.value = intern?.internshipProgramId ? String(intern.internshipProgramId) : "";
  }
  function renderRows(assignments, programs) {
    const rows = document.getElementById("assignmentRows");
    rows.replaceChildren();
    if (!assignments.length) {
      const cell = document.createElement("td"); cell.colSpan = 6; cell.textContent = "Chưa có phân công Mentor.";
      const row = document.createElement("tr"); row.append(cell); rows.append(row); return;
    }
    assignments.forEach(item => {
      const row = document.createElement("tr");
      const program = programs.find(program => program.id === item.internshipProgramId);
      [item.fullName, item.email, item.mentorName || "Mentor không còn khả dụng", program ? programLabel(program) : "Chưa xếp chương trình", item.status || "—"].forEach(value => {
        const cell = document.createElement("td"); cell.textContent = value; row.append(cell);
      });
      const cell = document.createElement("td"); const edit = document.createElement("a");
      edit.href = `mentor-assignment.html?internId=${encodeURIComponent(item.internId)}`;
      edit.className = "btn-secondary"; edit.textContent = "Cập nhật";
      cell.append(edit); row.append(cell); rows.append(row);
    });
  }
  async function reload() {
    if (!form) {
      const [assignments, programs] = await Promise.all([API.getAssignments(), API.getAssignmentPrograms()]);
      renderRows(assignments, programs); return "";
    }
    const [allInterns, mentors, programs] = await Promise.all([
      API.getAssignmentInterns(), API.getAssignmentMentors(), API.getAssignmentPrograms(),
    ]);
    interns = allInterns;
    options(internSelect, interns, item => `${item.fullName} (${item.email})`);
    options(mentorSelect, mentors, item => `${item.fullName} (${item.email})`);
    options(programSelect, programs, programLabel);
    form.hidden = false;
    button.disabled = !interns.length || !mentors.length;
    return !interns.length ? "Chưa có thực tập sinh để phân công." : !mentors.length ? "Chưa có tài khoản Mentor để phân công." : "";
  }
  internSelect?.addEventListener("change", () => selectIntern(internSelect.value));
  form?.addEventListener("submit", async event => {
    event.preventDefault(); if (saving || !form.reportValidity()) return;
    saving = true; button.disabled = true; button.textContent = "Đang lưu...";
    const internId = Number(internSelect.value);
    const payload = { mentorUserId: Number(mentorSelect.value), internshipProgramId: programSelect.value ? Number(programSelect.value) : null };
    const controls = [...form.querySelectorAll("select")]; controls.forEach(control => control.disabled = true);
    let saved = false;
    try {
      await API.assignMentor(internId, payload); saved = true;
      const emptyMessage = await reload(); selectIntern(internId);
      const current = interns.find(item => item.internId === internId);
      notify(emptyMessage || `Đã lưu phân công Mentor.${current?.status ? ` Trạng thái: ${current.status}.` : ""}`);
    } catch (error) {
      notify(saved ? "Phân công đã lưu. Chưa thể tải lại danh sách, vui lòng mở lại trang." : error.message);
      Session.redirectIfExpired(error);
    } finally {
      saving = false; controls.forEach(control => control.disabled = false);
      button.disabled = !interns.length || mentorSelect.options.length <= 1; button.textContent = "Phân công";
    }
  });
  (async () => {
    if (!localStorage.getItem("token")) return;
    try {
      const session = await API.getCurrentUser();
      if (session.user.role !== "HR" || !Session.hasPermission(session, "ASSIGN_MENTOR")) {
        notify("Bạn không có quyền phân công Mentor."); return;
      }
      const emptyMessage = await reload();
      const selectedId = new URLSearchParams(location.search).get("internId");
      if (form && selectedId) {
        if (interns.some(item => String(item.internId) === selectedId)) selectIntern(selectedId);
        else { notify("Không tìm thấy thực tập sinh cần cập nhật."); return; }
      }
      if (emptyMessage) notify(emptyMessage); else message.hidden = true;
    } catch (error) { notify(error.message); Session.redirectIfExpired(error); }
  })();
})();
