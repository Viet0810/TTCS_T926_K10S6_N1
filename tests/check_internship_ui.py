"""Exercise US12/US14 pages and shared navigation with mocked API responses."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess

repo = Path(__file__).resolve().parents[1]
frontend = repo / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
cases = [
    ('internship-profile', 'internship-profile.html', 'INTERN', 'ready', '''
assert(document.getElementById('internName').textContent==='Intern A' && document.getElementById('internDetails').textContent.includes('ICTU'),'separate profile uses own API and shared renderer');
assert(!document.getElementById('uploadArea'),'profile contains no document upload');
assert(requests.some(r=>r.url.endsWith('/interns/me')&&r.options.headers.Authorization==='Bearer test'),'profile owner determined by token');
assert(document.querySelector('.role-menu a[aria-current="page"]').getAttribute('href')==='internship-profile.html','profile active navigation');
'''),
    ('internship-profile-empty', 'internship-profile.html', 'INTERN', 'empty-profile', '''
assert(document.getElementById('profileMessage').textContent==='Chưa có hồ sơ thực tập.','empty own profile');
'''),
    ('internship-profile-failure', 'internship-profile.html', 'INTERN', 'offline-profile', '''
assert(!document.getElementById('retryProfileBtn').hidden && document.getElementById('profileMessage').textContent.includes('Không thể kết nối'),'profile error and retry');
mode='ready';document.getElementById('retryProfileBtn').click();await tick();await tick();await tick();assert(document.getElementById('internName').textContent==='Intern A','profile retry recovers');
'''),
    ('internship-profile-wrong-role', 'internship-profile.html', 'HR', 'ready', '''
assert(document.getElementById('profileMessage').textContent.includes('dành cho'),'profile role guard');
assert(!requests.some(r=>r.url.endsWith('/interns/me')),'HR never requests intern profile');
'''),
    ('assignment', 'mentor-assignment.html', 'HR', 'ready', '''
assert(!form.hidden && select.options.length === 2, 'intern options load');
assert(document.getElementById('mentorUserId').options.length === 3, 'mentor options load');
select.value='12';select.dispatchEvent(new Event('change'));
document.getElementById('mentorUserId').value='22';document.getElementById('internshipProgramId').value='31';
form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));
assert(button.disabled && select.disabled, 'save locks form');
await tick();await tick();await tick();
const puts=requests.filter(r=>r.options.method==='PUT');assert(puts.length===1,'duplicate submit guarded');
assert(puts[0].url.endsWith('/intern-assignments/12') && puts[0].options.headers.Authorization==='Bearer test','assignment route and token');
const payload=JSON.parse(puts[0].options.body);assert(payload.mentorUserId===22 && payload.internshipProgramId===31 && !('role' in payload),'assignment contract');
assert(!button.disabled && !select.disabled,'save unlocks form');
assert(!document.getElementById('assignmentRows') && document.querySelector('main a[href="mentor-assignment-list.html"]'),'form page contains no list and links to assignment list');
assert(select.value==='12' && document.getElementById('mentorUserId').value==='22','save refreshes selected assignment');
assert(message.textContent.includes('Trạng thái: Đang thực tập'),'save displays reloaded backend status');
document.getElementById('mentorUserId').value='21';form.dispatchEvent(new Event('submit',{cancelable:true}));await tick();await tick();await tick();
assert(interns.length===1 && document.getElementById('mentorUserId').value==='21','reassign updates one intern');
mode='offline';form.dispatchEvent(new Event('submit',{cancelable:true}));await tick();await tick();assert(!button.disabled && !select.disabled && !message.hidden,'failure restores controls');
'''),
    ('assignment-refresh', 'mentor-assignment.html', 'HR', 'assigned', '''
assert(select.value==='12' && document.getElementById('mentorUserId').value==='22' && document.getElementById('internshipProgramId').value==='31','query selects persisted assignment for update');
'''),
    ('assignment-list', 'mentor-assignment-list.html', 'HR', 'assigned', '''
assert(!form && document.querySelectorAll('#assignmentRows tr').length===1,'list page has no form');
assert(document.getElementById('assignmentRows').textContent.includes('Mentor B') && document.getElementById('assignmentRows').textContent.includes('#31'),'list uses actual mentor and program data');
assert(document.querySelector('#assignmentRows a').getAttribute('href')==='mentor-assignment.html?internId=12','update links to selected intern');
assert(!document.getElementById('assignmentRows').querySelector('img'),'assignment text is safe');
assert(document.getElementById('assignmentRows').textContent.includes('Đang thực tập'),'list displays persisted backend status');
'''),
    ('assignment-list-empty', 'mentor-assignment-list.html', 'HR', 'ready', '''
assert(document.getElementById('assignmentRows').textContent.includes('Chưa có phân công'),'empty assignment list');
'''),
    ('assignment-no-intern', 'mentor-assignment.html', 'HR', 'no-intern', '''
assert(button.disabled && message.textContent.includes('Chưa có thực tập sinh'),'empty intern state');
'''),
    ('assignment-no-mentor', 'mentor-assignment.html', 'HR', 'no-mentor', '''
assert(button.disabled && message.textContent.includes('Chưa có tài khoản Mentor'),'empty mentor state');
'''),
    ('assignment-wrong-role', 'mentor-assignment.html', 'INTERN', 'ready', '''
assert(form.hidden && message.textContent.includes('không có quyền'),'assignment role guard');
assert(!requests.some(r=>r.url.includes('/intern-assignments')),'wrong role does not load HR data');
'''),
    ('schedule', 'intern-schedule.html', 'INTERN', 'ready', '''
assert(!document.getElementById('scheduleDetails').hidden,'schedule visible');
assert(document.getElementById('scheduleStart').textContent==='1/10/2026' && document.getElementById('scheduleEnd').textContent==='31/12/2026','program dates');
assert(document.getElementById('scheduleMentor').textContent==='Mentor B' && document.getElementById('scheduleDepartment').textContent==='IT','actual mentor/department');
assert(requests.some(r=>r.url.endsWith('/interns/me/schedule') && r.options.headers.Authorization==='Bearer test'),'own schedule route and token');
'''),
    ('schedule-empty', 'intern-schedule.html', 'INTERN', 'empty', '''
assert(document.getElementById('scheduleDetails').hidden && message.textContent==='Lịch thực tập của bạn chưa được thiết lập.','empty schedule');
'''),
    ('schedule-wrong-role', 'intern-schedule.html', 'HR', 'ready', '''
assert(document.getElementById('scheduleDetails').hidden && message.textContent.includes('không có quyền'),'schedule role guard');
assert(!requests.some(r=>r.url.endsWith('/interns/me/schedule')),'HR does not load intern schedule');
'''),
    ('schedule-offline', 'intern-schedule.html', 'INTERN', 'offline-schedule', '''
assert(document.getElementById('scheduleDetails').hidden && !message.hidden && message.textContent.includes('Không thể kết nối'),'safe connection feedback');
'''),
    ('profile-intern-menu', 'profile.html', 'INTERN', 'ready', '''
assert(document.querySelector('.role-menu a[href="intern-schedule.html"]'),'Intern sidebar links from existing page');
assert(!document.querySelector('.role-menu a[href="mentor-assignment.html"]'),'Intern sidebar excludes HR assignment');
'''),
    ('search-hr-menu', 'hr-search-filter.html', 'HR', 'ready', '''
assert(document.querySelector('.role-menu a[href="mentor-assignment.html"]'),'HR sidebar links from existing page');
assert(!document.querySelector('.role-menu a[href="intern-schedule.html"]'),'HR sidebar excludes personal schedule');
'''),
]
setup = '''<script>
localStorage.setItem('token','test');const requests=[],pageErrors=[];
window.addEventListener('error',e=>pageErrors.push(e.message));window.addEventListener('unhandledrejection',e=>pageErrors.push(String(e.reason)));
let mode=MODE;const role=ROLE;
const interns=mode==='no-intern'?[]:[{internId:12,fullName:'<img src=x> Intern A',email:'intern@ictu.edu.vn',mentorUserId:null,mentorName:null,internshipProgramId:null,status:mode==='assigned'?'Đang thực tập':'Chờ tiếp nhận'}];
const mentors=mode==='no-mentor'?[]:[{id:21,fullName:'Mentor A',email:'a@gmail.com'},{id:22,fullName:'Mentor B',email:'b@gmail.com'}];
if(mode==='assigned')Object.assign(interns[0],{mentorUserId:22,mentorName:'Mentor B',internshipProgramId:31});
window.fetch=async(url,options={})=>{
requests.push({url,options});if(mode==='offline'||(mode==='offline-schedule'&&url.endsWith('/me/schedule'))||(mode==='offline-profile'&&url.endsWith('/interns/me')))throw new TypeError('offline');
await new Promise(r=>setTimeout(r,0));let data;
if(url.endsWith('/auth/me'))data={user:{role,fullName:'Test'},permissions:role==='HR'?['VIEW_PROFILE','VIEW_INTERNS','SEARCH_INTERNS','ASSIGN_MENTOR']:['VIEW_PROFILE']};
else if(options.method==='PUT') { const body=JSON.parse(options.body);Object.assign(interns[0],body,{mentorName:mentors.find(m=>m.id===body.mentorUserId).fullName,status:'Đang thực tập'});data={success:true}; }
else if(url.endsWith('/intern-assignments/interns'))data=interns;
else if(url.endsWith('/intern-assignments/mentors'))data=mentors;
else if(url.endsWith('/intern-assignments/programs'))data=[{id:31,startDate:'2026-10-01T00:00:00',endDate:'2026-12-31T00:00:00'}];
else if(url.endsWith('/intern-assignments'))data=interns.filter(i=>i.mentorUserId);
else if(url.endsWith('/interns/me/schedule'))data={internId:12,startDate:mode==='empty'?null:'2026-10-01',endDate:mode==='empty'?null:'2026-12-31',mentorName:'Mentor B',departmentName:'IT',status:'ACTIVE'};
else if(url.endsWith('/interns/me'))data=mode==='empty-profile'?null:{id:12,fullName:'Intern A',email:'intern@ictu.edu.vn',phone:'0912345678',school:'ICTU',major:'IT'};
else if(url.includes('/interns/search'))data={total:0,items:[]};
else data=[];
return {ok:true,status:200,json:async()=>data};};
</script>'''
with TemporaryDirectory(prefix='internship-ui-') as directory:
    temp = Path(directory)
    for name, page, role, mode, checks in cases:
        original = frontend / 'pages' / page
        preamble = setup.replace('MODE', json.dumps(mode)).replace('ROLE', json.dumps(role))
        html = original.read_text(encoding='utf-8').replace('<head>', '<head><base href="' + original.parent.as_uri() + '/">' + preamble)
        script = '''<pre id="result"></pre><script>(async()=>{try{
const assert=(value,text)=>{if(!value)throw Error(text)},tick=()=>new Promise(r=>setTimeout(r,10));
await new Promise(r=>setTimeout(r,100));
const form=document.getElementById('assignmentForm'),select=document.getElementById('internId'),button=document.getElementById('assignBtn'),message=document.getElementById('dashboardMessage');
''' + checks + '''
assert(pageErrors.length===0,pageErrors.join(';'));
assert(document.documentElement.scrollWidth<=innerWidth,'responsive width');
const header=document.querySelector('.dashboard-header').getBoundingClientRect();assert(header.left===0 && header.width===document.documentElement.clientWidth,'full width topbar');
const nav=document.querySelector('.role-menu').getBoundingClientRect();assert(nav.top>=header.bottom,'sidebar below topbar');
if(role==='HR'){assert(document.querySelector('.role-menu a[href="mentor-assignment.html"]'),'HR assignment menu');assert(!document.querySelector('.role-menu a[href="intern-schedule.html"]'),'HR has no own schedule menu');}
else {assert(document.querySelector('.role-menu a[href="intern-schedule.html"]'),'Intern schedule menu');assert(!document.querySelector('.role-menu a[href="mentor-assignment.html"]'),'Intern has no assignment menu');}
document.getElementById('result').textContent='PASS';
}catch(error){document.getElementById('result').textContent='FAIL: '+error.message}})();</script>'''
        target = temp / (name + '.html')
        target.write_text(html.replace('</body>', script + '</body>'), encoding='utf-8')
        for width in (390, 1440):
            uri = target.as_uri() + ('?internId=12' if name == 'assignment-refresh' else '')
            output = subprocess.run([str(chrome), '--headless', '--disable-gpu', '--no-sandbox', '--allow-file-access-from-files', '--user-data-dir='+str(temp/(name+str(width))), '--virtual-time-budget=5000', f'--window-size={width},1000', '--dump-dom', uri], capture_output=True, text=True, encoding='utf-8', timeout=30)
            result = re.search(r'<pre id="result">(.*?)</pre>', output.stdout, re.S)
            status = result[1] if result else 'NO RESULT'
            print(name, width, status)
            assert status == 'PASS', status
