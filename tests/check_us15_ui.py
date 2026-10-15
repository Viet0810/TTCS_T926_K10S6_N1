"""US15 browser regressions with a mock API; no persistent writes."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess

pages = Path(__file__).resolve().parents[1] / 'Frontend' / 'pages'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = '''<script>
let session={user:{role:ROLE},permissions:GRANTS},writes=[],reads=[],accept=false,emptyTasks=false,failReads=false,longText=false;
API.getCurrentUser=async()=>session;
AppUI.confirm=async()=>accept;
window.fetch=async(url,options={})=>{
 reads.push(url);let data;
 if(failReads)throw new TypeError('Test connection failure');
 if(options.method==='POST'){writes.push(JSON.parse(options.body));data={id:1};}
 else if(url.endsWith('/tasks/interns'))data=[{internId:12,fullName:'Intern A',email:'intern@company.vn'}];
 else data=[{id:1,internName:'Intern A',mentorName:'Mentor A',content:'Nội dung <script>alert(1)<ENDSCRIPT>',deadline:'2026-12-01T10:00:00+07:00',status:'ASSIGNED'}];
 if(url.endsWith('/tasks')&&!options.method){if(emptyTasks)data=[];else if(longText)data[0].content='Nội dung rất dài '.repeat(150);}
 return {ok:true,status:options.method==='POST'?201:200,json:async()=>data};
};
</script>'''.replace('<ENDSCRIPT>', '</scr'+'ipt>')
# Escape the mock task text so it cannot terminate the enclosing script element.
setup = setup.replace("alert(1)</script>'", "alert(1)<\\/script>'")
checks = '''<pre id="result"></pre><script>
(async()=>{const assert=(v,m)=>{if(!v)throw Error(m)},settle=()=>new Promise(r=>setTimeout(r,100));
try{await settle();ACTIONS;document.getElementById('result').textContent='PASS';}
catch(e){document.getElementById('result').textContent='FAIL '+e.message;}})();
</script>'''
cases = [
 ('MENTOR',['MENTOR_TASK_READ','TASK_CREATE'], '''
assert(!document.getElementById('createTaskSection').hidden,'Mentor create visible');
assert(document.getElementById('taskRows').textContent.includes('<script>')&&!document.querySelector('#taskRows script'),'task content safely rendered');
const form=document.getElementById('taskForm');document.getElementById('taskIntern').value='12';document.getElementById('taskContent').value='Báo cáo tiếng Việt';
const future=new Date(Date.now()+86400000);document.getElementById('taskDeadline').value=future.toISOString().slice(0,16);
form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();assert(writes.length===0,'cancel prevents save');
accept=true;form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();
assert(writes.length===1&&writes[0].internId===12&&writes[0].content==='Báo cáo tiếng Việt','confirmed create and double submit guard');
session.permissions=[];document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:session}));
assert(document.getElementById('createTaskSection').hidden&&document.getElementById('taskListSection').hidden&&!document.getElementById('taskRows').children.length,'revocation hides actions and data');
'''),
 ('MENTOR',['MENTOR_TASK_READ'], "assert(document.getElementById('createTaskSection').hidden&&!document.getElementById('taskListSection').hidden&&!reads.some(url=>url.endsWith('/interns')),'read only Mentor has no create action');"),
 ('INTERN',['OWN_TASK_READ'], "assert(document.getElementById('createTaskSection').hidden&&document.getElementById('taskRows').textContent.includes('Nội dung')&&document.getElementById('taskListTitle').textContent==='Nhiệm vụ của tôi','Intern sees task without create action');"),
 ('MENTOR',[], "assert(document.getElementById('createTaskSection').hidden&&document.getElementById('taskListSection').hidden&&reads.length===0,'missing permission calls no task API');"),
 ('MENTOR',['MENTOR_TASK_READ','TASK_CREATE'], '''
const reload=document.getElementById('refreshTasks');
assert(document.querySelector('.task-deadline').textContent==='01/12/2026 10:00','exact Vietnam deadline format');
assert(document.querySelector('.task-status').textContent==='Đã giao','status badge');
const columns=getComputedStyle(document.getElementById('taskForm')).gridTemplateColumns.split(' ').length;
assert(columns===(innerWidth<=640?1:2),'responsive form columns');
longText=true;const before=reads.length;reload.click();assert(reload.disabled&&document.getElementById('taskRows').getAttribute('aria-busy')==='true','loading clearly indicated');await settle();
assert(reads.length===before+2&&!reload.disabled,'reload completes');
const preview=document.querySelector('.task-content-preview');assert(preview.clientHeight<=Math.ceil(parseFloat(getComputedStyle(preview).lineHeight)*2)&&preview.scrollHeight>preview.clientHeight,'long content clamped to two lines');
assert(document.documentElement.scrollWidth<=innerWidth,'no horizontal page overflow');
assert(document.querySelector('.table-container').scrollWidth>document.querySelector('.table-container').clientWidth||innerWidth>640,'mobile scroll stays inside table');
emptyTasks=true;reload.click();await settle();assert(document.getElementById('taskRows').textContent.includes('Chưa có nhiệm vụ'),'empty state');
failReads=true;reload.click();reload.click();await settle();assert(document.getElementById('taskRows').textContent.includes('Không tải được'),'error state');
assert(document.querySelectorAll('.ui-toast.is-error').length===1,'one error toast after rapid reload');
reload.click();await settle();assert(document.querySelectorAll('.ui-toast.is-error').length===1,'retry does not duplicate toast');
failReads=false;emptyTasks=false;reload.click();await settle();assert(document.querySelector('.task-content-preview')&&!reload.disabled,'reload recovers from error');
'''),
]
with TemporaryDirectory(prefix='us15-ui-') as directory:
 temp=Path(directory)
 for i,(role,grants,actions) in enumerate(cases):
  html=(pages/'tasks.html').read_text(encoding='utf-8').replace('<head>','<head><base href="'+pages.as_uri()+'/"><script>localStorage.setItem("token","test");</script>')
  mock=setup.replace('ROLE',json.dumps(role)).replace('GRANTS',json.dumps(grants))
  html=html.replace('<script src="../js/navigation.js">',mock+'<script src="../js/navigation.js">')
  target=temp/f'{i}.html';target.write_text(html.replace('</body>',checks.replace('ACTIONS',actions)+'</body>'),encoding='utf-8')
  for width in (390,1440):
   result=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--user-data-dir='+str(temp/f'profile-{i}-{width}'),f'--window-size={width},1000','--virtual-time-budget=3000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=40)
   match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout,re.S);status=match[1] if match else 'NO RESULT'
   print(role,grants,width,status);assert status=='PASS',status
