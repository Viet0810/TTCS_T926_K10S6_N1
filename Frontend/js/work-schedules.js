(() => {
const form=document.getElementById('scheduleForm'),list=document.getElementById('scheduleList'),message=document.getElementById('scheduleMessage'),reload=document.getElementById('reloadSchedules');
const intern=document.getElementById('scheduleIntern'),day=document.getElementById('scheduleDate'),start=document.getElementById('scheduleStart'),end=document.getElementById('scheduleEnd'),note=document.getElementById('scheduleNote');
let session,rows=[],editing=null,busy=false;
const has=p=>Session.hasPermission(session||{},p);
const api=(path='',options={})=>requestApi('/work-schedules'+path,{...options,headers:getAuthHeader()});
function permissions(){form.hidden=!has('WORK_SCHEDULE_CREATE')&&!has('WORK_SCHEDULE_UPDATE');document.getElementById('saveSchedule').disabled=busy||!has(editing?'WORK_SCHEDULE_UPDATE':'WORK_SCHEDULE_CREATE');reload.disabled=busy;if(!has('WORK_SCHEDULE_READ')){rows=[];list.replaceChildren();}}
function reset(){editing=null;form.reset();intern.disabled=false;day.disabled=false;permissions();}
function render(){list.replaceChildren();if(!rows.length){list.textContent='Chưa có lịch làm việc.';return;}rows.forEach(row=>{
const card=document.createElement('article');card.className='weekly-report';
const title=document.createElement('h3');title.textContent=row.internName+' · '+row.workDate.split('-').reverse().join('/');
const text=document.createElement('p');text.textContent=row.startTime.slice(0,5)+' – '+row.endTime.slice(0,5)+(row.note?' · '+row.note:'');card.append(title,text);
if(has('WORK_SCHEDULE_UPDATE')){const button=document.createElement('button');button.type='button';button.textContent='Sửa lịch';button.className='btn-secondary';button.onclick=()=>{editing=row.id;intern.value=row.internId;day.value=row.workDate;start.value=row.startTime.slice(0,5);end.value=row.endTime.slice(0,5);note.value=row.note||'';intern.disabled=true;day.disabled=true;permissions();start.focus();};card.append(button);}list.append(card);});}
async function load(){if(busy||!has('WORK_SCHEDULE_READ'))return;busy=true;permissions();list.textContent='Đang tải lịch…';try{const result=await api();if(has('WORK_SCHEDULE_READ')){rows=result;render();}}catch(e){list.textContent='Không tải được lịch. Hãy thử lại.';AppUI.feedback(message,e.message,'error');Session.redirectIfExpired(e);}finally{busy=false;permissions();}}
form.onsubmit=async e=>{e.preventDefault();if(busy||!has(editing?'WORK_SCHEDULE_UPDATE':'WORK_SCHEDULE_CREATE')||!form.reportValidity())return;
if(start.value>=end.value){AppUI.feedback(message,'Giờ bắt đầu phải trước giờ kết thúc.','error');return;}
busy=true;permissions();let saved=false;try{await api(editing?'/'+editing:'',{method:editing?'PUT':'POST',body:JSON.stringify({internId:Number(intern.value),workDate:day.value,startTime:start.value+':00',endTime:end.value+':00',note:note.value.trim()})});saved=true;reset();AppUI.feedback(message,'Đã lưu lịch làm việc.','success');}catch(e){AppUI.feedback(message,e.message,'error');Session.redirectIfExpired(e);}finally{busy=false;permissions();if(saved)await load();}};
document.getElementById('cancelSchedule').onclick=reset;reload.onclick=load;
document.addEventListener('session-permissions-updated',e=>{session=e.detail;permissions();render();});
(async()=>{try{session=await API.getCurrentUser();permissions();if(has('WORK_SCHEDULE_READ')&&session.user.role!=='INTERN'){const options=await api('/interns');options.forEach(i=>intern.add(new Option(i.fullName,i.id)));}await load();}catch(e){AppUI.feedback(message,e.message,'error');Session.redirectIfExpired(e);}})();
})();