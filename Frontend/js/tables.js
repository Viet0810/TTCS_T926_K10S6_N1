window.TableUI = (() => {
  const states = new WeakMap();
  function controls(body, change) {
    if (states.has(body)) return states.get(body);
    const island=body.closest('.table-container')||body.closest('table').parentElement;
    island.classList.add('table-island');body.closest('table').classList.add('list-table');
    const bar=document.createElement('div');bar.className='table-pagination';
    const summary=document.createElement('span');summary.className='pagination-summary';summary.setAttribute('aria-live','polite');
    const nav=document.createElement('nav');nav.setAttribute('aria-label','Phân trang danh sách');bar.append(summary,nav);island.after(bar);
    const state={page:1,pageSize:10,total:0,bar,summary,nav,change};states.set(body,state);return state;
  }
  function draw(state) {
    const pages=Math.ceil(state.total/state.pageSize);state.page=Math.max(1,Math.min(state.page,Math.max(1,pages)));
    const first=state.total?(state.page-1)*state.pageSize+1:0,last=Math.min(state.page*state.pageSize,state.total);
    state.summary.textContent=`Hiển thị ${first}–${last} / ${state.total}`;state.nav.replaceChildren();
    function button(label,page,disabled=false) {
      const b=document.createElement('button');b.type='button';b.textContent=label;b.disabled=disabled||state.busy;
      b.setAttribute('aria-label',label==='‹'?'Trang trước':label==='›'?'Trang sau':`Trang ${page}`);
      if(page===state.page&&label!=='‹'&&label!=='›'){b.className='is-active';b.setAttribute('aria-current','page');}
      b.onclick=()=>{if(page===state.page)return;state.page=page;state.change(page);};state.nav.append(b);
    }
    button('‹',state.page-1,state.page<=1);
    const radius=innerWidth<=560?0:1;
    const numbers=[...new Set([1,...Array.from({length:radius*2+1},(_,i)=>state.page+i-radius),pages])].filter(p=>p>=1&&p<=pages).sort((a,b)=>a-b);
    let previous=0;for(const page of numbers){if(previous&&page-previous>1){const dots=document.createElement('span');dots.textContent='…';state.nav.append(dots);}button(String(page),page);previous=page;}
    button('›',state.page+1,!pages||state.page>=pages);state.bar.hidden=!state.total;
  }
  function decorate(body) {
    for(const row of body.rows) for(const cell of row.cells) {
      if(cell.colSpan>1||cell.querySelector('button,a,input'))continue;
      cell.title=cell.textContent.trim();
      if(!cell.children.length){const text=document.createElement('span');text.className='table-cell-text';text.textContent=cell.textContent;cell.replaceChildren(text);}
    }
  }
  function client(body) {
    const state=controls(body,()=>apply());
    function apply(){
      const rows=state.rows||[];state.total=rows.length;draw(state);rows.forEach((row,i)=>row.hidden=i<(state.page-1)*10||i>=state.page*10);
      const empty=body.querySelector('.pagination-empty');
      if(rows.length)empty?.remove();
      else if(state.all?.length&&!empty){const row=body.insertRow();row.className='pagination-empty';const cell=row.insertCell();cell.colSpan=body.closest('table').tHead.rows[0].cells.length;cell.className='empty-state';cell.textContent='Không có dữ liệu phù hợp.';}
    }
    function resetRows(){
      state.rows=state.all.filter(row=>!state.query||row.textContent.toLocaleLowerCase('vi').includes(state.query));
      state.all.forEach(row=>row.hidden=true);state.page=1;apply();
    }
    function refresh(){decorate(body);state.all=[...body.rows].filter(row=>row.cells.length>1&&!row.querySelector('.ui-skeleton'));resetRows();}
    const observer=new MutationObserver(()=>{observer.disconnect();refresh();observer.observe(body,{childList:true});});
    refresh();observer.observe(body,{childList:true});
    return {filter(text){state.query=text.trim().toLocaleLowerCase('vi');resetRows();}};
  }
  function server(body,request,render) {
    body.dataset.serverPaging='true';let revision=0;
    const state=controls(body,()=>load());
    async function load(reset=false) {
      if(reset)state.page=1;const current=++revision;state.busy=true;draw(state);
      const done=AppUI.loading(body,body.closest('table').tHead.rows[0].cells.length);
      try {
        const response=await request(state.page,state.pageSize);
        if(current!==revision)return;
        const data=Array.isArray(response)?{items:response,page:1,totalItems:response.length}:response;
        state.page=data.page;state.total=data.totalItems;render(data.items);decorate(body);
      }catch(error){
        if(current!==revision)return;state.total=0;body.replaceChildren();const cell=body.insertRow().insertCell();cell.colSpan=body.closest('table').tHead.rows[0].cells.length;cell.className='empty-state';cell.textContent='Không thể tải danh sách. Vui lòng thử lại.';throw error;
      }finally{if(current===revision){state.busy=false;done();draw(state);}}
    }
    state.change=()=>load().catch(error=>AppUI.toast(error.message,'error'));return {load,state};
  }
  const clients=new WeakMap();
  document.addEventListener('DOMContentLoaded',()=>{
    document.querySelectorAll('.table-container tbody,.program-table-wrapper tbody').forEach(body=>{if(!body.dataset.serverPaging){const instance=client(body);clients.set(body,instance);}});
  });
  return {server,filter(body,text){clients.get(body)?.filter(text);}};
})();
