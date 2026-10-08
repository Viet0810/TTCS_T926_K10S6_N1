"""Sprint 2 frontend regressions using a mock API; no real data writes."""
from pathlib import Path
from tempfile import TemporaryDirectory
import subprocess, re, os
root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = """<script>
let pageErrors=[];window.addEventListener('error',e=>pageErrors.push(e.message));window.addEventListener('unhandledrejection',e=>pageErrors.push(String(e.reason)));localStorage.setItem('token','test');let requests=[],response={records:[],stats:{totalShifts:0,attendanceRate:'0.0%',lateOrEarlyCount:0,approvedLeaveCount:0,absentCount:0}},mode='ok';
window.fetch=async(url,options={})=>{requests.push({url,options});if(mode==='offline')throw new TypeError('offline');let data=url.endsWith('/auth/me')?{user:{role:'HR'},permissions:['VIEW_INTERNS','MANAGE_INTERNS','VIEW_ATTENDANCE_REPORT','MANAGE_PROGRAMS']}:url.endsWith('/interns')?[]:url.includes('program-schedule')?{data:[],message:'ok'}:response;return {ok:true,status:200,json:async()=>data}};
window.confirm=()=>true;
</script>"""
checks = {
'register.html': """
const form=document.getElementById('registerForm'),fill=()=>{for(const [name,value] of Object.entries({fullName:'Test',email:'test@ictu.edu.vn',password:'Abc@1234',confirmPassword:'Abc@1234',phone:'0912345678',school:'School',major:'Major'}))form.elements[name].value=value};
assert(!Validation.validateEmail('test@ictu.edu.vn')&&Validation.validateEmail('test@yahoo.com'),'shared email validation');
fill();form.elements.email.value='test@yahoo.com';form.dispatchEvent(new Event('submit',{cancelable:true}));assert(requests.length===0&&!document.getElementById('emailError').hidden,'invalid email never reaches API');
fill();form.elements.confirmPassword.value='wrong';form.dispatchEvent(new Event('submit',{cancelable:true}));assert(requests.length===0&&!document.getElementById('confirmPasswordError').hidden,'confirmation mismatch');
fill();form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await tick();assert(requests.length===1&&form.hidden,'single registration');const payload=JSON.parse(requests[0].options.body);assert(payload.email==='test@ictu.edu.vn'&&!('role' in payload)&&!('confirmPassword' in payload),'registration contract');
""",
'pages/attendance-report.html': """
assert(requests.some(r=>r.url.startsWith(BASE_URL+'/attendance/report?')),'correct backend report URL');assert(currentRecords.length===0&&document.getElementById('totalShiftsCount').textContent==='0','empty report is never replaced with mock data');
assert(document.querySelectorAll('#internFilter option').length===1,'empty intern list stays empty');
await AttendanceService.getAttendanceReport({internId:42,keyword:'Test',startDate:'2026-10-01',endDate:'2026-10-08',status:'LATE'});const request=requests.at(-1);assert(request.url.includes('internId=42')&&request.url.includes('search=Test')&&request.options.headers.Authorization==='Bearer test','filters and authorization');
mode='offline';await loadReport();assert(currentRecords.length===0&&document.getElementById('exportBtn').disabled,'error clears stale data and disables export');
""",
'pages/program-setting.html': """
assert(requests.some(r=>r.url.includes('program-schedule')&&r.options.headers.Authorization==='Bearer test'),'program list sends token');
const form=document.getElementById('programDateForm');document.getElementById('programStartDate').value='2026-10-01';document.getElementById('programEndDate').value='2026-10-08';form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await tick();await tick();const posts=requests.filter(r=>r.options.method==='POST');assert(posts.length===1&&posts[0].options.headers.Authorization==='Bearer test','program create token and duplicate guard');assert(JSON.parse(posts[0].options.body).startDate==='2026-10-01','program date contract');
"""}
with TemporaryDirectory(prefix='sprint2-ui-') as temp:
    temp=Path(temp)
    for page,check in checks.items():
        source=(root/page).read_text(encoding='utf-8')
        source=source.replace('<head>','<head><base href="'+(root/page).parent.as_uri()+'/">'+setup)
        injected='<pre id="result"></pre><script>(async()=>{try{const tick=()=>new Promise(r=>setTimeout(r,0));const assert=(v,m)=>{if(!v)throw Error(m)};await new Promise(r=>setTimeout(r,50));'+check+';assert(pageErrors.length===0,pageErrors.join(";"));assert(document.documentElement.scrollWidth<=innerWidth,"responsive width");document.getElementById("result").textContent="PASS"}catch(e){document.getElementById("result").textContent="FAIL: "+e.message}})();</script>'
        target=temp/Path(page).name;target.write_text(source.replace('</body>',injected+'</body>'),encoding='utf-8')
        for width in (390,1440):
            output=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--allow-file-access-from-files','--user-data-dir='+str(temp/(str(width)+target.stem)),'--virtual-time-budget=3000',f'--window-size={width},1000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=30)
            match=re.search(r'<pre id="result">(.*?)</pre>',output.stdout,re.S)
            status=match[1] if match else 'NO RESULT';print(page,width,status)
            assert status=='PASS',status
