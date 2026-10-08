"""Check shared API diagnostics and account UI without real requests or SQL writes."""
from pathlib import Path
from tempfile import TemporaryDirectory
import os, re, subprocess

root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
bootstrap = '''<script>
localStorage.clear();localStorage.setItem('token','private-token');localStorage.setItem('unrelated','keep');
let logs=[],alerts=[];console.error=(...values)=>logs.push(values);window.alert=value=>alerts.push(value);window.confirm=()=>true;
let fail=true,hold=false,releaseRequest,creates=0,deletes=0,resends=0,stored=[];
window.fetch=async(url,options={})=>{
if(url.endsWith('/auth/me'))return{ok:true,status:200,json:async()=>({user:{role:'ADMIN'},permissions:['MANAGE_USERS','CREATE_USER','DELETE_USER']})};
if(fail)return{ok:false,status:500,json:async()=>({})};
if(url.endsWith('/resend-login-email')){resends++;return{ok:true,status:200,json:async()=>({emailSent:true,message:'Resent'})};}
if(options.method==='POST'){
creates++;if(hold)await new Promise(resolve=>releaseRequest=resolve);
stored.push({id:42,fullName:'<b>Plain name</b>',email:'test@example.invalid',role:'INTERN'});
return{ok:true,status:201,json:async()=>({success:true,message:'Created',data:stored[0]})};
}
if(options.method==='DELETE'){deletes++;stored=[];return{ok:true,status:204,json:async()=>{throw Error('204 has no body')}};}
return{ok:true,status:200,json:async()=>structuredClone(stored)};
};
</script>'''
common = '''const tick=()=>new Promise(resolve=>setTimeout(resolve,0));
const settle=async()=>{await tick();await tick();await tick()};
const assert=(ok,label)=>{if(!ok)throw Error(label)};
'''
api_checks = '''
for(const [status,category] of [[400,'validation'],[401,'authentication'],[403,'permission'],[404,'not_found'],[409,'conflict'],[500,'server']]){
window.fetch=async()=>({ok:false,status,headers:new Headers({'X-Request-ID':'test-trace'}),json:async()=>({})});
try{await requestApi('/test?token=private-query',{method:'POST',body:'private-password'});throw Error('expected failure')}
catch(error){assert(error instanceof ApiError&&error.status===status&&error.category===category,'typed HTTP '+status);assert(error.endpoint==='/test'&&error.traceId==='test-trace','correlation metadata');}
}
window.fetch=async()=>{throw Error('private-network-detail')};
try{await API.downloadMyDocument('cv');throw Error('expected failure')}
catch(error){assert(error.status===0&&error.message.includes('Không thể kết nối'),'binary download network feedback');}
window.fetch=async()=>({ok:true,status:204,json:async()=>{throw Error('no body')}});
assert(await API.deleteUser(42)===null,'204 handled without JSON');
window.fetch=async()=>({ok:true,status:200,json:async()=>{throw Error('private-invalid-json')}});
try{await API.getUsers();throw Error('expected failure')}
catch(error){assert(error instanceof ApiError&&error.message.includes('dữ liệu không hợp lệ'),'malformed success is reported');}
window.fetch=async()=>({ok:false,status:500,json:async()=>({detail:'private-stack-trace'})});
try{await API.getUsers();throw Error('expected failure')}
catch(error){assert(!error.message.includes('private-stack'),'server detail never displayed');}
const diagnostics=JSON.stringify(logs);
assert(!diagnostics.includes('private-token')&&!diagnostics.includes('private-password')&&!diagnostics.includes('private-query')&&!diagnostics.includes('private-network-detail'),'diagnostics contain no credentials, body or query');
Session.clear();assert(localStorage.getItem('unrelated')==='keep'&&!localStorage.getItem('token'),'session clearing preserves unrelated storage');
assert(Session.hasPermission({permissions:['EDIT_INTERNS']},'EDIT_INTERNS')&&!Session.hasPermission({},'EDIT_INTERNS'),'permission helper fails closed');
'''
account_checks = '''
await settle();assert(localStorage.getItem('token')==='private-token','creation initialization preserves session');
fail=false;await initializePage();
document.getElementById('fullName').value='Test';document.getElementById('email').value='test@example.invalid';
document.getElementById('password').value='Account-test-9';document.getElementById('confirmPassword').value='Account-test-9';document.getElementById('role').value='INTERN';
const form=document.getElementById('createUserForm');const submit=()=>form.dispatchEvent(new Event('submit',{cancelable:true}));
hold=true;submit();submit();assert(creates===1&&document.getElementById('password').disabled,'duplicate account creation blocked');
assert(form.getAttribute('aria-busy')==='true'&&form.querySelector('[type=submit]').textContent.includes('Đang tạo'),'account submit loading UI');
hold=false;releaseRequest();await settle();assert(!document.getElementById('password').disabled&&form.elements.password.value===''&&stored.length===1,'account restored and form reset');
assert(!document.getElementById('userTableBody'),'create page does not load list DOM');
assert(document.getElementById('accountMessage')&&!alerts.length,'account notifications are inline');
document.getElementById('fullName').value='HR Test';document.getElementById('email').value='hr@gmail.com';
document.getElementById('role').value='HR';document.getElementById('role').dispatchEvent(new Event('change'));
assert(document.getElementById('password').closest('.form-group').hidden && !document.getElementById('password').required,'HR password is generated by backend');
let accountPayload;const previousFetch=window.fetch;window.fetch=(url,options={})=>{if(options.method==='POST')accountPayload=JSON.parse(options.body);return previousFetch(url,options)};
submit();await settle();assert(accountPayload.role==='HR'&&!('password' in accountPayload),'HR create submits email and role without plaintext password');

'''

