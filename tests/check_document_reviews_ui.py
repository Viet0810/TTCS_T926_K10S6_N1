"""US7 browser checks using a mocked API; no real database writes."""
from pathlib import Path
from tempfile import TemporaryDirectory
import os, re, subprocess

root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = '''<script>
localStorage.clear();localStorage.setItem('token','review-test');
let role='HR',items=['cv','application'].map((kind,i)=>({internId:1,fullName:'Test intern',email:'test@example.invalid',phone:'0912345678',school:'Test school',major:'Test major',createdAt:'2026-10-01T00:00:00+07:00',kind,fileName:kind+'.pdf',size:100,uploadedAt:'2026-10-02T00:00:00',status:'pending',version:'v'+i}));
let puts=0,downloaded=false,failList=false,failStatus=0,offline=false,hold=false,releaseReview,lastBody;
window.fetch=async(url,options={})=>{
if(offline)throw new TypeError('network');
if(url.endsWith('/auth/me'))return{ok:true,json:async()=>({user:{role},permissions:['APPROVE_DOCUMENTS']})};
if(failStatus)return{ok:false,status:failStatus,json:async()=>({message:'Test API error'})};
if(options.method==='PUT'){
puts++;lastBody=JSON.parse(options.body);if(hold)await new Promise(resolve=>releaseReview=resolve);
const parts=url.split('/'),item=items.find(item=>item.kind===parts.at(-2));
Object.assign(item,lastBody,{status:parts.at(-1)==='approve'?'approved':'rejected',version:'new-'+puts});
return{ok:true,json:async()=>({message:'saved'})};
}
if(url.endsWith('/document-reviews')){if(failList)throw new TypeError('list unavailable');return{ok:true,json:async()=>structuredClone(items)}}
if(url.endsWith('/interns/1'))return{ok:true,json:async()=>({...items[0],id:1})};
return{ok:true,blob:async()=>new Blob(['%PDF-1.4'],{type:'application/pdf'})};
};
HTMLAnchorElement.prototype.click=function(){downloaded=this.download==='cv.pdf'};
</script>'''
checks = '''<pre id="result"></pre><script>(async()=>{try{
const tick=()=>new Promise(resolve=>setTimeout(resolve,0)),settle=async()=>{await tick();await tick();await tick()};
const assert=(ok,label)=>{if(!ok)throw Error(label)};
const open=async(index=0)=>{document.querySelectorAll('#reviewRows tr')[index].querySelector('button:nth-child(2)').click();await settle()};
const submit=status=>{const button=document.querySelector('#reviewForm button[value="'+status+'"]');document.getElementById('reviewForm').dispatchEvent(new SubmitEvent('submit',{cancelable:true,submitter:button}))};
await settle();assert(document.querySelectorAll('#reviewRows tr').length===2,'CV and application listed');
assert(['Test intern','0912345678','Test school','Test major'].every(text=>document.getElementById('reviewRows').textContent.includes(text)),'candidate summary fields');
const search=document.getElementById('reviewSearch'),filter=document.getElementById('reviewFilter'),refresh=document.getElementById('refreshBtn');
assert([search,filter,refresh].every(control=>Math.round(control.getBoundingClientRect().height)===46),'toolbar controls have equal heights');
assert(document.documentElement.scrollWidth<=innerWidth,'page has no horizontal overflow');
assert(getComputedStyle(document.querySelector('.review-contact')).whiteSpace==='nowrap','email does not wrap awkwardly');
if(innerWidth>1100)assert(Math.abs(search.getBoundingClientRect().top-filter.getBoundingClientRect().top)<2,'desktop toolbar is one row');
if(innerWidth<600)assert(filter.getBoundingClientRect().top>search.getBoundingClientRect().bottom&&refresh.getBoundingClientRect().top>filter.getBoundingClientRect().bottom,'mobile toolbar stacks');
search.value='no-match';search.dispatchEvent(new Event('input'));assert(document.getElementById('reviewRows').textContent.includes('Chưa có'),'search still filters immediately');
search.value='test@example.invalid';search.dispatchEvent(new Event('input'));assert(document.querySelectorAll('#reviewRows tr').length===2,'email search');
search.value='';search.dispatchEvent(new Event('input'));filter.value='approved';filter.dispatchEvent(new Event('change'));assert(document.getElementById('reviewRows').textContent.includes('Chưa có'),'status filter');
filter.value='';filter.dispatchEvent(new Event('change'));
document.querySelector('#reviewRows tr button').click();await settle();assert(downloaded,'PDF download');
await open();assert(document.getElementById('reviewDetails').textContent.includes('Test school'),'full candidate details');
submit('rejected');assert(puts===0&&document.getElementById('dialogMessage').textContent.includes('lý do'),'rejection reason required');
hold=true;submit('approved');submit('approved');assert(puts===1&&document.querySelector('#reviewForm button').disabled,'loading and double submit guard');
hold=false;releaseReview();await settle();assert(!document.getElementById('reviewDialog').open&&document.getElementById('reviewRows').textContent.includes('Đã duyệt'),'approval updates table');
assert(lastBody.version==='v0'&&!('status' in lastBody),'explicit decision contract');
await open();assert([...document.querySelectorAll('#reviewForm button[type=submit]')].every(button=>button.hidden),'processed document hides actions');
submit('rejected');assert(puts===1,'processed document cannot submit');document.getElementById('closeReview').click();
await open(1);failStatus=500;document.getElementById('reviewComment').value='Missing details';submit('rejected');await settle();
assert(document.getElementById('reviewDialog').open&&document.getElementById('dialogMessage').textContent==='Test API error'&&!document.querySelector('#reviewForm button').disabled,'server failure keeps dialog and restores controls');
failStatus=0;failList=true;submit('rejected');await settle();assert(!document.getElementById('reviewDialog').open&&document.getElementById('reviewMessage').textContent.includes('Đã lưu kết quả duyệt, nhưng'),'successful save with failed refresh');
await open(1);assert([...document.querySelectorAll('#reviewForm button[type=submit]')].every(button=>button.hidden),'saved status prevents stale resubmission');document.getElementById('closeReview').click();
failList=false;document.getElementById('refreshBtn').click();await settle();
for(const [status,part] of [[401,'đăng nhập lại'],[403,'không có quyền'],[404,'Không tìm thấy'],[409,'Test API error'],[500,'Test API error']]){failStatus=status;document.querySelector('#reviewRows tr button').click();await settle();assert(document.getElementById('reviewMessage').textContent.includes(part),'HTTP '+status+' feedback')}
failStatus=0;offline=true;document.querySelector('#reviewRows tr button').click();await settle();assert(document.getElementById('reviewMessage').textContent.includes('Không thể kết nối'),'network error');offline=false;
document.getElementById('reviewRows').replaceChildren();role='ADMIN';await initializeReviews();assert(!allowed&&document.getElementById('reviewRows').children.length===0,'admin blocked despite permission');
document.getElementById('result').textContent='PASS: HR-only list/details/download, pending approval/rejection, locked final states, loading, API contract and error handling';
}catch(error){document.getElementById('result').textContent='FAIL: '+error.message}})();</script>'''
with TemporaryDirectory(prefix='intern-review-ui-') as temporary:
    temporary = Path(temporary)
    html = (root/'pages/document-reviews.html').read_text(encoding='utf-8')
    html = html.replace('<head>', '<head><base href="'+(root/'pages').as_uri()+'/">'+setup).replace('</body>', checks+'</body>')
    target = temporary/'reviews.html'
    target.write_text(html, encoding='utf-8')
    for width in (320, 390, 768, 1440):
        result = subprocess.run([str(chrome), '--headless', '--disable-gpu', '--no-sandbox', f'--window-size={width},900', '--user-data-dir='+str(temporary/f'profile-{width}'), '--virtual-time-budget=5000', '--dump-dom', target.as_uri()], capture_output=True, timeout=30)
        match = re.search(r'<pre id="result">(.*?)</pre>', result.stdout.decode('utf-8', errors='replace'), re.S)
        assert match and match.group(1).startswith('PASS:'), 'Browser failed' if not match else match.group(1)
        print(f'{width}px: {match.group(1)}')
