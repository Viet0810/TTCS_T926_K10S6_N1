"""Exercise HR document decisions in Chrome with an isolated mocked API."""
from pathlib import Path
from tempfile import TemporaryDirectory
import os, re, subprocess

root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = '''<script>
localStorage.clear();localStorage.setItem('token','review-test');
let items=['cv','application'].map((kind,i)=>({internId:1,fullName:'Test intern',email:'test@example.invalid',kind,fileName:kind+'.pdf',size:100,uploadedAt:'2026-10-02T00:00:00',status:'pending',version:'v'+i}));
let puts=0,downloaded=false,failList=false,failStatus=0,offline=false,hold=false,releaseReview,lastBody;
window.fetch=async(url,options={})=>{
if(offline)throw new TypeError('network');
if(url.endsWith('/auth/me'))return{ok:true,json:async()=>({permissions:['APPROVE_DOCUMENTS']})};
if(failStatus)return{ok:false,status:failStatus,json:async()=>({message:'Test API error'})};
if(options.method==='PUT'){
puts++;lastBody=JSON.parse(options.body);if(hold)await new Promise(resolve=>releaseReview=resolve);
const item=items.find(item=>item.kind===url.split('/').pop());Object.assign(item,lastBody,{version:'new-'+puts});
return{ok:true,json:async()=>({message:'saved'})};
}
if(url.endsWith('/document-reviews')){
if(failList)throw new TypeError('list unavailable');
return{ok:true,json:async()=>structuredClone(items)};
}
return{ok:true,blob:async()=>new Blob(['%PDF-1.4'],{type:'application/pdf'})};
};
HTMLAnchorElement.prototype.click=function(){downloaded=this.download==='cv.pdf'};
</script>'''
checks = '''<pre id="result"></pre><script>(async()=>{try{
const tick=()=>new Promise(resolve=>setTimeout(resolve,0));
const settle=async()=>{await tick();await tick();await tick()};
const assert=(ok,label)=>{if(!ok)throw Error(label)};
const open=()=>document.querySelector('#reviewRows tr button:nth-child(2)').click();
const submit=status=>{const button=document.querySelector('#reviewForm button[value="'+status+'"]');document.getElementById('reviewForm').dispatchEvent(new SubmitEvent('submit',{cancelable:true,submitter:button}))};
await settle();assert(document.querySelectorAll('#reviewRows tr').length===2,'CV and application visible');
assert(document.getElementById('reviewRows').textContent.includes('Test intern'),'owner visible');
document.querySelector('#reviewRows tr button').click();await settle();assert(downloaded,'download with original filename');
open();submit('rejected');await settle();assert(puts===0&&document.getElementById('dialogMessage').textContent.includes('lý do'),'reason required');
hold=true;submit('approved');submit('approved');assert(puts===1&&document.querySelector('#reviewForm button').disabled,'repeat requests blocked');
hold=false;releaseReview();await settle();assert(!document.getElementById('reviewDialog').open&&document.getElementById('reviewRows').textContent.includes('Đã duyệt'),'approval updates table');
assert(lastBody.version==='v0','existing version contract');
document.getElementById('refreshBtn').click();await settle();assert(document.getElementById('reviewRows').textContent.includes('Đã duyệt'),'reload preserves status');
open();assert(document.querySelector('#reviewForm button[value="approved"]').disabled,'repeat approval disabled');
document.getElementById('reviewComment').value='Missing details';submit('rejected');await settle();assert(document.getElementById('reviewRows').textContent.includes('Từ chối')&&document.getElementById('reviewRows').textContent.includes('Missing details'),'rejection and reason visible');
open();assert(document.querySelector('#reviewForm button[value="rejected"]').disabled,'repeat rejection disabled');
failStatus=500;submit('approved');await settle();assert(document.getElementById('reviewDialog').open&&document.getElementById('dialogMessage').textContent==='Test API error'&&!document.querySelector('#reviewForm button').disabled,'save error restores dialog');
failStatus=0;failList=true;submit('approved');await settle();assert(!document.getElementById('reviewDialog').open&&document.getElementById('reviewMessage').textContent.includes('Đã lưu kết quả duyệt, nhưng'),'successful save with failed reload is clear');
assert(document.querySelector('#reviewRows tr button:nth-child(2)').disabled,'no stale version resubmission');
failList=false;document.getElementById('refreshBtn').click();await settle();
for(const [status,part] of [[401,'đăng nhập lại'],[403,'không có quyền'],[404,'Không tìm thấy'],[400,'Test API error'],[500,'Test API error']]){
failStatus=status;document.querySelector('#reviewRows tr button').click();await settle();assert(document.getElementById('reviewMessage').textContent.includes(part),'HTTP '+status+' feedback');
}
failStatus=0;offline=true;document.querySelector('#reviewRows tr button').click();await settle();assert(document.getElementById('reviewMessage').textContent.includes('Không thể kết nối'),'network feedback');
document.getElementById('result').textContent='PASS: HR list/download/approval/rejection/reload, reason validation, repeated decisions and requests, failed save/reload, HTTP 401/403/404/400/500 and network feedback';
}catch(error){document.getElementById('result').textContent='FAIL: '+error.message}})();</script>'''
with TemporaryDirectory(prefix='intern-review-ui-') as temporary:
    temporary = Path(temporary)
    html = (root/'pages/document-reviews.html').read_text(encoding='utf-8')
    html = html.replace('<head>', '<head><base href="'+(root/'pages').as_uri()+'/">'+setup).replace('</body>', checks+'</body>')
    target = temporary/'reviews.html'
    target.write_text(html, encoding='utf-8')
    result = subprocess.run([str(chrome), '--headless', '--disable-gpu', '--no-sandbox', '--window-size=1100,900', '--user-data-dir='+str(temporary/'profile'), '--virtual-time-budget=5000', '--dump-dom', target.as_uri()], capture_output=True, timeout=30)
    match = re.search(r'<pre id="result">(.*?)</pre>', result.stdout.decode('utf-8', errors='replace'), re.S)
    assert match and match.group(1).startswith('PASS:'), 'Browser failed' if not match else match.group(1)
    print(match.group(1))
