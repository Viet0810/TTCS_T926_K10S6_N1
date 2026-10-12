"""Change-password and forced-login UI checks; API responses are mocked."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess

repo = Path(__file__).resolve().parents[1]
root = repo / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = '''<script>
localStorage.setItem('token','old');let requests=[],errors=[],mode='current-error',navigation=null;const role=ROLE,forced=FORCED;
window.recordNavigation=value=>navigation=value;
window.addEventListener('error',e=>errors.push(e.message));window.addEventListener('unhandledrejection',e=>errors.push(String(e.reason)));
window.fetch=async(url,options={})=>{requests.push({url,options});await new Promise(r=>setTimeout(r,0));
let ok=true,status=200,data={user:{role,fullName:'Test',mustChangePassword:forced},permissions:['VIEW_PROFILE'],token:'old'};
if(url.endsWith('/account/change-password')){if(mode==='current-error'){ok=false;status=400;data={code:'INVALID_CURRENT_PASSWORD',message:'Mật khẩu hiện tại không đúng.'}}else data={token:'new',user:{role,mustChangePassword:false}};}
return {ok,status,json:async()=>data};};
</script>'''
form_checks = '''
const form=document.getElementById('changePasswordForm'),fields=['currentPassword','newPassword','confirmPassword'].map(id=>document.getElementById(id));
const fill=(a,b,c)=>fields.forEach((input,i)=>input.value=[a,b,c][i]);const submit=()=>form.dispatchEvent(new Event('submit',{cancelable:true}));
assert(!form.hidden,'form available to authenticated role');
assert(document.getElementById('backToProfile').hidden===forced,'forced account cannot return to profile');
fill('Current-test-9','weak','weak');submit();assert(!document.getElementById('newPasswordError').hidden && !requests.some(r=>r.options.method==='PUT'),'weak password is inline and never sent');
fill('Current-test-9','Current-test-9','Current-test-9');submit();assert(!document.getElementById('newPasswordError').hidden,'same password rejected');
fill('Current-test-9','New-test-9','Mismatch');submit();assert(!document.getElementById('confirmPasswordError').hidden,'confirmation inline');
fill('Current-test-9','New-test-9','New-test-9');submit();submit();assert(form.querySelector('[type=submit]').disabled,'submit loading');await tick();await tick();
assert(requests.filter(r=>r.options.method==='PUT').length===1,'duplicate submit guarded');assert(!document.getElementById('currentPasswordError').hidden && !form.querySelector('[type=submit]').disabled,'server error maps to current field and restores controls');
fields[0].dispatchEvent(new Event('input'));assert(document.getElementById('currentPasswordError').hidden,'editing clears stale error');
document.querySelector('[data-password-toggle=currentPassword]').click();assert(fields[0].type==='text','password visibility toggle');
mode='success';submit();await tick();await tick();assert(localStorage.getItem('token')==='new' && navigation==='dashboard.html','successful change saves replacement session and enters dashboard');
const payload=JSON.parse(requests.filter(r=>r.options.method==='PUT').at(-1).options.body);assert(payload.currentPassword==='Current-test-9' && payload.newPassword==='New-test-9' && payload.confirmPassword==='New-test-9' && !('userId' in payload),'own password contract');
assert(document.documentElement.scrollWidth<=innerWidth,'responsive width');
'''
login_checks = '''
document.getElementById('username').value='test@gmail.com';document.getElementById('password').value='Current-test-9';document.getElementById('loginForm').dispatchEvent(new Event('submit',{cancelable:true}));await tick();await tick();
assert(navigation===(forced?'pages/change-password.html':'pages/dashboard.html'),'login selects correct destination');
if(forced){let denied=false;try{await API.getCurrentUser()}catch(e){denied=e.code==='PASSWORD_CHANGE_REQUIRED'}assert(denied && navigation==='pages/change-password.html','server session flag guards direct dashboard access');}
'''
with TemporaryDirectory(prefix='change-password-ui-') as directory:
    temp = Path(directory)
    for role in ('ADMIN', 'HR', 'MENTOR', 'INTERN'):
        forced = role in ('HR', 'MENTOR')
        for page, checks in [('pages/change-password.html', form_checks), ('index.html', login_checks)]:
            original = root / page
            html = original.read_text(encoding='utf-8')
            def inline(match):
                source = (original.parent / match[1]).resolve().read_text(encoding='utf-8')
                source = source.replace('location.replace(', 'window.recordNavigation(')
                source = source.replace('location.href = ', 'navigation = ')
                return '<script>' + source + '</script>'
            html = re.sub(r'<script src="([^"]+)"></script>', inline, html)
            preamble = setup.replace('ROLE',json.dumps(role)).replace('FORCED',str(forced).lower())
            html = html.replace('<head>','<head><base href="'+original.parent.as_uri()+'/">'+preamble)
            injected = '<pre id="result"></pre><script>(async()=>{try{const assert=(v,m)=>{if(!v)throw Error(m)},tick=()=>new Promise(r=>setTimeout(r,10));await new Promise(r=>setTimeout(r,60));'+checks+';assert(errors.length===0,errors.join(";"));document.getElementById("result").textContent="PASS"}catch(e){document.getElementById("result").textContent="FAIL: "+e.message}})();</script>'
            case = temp / (role + original.stem);case.mkdir(); target=case/original.name
            target.write_text(html.replace('</body>',injected+'</body>'),encoding='utf-8')
            for width in (390,1440):
                output = subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--allow-file-access-from-files','--user-data-dir='+str(case/str(width)),'--virtual-time-budget=5000',f'--window-size={width},1000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=30)
                result = re.search(r'<pre id="result">(.*?)</pre>',output.stdout,re.S)
                status=result[1] if result else 'NO RESULT';print(role,original.name,width,status);assert status=='PASS',status
