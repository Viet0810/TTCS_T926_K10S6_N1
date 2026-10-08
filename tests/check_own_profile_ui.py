"""Own profile UI: role isolation, inline validation, cancel, safe payload and save."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess
repo = Path(__file__).resolve().parents[1]
pages = repo / 'Frontend/pages'
setup = '''<script>
localStorage.setItem('token','test');const role=ROLE;let writes=0,reads=0,payload;
let profile={id:7,fullName:'Intern <b>A</b>',email:'a@ictu.edu.vn',phone:'0912345678',school:'ICTU',major:'IT',address:'Hanoi'};
window.fetch=async(url,options={})=>{await new Promise(r=>setTimeout(r,10));let data;
if(url.endsWith('/auth/me'))data={user:{fullName:'Account',email:profile.email,username:profile.email,role},permissions:['VIEW_PROFILE']};
else if(url.endsWith('/interns/me')){reads++;if(options.method==='PUT'){writes++;payload=JSON.parse(options.body);profile={...profile,...payload};}data=profile;}
else throw Error('Unexpected API');return{ok:true,status:200,json:async()=>structuredClone(data)};};
</script>'''
actions = '''
assert(!document.getElementById('ownProfile').hidden,'Intern profile visible');
assert(document.getElementById('ownProfileDetails').textContent.includes('Intern <b>A</b>')&&!document.querySelector('#ownProfileDetails b'),'safe rendering');
const edit=document.getElementById('editProfileBtn'),form=document.getElementById('ownProfileForm'),name=document.getElementById('ownFullName'),phone=document.getElementById('ownPhone');
edit.click();assert(!form.hidden,'edit form opens');name.value='Discard';document.getElementById('cancelProfileBtn').click();edit.click();assert(name.value===profile.fullName,'cancel discards edits');
phone.value='0123456789';form.dispatchEvent(new Event('submit',{cancelable:true}));assert(writes===0&&phone.getAttribute('aria-invalid')==='true'&&!document.getElementById('ownPhoneError').hidden,'phone inline validation prevents API');
phone.value='0987654321';phone.dispatchEvent(new Event('input'));assert(phone.getAttribute('aria-invalid')==='false','input clears error');
name.value='  Updated name  ';form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));assert(document.getElementById('saveProfileBtn').disabled,'loading disables save');await settle();
assert(writes===1&&form.hidden&&document.getElementById('profileName').textContent==='Updated name','one successful save updates view');
assert(Object.keys(payload).sort().join(',')==='address,fullName,major,phone,school','only editable fields sent');
assert(document.getElementById('profileEmail').textContent==='a@ictu.edu.vn'&&!form.querySelector('[name=email]'),'email readonly');
assert(document.getElementById('profileMessage').textContent.includes('thành công'),'success feedback');
'''
chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='own-profile-') as directory:
    temp=Path(directory)
    for role in ('INTERN','ADMIN','HR','MENTOR'):
        html=(pages/'profile.html').read_text(encoding='utf-8').replace('<head>','<head><base href="'+pages.as_uri()+'/">'+setup.replace('ROLE',json.dumps(role)))
        checks='''<pre id="result"></pre><script>(async()=>{try{const assert=(v,m)=>{if(!v)throw Error(m)},settle=()=>new Promise(r=>setTimeout(r,100));await settle();ACTIONS;assert(document.documentElement.scrollWidth<=innerWidth,'no overflow');document.getElementById('result').textContent='PASS';}catch(e){document.getElementById('result').textContent='FAIL '+e.message;}})();</script>'''.replace('ACTIONS',actions if role=='INTERN' else "assert(document.getElementById('ownProfile').hidden&&reads===0&&writes===0,'other roles keep account profile only');")
        target=temp/(role+'.html');target.write_text(html.replace('</body>',checks+'</body>'),encoding='utf-8')
        for width in (390,1440):
            result=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--user-data-dir='+str(temp/f'{role}-{width}'),f'--window-size={width},1000','--virtual-time-budget=3000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=40)
            match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout,re.S);status=match[1] if match else 'NO RESULT';print(role,width,status);assert status=='PASS',status
