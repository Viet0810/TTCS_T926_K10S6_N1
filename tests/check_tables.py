"""Browser checks for bounded pagination, filtering and table overflow islands."""
from pathlib import Path
from tempfile import TemporaryDirectory
import os,re,subprocess
root=Path(__file__).resolve().parents[1]/'Frontend'
chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
script=r'''
(async()=>{const assert=(ok,label)=>{if(!ok)throw Error(label)};const tick=()=>new Promise(r=>setTimeout(r,30));
try{
const body=document.getElementById('internTableBody');const all=Array.from({length:157},(_,i)=>({id:i+1,name:'Intern '+(i+1)}));let search='',requests=[];
const pager=TableUI.server(body,async(page,pageSize)=>{requests.push({page,pageSize});const list=all.filter(item=>item.name.includes(search));return{items:list.slice((page-1)*pageSize,page*pageSize),page,pageSize,totalItems:list.length};},items=>{
body.replaceChildren();for(const item of items){const row=body.insertRow();for(const text of [item.id,item.name,'long.email@custom-domain.example','0912345678','University with a long full institution name','Computer science and information systems','Software company','Active'])row.insertCell().textContent=text;const cell=row.insertCell();const button=document.createElement('button');button.textContent='Edit profile';cell.append(button);}});
await pager.load();assert(body.rows.length===10,'server renders only ten rows');
const nav=body.closest('.table-container').nextElementSibling;
assert(nav.textContent.includes('157')&&nav.textContent.includes('…'),'totals and ellipsis');
nav.querySelector('[aria-label="Trang sau"]').click();await tick();assert(body.rows[0].cells[0].textContent==='11','next page fetches bounded data');
search='Intern 157';await pager.load(true);assert(pager.state.page===1&&body.rows.length===1&&nav.textContent.includes('/ 1'),'search resets page and total');
search='Missing';await pager.load(true);assert(body.rows.length===0&&nav.hidden,'empty result resets pagination');
search='';await pager.load(true);
const island=body.closest('.table-container');assert(island.scrollWidth>island.clientWidth,'wide intern columns scroll inside island');
assert(document.documentElement.scrollWidth<=document.documentElement.clientWidth,'no horizontal page overflow');
const email=body.rows[0].cells[2];assert(email.title.includes('custom-domain'),'full text tooltip');
const action=body.rows[0].cells[8];assert(action.querySelector('button').getBoundingClientRect().width<=action.getBoundingClientRect().width,'action button fits column');
const client=document.getElementById('clientRows');for(let i=0;i<37;i++){const row=client.insertRow();row.insertCell().textContent='Contract '+i;row.insertCell().textContent=i%2?'odd':'even';}await tick();
assert([...client.rows].filter(row=>!row.hidden).length===10,'client pagination shows ten rows');
const clientNav=client.closest('.table-container').nextElementSibling;clientNav.querySelector('[aria-label="Trang sau"]').click();assert([...client.rows].filter(row=>!row.hidden)[0].textContent.includes('Contract 10'),'client next page');
TableUI.filter(client,'Contract 36');assert([...client.rows].filter(row=>!row.hidden).length===1&&clientNav.textContent.includes('/ 1'),'client filter resets page');
TableUI.filter(client,'');assert([...client.rows].filter(row=>!row.hidden).length===10,'client filter clearing restores first page');
assert(requests.every(item=>item.pageSize===10),'bounded server page size');
document.getElementById('result').textContent='PASS: server/client paging, ellipsis, filter reset, tooltips, actions and overflow island';
}catch(error){document.getElementById('result').textContent='FAIL: '+error.message}})();
'''
with TemporaryDirectory(prefix='intern-tables-') as directory:
 temp=Path(directory)
 for width in [390,768,1440]:
  page=temp/f'{width}.html'
  styles=''.join(f'<link rel="stylesheet" href="{(root/"css"/name).as_uri()}">' for name in ['base.css','style.css','ui.css','tables.css'])
  js=''.join(f'<script src="{(root/"js"/name).as_uri()}"></script>' for name in ['ui.js','tables.js'])
  page.write_text(f'<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">{styles}</head><body><main style="max-width:100%;padding:16px"><section class="card"><div class="table-container"><table><thead><tr>'+''.join('<th>'+label+'</th>' for label in ['Code','Full name','Email','Phone','School','Major','Company','Status','Actions'])+f'</tr></thead><tbody id="internTableBody"></tbody></table></div></section><section class="card"><div class="table-container"><table><thead><tr><th>Contract</th><th>Status</th></tr></thead><tbody id="clientRows"></tbody></table></div></section></main><pre id="result"></pre>{js}<script>{script}</script></body></html>',encoding='utf-8')
  result=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox',f'--user-data-dir={temp/str(width)}',f'--window-size={width},900','--virtual-time-budget=2000','--dump-dom',page.as_uri()],capture_output=True,timeout=35)
  match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout.decode('utf-8',errors='replace'),re.S)
  assert match and match.group(1).startswith('PASS:'),(width,match.group(1) if match else 'No browser result')
  print(width,match.group(1))
