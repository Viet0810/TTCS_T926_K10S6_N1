(() => {
  const message=document.getElementById("attendanceMessage");let allowed=false,busy=false,current=null,canIn=false,canOut=false;
  const labels={ON_TIME:"Đã check-in",LATE:"Đi muộn",EARLY:"Về sớm",LEAVE_APPROVED:"Nghỉ có phép",ABSENT:"Nghỉ không phép"};
  const ruleLabels={ON_TIME:'Đúng giờ',LATE:'Đi muộn',COMPLETED:'Đủ giờ',EARLY:'Về sớm'};
  function ruleBadge(element,status){element.textContent=ruleLabels[status]||'—';element.className=status?`status-badge ${['ON_TIME','COMPLETED'].includes(status)?'status-ontime':'status-late'}`:'';}
  function render(){
    document.getElementById("attendanceDate").textContent=current.date.split("-").reverse().join("/");
    document.getElementById("attendanceSchedule").textContent="Giờ làm việc: "+(current.scheduledStart||"08:00").slice(0,5)+" – "+(current.scheduledEnd||"17:30").slice(0,5)+" (Việt Nam). Không tính thêm giờ khi đến sớm/về muộn.";
    document.getElementById("attendanceIn").textContent=current.checkIn||"Chưa check-in";
    document.getElementById("attendanceOut").textContent=current.checkOut||"Chưa check-out";
    document.getElementById("attendanceStatus").textContent=current.checkOut?"Đã hoàn thành":current.checkIn?'Đang làm việc':labels[current.status]||"Chưa chấm công";
    ruleBadge(document.getElementById('attendanceInStatus'),current.checkInStatus);
    ruleBadge(document.getElementById('attendanceOutStatus'),current.checkOutStatus);
    document.getElementById("checkInBtn").hidden=!canIn;
    document.getElementById("checkOutBtn").hidden=!canOut;
    document.getElementById("checkInBtn").disabled=busy||!canIn||!!current.status;
    document.getElementById("checkOutBtn").disabled=busy||!canOut||!current.checkIn||!!current.checkOut;
    document.getElementById('checkOutBtn').classList.toggle('btn-primary',!!current.checkIn&&!current.checkOut);
  }
  async function load(){
    current=await requestApi("/interns/me/attendance",{headers:getAuthHeader()});render();
    const target=document.getElementById('attendanceHistory');
    try {
      const history=await requestApi('/interns/me/attendance/history',{headers:getAuthHeader()});target.replaceChildren();
      if(!history.length){const cell=target.insertRow().insertCell();cell.colSpan=7;cell.textContent='Chưa có lịch sử chấm công. Check-in để bắt đầu ngày làm việc.';}
      for(const item of history){const row=target.insertRow();for(const text of [item.date.split('-').reverse().join('/'),item.checkIn||'—',item.checkOut||'—'])row.insertCell().textContent=text;for(const status of [item.checkInStatus,item.checkOutStatus]){const badge=document.createElement('span');ruleBadge(badge,status);row.insertCell().append(badge);}row.insertCell().textContent=item.hours;row.insertCell().textContent=item.checkOut?'Hoàn thành':item.checkIn?'Đang làm việc':labels[item.status]||'—';}
    }catch(error){target.replaceChildren();const cell=target.insertRow().insertCell();cell.colSpan=7;cell.textContent=`Chưa tải được lịch sử: ${error.message}`;}
  }
  for(const action of ["check-in","check-out"]){const button=document.getElementById(action==="check-in"?"checkInBtn":"checkOutBtn");button.addEventListener("click",async()=>{
    if(!allowed||!(action==='check-in'?canIn:canOut)||busy||button.disabled)return;busy=true;render();const original=button.textContent;button.textContent="Đang ghi nhận…";
    try{const result=await requestApi(`/interns/me/attendance/${action}`,{method:"POST",headers:getAuthHeader()});AppUI.feedback(message, result.message);current=null;await load();}
    catch(error){AppUI.feedback(message, error.message,"error");Session.redirectIfExpired(error);try{await load();}catch{} }
    finally{busy=false;button.textContent=original;if(current)render();else{document.getElementById("checkInBtn").disabled=true;document.getElementById("checkOutBtn").disabled=true;}}
  });}
  document.getElementById("attendanceRefreshBtn").addEventListener("click",async event=>{
    if(!allowed||busy)return;const button=event.currentTarget;button.disabled=true;message.textContent="Đang tải chấm công…";
    try{await load();message.textContent="";}catch(error){AppUI.feedback(message, error.message,"error");}finally{button.disabled=false;}
  });
  function grants(session){allowed=Session.hasPermission(session,'OWN_ATTENDANCE');canIn=Session.hasPermission(session,'ATTENDANCE_CHECK_IN');canOut=Session.hasPermission(session,'ATTENDANCE_CHECK_OUT');if(current)render();}
  document.addEventListener('session-permissions-updated',event=>{grants(event.detail);if(!allowed){current=null;document.getElementById('attendanceHistory').replaceChildren();for(const id of ['attendanceDate','attendanceIn','attendanceOut','attendanceStatus','attendanceInStatus','attendanceOutStatus'])document.getElementById(id).textContent='—';document.getElementById('checkInBtn').hidden=true;document.getElementById('checkOutBtn').hidden=true;message.textContent='Bạn không còn quyền xem chấm công.';}});
  (async()=>{if(!localStorage.getItem("token"))return;try{const session=await API.getCurrentUser();grants(session);if(!allowed){message.textContent="Bạn không có quyền xem chấm công.";return;}await load();message.textContent="";}catch(error){AppUI.feedback(message, error.message,"error");Session.redirectIfExpired(error);}})();
})();
