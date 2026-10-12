"""Role dashboard metrics use API responses and respect current grants."""
from pathlib import Path
import subprocess, sys
repo = Path(__file__).resolve().parents[1]
setup = '''<script>
localStorage.setItem('token','fixture');const calls=[];
const session={user:{role:'HR',fullName:'HR fixture'},permissions:['VIEW_INTERNS','DOCUMENT_REVIEW_READ']};
window.fetch=async(url)=>{calls.push(url);let data;
if(url.endsWith('/auth/me'))data=session;
else if(url.endsWith('/document-reviews'))data=[{status:'pending'},{status:'approved'},{status:'approved'},{status:'rejected'}];
else if(url.endsWith('/users'))data=[{role:'ADMIN'},{role:'HR'},{role:'MENTOR'},{role:'INTERN'},{role:'INTERN'}];
else if(url.endsWith('/interns/me/documents'))data=[{status:'pending'},{status:'approved'}];
else if(url.endsWith('/interns/me'))data={fullName:'Intern fixture'};
else if(url.endsWith('/interns'))data=[{id:1},{id:2},{id:3}];
else throw Error('Unexpected dashboard API '+url);
return{ok:true,status:200,json:async()=>data};};
</script>'''
checks = '''<pre id="result"></pre><script>(async()=>{try{
const assert=(v,m)=>{if(!v)throw Error(m)},tick=()=>new Promise(r=>setTimeout(r,0));
for(let i=0;i<12;i++)await tick();
const target=document.getElementById('dashboardKpis'),values=()=>[...target.querySelectorAll('strong')].map(n=>n.textContent);
assert(JSON.stringify(values())===JSON.stringify(['3','1','2','1']),'HR metrics must match real response counts');
target.replaceChildren();await loadDashboardMetrics({user:{role:'ADMIN'},permissions:['MANAGE_USERS']});
assert(JSON.stringify(values())===JSON.stringify(['5','1','1','2']),'Admin account metrics count returned roles');
target.replaceChildren();await loadDashboardMetrics({user:{role:'INTERN',fullName:'Intern fixture'},permissions:['VIEW_OWN_PROFILE','VIEW_DOCUMENTS']});
assert(JSON.stringify(values())===JSON.stringify(['Intern fixture','2','1','1']),'Intern metrics only use own profile and documents');
const before=calls.length;target.replaceChildren();await loadDashboardMetrics({user:{role:'MENTOR'},permissions:['VIEW_INTERNS']});
assert(calls.length===before&&!target.querySelector('strong'),'Mentor must not invent assigned or task counts');
target.replaceChildren();await loadDashboardMetrics({user:{role:'HR'},permissions:[]});
assert(calls.length===before&&!target.querySelector('strong'),'revoked permissions never request protected metrics');
document.getElementById('result').textContent='PASS: dashboard API counts and revoked grants';
}catch(e){document.getElementById('result').textContent='FAIL '+e.message}})();</script>'''
# RUN_BROWSER
if __name__ == '__main__':
    subprocess.run([sys.executable,str(repo/'tests/check_sprint1_viewports.py'),'--dashboard'],check=True)
