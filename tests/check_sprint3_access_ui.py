"""Chrome tests for read-only Sprint 3 pages and rejected notification requests."""
from pathlib import Path
from tempfile import TemporaryDirectory
import json
import os
import re
import subprocess

repo = Path(__file__).resolve().parents[1]
pages = repo / 'Frontend/pages'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
setup = r'''<script>
let accessSession={user:{id:12,role:'INTERN'},permissions:GRANTS},reads=[],deny=false;
API.getCurrentUser=async()=>accessSession;
window.fetch=async(url,options={})=>{
 reads.push(url);
 if(deny)return {ok:false,status:403,headers:{get:()=>null},json:async()=>({message:'Permission denied'})};
 let data={};
 if(url.endsWith('/meetings'))data=[{id:1,title:'Own meeting',startTime:'2027-10-15T08:30:00+07:00',endTime:'2027-10-15T09:30:00+07:00',participantUserIds:[12],emailsSent:1}];
 else if(url.includes('/notifications/unread-count'))data={unreadCount:1};
 else if(url.includes('/notifications'))data={items:[{id:1,title:'Own notice',message:'Private notice',category:'SYSTEM',priority:'INFO',isRead:false,createdAt:'2026-10-15T08:30:00+07:00'}],total:1,unreadCount:1};
 else if(url.includes('/leave-requests'))data={items:[{id:1,leaveType:'OTHER',leaveTypeLabel:'Other',durationType:'FULL_DAY',durationTypeLabel:'Full day',startDate:'2026-10-22',endDate:'2026-10-22',totalDays:1,reason:'My leave request',status:'PENDING',statusLabel:'Pending',createdAt:'2026-10-15T08:30:00+07:00'}],total:1,pendingCount:1,approvedCount:0,rejectedCount:0,cancelledCount:0};
 return {ok:true,status:200,headers:{get:()=>null},json:async()=>data};
};
</script>'''
cases = [
    ('meetings.html', ['MEETING_READ'], r'''
assert(document.getElementById('meetingCreate').hidden&&!document.getElementById('meetingList').hidden,'participant has read-only meeting UI');
assert(document.getElementById('meetingList').textContent.includes('Own meeting'),'participant meeting displayed');
assert(!reads.some(url=>url.endsWith('/meetings/participants')),'read-only participant does not request directory');
accessSession.permissions=[];document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:accessSession}));await settle();
assert(document.getElementById('meetingList').hidden&&!document.getElementById('meetingList').textContent.includes('Own meeting'),'revocation clears meeting data');
'''),
    ('notifications.html', ['NOTIFICATION_READ'], r'''
assert(document.getElementById('fullNotificationList').textContent.includes('Own notice'),'own notifications displayed');
assert(document.getElementById('pageMarkAllReadBtn').hidden&&!document.querySelector('.mark-single-btn'),'read-only has no mark-read actions');
assert(!document.getElementById('pageSeedBtn')||document.getElementById('pageSeedBtn').hidden,'no unauthorized seed action');
const originalFetch=window.fetch;window.fetch=async()=>new Response('{}',{status:503});let failed=false;
try{await NotificationService.getNotifications();}catch(error){failed=error.status===503;}
assert(failed,'503 list must not return cached success');
window.fetch=originalFetch;
deny=true;let rejected=false;try{await NotificationService.markAsRead(1);}catch(error){rejected=error.status===403;}
assert(rejected,'403 mark-read must not become fake success');
rejected=false;try{await NotificationService.getNotifications();}catch(error){rejected=error.status===403;}
assert(rejected,'403 list must not return stale cache');
accessSession.permissions=[];document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:accessSession}));await settle();
assert(!document.getElementById('fullNotificationList').textContent.includes('Own notice'),'revocation clears notification data');
'''),
    ('intern-leave-request.html', ['OWN_LEAVE_REQUEST_READ'], r'''
assert(document.getElementById('leaveRequestForm').hidden,'no create-leave action without create permission');
assert(!document.querySelector('.btn-action-cancel'),'no cancel action without cancel permission');
accessSession.permissions=['OWN_LEAVE_REQUEST_READ','LEAVE_REQUEST_CREATE','LEAVE_REQUEST_CANCEL'];document.dispatchEvent(new CustomEvent('session-permissions-updated',{detail:accessSession}));
assert(!document.getElementById('leaveRequestForm').hidden,'regrant shows create action');
document.getElementById('refreshListBtn').click();await settle();assert(document.querySelector('.btn-action-cancel'),'regrant shows cancel action on reload');
''')
]
syntax_files = ['permissions.js','navigation.js','meetings.js','user-manage.js','mentor-assignment.js','notification-ui.js','notification-service.js','notifications-page.js','final-evaluation.js','intern-leave-request.js']
sources = [(name, (repo / 'Frontend/js' / name).read_text(encoding='utf-8-sig')) for name in syntax_files]
checks = r'''<pre id="result"></pre><script>
(async()=>{const assert=(v,m)=>{if(!v)throw Error(m)},settle=()=>new Promise(r=>setTimeout(r,200));
try{for(const [name,source] of SOURCES){try{new Function(source)}catch(error){throw Error(name+': '+error.message)}}await settle();ACTIONS;
assert(document.documentElement.scrollWidth<=innerWidth,'no horizontal page overflow');document.getElementById('result').textContent='PASS';}
catch(e){document.getElementById('result').textContent='FAIL '+e.message;}})();
</script>'''.replace('SOURCES', json.dumps(sources).replace('</', r'<\/'))
with TemporaryDirectory(prefix='sprint3-access-ui-') as directory:
    temp = Path(directory)
    for i, (page, grants, actions) in enumerate(cases):
        html = (pages / page).read_text(encoding='utf-8').replace('<head>', '<head><base href="' + pages.as_uri() + '/"><script>localStorage.setItem("token","test");</script>')
        html = html.replace('<script src="../js/navigation.js">', setup.replace('GRANTS', json.dumps(grants)) + '<script src="../js/navigation.js">')
        target = temp / f'{i}.html'
        target.write_text(html.replace('</body>', checks.replace('ACTIONS', actions) + '</body>'), encoding='utf-8')
        for width in (390, 1440):
            result = subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox','--user-data-dir=' + str(temp / f'profile-{i}-{width}'),f'--window-size={width},1000','--virtual-time-budget=4000','--dump-dom',target.as_uri()],capture_output=True,text=True,encoding='utf-8',timeout=40)
            match = re.search(r'<pre id="result">(.*?)</pre>', result.stdout, re.S)
            status = match[1] if match else 'NO RESULT'
            print(page, width, status)
            assert status == 'PASS', status
