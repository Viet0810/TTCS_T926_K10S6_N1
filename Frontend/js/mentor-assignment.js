(() => {
  const form = document.getElementById("assignmentForm");
  const internSelect = document.getElementById("internId");
  const mentorSelect = document.getElementById("mentorUserId");
  const programSelect = document.getElementById("internshipProgramId");
  const button = document.getElementById("assignBtn");
  const message = document.getElementById("dashboardMessage");
  let interns = [];
  let saving = false;
  let grants = [];
  const has = code => grants.includes(code);
  const canAssign = id => has('ASSIGN_MENTOR') && has(interns.find(item=>item.internId===Number(id))?.mentorUserId ? 'ASSIGNMENT_UPDATE' : 'ASSIGNMENT_CREATE');
  function lockProgram() {
    const intern = interns.find(item => item.internId === Number(internSelect.value));
    programSelect.disabled = saving || Boolean(intern?.mentorUserId && intern?.internshipProgramId);
  }
  document.addEventListener('session-permissions-updated',event=>{
    grants=event.detail.permissions||[];
    if(button){button.hidden=!has('ASSIGNMENT_CREATE')&&!has('ASSIGNMENT_UPDATE');button.disabled=saving||!has('ASSIGN_MENTOR')||!canAssign(internSelect.value);}
    document.querySelectorAll('#assignmentRows a').forEach(link=>link.hidden=!has('ASSIGNMENT_UPDATE'));
    document.querySelectorAll('#assignmentRows .btn-danger').forEach(button=>button.hidden=!has('ASSIGNMENT_DELETE'));
    if(!has('ASSIGN_MENTOR')){if(form)form.hidden=true;document.getElementById('assignmentRows')?.replaceChildren();}
  });
  const programLabel = item => `${item.name || `#${item.id}`}${item.department ? ` (${item.department})` : ""}: ${new Date(item.startDate).toLocaleDateString("vi-VN")} – ${new Date(item.endDate).toLocaleDateString("vi-VN")}`;

  function notify(text, type = "error") { AppUI.feedback(message, text, type); }
  function options(select, items, label) {
    select.replaceChildren(new Option(select.options[0].text, ""));
    items.forEach(item => select.add(new Option(label(item), item.id ?? item.internId)));
  }
  function selectIntern(id) {
    const intern = interns.find(item => item.internId === Number(id));
    internSelect.value = intern ? String(intern.internId) : "";
    mentorSelect.value = intern?.mentorUserId ? String(intern.mentorUserId) : "";
    programSelect.value = intern?.internshipProgramId ? String(intern.internshipProgramId) : "";
    lockProgram();
    button.disabled=!intern || !canAssign(id) || mentorSelect.options.length<=1;
  }
  function renderMentorLoad(rowsData) {
    const rows = document.getElementById("mentorLoadRows");
    if (!rows) return;
    rows.replaceChildren();
    if (!rowsData.length) {
      const cell = document.createElement("td"); cell.colSpan = 3; cell.textContent = "Chưa có Mentor nào được phân công Intern.";
      const row = document.createElement("tr"); row.append(cell); rows.append(row); return;
    }
    rowsData.forEach(item => {
      const row = document.createElement("tr");
      [item.mentorName || "Mentor chưa có tên", item.email || "—", `${item.internCount} Intern`].forEach(value => {
        const cell = document.createElement("td"); cell.textContent = value; row.append(cell);
      });
      rows.append(row);
    });
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
      if(has('ASSIGNMENT_UPDATE')) cell.append(edit);
      if(has('ASSIGNMENT_DELETE')) {const remove=document.createElement('button');remove.type='button';remove.className='btn-danger';remove.textContent='Xóa';remove.onclick=async()=>{if(saving||!has('ASSIGNMENT_DELETE')||!await AppUI.confirm('Xóa phân công của '+item.fullName+'?'))return;saving=true;remove.disabled=true;try{await requestApi('/intern-assignments/'+item.internId,{method:'DELETE',headers:getAuthHeader()});await reload();notify('Đã xóa phân công.', "success");}catch(error){notify(error.message);}finally{saving=false;remove.disabled=false;}};cell.append(remove);}
      row.append(cell); rows.append(row);
    });
  }
  async function reload() {
    if (!form) {
      const [assignments, programs, mentorLoad] = await Promise.all([API.getAssignments(), API.getAssignmentPrograms(), API.getMentorLoad()]);
      renderRows(assignments, programs); renderMentorLoad(mentorLoad); return "";
    }
    const [allInterns, mentors, programs] = await Promise.all([
      API.getAssignmentInterns(), API.getAssignmentMentors(), API.getAssignmentPrograms(),
    ]);
    interns = allInterns;
    options(internSelect, interns, item => `${item.fullName} (${item.email})`);
    options(mentorSelect, mentors, item => `${item.fullName} (${item.email})`);
    options(programSelect, programs, programLabel);
    form.hidden = false;
    button.hidden = !has('ASSIGNMENT_CREATE') && !has('ASSIGNMENT_UPDATE');
    button.disabled = !interns.length || !mentors.length || !canAssign(internSelect.value);
    return !interns.length ? "Chưa có thực tập sinh để phân công." : !mentors.length ? "Chưa có tài khoản Mentor để phân công." : "";
  }
  document.getElementById("assignmentSearch")?.addEventListener("input",event=>{TableUI.filter(document.getElementById("assignmentRows"),event.target.value);});
  internSelect?.addEventListener("change", () => selectIntern(internSelect.value));
  form?.addEventListener("submit", async event => {
    event.preventDefault(); if (saving || !canAssign(internSelect.value) || !form.reportValidity()) return;
    saving = true; button.disabled = true; button.textContent = "Đang lưu...";
    const internId = Number(internSelect.value);
    const payload = { mentorUserId: Number(mentorSelect.value), internshipProgramId: programSelect.value ? Number(programSelect.value) : null };
    const controls = [...form.querySelectorAll("select")]; controls.forEach(control => control.disabled = true);
    let saved = false;
    try {
      if(!await AppUI.confirm(`${internSelect.selectedOptions[0].text} → ${mentorSelect.selectedOptions[0].text}\n${programSelect.selectedOptions[0].text}`, "Xác nhận phân công", "Phân công"))return;
      await API.assignMentor(internId, payload); saved = true;
      const emptyMessage = await reload(); selectIntern(internId);
      const current = interns.find(item => item.internId === internId);
      notify(emptyMessage || `Đã lưu phân công Mentor.${current?.status ? ` Trạng thái: ${current.status}.` : ""}`, emptyMessage ? "info" : "success");
    } catch (error) {
      notify(saved ? "Phân công đã lưu. Chưa thể tải lại danh sách, vui lòng mở lại trang." : error.message, saved ? "warning" : "error");
      Session.redirectIfExpired(error);
    } finally {
      saving = false; controls.forEach(control => control.disabled = false);
      lockProgram();
      button.disabled = !interns.length || mentorSelect.options.length <= 1 || !canAssign(internSelect.value); button.textContent = "Phân công";
    }
  });
  (async () => {
    if (!localStorage.getItem("token")) return;
    try {
      const session = await API.getCurrentUser();
      grants=session.permissions||[];
      if (!Session.hasPermission(session, "ASSIGN_MENTOR")) {
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
