"""Exercise permission-dependent UI without touching the application's database."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess

root=Path(__file__).resolve().parents[1]/'Frontend'
chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
profile={'id':42,'internId':42,'kind':'cv','fileName':'cv.pdf','size':100,'uploadedAt':'2026-01-01T00:00:00','status':'pending','version':'AAAAAAAAAAE=','fullName':'Permission UI profile','email':'ui-check@example.invalid','phone':'0900000000','school':'UI check school','major':'UI check major','createdAt':'2026-01-01T00:00:00Z','studentCode':'SV-2026','dateOfBirth':'2004-01-02','startDate':'2026-01-01','endDate':'2026-06-30','mentorEmail':'mentor@example.invalid','notes':'<b>Plain text notes</b>'}
cases=[
    ('reviews-hr','document-reviews.html','HR','', '''
assert(document.getElementById('reviewRows').textContent.includes('cv.pdf'),'HR queue displays submitted document');
const button=[...document.querySelectorAll('#reviewRows button')].find(button=>button.textContent.includes('/'));
button.click();assert(document.getElementById('reviewDialog').open,'review action opens dialog');
const reject=document.querySelector('[name=decision][value=rejected]');
document.getElementById('reviewForm').dispatchEvent(new SubmitEvent('submit',{cancelable:true,bubbles:true,submitter:reject}));
assert(!calls.some(call=>call.options.method==='PUT'),'empty rejection reason never reaches API');
document.getElementById('reviewComment').value='Please update';
document.getElementById('reviewForm').dispatchEvent(new SubmitEvent('submit',{cancelable:true,bubbles:true,submitter:reject}));await tick();await tick();
const request=calls.find(call=>call.options.method==='PUT');const payload=JSON.parse(request.options.body);
assert(request.url.endsWith('/document-reviews/42/cv')&&payload.status==='rejected'&&payload.comment==='Please update'&&payload.version==='AAAAAAAAAAE=','review sends decision and version');
assert(!document.getElementById('reviewDialog').open,'successful review closes dialog');
'''),
    ('search-hr','hr-search-filter.html','HR','', '''
assert(document.querySelector('#searchTableBody a').getAttribute('href')==='intern-manage.html?edit=42','HR search links to the selected profile');
assert(!document.getElementById('addInternLink').hidden,'HR can add profiles');
document.querySelector('#searchTableBody button').click();
assert(document.getElementById('profileDialog').open,'profile detail dialog opens');
assert(document.getElementById('profileDialogDetails').textContent.includes('02/01/2004'),'birth date displays in Vietnamese format');
assert(document.getElementById('profileDialogDetails').textContent.includes('mentor@example.invalid'),'mentor contact appears in details');
assert(!document.querySelector('#profileDialogDetails b'),'notes render as plain text');
document.getElementById('closeProfileDialog').click();
assert(!document.getElementById('profileDialog').open,'close button dismisses profile dialog');
document.getElementById('schoolFilter').value='UI check school';document.getElementById('majorFilter').value='UI check major';document.getElementById('searchInput').value='profile';
submit('filterForm');await tick();await tick();
const request=calls.findLast(call=>call.url.includes('/interns/search?'));const query=new URL(request.url).searchParams;
assert(query.get('search')==='profile'&&query.get('school')==='UI check school'&&query.get('major')==='UI check major','search passes real filter parameter names');
'''),
    ('search-mentor','hr-search-filter.html','MENTOR','', '''
assert(!document.querySelector('#searchTableBody a'),'MENTOR sees no edit actions');
assert(document.getElementById('addInternLink').hidden,'MENTOR sees no add action');
document.querySelector('#searchTableBody button').click();
assert(document.getElementById('editProfileDialog').hidden,'MENTOR sees no dialog edit button');
document.getElementById('closeProfileDialog').click();
assert(!document.getElementById('profileDialog').open,'MENTOR can close profile details');
'''),
    ('edit-hr','intern-manage.html','HR','?edit=42', '''
assert(!document.getElementById('internFormSection').classList.contains('hidden'),'HR deep link opens edit form');
assert(document.getElementById('fullName').value==='Permission UI profile','edit form loads selected API profile');
assert(document.getElementById('studentCode').value==='SV-2026'&&document.getElementById('startDate').value==='2026-01-01','edit form loads extended profile fields');
document.getElementById('fullName').value='  Updated UI name  ';submit('createInternForm');await tick();await tick();
const request=calls.find(call=>call.options.method==='PUT');const payload=JSON.parse(request.options.body);
assert(request.url.endsWith('/interns/42')&&payload.fullName==='Updated UI name','HR edit uses PUT and trims form fields');
assert(Object.keys(payload).length===InternProfile.fields.length&&'studentCode' in payload&&'startDate' in payload,'edit payload matches backend DTO');
assert(payload.studentCode==='SV-2026'&&payload.notes==='<b>Plain text notes</b>'&&payload.startDate==='2026-01-01','edit preserves extended fields');
assert(document.getElementById('internTableBody').textContent.includes('Updated UI name'),'saved response is refreshed in profile list');
document.querySelector('#internTableBody button').click();
assert(document.getElementById('profileDialog').open,'view button opens selected profile');
document.getElementById('editProfileDialog').click();
assert(!document.getElementById('profileDialog').open&&!document.getElementById('internFormSection').classList.contains('hidden'),'edit from details opens form and closes dialog');
assert(document.getElementById('fullName').value==='Updated UI name','detail edit selects the correct profile');
document.getElementById('cancelBtn').click();
assert(document.getElementById('internFormSection').classList.contains('hidden'),'cancel hides edit form');
document.getElementById('openFormBtn').click();
assert(!document.getElementById('internFormSection').classList.contains('hidden')&&document.getElementById('fullName').value==='','add opens an empty form');
document.getElementById('closeFormBtn').click();
assert(document.getElementById('internFormSection').classList.contains('hidden'),'close hides add form');

'''),
    ('edit-mentor','intern-manage.html','MENTOR','?edit=42', '''
assert(document.getElementById('internFormSection').classList.contains('hidden'),'MENTOR cannot open edit form by URL');
assert(![...document.querySelectorAll('#internTableBody button')].some(button=>button.textContent==='Chỉnh sửa'),'MENTOR sees no edit buttons');
submit('createInternForm');await tick();assert(!calls.some(call=>call.options.method==='PUT'),'forged frontend submit cannot issue edit request');
'''),
    ('dashboard-hr','dashboard.html','HR','', '''
const links=[...document.querySelectorAll('#roleMenu a')].map(link=>link.getAttribute('href'));
assert(links.includes('hr-search-filter.html')&&links.includes('intern-manage.html'),'HR dashboard offers search and profile pages');
assert(new Set(links).size===links.length,'dashboard has no duplicate links for overlapping permissions');
assert(document.getElementById('welcomeTitle').textContent.startsWith('Xin chào, '),'dashboard displays personalized greeting');
assert(!document.getElementById('permissionList')&&!document.getElementById('welcomeDescription'),'welcome area has no permission list or role description');
''')
]
with TemporaryDirectory(prefix='intern-permissions-ui-') as temporary:
    temporary=Path(temporary)
    for name,page,role,query,actions in cases:
        permissions=['VIEW_INTERNS','SEARCH_INTERNS','VIEW_PROFILE']+(['MANAGE_INTERNS','EDIT_INTERNS','APPROVE_DOCUMENTS'] if role=='HR' else [])
        setup='''<script>localStorage.clear();localStorage.setItem('token','isolated-ui-test');const calls=[];let fixture='''+json.dumps(profile)+''';
window.fetch=async(url,options={})=>{calls.push({url,options});let data;
if(url.endsWith('/auth/me'))data='''+json.dumps({'user':{'id':1,'role':role,'fullName':'UI permission check'},'permissions':permissions})+''';
else if(options.method==='PUT'){fixture={...fixture,...JSON.parse(options.body)};data=fixture;}
else data=[fixture];return{ok:true,status:200,json:async()=>data}};</script>'''
        contents=(root/'pages'/page).read_text(encoding='utf-8').replace('<head>','<head><base href="'+(root/'pages').as_uri()+'/">'+setup)
        test='''<pre id="checkResult"></pre><script>(async()=>{try{const checks=[];const tick=()=>new Promise(resolve=>setTimeout(resolve,0));function assert(ok,name){if(!ok)throw Error(name);checks.push(name)}function submit(id){document.getElementById(id).dispatchEvent(new Event('submit',{cancelable:true,bubbles:true}))}await tick();await tick();'''+actions+'''document.getElementById('checkResult').textContent='PASS: '''+name+''' '+checks.join('; ')}catch(error){document.getElementById('checkResult').textContent='FAIL: '+error.message}})();</script>'''
        target=temporary/(name+'.html');target.write_text(contents.replace('</body>',test+'</body>'),encoding='utf-8')
        result=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--user-data-dir='+str(temporary/'profile'),'--virtual-time-budget=2000','--dump-dom',target.as_uri()+query],capture_output=True,timeout=30)
        match=re.search(r'<pre id="checkResult">(.*?)</pre>',result.stdout.decode('utf-8',errors='replace'),re.S)
        assert match and match.group(1).startswith('PASS:'),(name,'Browser failed' if not match else match.group(1))
        print(match.group(1))
