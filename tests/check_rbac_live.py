"""Real Admin UI -> live API -> temporary SQL integration database.

Invoked only by ApiChecks with its isolated API address on stdin.
"""
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.request import Request, urlopen
import json, os, subprocess, sys, time
import websocket

repo = Path(__file__).resolve().parents[1]
base = json.loads(sys.stdin.readline())['base'].rstrip('/')
assert base.startswith('http://127.0.0.1:'), 'Only isolated loopback test APIs are accepted'
def api(path, method='GET', payload=None, token=None):
    headers = {'Content-Type':'application/json'}
    if token: headers['Authorization'] = 'Bearer '+token
    request = Request(base+path, data=None if payload is None else json.dumps(payload).encode(), headers=headers, method=method)
    with urlopen(request, timeout=10) as response: return json.load(response)
admin = api('/api/auth/login', 'POST', {'username':'permission-admin','password':'Permission-test-9'})['token']
hr = api('/api/auth/login', 'POST', {'username':'permission-hr','password':'Permission-test-9'})['token']
mentor = api('/api/auth/login', 'POST', {'username':'permission-mentor','password':'Permission-test-9'})['token']
intern = api('/api/auth/login', 'POST', {'username':'permission-intern','password':'Permission-test-9'})['token']
profile = api('/api/interns', token=hr)[0]
chrome = Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='rbac-live-') as directory:
    directory = Path(directory)
    page = directory/'permissions.html'
    html = (repo/'Frontend/pages/permissions.html').read_text(encoding='utf-8')
    bootstrap = '<base href="'+(repo/'Frontend/pages').as_uri()+'/"><script>window.INTERN_API_BASE_URL='+json.dumps(base+'/api')+';localStorage.setItem("token",'+json.dumps(admin)+');</script>'
    page.write_text(html.replace('<head>', '<head>'+bootstrap), encoding='utf-8')
    process = subprocess.Popen([str(chrome),'--headless','--disable-gpu','--no-sandbox','--allow-file-access-from-files','--remote-debugging-port=0','--user-data-dir='+str(directory/'profile'),'about:blank'], stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    ws = None
    sequence = 0
    try:
        port_file = directory/'profile/DevToolsActivePort'
        deadline = time.monotonic()+15
        while not port_file.exists():
            assert time.monotonic()<deadline, 'Chrome startup timeout'; time.sleep(.05)
        port = port_file.read_text().splitlines()[0]
        pages = json.load(urlopen('http://127.0.0.1:'+port+'/json/list'))
        ws = websocket.create_connection(next(p['webSocketDebuggerUrl'] for p in pages if p['type']=='page'), suppress_origin=True, timeout=15)
        def command(method, params=None):
            global sequence
            sequence += 1; ws.send(json.dumps(dict(id=sequence,method=method,params=params or {})))
            while True:
                result = json.loads(ws.recv())
                if result.get('id') == sequence:
                    assert 'error' not in result, 'CDP command failed'; return result.get('result',{})
        def evaluate(expression, wait=False):
            result = command('Runtime.evaluate',dict(expression=expression,returnByValue=True,awaitPromise=wait))
            assert 'exceptionDetails' not in result, 'Browser script failed'
            return result.get('result',{}).get('value')
        def until(expression):
            deadline = time.monotonic()+15
            while not evaluate(expression):
                assert time.monotonic()<deadline, 'Live UI did not settle'; time.sleep(.05)
        command('Page.navigate', {'url':page.as_uri()})
        until('typeof matrix!=="undefined" && !!matrix && !busy')
        evaluate('document.querySelector("[data-role=HR]").click()')
        def toggle_save(code):
            evaluate('document.querySelector("[data-permission='+code+']").click();document.getElementById("savePermissionsBtn").click()')
            until('!busy && !dirty() && document.getElementById("retryPermissionsBtn").hidden')
            return evaluate('document.querySelector("[data-permission='+code+']").checked')
        def hr_status(path, method='GET', payload=None, token=hr):
            options = dict(method=method,headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
            if payload is not None: options['body'] = json.dumps(payload)
            return evaluate('fetch('+json.dumps(base+path)+','+json.dumps(options)+').then(r=>r.status)', True)
        assert not toggle_save('EDIT_INTERNS')
        assert hr_status('/api/interns/'+str(profile['id']),'PUT',profile)==403
        assert toggle_save('EDIT_INTERNS')
        assert hr_status('/api/interns/'+str(profile['id']),'PUT',profile)==200
        assert not toggle_save('APPROVE_DOCUMENTS')
        assert hr_status('/api/document-reviews')==200
        assert hr_status('/api/document-reviews/1/cv/approve','PUT',{'comment':'','version':'AAAAAAAAAAE='})==403
        assert toggle_save('APPROVE_DOCUMENTS')
        assert 'APPROVE_DOCUMENTS' in api('/api/permissions',token=admin)['roles']['HR']
        # Sprint 2 lifecycle fixture uses real registration, upload, review and contract APIs.
        live_email='live-sprint2@ictu.edu.vn'
        api('/api/auth/register','POST',dict(fullName='Live Sprint 2',email=live_email,password='Live-test-9',phone='0912345678',school='ICTU',major='IT'))
        live_token=api('/api/auth/login','POST',dict(username=live_email,password='Live-test-9'))['token']
        live_owner=api('/api/interns/me',token=live_token)['id']
        def upload_status(path,token):
            expression='const body=new FormData();body.append("file",new Blob(["%PDF-1.4 live"],{type:"application/pdf"}),"live.pdf");return (await fetch('+json.dumps(base+path)+',{method:"PUT",headers:{Authorization:'+json.dumps('Bearer '+token)+'},body})).status'
            return evaluate('(async()=>{'+expression+'})()',True)
        assert upload_status('/api/interns/me/documents/application',live_token)==200
        doc=next(d for d in api('/api/document-reviews',token=hr) if d['internId']==live_owner and d['kind']=='application')
        assert not toggle_save('APPROVE_DOCUMENTS')
        assert hr_status(f'/api/document-reviews/{live_owner}/application/approve','PUT',dict(version=doc['version'],comment='Accepted'))==403
        assert toggle_save('APPROVE_DOCUMENTS')
        api(f'/api/document-reviews/{live_owner}/application/approve','PUT',dict(version=doc['version'],comment='Accepted'),hr)
        assert not toggle_save('CONTRACT_CREATE')
        assert hr_status('/api/contracts')==200
        assert upload_status('/api/contracts/'+str(live_owner),hr)==403
        assert toggle_save('CONTRACT_CREATE')
        assert upload_status('/api/contracts/'+str(live_owner),hr)==200
        program=api('/api/program-schedule','POST',dict(name='Live lifecycle',department='Engineering',startDate='2026-10-01',endDate='2026-12-31'),hr)['data']['id']
        mentor_id=api('/api/intern-assignments/mentors',token=hr)[0]['id']
        assignment=dict(mentorUserId=mentor_id,internshipProgramId=program)
        assert not toggle_save('ASSIGNMENT_CREATE')
        assert hr_status('/api/intern-assignments')==200
        assert hr_status('/api/intern-assignments/'+str(live_owner),'PUT',assignment)==403
        assert toggle_save('ASSIGNMENT_CREATE')
        assert hr_status('/api/intern-assignments/'+str(live_owner),'PUT',assignment)==200
        assert not toggle_save('VIEW_ATTENDANCE_REPORT')
        assert hr_status('/api/attendance/report')==403
        assert toggle_save('VIEW_ATTENDANCE_REPORT')
        assert hr_status('/api/attendance/report')==200
        evaluate('document.querySelector("[data-role=MENTOR]").click()')
        assert toggle_save('EDIT_INTERNS')
        assert hr_status('/api/interns/'+str(profile['id']),'PUT',profile,mentor)==200
        assert not toggle_save('EDIT_INTERNS')
        assert hr_status('/api/interns/'+str(profile['id']),'PUT',profile,mentor)==403
        for code in ['MANAGE_PROGRAMS','PROGRAM_CREATE','PROGRAM_UPDATE','PROGRAM_DELETE']:
            assert toggle_save(code)
        evaluate('document.querySelector("[data-role=INTERN]").click()')
        defaults = api('/api/permissions/INTERN/defaults',token=admin)['permissions']
        assert not toggle_save('UPLOAD_DOCUMENTS')
        stored = api('/api/permissions',token=admin)['roles']['INTERN']
        assert 'UPLOAD_DOCUMENTS' not in stored and 'VIEW_DOCUMENTS' in stored
        assert toggle_save('UPLOAD_DOCUMENTS')
        assert not toggle_save('VIEW_DOCUMENTS')
        assert hr_status('/api/interns/me/documents',token=intern)==403
        assert toggle_save('VIEW_DOCUMENTS')
        assert toggle_save('UPLOAD_DOCUMENTS')
        assert set(api('/api/permissions',token=admin)['roles']['INTERN'])==set(defaults)
        # Use the new grants in the real Mentor program page, without mocked API requests.
        program_page=directory/'program-setting.html'
        program_html=(repo/'Frontend/pages/program-setting.html').read_text(encoding='utf-8')
        mentor_setup='<base href="'+(repo/'Frontend/pages').as_uri()+'/"><script>window.INTERN_API_BASE_URL='+json.dumps(base+'/api')+';localStorage.setItem("token",'+json.dumps(mentor)+');</script>'
        program_page.write_text(program_html.replace('<head>','<head>'+mentor_setup),encoding='utf-8')
        command('Page.navigate',{'url':program_page.as_uri()})
        until('document.getElementById("programDateForm") && !document.getElementById("programDateForm").hidden && !document.getElementById("programSaveBtn").disabled')
        until("!!document.querySelector('.role-menu a[href=\"program-setting.html\"]') && !document.getElementById('programDateForm').hidden && !document.getElementById('programSaveBtn').disabled")
        evaluate('document.getElementById("programName").value="Live UI Mentor";document.getElementById("programStartDate").value="2026-10-01";document.getElementById("programEndDate").value="2026-10-31";document.getElementById("programSaveBtn").click()')
        until('document.getElementById("programHistoryBody").textContent.includes("Live UI Mentor") && !document.getElementById("programSaveBtn").disabled')
        evaluate('const liveRow=[...document.querySelectorAll("#programHistoryBody tr")].find(row=>row.textContent.includes("Live UI Mentor"));liveRow.querySelector(".btn-secondary").click();document.getElementById("programName").value="Live UI Mentor updated";document.getElementById("programSaveBtn").click()')
        until('document.getElementById("programHistoryBody").textContent.includes("Live UI Mentor updated") && !document.getElementById("programSaveBtn").disabled')
        evaluate('[...document.querySelectorAll("#programHistoryBody tr")].find(row=>row.textContent.includes("Live UI Mentor updated")).querySelector(".btn-delete-program").click()')
        until('!!document.querySelector(".shared-confirm[open]")')
        evaluate('document.querySelector(".shared-confirm [value=confirm]").click()')
        until('!document.getElementById("programHistoryBody").textContent.includes("Live UI Mentor updated") && !document.getElementById("programSaveBtn").disabled')
        api('/api/permissions/MENTOR','PUT',{'permissions':api('/api/permissions/MENTOR/defaults',token=admin)['permissions']},admin)
        evaluate('window.dispatchEvent(new Event("focus"))')
        until("document.getElementById('programDateForm').hidden && !document.querySelector('.role-menu a[href=\"program-setting.html\"]')")
        print('PASS: live Admin browser saves HR, Mentor and Intern grants with runtime API enforcement')
    finally:
        if ws:
            try: ws.send(json.dumps(dict(id=sequence+1,method='Browser.close')))
            except Exception: pass
        try: process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            process.wait(timeout=10)
        if ws: ws.close()
