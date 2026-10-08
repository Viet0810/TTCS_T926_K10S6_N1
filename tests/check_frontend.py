"""Check HTML assets and authentication interactions in an isolated Chrome profile."""
from pathlib import Path
from html.parser import HTMLParser
from tempfile import TemporaryDirectory
import json, os, re, subprocess

root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
if not chrome.exists(): raise SystemExit('Chrome is required to run this check.')

class Check(HTMLParser):
    void = {'area','base','br','col','embed','hr','img','input','link','meta','param','source','track','wbr'}
    def __init__(self,path):
        super().__init__(); self.path=path; self.ids=set(); self.stack=[]
    def handle_starttag(self,tag,attrs):
        attributes=dict(attrs)
        if 'id' in attributes:
            assert attributes['id'] not in self.ids, (self.path,'duplicate ID',attributes['id'])
            self.ids.add(attributes['id'])
        for key in ['href','src']:
            value=attributes.get(key,'').split('#')[0].split('?')[0]
            if value and not value.startswith(('http:','https:','mailto:','data:')):
                assert (self.path.parent/value).exists(), (self.path,'missing asset',value)
        if tag not in self.void: self.stack.append(tag)
    def handle_endtag(self,tag):
        if tag in self.void:return
        assert self.stack and self.stack[-1]==tag, (self.path,tag,self.stack[-5:])
        self.stack.pop()

scripts=[]
for path in root.rglob('*.html'):
    text=path.read_text(encoding='utf-8')
    check=Check(path); check.feed(text); assert not check.stack,(path,check.stack)
    scripts.extend((str(path),script) for script in re.findall(r'<script[^>]*>(.*?)</script>',text,re.S) if script.strip())
for path in (root/'js').glob('*.js'): scripts.append((str(path),path.read_text(encoding='utf-8')))
for path in (root/'css').glob('*.css'):
    text=path.read_text(encoding='utf-8'); assert text.count('{')==text.count('}'),path
print('PASS: HTML structure, unique IDs, local assets and CSS structure')

