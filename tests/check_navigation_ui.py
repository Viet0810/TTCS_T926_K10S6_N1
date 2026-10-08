"""Compare actual shared navigation on every authenticated HTML page for each backend role."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess

repo=Path(__file__).resolve().parents[1]; pages=repo/'Frontend/pages'
source=(repo/'Backend/InternManagement/Services/RolePermissionService.cs').read_text(encoding='utf-8')
names=dict(re.findall(r'public const string (\w+) = "([A-Z_]+)"',source))
roles={role:[names[key] for key in re.findall(r'PermissionNames\.(\w+)',body)] for role,body in re.findall(r'\["(ADMIN|HR|MENTOR|INTERN)"\] =\s*\[(.*?)\]',source,re.S)}
expected={
 'ADMIN':['dashboard.html','user-manage.html','intern-manage.html','hr-search-filter.html','profile.html','permissions.html'],
 'HR':['dashboard.html','intern-manage.html','hr-search-filter.html','mentor-assignment.html','mentor-assignment-list.html','profile.html','document-reviews.html','contracts.html','program-setting.html','attendance-report.html'],
 'MENTOR':['dashboard.html','intern-manage.html','hr-search-filter.html','profile.html'],
 'INTERN':['dashboard.html','profile.html','intern-schedule.html','intern-upload-cv.html','internship-profile.html','intern-contract.html','intern-attendance.html'],
}
chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='navigation-ui-') as directory:
 temp=Path(directory)
 for role,permissions in roles.items():
  targets=[]; case=temp/role;case.mkdir()
  for original in sorted(pages.glob('*.html')):
   html=original.read_text(encoding='utf-8')
   assert html.count('../js/navigation.js')==1,original.name+' must use one navigation script'
   assert len(re.findall(r'<nav\b[^>]*class="role-menu"',html))==1,original.name+' must have one nav'
   html=re.sub(r'<script src="([^"]+)"></script>',lambda m:m[0] if m[1] in ('../js/api.js','../js/navigation.js') else '',html)
   setup='<script>localStorage.setItem("token","test");window.navErrors=[];window.addEventListener("error",e=>navErrors.push(e.message));window.addEventListener("unhandledrejection",e=>navErrors.push(String(e.reason)));window.fetch=async()=>({ok:true,status:200,json:async()=>('+json.dumps({'user':{'role':role},'permissions':permissions})+')});</script>'
   html=html.replace('<head>','<head><base href="'+pages.as_uri()+'/">'+setup)
   target=case/original.name;target.write_text(html,encoding='utf-8');targets.append(target.as_uri())
  checks='''<pre id="result"></pre><script>(async()=>{try{
const expected=EXPECTED,targets=TARGETS,assert=(v,m)=>{if(!v)throw Error(m)};
const frames=targets.map(url=>{const frame=document.createElement('iframe');frame.style='display:block;width:100%;height:900px;border:0';frame.src=url;document.body.append(frame);return frame});
await Promise.all(frames.map(frame=>new Promise(resolve=>frame.onload=resolve)));await new Promise(r=>setTimeout(r,100));
for(const frame of frames){const doc=frame.contentDocument,name=frame.contentWindow.location.pathname.split('/').pop();
assert(frame.contentWindow.navErrors.length===0,name+' JS errors');
const links=[...doc.querySelectorAll('.role-menu a')];assert(JSON.stringify(links.map(a=>a.getAttribute('href')))===JSON.stringify(expected),name+' complete role menu');
const current=name==='user-create.html'?'user-manage.html':name==='change-password.html'?'profile.html':name;
assert(links.filter(a=>a.getAttribute('aria-current')==='page').length===(expected.includes(current)?1:0),name+' active count');
const active=links.find(a=>a.getAttribute('aria-current')==='page');if(active)assert(active.getAttribute('href')===current,name+' active target');
assert(doc.getElementById('roleBadge').textContent==='ROLE',name+' role badge');
assert(doc.querySelectorAll('.dashboard-session a[href="dashboard.html"]').length===1,name+' one dashboard action');
assert(doc.querySelectorAll('.dashboard-session a[href="change-password.html"]').length===1,name+' one password action');
assert(doc.getElementById('logoutBtn').previousElementSibling.getAttribute('href')==='change-password.html',name+' password immediately before logout');
assert(!doc.querySelector('.role-menu a[href="change-password.html"]'),name+' no password sidebar item');
frame.contentWindow.AppNavigation.render({user:{role:'ROLE'},permissions:PERMISSIONS});
assert(doc.querySelectorAll('.dashboard-session a[href="change-password.html"]').length===1,name+' repeated render has no duplicate');
frame.contentWindow.logoutCalls=0;frame.contentWindow.eval('Session.logout=()=>{window.logoutCalls++}');doc.getElementById('logoutBtn').click();assert(frame.contentWindow.logoutCalls===1,name+' one shared logout handler');
const header=doc.querySelector('.dashboard-header').getBoundingClientRect(),nav=doc.querySelector('.role-menu').getBoundingClientRect();
assert(header.left===0 && header.width===doc.documentElement.clientWidth,name+' full width topbar');
assert(nav.top>=header.bottom,name+' sidebar below topbar');assert(doc.documentElement.scrollWidth<=doc.documentElement.clientWidth,name+' page responsive width');assert(doc.querySelector('.role-menu').scrollWidth<=doc.querySelector('.role-menu').clientWidth,name+' sidebar no horizontal scroll');
frame.contentWindow.AppNavigation.render({user:{role:'ROLE',mustChangePassword:true},permissions:PERMISSIONS});
assert(!doc.querySelector('.role-menu'),name+' forced password change hides business navigation');
assert(doc.getElementById('logoutBtn').previousElementSibling.getAttribute('href')==='change-password.html',name+' forced password change retains account actions');
}
document.getElementById('result').textContent='PASS '+frames.length+' pages';
}catch(e){document.getElementById('result').textContent='FAIL '+e.message}})();</script>'''.replace('EXPECTED',json.dumps(expected[role])).replace('TARGETS',json.dumps(targets)).replace('ROLE',role).replace('PERMISSIONS',json.dumps(permissions))
  parent=case/'test.html';parent.write_text('<!doctype html><html><head><style>body{margin:0}</style></head><body>'+checks+'</body></html>',encoding='utf-8')
  for width in (320,390,768,1440):
   output=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--allow-file-access-from-files','--user-data-dir='+str(case/str(width)),'--virtual-time-budget=5000',f'--window-size={width},1000','--dump-dom',parent.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=45)
   result=re.search(r'<pre id="result">(.*?)</pre>',output.stdout,re.S);status=result[1] if result else 'NO RESULT';print(role,width,status);assert status.startswith('PASS'),status
