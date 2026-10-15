"""US30 browser regression; mock API only, no persistent data writes."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json, os, re, subprocess

pages = Path(__file__).resolve().parents[1] / 'Frontend' / 'pages'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = '''<script>
localStorage.setItem('token','us30-test');let writes=[],confirmations=0,accept=false;
const grants=GRANTS;
const intern={internId:12,fullName:'Intern A',email:'a@example.invalid',mentorUserId:2,mentorName:'Mentor A',internshipProgramId:7,status:'Đang thực tập'};
API.getCurrentUser=async()=>({user:{role:'HR'},permissions:grants});
API.getAssignmentInterns=async()=>[intern];
API.getAssignmentMentors=async()=>[{id:2,fullName:'Mentor A',email:'ma@example.invalid'},{id:3,fullName:'Mentor B',email:'mb@example.invalid'}];
API.getAssignmentPrograms=async()=>[{id:7,name:'Program A',startDate:'2026-10-01',endDate:'2026-12-31'}];
API.getAssignments=async()=>[intern];
API.assignMentor=async(id,payload)=>{writes.push({id,payload});intern.mentorUserId=payload.mentorUserId;};
AppUI.confirm=async()=>{confirmations++;return accept;};
</script>'''
checks = '''<pre id="result"></pre><script>
(async()=>{const assert=(v,m)=>{if(!v)throw Error(m)},settle=()=>new Promise(r=>setTimeout(r,100));
try{await settle();ACTIONS;document.getElementById('result').textContent='PASS';}
catch(e){document.getElementById('result').textContent='FAIL '+e.message;}})();
</script>'''
cases = [
    ('mentor-assignment.html', ['ASSIGN_MENTOR','ASSIGNMENT_UPDATE'], '''
const form=document.getElementById('assignmentForm'),select=document.getElementById('internId'),program=document.getElementById('internshipProgramId');
select.value='12';select.dispatchEvent(new Event('change'));
assert(program.disabled&&program.value==='7','existing program locked');
document.getElementById('mentorUserId').value='3';form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();
assert(confirmations===1&&writes.length===0&&program.disabled,'cancel preserves data and program lock');
accept=true;form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();
assert(writes.length===1&&writes[0].id===12&&writes[0].payload.mentorUserId===3&&writes[0].payload.internshipProgramId===7,'confirmed mentor change preserves program and prevents double save');
document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:{permissions:[]}}));
assert(form.hidden&&document.getElementById('assignBtn').hidden,'revocation hides action');
'''),
    ('mentor-assignment.html', [], "assert(document.getElementById('assignmentForm').hidden&&writes.length===0,'no permission hides form');"),
    ('mentor-assignment-list.html', ['ASSIGN_MENTOR'], "assert(document.getElementById('assignmentRows').textContent.includes('Mentor A')&&!document.querySelector('#assignmentRows a, #assignmentRows button'),'read only has no actions');"),
]
with TemporaryDirectory(prefix='us30-ui-') as directory:
    temp = Path(directory)
    for i, (page, grants, actions) in enumerate(cases):
        html = (pages / page).read_text(encoding='utf-8').replace('<head>', '<head><base href="'+pages.as_uri()+'/"><script>localStorage.setItem("token","us30-test");</script>')
        html = html.replace('<script src="../js/navigation.js">', setup.replace('GRANTS', json.dumps(grants))+'<script src="../js/navigation.js">')
        target = temp / f'{i}.html'
        target.write_text(html.replace('</body>', checks.replace('ACTIONS', actions)+'</body>'), encoding='utf-8')
        result = subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--user-data-dir='+str(temp / f'profile-{i}'),'--virtual-time-budget=3000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=40)
        match = re.search(r'<pre id="result">(.*?)</pre>', result.stdout, re.S)
        status = match[1] if match else 'NO RESULT'
        if not match:
            print('Chrome exit:', result.returncode, 'stderr:', result.stderr[-1200:], 'DOM:', result.stdout[:300])
        print(page, grants, status)
        assert status == 'PASS', status
