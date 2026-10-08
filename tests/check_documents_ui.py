"""Check document upload interactions in an isolated browser without real SQL writes."""
from pathlib import Path
from tempfile import TemporaryDirectory
import os, re, subprocess

root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = '''<script>
localStorage.clear();localStorage.setItem('token','isolated-document-test');
let stored=[],uploads=0,fail=false,downloaded=false,offline=false,serverError=false,hold=false,releaseUpload;
window.fetch=async(url,options={})=>{
let data;
if(url.endsWith('/auth/me'))data={user:{role:'INTERN'},permissions:['UPLOAD_DOCUMENTS']};
else if(url.endsWith('/interns/me'))data={fullName:'Document UI test',email:'test@example.invalid'};
else if(options.method==='PUT'){
uploads++; if(fail)return{ok:false,status:400,json:async()=>({message:'Rejected upload'})};
if(serverError)return{ok:false,status:500,json:async()=>({})};
if(offline)throw new TypeError('Failed to fetch');
if(hold)await new Promise(resolve=>releaseUpload=resolve);
if(!(options.body instanceof FormData)||options.headers['Content-Type'])throw Error('Invalid multipart upload');
const kind=url.split('/').pop(),file=options.body.get('file');
stored=stored.filter(item=>item.kind!==kind);stored.push({kind,fileName:file.name,size:file.size,uploadedAt:'2026-01-01T00:00:00',status:'pending'});data={message:'saved'};
}else if(url.endsWith('/documents'))data=stored;
else return{ok:true,blob:async()=>new Blob(['%PDF-1.4'],{type:'application/pdf'})};
return{ok:true,status:200,json:async()=>data};
};
HTMLAnchorElement.prototype.click=function(){downloaded=Boolean(this.download)};
</script>'''
checks = '''<pre id="result"></pre><script>(async()=>{try{
const tick=()=>new Promise(resolve=>setTimeout(resolve,0));
const assert=(ok,message)=>{if(!ok)throw Error(message)};
const select=(kind,name,contents,type='')=>{const transfer=new DataTransfer();transfer.items.add(new File([contents],name,{type}));const input=document.getElementById(kind+'File');input.files=transfer.files;input.dispatchEvent(new Event('change'))};
const submit=kind=>document.getElementById(kind+'Form').dispatchEvent(new Event('submit',{cancelable:true,bubbles:true}));
await tick();await tick();await tick();
assert(!document.getElementById('uploadArea').hidden,'intern upload area');
assert(!document.getElementById('internDetails'),'upload page contains no internship profile');
assert(document.querySelector('label.file-picker[for=cvFile]')&&document.getElementById('cvFile').getBoundingClientRect().width===1,'custom picker uses real accessible file input');
const dropTransfer=new DataTransfer();dropTransfer.items.add(new File(['%PDF-1.4'],'dropped.pdf',{type:'application/pdf'}));
document.querySelector('.file-picker').dispatchEvent(new DragEvent('drop',{bubbles:true,cancelable:true,dataTransfer:dropTransfer}));
assert(document.getElementById('cvFile').files[0].name==='dropped.pdf'&&document.getElementById('cvSelected').textContent.includes('dropped.pdf'),'drag and drop selects file');
document.getElementById('cvFile').value='';
submit('cv');await tick();assert(uploads===0&&document.getElementById('cvMessage').textContent.includes('chọn tài liệu'),'missing file feedback');
select('cv','empty.pdf','');submit('cv');await tick();assert(uploads===0&&document.getElementById('cvMessage').textContent.includes('rỗng'),'empty file blocked');
select('cv','large.pdf',new Uint8Array(5*1024*1024+1));submit('cv');await tick();assert(uploads===0&&document.getElementById('cvMessage').textContent.includes('5 MB'),'oversized file blocked');
select('cv','fake.pdf','%PDF-1.4','text/html');submit('cv');await tick();assert(uploads===0,'incompatible MIME blocked');
select('cv','cv.txt','bad');submit('cv');await tick();assert(uploads===0,'invalid extension blocked');
select('cv','cv.pdf','%PDF-1.4','application/pdf');
assert(document.getElementById('cvSelected').textContent.includes('cv.pdf'),'selected filename displayed');
hold=true;submit('cv');submit('cv');assert(uploads===1&&document.getElementById('cvFile').disabled&&document.querySelector('#cvForm button[type=submit]').disabled,'pending upload blocks repeat requests');
hold=false;releaseUpload();await tick();await tick();
assert(uploads===1&&document.getElementById('cvInfo').textContent.includes('cv.pdf'),'saved CV metadata');
assert(document.querySelector('#cvInfo .status-badge.pending')&&document.querySelector('#cvInfo .document-filename').title==='cv.pdf','real status badge and filename tooltip');
assert(!document.getElementById('cvDownload').disabled,'download enabled');
document.getElementById('cvDownload').click();await tick();assert(downloaded,'download uses saved filename');
select('application','application.pdf','%PDF-1.4');submit('application');await tick();await tick();
assert(stored.length===2,'both document kinds saved');
select('cv','cv.pdf','%PDF-1.7');submit('cv');await tick();await tick();assert(stored.length===2&&stored.find(item=>item.kind==='application').fileName==='application.pdf','same filename replaces only the selected kind');
select('cv','replacement.pdf','%PDF-1.7');submit('cv');await tick();await tick();assert(stored.length===2&&document.getElementById('cvInfo').textContent.includes('replacement.pdf'),'replacement updates metadata');
fail=true;select('cv','error.pdf','%PDF-1.4');submit('cv');await tick();await tick();
assert(document.getElementById('cvMessage').textContent==='Rejected upload','server failure displayed');
assert(!document.querySelector('#cvForm button[type=submit]').disabled,'submit restored after error');
fail=false;offline=true;submit('cv');await tick();await tick();assert(document.getElementById('cvMessage').textContent.includes('Không thể kết nối máy chủ'),'connection failure translated');
assert(!document.getElementById('cvFile').disabled,'file selection restored after network error');
offline=false;serverError=true;submit('cv');await tick();await tick();assert(document.getElementById('cvMessage').textContent.includes('Máy chủ chưa thể xử lý'),'HTTP 500 fallback feedback');
assert(document.documentElement.scrollWidth<=innerWidth,'responsive layout');
document.getElementById('result').textContent='PASS: CV/application upload, missing/empty/invalid/MIME/oversized files, selected filename, repeat-request guard, same-name replacement, download, server and network failure feedback';
}catch(error){document.getElementById('result').textContent='FAIL: '+error.message}})();</script>'''
with TemporaryDirectory(prefix='intern-document-ui-') as temporary:
    temporary=Path(temporary)
    html=(root/'pages/intern-upload-cv.html').read_text(encoding='utf-8')
    html=html.replace('<head>','<head><base href="'+(root/'pages').as_uri()+'/">'+setup).replace('</body>',checks+'</body>')
    target=temporary/'upload.html';target.write_text(html,encoding='utf-8')
    for width in (320,390,768,1440):
        result=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox',f'--window-size={width},900','--user-data-dir='+str(temporary/f'profile-{width}'),'--virtual-time-budget=3000','--dump-dom',target.as_uri()],capture_output=True,timeout=30)
        match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout.decode('utf-8',errors='replace'),re.S)
        assert match and match.group(1).startswith('PASS:'), 'Browser failed' if not match else match.group(1)
        print(width,match.group(1))