bootstrap='''<script>
let responseStatus=401;
let responseData={message:"Thông tin đăng nhập không đúng."};
let lastRequest;
window.fetch=async(url,options)=>{lastRequest={url,options};return {ok:responseStatus>=200&&responseStatus<300,status:responseStatus,json:async()=>responseData}};
localStorage.clear();
</script>'''
common='''const checks=[];function assert(ok,name){if(!ok)throw Error(name);checks.push(name)}
const tick=()=>new Promise(resolve=>setTimeout(resolve,0));
function submit(id){document.getElementById(id).dispatchEvent(new Event("submit",{bubbles:true,cancelable:true}))}
'''
login_checks='''
const password=document.getElementById("password");
document.querySelector('[data-password-toggle="password"]').click();
assert(password.type==="text"&&document.querySelector('[data-password-toggle="password"]').getAttribute("aria-pressed")==="true","password visibility and accessible state");
document.getElementById("username").value="auth-ui@gmail.com";
document.getElementById("forgotPasswordLink").click();
assert(document.getElementById("loginView").hidden&&!document.getElementById("forgotView").hidden&&document.getElementById("recoveryEmail").value==="auth-ui@gmail.com","recovery view and prefilled email");
responseStatus=200;responseData={message:"Nếu email đã được đăng ký, bạn sẽ nhận được liên kết."};
submit("forgotForm");assert(document.getElementById("forgotSubmit").disabled,"recovery submit disabled while request is pending");await tick();
assert(lastRequest.url.endsWith("/auth/forgot-password")&&JSON.parse(lastRequest.options.body).email==="auth-ui@gmail.com","recovery submits the correct API payload");
assert(document.getElementById("forgotMessage").classList.contains("is-success")&&!document.getElementById("forgotSubmit").disabled,"recovery success feedback and submit restored");
responseStatus=503;responseData={message:"Chưa cấu hình địa chỉ trang đặt lại mật khẩu (PasswordReset:ResetPageUrl)."};submit("forgotForm");await tick();
assert(!document.getElementById("forgotMessage").classList.contains("is-success")&&document.getElementById("forgotMessage").textContent===responseData.message,"recovery displays backend URL error without guessing SMTP failure");
document.getElementById("backToLogin").click();
responseStatus=401;responseData={message:"Thông tin đăng nhập không đúng."};
password.value="Wrong-password-9";localStorage.setItem("token","old-session");submit("loginForm");await tick();
assert(!document.getElementById("loginMessage").hidden&&!localStorage.getItem("token")&&!document.getElementById("loginSubmit").disabled,"login errors clear stale sessions and restore the form");
responseStatus=200;responseData={token:"invalid-session",user:{role:"INVALID"}};submit("loginForm");await tick();
assert(!localStorage.getItem("token")&&!document.getElementById("loginMessage").hidden,"invalid account responses cannot create a session");
'''
reset_checks='''
assert(!location.hash,"reset token removed from browser address");
document.getElementById("newPassword").value="New-password-9";
document.getElementById("confirmPassword").value="Different-password-9";
submit("resetForm");await tick();
assert(!lastRequest&&!document.getElementById("confirmPasswordError").hidden&&document.getElementById("confirmPassword").getAttribute("aria-invalid")==="true"&&document.getElementById("confirmPasswordError").textContent.includes("không khớp"),"mismatched passwords never reach the API");
document.getElementById("confirmPassword").value="New-password-9";
responseStatus=400;responseData={message:"Liên kết đã hết hạn."};submit("resetForm");await tick();
assert(!document.getElementById("resetMessage").classList.contains("is-success")&&!document.getElementById("resetSubmit").disabled,"expired links show an error and restore the form");
responseStatus=200;responseData={message:"Đã đặt lại mật khẩu."};
localStorage.setItem("token","old-session");submit("resetForm");await tick();
assert(lastRequest.url.endsWith("/auth/reset-password")&&JSON.parse(lastRequest.options.body).password==="New-password-9","reset submits a new password to the API");
assert(!localStorage.getItem("token")&&document.getElementById("resetSubmit").hidden&&!document.getElementById("returnToLogin").hidden&&document.getElementById("newPassword").value==="","successful reset clears session and password values");
'''

with TemporaryDirectory(prefix='intern-auth-ui-') as temp:
    temp=Path(temp)
    for name,actions,fragment in [('login',''+login_checks,''),('reset',reset_checks,'#token='+'A'*64)]:
        source=root/('index.html' if name=='login' else 'reset-password.html')
        text=source.read_text(encoding='utf-8').replace('<head>','<head><base href="'+root.as_uri()+'/">'+bootstrap)
        syntax=json.dumps(scripts,ensure_ascii=True).replace('</','<\\/')
        tests='<pre id="checkResult"></pre><script>(async()=>{try{'+common+'for(const [name,code] of '+syntax+')new Function(code);'+actions+'assert(document.documentElement.scrollWidth<=innerWidth,"layout has no horizontal overflow");document.getElementById("checkResult").textContent="PASS: '+name+' "+checks.join("; ")}catch(error){document.getElementById("checkResult").textContent="FAIL: "+error.message}})();</script>'
        page=temp/(name+'.html');page.write_text(text.replace('</body>',tests+'</body>'),encoding='utf-8')
        command=[str(chrome),'--headless','--disable-gpu','--no-sandbox','--user-data-dir='+str(temp/'profile'),'--window-size=540,900','--virtual-time-budget=1500','--dump-dom',page.as_uri()+fragment]
        result=subprocess.run(command,capture_output=True,timeout=30)
        match=re.search(r'<pre id="checkResult">(.*?)</pre>',result.stdout.decode('utf-8',errors='replace'),re.S)
        assert match and match.group(1).startswith('PASS:'),(name,'Browser check failed' if not match else match.group(1))
        print(match.group(1))
