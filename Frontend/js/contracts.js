(() => {
  const hr = !!document.getElementById("contractForm");
  const message = document.getElementById("contractMessage");
  let current = null, busy = false, allowed = false;
  const date = value => value ? new Date(value + (/[Z+]/.test(value) ? "" : "Z")).toLocaleString("vi-VN") : "Chưa xác nhận";
  const notice = text => { message.textContent = text; };
  async function download(item, button) {
    button.disabled = true;
    try { saveDownload(await requestApi(hr ? `/contracts/${item.internId}/file` : "/interns/me/contract/file", {headers:getAuthHeader()}, "blob"), item.fileName); }
    catch(error) { notice(error.message); Session.redirectIfExpired(error); }
    finally { button.disabled = false; }
  }
  async function load() {
    if (!allowed) return;
    if (hr) {
      const items = await requestApi("/contracts", {headers:getAuthHeader()});
      const rows = document.getElementById("contractRows"); rows.replaceChildren();
      if (!items.length) {const cell=rows.insertRow().insertCell();cell.colSpan=5;cell.className="empty-state";cell.textContent="Chưa có hợp đồng.";}
      for (const item of items) {
        const row=rows.insertRow();
        for (const text of [item.fullName,item.email,item.fileName,date(item.confirmedAt)]) {const cell=row.insertCell();cell.textContent=text;cell.title=text;}
        const button=document.createElement("button");button.type="button";button.className="btn-secondary btn-compact";button.textContent="Tải để xem";button.onclick=()=>download(item,button);row.insertCell().append(button);
      }
    } else {
      current = await requestApi("/interns/me/contract", {headers:getAuthHeader()});
      document.getElementById("contractDetails").hidden = !current;
      if(!current){notice("Chưa có hợp đồng thực tập.");return;}
      document.getElementById("contractFileName").textContent=current.fileName;
      document.getElementById("contractFileName").title=current.fileName;
      document.getElementById("contractUploaded").textContent=date(current.uploadedAt);
      document.getElementById("contractConfirmed").textContent=date(current.confirmedAt);
      document.getElementById("confirmContractBtn").disabled=!!current.confirmedAt;
    }
  }
  const form=document.getElementById("contractForm");
  form?.addEventListener("submit",async event=>{
    event.preventDefault();if(busy||!allowed||!form.reportValidity())return;
    const input=document.getElementById("contractFile"),file=input.files[0];
    if(!file||!file.name.toLowerCase().endsWith(".pdf")||!file.size||file.size>5*1024*1024){notice("Vui lòng chọn PDF có nội dung, tối đa 5 MB.");return;}
    busy=true;const button=form.querySelector('[type=submit]');button.disabled=input.disabled=true;button.textContent="Đang tải lên…";
    try{const body=new FormData();body.append("file",file);await requestApi(`/contracts/${encodeURIComponent(document.getElementById("contractInternId").value)}`,{method:"PUT",headers:{Authorization:getAuthHeader().Authorization},body});form.reset();document.getElementById("contractSelected").textContent="";notice("Đã lưu hợp đồng.");await load();}
    catch(error){notice(error.message);Session.redirectIfExpired(error);}
    finally{busy=false;button.disabled=input.disabled=false;button.textContent="Tải lên hợp đồng";}
  });
  const fileInput=document.getElementById("contractFile");
  fileInput?.addEventListener("change",()=>{const file=fileInput.files[0];document.getElementById("contractSelected").textContent=file?`${file.name} · ${(file.size/1024/1024).toFixed(2)} MB`:"";});
  document.getElementById("contractDownloadBtn")?.addEventListener("click",event=>{if(current&&allowed)download(current,event.currentTarget);});
  document.getElementById("confirmContractBtn")?.addEventListener("click",async event=>{
    if(!allowed||busy||!current||current.confirmedAt||!document.getElementById("acceptContract").checked){notice("Vui lòng đọc và đánh dấu đồng ý trước khi xác nhận.");return;}
    busy=true;const button=event.currentTarget;button.disabled=true;button.textContent="Đang xác nhận…";
    try{await requestApi("/interns/me/contract/confirm",{method:"PUT",headers:getAuthHeader(),body:JSON.stringify({version:current.version})});current.confirmedAt=new Date().toISOString();notice("Đã xác nhận hợp đồng.");await load();}
    catch(error){notice(error.message);Session.redirectIfExpired(error);}
    finally{busy=false;button.disabled=!!current?.confirmedAt;button.textContent="Xác nhận hợp đồng";}
  });
  document.getElementById("contractRefreshBtn").addEventListener("click",async event=>{
    if(busy||!allowed)return;const button=event.currentTarget;button.disabled=true;notice("Đang tải hợp đồng…");
    try{await load();if(hr||current)notice("Đã tải lại hợp đồng.");}catch(error){notice(error.message);}finally{button.disabled=false;}
  });
  (async()=>{
    if(!localStorage.getItem("token"))return;
    try{
      const session=await API.getCurrentUser();allowed=session.user.role===(hr?"HR":"INTERN")&&Session.hasPermission(session,hr?"MANAGE_CONTRACTS":"VIEW_OWN_CONTRACT");
      if(!allowed){notice("Bạn không có quyền truy cập hợp đồng.");return;}
      if(hr){const interns=await API.getInterns();const select=document.getElementById("contractInternId");for(const intern of interns)select.add(new Option(`${intern.fullName} (${intern.email})`,intern.id));form.hidden=false;form.querySelector('[type=submit]').disabled=!interns.length;}
      await load();if(hr||current)notice("");
    }catch(error){notice(error.message);Session.redirectIfExpired(error);}
  })();
})();
