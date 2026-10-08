"""Contract and own attendance UI checks with isolated API fixtures."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess
repo=Path(__file__).resolve().parents[1];pages=repo/'Frontend/pages'
setup='''<script>
localStorage.setItem('token','test');const requests=[];let writes=0,confirmed=false,checkedIn=false,checkedOut=false,failReads=false;
const role=ROLE;const contract={internId:12,fullName:'Intern <b>A</b>',email:'a@ictu.edu.vn',fileName:'contract.pdf',uploadedAt:'2026-10-09T00:00:00',confirmedAt:null,version:'AAAAAAAAAAA='};
window.fetch=async(url,options={})=>{requests.push({url,options});await new Promise(r=>setTimeout(r,0));if(failReads&&url.endsWith('/attendance'))throw new TypeError('offline');let data;
if(url.endsWith('/auth/me'))data={user:{role},permissions:role==='HR'?['VIEW_PROFILE','MANAGE_CONTRACTS']:role==='INTERN'?['VIEW_PROFILE','VIEW_OWN_CONTRACT','OWN_ATTENDANCE']:[]};
else if(url.endsWith('/interns'))data=[{id:12,fullName:'Intern A',email:'a@ictu.edu.vn'}];
else if(options.method==='PUT'){writes++;if(url.endsWith('/confirm')){const body=JSON.parse(options.body);if(body.version!==contract.version)throw Error('version missing');confirmed=true;contract.confirmedAt='2026-10-09T01:00:00';}else{if(!(options.body instanceof FormData)||options.headers['Content-Type'])throw Error('invalid multipart');}data={message:'Saved'};}
else if(options.method==='POST'){writes++;if(url.endsWith('check-in'))checkedIn=true;else checkedOut=true;data={message:'Saved'};}
else if(url.endsWith('/attendance'))data={date:'2026-10-09',checkIn:checkedIn?'08:00:00':null,checkOut:checkedOut?'17:00:00':null,hours:checkedOut?9:0,status:checkedIn?'ON_TIME':null};
else if(url.endsWith('/contracts'))data=[contract];
else if(url.endsWith('/contract'))data=contract;
else return{ok:true,status:200,blob:async()=>new Blob(['%PDF-1.4'])};
return{ok:true,status:200,json:async()=>structuredClone(data)};};
HTMLAnchorElement.prototype.click=function(){window.downloaded=this.download==='contract.pdf'};
</script>'''
cases=[('contracts.html','HR','''
assert(!document.getElementById('contractForm').hidden,'HR upload form');assert(document.getElementById('contractRows').textContent.includes('Intern <b>A</b>')&&!document.querySelector('#contractRows b'),'safe real data');
document.getElementById('contractInternId').value='12';const transfer=new DataTransfer();transfer.items.add(new File(['%PDF-1.4'],'contract.pdf',{type:'application/pdf'}));document.getElementById('contractFile').files=transfer.files;
const form=document.getElementById('contractForm');form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();assert(writes===1&&!form.querySelector('[type=submit]').disabled,'upload and duplicate guard');
document.querySelector('#contractRows button').click();await settle();assert(window.downloaded,'HR contract download');
'''),('intern-contract.html','INTERN','''
assert(!document.getElementById('contractDetails').hidden,'own contract visible');document.getElementById('contractDownloadBtn').click();await settle();assert(window.downloaded,'own download');
document.getElementById('confirmContractBtn').click();assert(writes===0,'explicit agreement required');document.getElementById('acceptContract').checked=true;
document.getElementById('confirmContractBtn').click();document.getElementById('confirmContractBtn').click();await settle();assert(writes===1&&confirmed&&document.getElementById('confirmContractBtn').disabled,'confirmation persisted and locked');
assert(requests.some(r=>r.url.endsWith('/interns/me/contract/confirm')&&JSON.parse(r.options.body).version),'version and own route');
'''),('intern-attendance.html','INTERN','''
const start=document.getElementById('checkInBtn'),end=document.getElementById('checkOutBtn');assert(!start.disabled&&end.disabled,'initial attendance controls');start.click();start.click();await settle();assert(writes===1&&start.disabled&&!end.disabled,'check-in locks duplicate');end.click();await settle();assert(writes===2&&end.disabled&&document.getElementById('attendanceOut').textContent==='17:00:00','check-out reload');
assert(requests.filter(r=>r.options.method==='POST').every(r=>!r.options.body),'client supplies no owner or time');
checkedIn=false;checkedOut=false;document.getElementById('attendanceRefreshBtn').click();await settle();failReads=true;start.click();await settle();assert(start.disabled&&end.disabled,'saved check-in with failed reload keeps stale controls locked');failReads=false;document.getElementById('attendanceRefreshBtn').click();await settle();assert(start.disabled&&!end.disabled,'refresh recovers persisted check-in');
''')]
for page in ('contracts.html','intern-contract.html','intern-attendance.html'):
 for role in ('ADMIN','MENTOR'):
  cases.append((page,role,"assert(writes===0&&!requests.some(r=>r.url.includes('/contracts')||r.url.includes('/interns/me/')),'wrong role calls no business API');"))
cases += [('contracts.html','INTERN',"assert(document.getElementById('contractForm').hidden&&writes===0,'Intern cannot manage contracts');"),('intern-contract.html','HR',"assert(document.getElementById('contractDetails').hidden&&writes===0,'HR cannot confirm own Intern contract');"),('intern-attendance.html','HR',"assert(document.getElementById('checkInBtn').disabled&&writes===0,'HR cannot use Intern attendance');")]
chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='sprint2-completion-') as directory:
 temp=Path(directory)
 for i,(page,role,actions) in enumerate(cases):
  html=(pages/page).read_text(encoding='utf-8').replace('<head>','<head><base href="'+pages.as_uri()+'/">'+setup.replace('ROLE',json.dumps(role)))
  checks='''<pre id="result"></pre><script>(async()=>{try{const assert=(v,m)=>{if(!v)throw Error(m)},settle=()=>new Promise(r=>setTimeout(r,100));await settle();ACTIONS;assert(document.documentElement.scrollWidth<=innerWidth,'no page overflow');document.getElementById('result').textContent='PASS';}catch(e){document.getElementById('result').textContent='FAIL '+e.message;}})();</script>'''.replace('ACTIONS',actions)
  target=temp/(str(i)+'.html');target.write_text(html.replace('</body>',checks+'</body>'),encoding='utf-8')
  for width in (390,1440):
   result=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--user-data-dir='+str(temp/f'{i}-{width}'),f'--window-size={width},1000','--virtual-time-budget=3000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=40)
   match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout,re.S);status=match[1] if match else 'NO RESULT';print(page,role,width,status);assert status=='PASS',status
