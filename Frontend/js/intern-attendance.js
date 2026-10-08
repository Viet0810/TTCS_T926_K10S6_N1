(() => {
  const message=document.getElementById("attendanceMessage");let allowed=false,busy=false,current=null;
  const labels={ON_TIME:"Đã check-in",LATE:"Đi muộn",EARLY:"Về sớm",LEAVE_APPROVED:"Nghỉ có phép",ABSENT:"Nghỉ không phép"};
  function render(){
    document.getElementById("attendanceDate").textContent=current.date.split("-").reverse().join("/");
    document.getElementById("attendanceIn").textContent=current.checkIn||"Chưa check-in";
    document.getElementById("attendanceOut").textContent=current.checkOut||"Chưa check-out";
    document.getElementById("attendanceStatus").textContent=current.checkOut?"Đã check-out":labels[current.status]||"Chưa chấm công";
    document.getElementById("checkInBtn").disabled=busy||!!current.status;
    document.getElementById("checkOutBtn").disabled=busy||!current.checkIn||!!current.checkOut;
  }
  async function load(){current=await requestApi("/interns/me/attendance",{headers:getAuthHeader()});render();}
  for(const action of ["check-in","check-out"]){const button=document.getElementById(action==="check-in"?"checkInBtn":"checkOutBtn");button.addEventListener("click",async()=>{
    if(!allowed||busy||button.disabled)return;busy=true;render();const original=button.textContent;button.textContent="Đang ghi nhận…";
    try{const result=await requestApi(`/interns/me/attendance/${action}`,{method:"POST",headers:getAuthHeader()});message.textContent=result.message;current=null;await load();}
    catch(error){message.textContent=error.message;Session.redirectIfExpired(error);try{await load();}catch{} }
    finally{busy=false;button.textContent=original;if(current)render();else{document.getElementById("checkInBtn").disabled=true;document.getElementById("checkOutBtn").disabled=true;}}
  });}
  document.getElementById("attendanceRefreshBtn").addEventListener("click",async event=>{
    if(!allowed||busy)return;const button=event.currentTarget;button.disabled=true;message.textContent="Đang tải chấm công…";
    try{await load();message.textContent="";}catch(error){message.textContent=error.message;}finally{button.disabled=false;}
  });
  (async()=>{if(!localStorage.getItem("token"))return;try{const session=await API.getCurrentUser();allowed=session.user.role==="INTERN"&&Session.hasPermission(session,"OWN_ATTENDANCE");if(!allowed){message.textContent="Chấm công dành cho thực tập sinh.";return;}await load();message.textContent="";}catch(error){message.textContent=error.message;Session.redirectIfExpired(error);}})();
})();
