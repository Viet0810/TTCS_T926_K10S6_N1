document.addEventListener('DOMContentLoaded', () => {
  const form=document.getElementById('programDateForm'); if(!form)return;
  const start=document.getElementById('programStartDate'), end=document.getElementById('programEndDate');
  const save=document.getElementById('programSaveBtn'), message=document.getElementById('programDateMessage');
  const rows=document.getElementById('programHistoryBody'), empty=document.getElementById('emptyProgramHistory');
  let grants=[], programs=[], editing=null, busy=false, allowed=false;
  const has=code=>grants.includes(code);
  function notice(text,type='error'){AppUI.feedback(message,text,type);}
  function preview(){
    end.min=start.value;
    const days=start.value&&end.value?Math.floor((new Date(end.value+'T00:00:00Z')-new Date(start.value+'T00:00:00Z'))/86400000)+1:0;
    document.getElementById('programDuration').textContent=days>0?days+' ngày':'Chưa xác định';
    const today=new Date(Date.now()+7*3600000).toISOString().slice(0,10), status=document.getElementById('programStatus');
    status.textContent=days<=0?'Chưa thiết lập':today<start.value?'Sắp diễn ra':today>end.value?'Đã kết thúc':'Đang diễn ra';
    status.className='program-status '+(days<=0?'status-empty':today<start.value?'status-upcoming':today>end.value?'status-ended':'status-active');
  }
  function controls(){
    form.hidden=!allowed||(!has('PROGRAM_CREATE')&&!has('PROGRAM_UPDATE'));
    save.hidden=!has(editing?'PROGRAM_UPDATE':'PROGRAM_CREATE');save.disabled=busy||!allowed||!has(editing?'PROGRAM_UPDATE':'PROGRAM_CREATE');
    save.textContent=busy?'Đang lưu…':editing?'Lưu thay đổi':'Lưu thời gian';
    [...form.elements].forEach(element=>{if(element!==save)element.disabled=busy;});
  }
  function clear(){editing=null;form.reset();preview();controls();}
  function render(){
    rows.replaceChildren();empty.style.display=programs.length?'none':'block';empty.textContent='Chưa có thời gian chương trình nào được thiết lập.';
    const labels={UPCOMING:'Sắp diễn ra',ACTIVE:'Đang diễn ra',ENDED:'Đã kết thúc'};
    programs.forEach((program,index)=>{
      const row=rows.insertRow(), date=value=>(value||'').slice(0,10).split('-').reverse().join('/');
      for(const value of [index+1,program.name||`Chương trình #${program.id}`,program.department||'Chưa bổ sung',date(program.startDate),date(program.endDate),program.durationDays+' ngày',labels[program.status]||'Chưa xác định'])row.insertCell().textContent=String(value);
      const actions=row.insertCell();
      if(has('PROGRAM_UPDATE')) {const edit=document.createElement('button');edit.type='button';edit.className='btn-secondary';edit.textContent='Sửa';edit.onclick=()=>{if(busy||!has('PROGRAM_UPDATE'))return;editing=program.id;document.getElementById('programName').value=program.name||'';document.getElementById('programDepartment').value=program.department||'';start.value=program.startDate.slice(0,10);end.value=program.endDate.slice(0,10);preview();controls();form.scrollIntoView({block:'center'});start.focus();};actions.append(edit);}
      if(has('PROGRAM_DELETE')) {const remove=document.createElement('button');remove.type='button';remove.className='btn-delete-program';remove.dataset.id=program.id;remove.textContent='Xóa';remove.onclick=async()=>{if(busy||!allowed||!has('PROGRAM_DELETE')||!await AppUI.confirm('Xóa chương trình này?'))return;busy=true;remove.disabled=true;controls();try{await requestApi('/program-schedule/'+program.id,{method:'DELETE',headers:getAuthHeader()});if(editing===program.id)clear();await load();notice('Đã xóa chương trình.','success');}catch(error){notice(error.message);Session.redirectIfExpired(error);}finally{busy=false;controls();remove.disabled=false;}};actions.append(remove);}
    });
  }
  async function load(){if(!allowed)return;empty.style.display='block';empty.textContent='Đang tải chương trình…';const result=await requestApi('/program-schedule',{headers:getAuthHeader()});programs=result.data;render();}
  form.addEventListener('submit',async event=>{
    event.preventDefault();if(busy||!allowed||!has(editing?'PROGRAM_UPDATE':'PROGRAM_CREATE'))return;
    if(!start.value||!end.value||end.value<start.value){notice('Ngày kết thúc không được trước ngày bắt đầu.');return;}if(!form.reportValidity())return;
    const id=editing;busy=true;controls();let persisted=false;
    try{const body={name:document.getElementById('programName').value.trim()||null,department:document.getElementById('programDepartment').value.trim()||null,startDate:start.value,endDate:end.value};await requestApi('/program-schedule'+(id?'/'+id:''),{method:id?'PUT':'POST',headers:getAuthHeader(),body:JSON.stringify(body)});persisted=true;clear();await load();notice('Đã lưu chương trình.','success');}
    catch(error){notice(persisted?'Đã lưu nhưng chưa tải lại được danh sách. '+error.message:error.message);Session.redirectIfExpired(error);}
    finally{busy=false;controls();}
  });
  start.addEventListener('change',()=>{if(end.value<start.value)end.value='';preview();});end.addEventListener('change',preview);
  document.getElementById('programCancelBtn').addEventListener('click',()=>{if(!busy){clear();notice('','');}});
  function sessionChanged(session){grants=session.permissions||[];allowed=has('MANAGE_PROGRAMS');controls();if(!allowed){programs=[];render();notice('Bạn không có quyền xem chương trình.');}else render();}
  document.addEventListener('session-permissions-updated',event=>sessionChanged(event.detail));
  preview();controls();
  (async()=>{try{sessionChanged(await API.getCurrentUser());if(allowed)await load();}catch(error){notice(error.message);empty.style.display='block';empty.textContent='Chưa thể tải danh sách chương trình.';Session.redirectIfExpired(error);}})();
});