list_checks = '''await settle();fail=false;stored=[{id:42,fullName:'<b>Plain name</b>',email:'test@example.invalid',role:'INTERN'},{id:1,fullName:'Admin',email:'admin@example.invalid',role:'ADMIN'}];await initializePage();
assert(document.getElementById('userTableBody').textContent.includes('<b>Plain name</b>')&&!document.querySelector('#userTableBody b'),'account names are rendered safely');
assert(document.querySelectorAll('#userTableBody button').length===1,'Admin cannot be deleted');
document.querySelector('#userTableBody button').click();await settle();assert(deletes===1&&stored.length===0,'account delete handles 204');
assert(document.querySelector('#userTableBody .empty-state')&&document.getElementById('accountMessage'),'account empty state and inline delete notification');
stored=[{id:22,fullName:'HR',email:'hr@gmail.com',role:'HR'},{id:23,fullName:'Mentor',email:'mentor@gmail.com',role:'MENTOR'}];await loadUsers();
assert(document.querySelectorAll('[data-resend-id]').length===2,'resend offered for HR and Mentor');
const resend=document.querySelector('[data-resend-id]');resend.click();resend.click();await settle();assert(resends===1 && !resend.disabled,'resend guarded and button restored');
'''

with TemporaryDirectory(prefix='intern-api-ui-') as temporary:
    temporary = Path(temporary)
    for name, actions in [('api', api_checks), ('accounts', account_checks), ('list', list_checks)]:
        if name == 'api':
            html = '<!doctype html><html><head></head><body><script src="../js/api.js"></script></body></html>'
        else:
            html = (root/('pages/user-create.html' if name == 'accounts' else 'pages/user-manage.html')).read_text(encoding='utf-8')
        html = html.replace('<head>', '<head><base href="'+(root/'pages').as_uri()+'/">'+bootstrap)
        checks = '<pre id="result"></pre><script>(async()=>{try{'+common+actions+'document.getElementById("result").textContent="PASS: '+name+' shared diagnostics and account regression"}catch(error){document.getElementById("result").textContent="FAIL: "+error.message}})();</script>'
        target = temporary/(name+'.html')
        target.write_text(html.replace('</body>', checks+'</body>'), encoding='utf-8')
        result = subprocess.run([str(chrome), '--headless', '--disable-gpu', '--no-sandbox', '--window-size=1100,900', '--user-data-dir='+str(temporary/(name+'-profile')), '--virtual-time-budget=4000', '--dump-dom', target.as_uri()], capture_output=True, timeout=30)
        match = re.search(r'<pre id="result">(.*?)</pre>', result.stdout.decode('utf-8', errors='replace'), re.S)
        assert match and match.group(1).startswith('PASS:'), 'Browser failed' if not match else match.group(1)
        print(match.group(1))
