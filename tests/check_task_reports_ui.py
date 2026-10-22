"""Chrome UI checks for US16/17/18, using an isolated mock API and temporary profiles."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json
import os
import re
import subprocess

pages = Path(__file__).resolve().parents[1] / 'Frontend' / 'pages'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = r'''<script>
let session={user:{role:ROLE},permissions:GRANTS},writes=[],fail=false,empty=false;
let tasks=[{id:1,internName:'Intern A',mentorName:'Mentor A',content:'Task A',deadline:'2026-12-01T10:00:00+07:00',status:'IN_PROGRESS',progress:35,progressNote:'Working'}];
let reports=[{id:1,internId:12,internName:'Intern A',programName:'Sprint 3',weekStartDate:'2026-10-12',weekEndDate:'2026-10-18',content:'Report <b>plain text</b> '+'long content '.repeat(100),createdAt:'2026-10-15T08:30:00+07:00',feedback:[{id:1,mentorName:'Mentor A',content:'Feedback <script>plain text<END>',createdAt:'2026-10-15T09:00:00+07:00'}]}];
API.getCurrentUser=async()=>session;
window.fetch=async(url,options={})=>{
 if(fail)throw new TypeError('Test connection failure');
 if(options.method==='PATCH'||options.method==='POST'){
  const body=JSON.parse(options.body);writes.push({url,body});
  if(options.method==='PATCH'){tasks[0].progress=body.progress;tasks[0].progressNote=body.progressNote;tasks[0].status=body.progress===100?'COMPLETED':'IN_PROGRESS';}
  else if(url.endsWith('/feedback'))reports[0].feedback.push({mentorName:'Mentor A',content:body.content,createdAt:'2026-10-15T10:00:00+07:00'});
  else reports.push({...reports[0],id:2,content:body.content,feedback:[]});
  return {ok:true,status:200,json:async()=>({id:2})};
 }
 return {ok:true,status:200,json:async()=>empty?[]:url.includes('/weekly-reports')?reports:tasks};
};
</script>'''.replace('<END>', r'<\/script>')

cases = [
    ('tasks.html', 'INTERN', ['OWN_TASK_READ', 'OWN_TASK_UPDATE'], r'''
assert(!document.getElementById('updateProgressSection').hidden,'progress form visible');
assert(document.getElementById('createTaskSection').hidden,'no Mentor fields');
document.querySelector('.task-update-action').click();
assert(document.getElementById('progressTask').value==='1','row action selects task');
assert(document.querySelector('.task-progress-bar').value===35,'progress bar reflects data');
assert(document.getElementById('progressStatus').value==='Đang thực hiện','derived status visible');
assert(document.getElementById('taskProgress').value==='35'&&document.getElementById('progressNote').value==='Working','existing progress populated');
document.getElementById('taskProgress').value='80';document.getElementById('progressNote').value='Updated';
const form=document.getElementById('progressForm');form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();
assert(writes.length===1&&writes[0].url.endsWith('/tasks/1/progress')&&writes[0].body.progress===80&&Object.keys(writes[0].body).length===2,'single PATCH only Intern fields');
assert(document.getElementById('taskRows').textContent.includes('80%'),'updated progress displayed');
for(const value of [0,50,100]) {
 const input=document.getElementById('taskProgress');input.value=String(value);input.dispatchEvent(new Event('input'));
 assert(document.getElementById('progressStatus').value===(value===0?'Đã giao':value===100?'Hoàn thành':'Đang thực hiện'),'status matches progress');
 form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();
 assert(document.querySelector('.task-progress-bar').value===value,'row bar refreshed');
}
const count=writes.length;
for(const value of [-1,101]) {document.getElementById('taskProgress').value=String(value);form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();}
assert(writes.length===count,'invalid progress prevented');
session.permissions=['OWN_TASK_READ'];document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:session}));
assert(document.getElementById('updateProgressSection').hidden,'revoked progress action hidden');
'''),
    ('tasks.html', 'MENTOR', ['MENTOR_TASK_READ'], r'''
assert(document.getElementById('updateProgressSection').hidden,'Mentor cannot edit progress');
assert(document.getElementById('taskRows').textContent.includes('35%')&&document.getElementById('taskRows').textContent.includes('Working'),'Mentor sees progress and note');
'''),
    ('weekly-reports.html', 'INTERN', ['OWN_WEEKLY_REPORT_READ', 'WEEKLY_REPORT_CREATE'], r'''
assert(!document.getElementById('createReportSection').hidden,'Intern create visible');
assert(document.querySelectorAll('.weekly-report').length===1&&!document.querySelector('#reportList b')&&!document.querySelector('#reportList script'),'report text escaped');
assert(!document.querySelector('.feedback-form'),'Intern cannot write feedback');
assert(document.getElementById('reportList').textContent.includes('Feedback <script>'),'Intern sees feedback text');
document.getElementById('reportWeek').value='2026-10-19';document.getElementById('reportContent').value='Next report';
['reportResults','reportDifficulties','reportNextPlan'].forEach(id=>document.getElementById(id).value='Test section');
const form=document.getElementById('reportForm');form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();
assert(writes.length===1&&writes[0].body.weekStartDate==='2026-10-19'&&writes[0].body.content.includes('Next report')&&writes[0].body.content.includes('Test section')&&Object.keys(writes[0].body).length===6,'report POST excludes owner identifiers and prevents double submit');
assert(document.querySelectorAll('.weekly-report').length===2,'history refreshed');
empty=true;document.getElementById('reloadReports').click();await settle();assert(document.getElementById('reportList').textContent.includes('Chưa có'),'empty state');
empty=false;fail=true;document.getElementById('reloadReports').click();document.getElementById('reloadReports').click();await settle();
assert(document.getElementById('reportList').textContent.includes('Không tải được')&&document.querySelectorAll('.ui-toast.is-error').length===1,'error state and single toast');
fail=false;document.getElementById('reloadReports').click();await settle();assert(document.querySelector('.weekly-report'),'reload recovers');
session.permissions=[];document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:session}));
assert(document.getElementById('createReportSection').hidden&&document.getElementById('reportListSection').hidden&&!document.getElementById('reportList').children.length,'revocation hides actions and clears data');
'''),
    ('weekly-reports.html', 'MENTOR', ['MENTOR_WEEKLY_REPORT_READ', 'WEEKLY_REPORT_FEEDBACK'], r'''
assert(document.getElementById('createReportSection').hidden&&document.querySelector('.feedback-form'),'Mentor can only respond');
assert(document.querySelector('.report-feedback-status.is-reviewed').textContent==='Đã phản hồi','reviewed status visible');
reports[0].feedback=[];document.getElementById('reloadReports').click();await settle();
assert(document.querySelector('.report-feedback-status.is-pending').textContent==='Chưa phản hồi','pending status visible');
const form=document.querySelector('.feedback-form');form.querySelector('textarea').value='New feedback';
form.dispatchEvent(new Event('submit',{cancelable:true}));form.dispatchEvent(new Event('submit',{cancelable:true}));await settle();
assert(document.querySelector('.report-feedback-status.is-reviewed'),'saved feedback updates status');
assert(writes.length===1&&writes[0].url.endsWith('/weekly-reports/1/feedback')&&Object.keys(writes[0].body).length===1,'single feedback POST with no report overwrite');
assert(document.getElementById('reportList').textContent.includes('New feedback'),'new feedback displayed');
session.permissions=['MENTOR_WEEKLY_REPORT_READ'];document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:session}));
assert(document.querySelector('.feedback-form').hidden,'revoked feedback action hidden');
'''),
    ('weekly-reports.html', 'MENTOR', ['MENTOR_WEEKLY_REPORT_READ'], r'''
assert(document.getElementById('createReportSection').hidden&&!document.querySelector('.feedback-form')&&document.querySelector('.weekly-report'),'read-only Mentor no write action');
'''),
]
checks = r'''<pre id="result"></pre><script>
(async()=>{const assert=(v,m)=>{if(!v)throw Error(m)},settle=()=>new Promise(r=>setTimeout(r,120));
try{await settle();ACTIONS;
assert(document.documentElement.scrollWidth<=innerWidth,'no horizontal page overflow');
document.getElementById('result').textContent='PASS';}
catch(e){document.getElementById('result').textContent='FAIL '+e.message;}})();
</script>'''
with TemporaryDirectory(prefix='task-reports-ui-') as directory:
    temp = Path(directory)
    for i, (page, role, grants, actions) in enumerate(cases):
        html = (pages / page).read_text(encoding='utf-8').replace('<head>', '<head><base href="' + pages.as_uri() + '/"><script>localStorage.setItem("token","test");</script>')
        mock = setup.replace('ROLE', json.dumps(role)).replace('GRANTS', json.dumps(grants))
        html = html.replace('<script src="../js/navigation.js">', mock + '<script src="../js/navigation.js">')
        target = temp / f'{i}.html'
        target.write_text(html.replace('</body>', checks.replace('ACTIONS', actions) + '</body>'), encoding='utf-8')
        for width in (390, 1440):
            result = subprocess.run([str(chrome), '--headless', '--disable-gpu', '--no-sandbox', '--user-data-dir=' + str(temp / f'profile-{i}-{width}'), f'--window-size={width},1000', '--virtual-time-budget=3500', '--dump-dom', target.as_uri()], capture_output=True, text=True, encoding='utf-8', timeout=40)
            match = re.search(r'<pre id="result">(.*?)</pre>', result.stdout, re.S)
            status = match[1] if match else 'NO RESULT'
            print(page, role, width, status)
            assert status == 'PASS', status
