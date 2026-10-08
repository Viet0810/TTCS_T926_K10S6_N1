"""Render the role matrix using permission values extracted from the backend."""
from pathlib import Path
from tempfile import TemporaryDirectory
import subprocess,re,json,os
repo=Path(__file__).resolve().parents[1]
source=(repo/'Backend/InternManagement/Services/RolePermissionService.cs').read_text(encoding='utf-8')
names=dict(re.findall(r'public const string (\w+) = "([A-Z_]+)"',source))
roles={role:[names[key] for key in re.findall(r'PermissionNames\.(\w+)',body)] for role,body in re.findall(r'\["(ADMIN|HR|MENTOR|INTERN)"\] =\s*\[(.*?)\]',source,re.S)}
root=repo/'Frontend/pages'
setup='<script>localStorage.setItem("token","mock");const roles='+json.dumps(roles)+';window.fetch=async()=>({ok:true,status:200,json:async()=>roles});</script>'
checks='''<pre id="result"></pre><script>(async()=>{try{const assert=(v,m)=>{if(!v)throw Error(m)};await new Promise(r=>setTimeout(r,30));const cards=[...document.querySelectorAll('.permission-role-card')];assert(cards.length===4,'four role cards');for(const card of cards){const role=card.querySelector('h3').textContent;assert(card.querySelector('.permission-count').textContent.startsWith(roles[role].length+' '),'real count '+role);assert(card.querySelectorAll('li').length===roles[role].length,'one row per permission');assert(card.querySelector('h3').classList.contains(role.toLowerCase()),'role color')}assert(document.querySelectorAll('.dashboard-header a[href="dashboard.html"]').length===1,'one dashboard action');assert(document.documentElement.scrollWidth<=innerWidth,'no horizontal scroll');const columns=getComputedStyle(document.querySelector('.permission-card-grid')).gridTemplateColumns.split(' ').length;assert(columns===(innerWidth>700?2:1),'responsive columns');document.getElementById('result').textContent='PASS'}catch(e){document.getElementById('result').textContent='FAIL '+e.message}})();</script>'''
with TemporaryDirectory() as temp:
 temp=Path(temp)
 html=(root/'permissions.html').read_text(encoding='utf-8').replace('<head>','<head><base href="'+root.as_uri()+'/">'+setup).replace('</body>',checks+'</body>')
 target=temp/'permissions.html';target.write_text(html,encoding='utf-8')
 for width in (320,390,768,1440):
  result=subprocess.run([os.environ.get('PROGRAMFILES','C:/Program Files')+'/Google/Chrome/Application/chrome.exe','--headless','--disable-gpu','--no-sandbox','--allow-file-access-from-files','--user-data-dir='+str(temp/str(width)),f'--window-size={width},1000','--virtual-time-budget=2000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=30)
  match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout,re.S);status=match[1] if match else 'NO RESULT';print(width,status);assert status=='PASS'
