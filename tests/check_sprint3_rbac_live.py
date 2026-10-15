"""Real Sprint 3 permission matrix -> Admin API -> isolated test SQL database."""
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.request import Request, urlopen
import json
import os
import subprocess
import sys
import time
import websocket

repo = Path(__file__).resolve().parents[1]
base = json.loads(sys.stdin.readline())['base'].rstrip('/')
assert base.startswith('http://127.0.0.1:'), 'Only isolated loopback API is accepted'

def api(path, method='GET', payload=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    request = Request(base + path, data=None if payload is None else json.dumps(payload).encode(), headers=headers, method=method)
    with urlopen(request, timeout=20) as response:
        return json.load(response)

admin = api('/api/auth/login', 'POST', {'username': 's3-admin', 'password': 'Sprint3-rbac-test-9'})['token']
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='sprint3-rbac-live-') as directory:
    directory = Path(directory)
    page = directory / 'permissions.html'
    html = (repo / 'Frontend/pages/permissions.html').read_text(encoding='utf-8')
    bootstrap = '<base href="' + (repo / 'Frontend/pages').as_uri() + '/"><script>window.INTERN_API_BASE_URL=' + json.dumps(base + '/api') + ';localStorage.setItem("token",' + json.dumps(admin) + ');</script>'
    page.write_text(html.replace('<head>', '<head>' + bootstrap), encoding='utf-8')
    process = subprocess.Popen([str(chrome), '--headless', '--disable-gpu', '--no-sandbox', '--allow-file-access-from-files', '--remote-debugging-port=0', '--user-data-dir=' + str(directory / 'profile'), 'about:blank'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    ws = None
    sequence = 0
    try:
        port_file = directory / 'profile/DevToolsActivePort'
        deadline = time.monotonic() + 20
        while not port_file.exists():
            assert time.monotonic() < deadline, 'Chrome startup timeout'
            time.sleep(.1)
        port = port_file.read_text().splitlines()[0]
        targets = json.load(urlopen('http://127.0.0.1:' + port + '/json/list'))
        ws = websocket.create_connection(next(p['webSocketDebuggerUrl'] for p in targets if p['type'] == 'page'), suppress_origin=True, timeout=20)

        def command(method, params=None):
            global sequence
            sequence += 1
            ws.send(json.dumps(dict(id=sequence, method=method, params=params or {})))
            while True:
                result = json.loads(ws.recv())
                if result.get('id') == sequence:
                    assert 'error' not in result, 'CDP command failed'
                    return result.get('result', {})

        def evaluate(script):
            result = command('Runtime.evaluate', {'expression': script, 'returnByValue': True, 'awaitPromise': True})
            assert 'exceptionDetails' not in result, 'UI JavaScript exception'
            return result.get('result', {}).get('value')

        def wait(expression):
            until = time.monotonic() + 25
            while not evaluate(expression):
                assert time.monotonic() < until, 'UI wait failed: ' + expression
                time.sleep(.1)

        command('Page.navigate', {'url': page.as_uri()})
        wait('typeof matrix !== "undefined" && matrix && !busy')
        for code in api('/api/permissions', token=admin)['essentialAdminPermissions']:
            assert evaluate('document.querySelector(' + json.dumps('[data-permission="' + code + '"]') + ').disabled'), 'Admin essential checkbox must be locked'
        assert not evaluate('document.getElementById("permissionMatrix").textContent.includes("MEETING_CREATE")'), 'Raw codes must not be primary labels'
        for role, code in [('MENTOR', 'TASK_CREATE'), ('INTERN', 'OWN_TASK_UPDATE'), ('MENTOR', 'WEEKLY_REPORT_FEEDBACK'), ('HR', 'ASSIGNMENT_CREATE'), ('HR', 'MENTOR_WORKLOAD_READ'), ('HR', 'MEETING_CREATE'), ('INTERN', 'NOTIFICATION_UPDATE'), ('INTERN', 'NOTIFICATION_READ'), ('INTERN', 'LEAVE_REQUEST_CANCEL')]:
            original = api('/api/permissions/' + role, token=admin)['permissions']
            evaluate('document.querySelector(' + json.dumps('[data-role="' + role + '"]') + ').click()')
            selector = json.dumps('[data-permission="' + code + '"]')
            wait('selectedRole === ' + json.dumps(role) + ' && !busy && !!document.querySelector(' + selector + ')')
            assert evaluate('document.querySelector(' + selector + ').checked') == (code in original), 'Checkbox differs from DB'
            evaluate('document.querySelector(' + selector + ').click()')
            assert evaluate('dirty() && !document.getElementById("savePermissionsBtn").disabled'), 'Missing dirty state'
            expected = evaluate('[...draft].sort()')
            evaluate('document.getElementById("savePermissionsBtn").click()')
            wait('!busy && !dirty()')
            saved = api('/api/permissions/' + role, token=admin)['permissions']
            assert sorted(saved) == expected, 'UI save/refetch differs from DB'
            assert evaluate('document.querySelector(' + selector + ').checked') == (code in saved), 'Refetched checkbox differs from DB'
            # Restore the isolated fixture, including dependent grants removed by unchecking READ.
            api('/api/permissions/' + role, 'PUT', {'permissions': original}, token=admin)
            evaluate('loadRolePermissions()')
            wait('!busy && !dirty()')
            print('PASS: live matrix DB load/save/refetch', role, code)
        for width in (390, 1440):
            command('Emulation.setDeviceMetricsOverride', {'width': width, 'height': 1000, 'deviceScaleFactor': 1, 'mobile': width < 640})
            assert evaluate('document.documentElement.scrollWidth <= innerWidth'), 'Page overflow'
        print('PASS: role tabs, Vietnamese labels, dirty state, dependency cascade, Admin protection, mobile/desktop')

        def open_feature(name, username):
            token=api('/api/auth/login','POST',{'username':username,'password':'Sprint3-rbac-test-9'})['token']
            html=(repo/'Frontend/pages'/name).read_text(encoding='utf-8')
            bootstrap='<base href="'+(repo/'Frontend/pages').as_uri()+'/"><script>window.INTERN_API_BASE_URL='+json.dumps(base+'/api')+';localStorage.setItem("token",'+json.dumps(token)+');</script>'
            page=directory/(username+'-'+name)
            page.write_text(html.replace('<head>','<head>'+bootstrap),encoding='utf-8')
            command('Page.navigate',{'url':page.as_uri()})
            wait('!!document.querySelector("#roleMenu a")')
            return token
        def smoke():
            for width in (390,1440):
                command('Emulation.setDeviceMetricsOverride',{'width':width,'height':1000,'deviceScaleFactor':1,'mobile':width<640})
                assert evaluate('document.documentElement.scrollWidth<=innerWidth'),'Feature overflow'
        # Test real UI writes against isolated SQL; SMTP is disabled in this API fixture.
        open_feature('user-manage.html','s3-admin')
        wait('document.querySelectorAll("#userTableBody tr").length>0')
        assert not evaluate('document.getElementById("userTableBody").textContent.includes("s3-admin@example.org")'),'Admin account leaked into business account list'
        assert evaluate('!!document.querySelector(' + json.dumps('#roleMenu a[href="permissions.html"]') + ')'),'Admin RBAC menu missing'
        command('Page.reload')
        wait('document.querySelectorAll("#userTableBody tr").length>0')
        assert not evaluate('document.getElementById("userTableBody").textContent.includes("s3-admin@example.org")'),'Admin account leaked after reload'
        open_feature('permissions.html','s3-admin')
        wait('typeof matrix !== "undefined" && matrix && !busy')
        assert evaluate('!!document.querySelector(' + json.dumps('#roleMenu a[href="user-manage.html"]') + ')'),'Admin account menu missing'
        print('PASS: ADMIN login, account management, RBAC matrix, menus, reload')
        hr=open_feature('user-create.html','s3-hr')
        wait('document.getElementById("role").value==="MENTOR"')
        evaluate('document.getElementById("fullName").value="Live Mentor";document.getElementById("email").value="live-mentor@example.org";document.getElementById("createUserForm").requestSubmit()')
        until=time.monotonic()+20
        while True:
            payload=api('/api/users',token=hr)
            users=payload if isinstance(payload,list) else payload.get('items',payload.get('data',[]))
            if isinstance(users,dict): users=users.get('items',[])
            if any(u.get('email')=='live-mentor@example.org' for u in users):break
            assert time.monotonic()<until,'Mentor creation UI failed'
            time.sleep(.2)
        open_feature('user-manage.html','s3-hr')
        wait('document.getElementById("userTableBody").textContent.includes("live-mentor@example.org")')
        assert evaluate('document.title.startsWith("Quản lý Mentor")'),'HR page title must identify Mentor management'
        assert evaluate('document.querySelector(".dashboard-title h1").textContent.trim()==="Quản lý Mentor"'),'HR header still uses Admin account wording'
        assert evaluate('document.querySelector(".user-list-heading h2").textContent.trim()==="Quản lý Mentor"'),'HR list heading still uses account wording'
        assert evaluate('document.getElementById("createUserLink").textContent.trim()==="+ Thêm Mentor"'),'HR create action wording is incorrect'
        assert evaluate('[...document.querySelectorAll("#userTableBody .badge")].every(x=>x.textContent.trim()==="MENTOR")'),'HR account table contains a non-Mentor role'
        smoke()
        print('PASS: Chrome US29 create Mentor and persisted list')

        open_feature('mentor-assignment.html','s3-hr')
        wait('document.getElementById("internId").options.length>1')
        evaluate('const s=document.getElementById("internId");s.value=[...s.options].find(o=>o.textContent.includes("Unassigned")).value;s.dispatchEvent(new Event("change"));const m=document.getElementById("mentorUserId");m.value=[...m.options].find(o=>o.textContent.includes("Mentor B")).value;document.getElementById("assignmentForm").requestSubmit()')
        wait('!!document.querySelector(".shared-confirm[open]")')
        evaluate('document.querySelector(".shared-confirm button[value=confirm]").click()')
        wait('!document.getElementById("assignBtn").disabled')
        open_feature('mentor-assignment-list.html','s3-hr')
        wait('document.getElementById("mentorLoadRows").textContent.includes("Mentor B")')
        assert evaluate('document.getElementById("assignmentRows").textContent.includes("Unassigned")'),'Assignment persisted'
        smoke()
        print('PASS: Chrome US30 assignment and US31 DB workload')

        open_feature('tasks.html','s3-ma')
        wait('document.getElementById("taskIntern").options.length>1')
        evaluate('document.getElementById("taskIntern").selectedIndex=1;document.getElementById("taskContent").value="Live task from Mentor";document.getElementById("taskDeadline").value="2027-02-02T17:00";document.getElementById("taskForm").requestSubmit()')
        wait('!!document.querySelector(".shared-confirm[open]")')
        evaluate('document.querySelector(".shared-confirm button[value=confirm]").click()')
        wait('document.getElementById("taskRows").textContent.includes("Live task from Mentor")')
        open_feature('tasks.html','s3-ia')
        wait('document.getElementById("taskRows").textContent.includes("Live task from Mentor")')
        smoke()
        print('PASS: Chrome US15 actual task assignment and own Intern view')

        open_feature('final-evaluation.html','s3-ma')
        wait('!!document.querySelector("#evaluationTableBody .primary-btn")')
        evaluate('document.querySelector("#evaluationTableBody .primary-btn").click();document.getElementById("evaluationPeriod").value="Live period";["evaluationAttendance","evaluationAttitude","evaluationProfessionalSkills","evaluationWorkResult","evaluationFinalScore"].forEach(id=>document.getElementById(id).value=8);document.getElementById("evaluationClassification").selectedIndex=2;document.getElementById("evaluationComment").value="Live Mentor evaluation";document.getElementById("evaluationForm").requestSubmit()')
        wait('document.getElementById("evaluationFormSection").hidden')
        smoke()
        open_feature('final-evaluation.html','s3-hr')
        wait('document.getElementById("evaluationTableBody").textContent.includes("Live period")')
        evaluate('const row=[...document.querySelectorAll("#evaluationTableBody tr")].find(r=>r.textContent.includes("Live period"));row.querySelector(".detail-btn").click()')
        wait('document.getElementById("internInfo").textContent.includes("Live Mentor evaluation")')
        smoke()
        print('PASS: Chrome US19 Mentor evaluation reaches US20 HR details')

        intern=open_feature('intern-leave-request.html','s3-ia')
        wait('!document.getElementById("submitLeaveBtn").disabled')
        evaluate('document.getElementById("startDate").value="2027-02-01";document.getElementById("startDate").dispatchEvent(new Event("change"));document.getElementById("reason").value="Live leave request reason";document.getElementById("leaveRequestForm").requestSubmit()')
        wait('!!document.querySelector(".btn-action-cancel")')
        command('Page.reload');wait('!!document.querySelector(".btn-action-cancel")')
        smoke()
        evaluate('document.querySelector(".btn-action-cancel").click();document.getElementById("confirmCancelBtn").click()')
        wait('document.getElementById("cancelModal").style.display==="none"')
        print('PASS: Chrome US24 own leave create/reload/cancel')

        open_feature('meetings.html','s3-hr')
        wait('!!document.querySelector("#participantOptions input")')
        evaluate('document.querySelector("#meetingForm [name=title]").value="Live meeting";document.getElementById("meetingStart").value="2027-02-02T09:00";document.getElementById("meetingStart").dispatchEvent(new Event("change"));document.getElementById("meetingEnd").value="2027-02-02T10:00";document.getElementById("meetingEnd").dispatchEvent(new Event("change"));const p=[...document.querySelectorAll("#participantOptions label")].find(x=>x.textContent.includes("s3-ia@example.org"));p.querySelector("input").click();document.getElementById("meetingForm").requestSubmit()')
        wait('document.getElementById("meetingRows").textContent.includes("Live meeting")')
        smoke()
        open_feature('meetings.html','s3-ia')
        wait('document.getElementById("meetingRows").textContent.includes("Live meeting")')
        print('PASS: Chrome US35 HR creates meeting, participant reads persisted meeting')
        open_feature('notifications.html','s3-ia')
        wait('document.body.textContent.includes("Live meeting")')
        assert not evaluate('!!document.getElementById("pageSeedBtn")'),'No production demo'
        evaluate('document.getElementById("pageMarkAllReadBtn").click()')
        wait('document.getElementById("unreadNotifsCount").textContent==="0"')
        smoke()
        print('PASS: Chrome US36 real meeting notification and mark all read')
    finally:
        if ws:
            ws.close()
        process.terminate()
        process.wait(timeout=15)
