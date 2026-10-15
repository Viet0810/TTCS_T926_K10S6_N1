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

chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='reports-live-') as directory:
    directory=Path(directory)
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

        def open_page(name, username):
            token=api('/api/auth/login','POST',{'username':username,'password':'Progress-report-test-9'})['token']
            html=(repo/'Frontend/pages'/name).read_text(encoding='utf-8')
            bootstrap='<base href="'+(repo/'Frontend/pages').as_uri()+'/"><script>window.INTERN_API_BASE_URL='+json.dumps(base+'/api')+';localStorage.setItem("token",'+json.dumps(token)+');</script>'
            page=directory/(username+'-'+name)
            page.write_text(html.replace('<head>','<head>'+bootstrap),encoding='utf-8')
            command('Page.navigate',{'url':page.as_uri()})
            wait('!!document.querySelector(' + json.dumps('#roleMenu a[href="'+('work-schedules.html' if name=='work-schedules.html' else 'weekly-reports.html')+'"]') + ')')
            return token
        open_page('tasks.html','report-ia')
        wait('!!document.querySelector(".task-update-action") && !document.querySelector(".task-update-action").disabled')
        evaluate('document.querySelector(".task-update-action").click();document.getElementById("taskProgress").value=50;document.getElementById("progressForm").requestSubmit()')
        wait('document.querySelector(".task-progress-bar")?.value===50 && !document.getElementById("saveProgress").disabled')
        command('Page.reload');wait('document.querySelector(".task-progress-bar")?.value===50')
        token=open_page('weekly-reports.html','report-ia')
        wait('!document.getElementById("submitReport").disabled')
        evaluate("document.getElementById('reportWeek').value='2026-11-02';['reportContent','reportResults','reportDifficulties','reportNextPlan'].forEach(id=>document.getElementById(id).value='Live UI '+id);document.getElementById('reportForm').requestSubmit()")
        wait('document.getElementById("reportList").textContent.includes("Live UI reportNextPlan")')
        command('Page.reload');wait('document.getElementById("reportList")?.textContent.includes("Live UI reportNextPlan")')
        report=next(r for r in api('/api/weekly-reports',token=token) if 'Live UI reportContent' in r['content'])
        assert report['workDone']=='Live UI reportContent' and report['nextPlan']=='Live UI reportNextPlan','Report sections persist separately'
        open_page('weekly-reports.html','report-ma')
        selector='[data-report-id="'+str(report['id'])+'"]'
        wait('!!document.querySelector('+json.dumps(selector+' .feedback-form')+')')
        evaluate('document.getElementById("reportInternFilter").value='+json.dumps(str(report['internId']))+';document.getElementById("reportInternFilter").dispatchEvent(new Event("change"))')
        evaluate('document.querySelector('+json.dumps(selector+' textarea')+').value="Live Mentor feedback";document.querySelector('+json.dumps(selector+' form')+').requestSubmit()')
        wait('document.querySelector('+json.dumps(selector)+')?.textContent.includes("Live Mentor feedback")')
        open_page('weekly-reports.html','report-ia')
        wait('document.getElementById("reportList")?.textContent.includes("Live Mentor feedback")')
        for width in (390,1440):
            command('Emulation.setDeviceMetricsOverride',{'width':width,'height':1000,'deviceScaleFactor':1,'mobile':width<640})
            assert evaluate('document.documentElement.scrollWidth<=innerWidth'),'Page overflow'
        open_page('weekly-reports.html','report-mb')
        wait('document.getElementById("reportList").getAttribute("aria-busy")==="false"')
        assert not evaluate('!!document.querySelector('+json.dumps(selector)+')'),'Foreign report visible'
        status=evaluate('(async()=>{const r=await fetch(window.INTERN_API_BASE_URL+"/weekly-reports/'+str(report['id'])+'",{headers:{Authorization:"Bearer "+localStorage.getItem("token")}});return r.status})()')
        assert status==403,'Foreign Mentor should receive 403'
        open_page('work-schedules.html','report-hr')
        wait('document.getElementById("scheduleIntern").options.length>1 && !document.getElementById("saveSchedule").disabled')
        evaluate('document.getElementById("scheduleIntern").value='+json.dumps(str(report['internId']))+';document.getElementById("scheduleDate").value="2026-11-03";document.getElementById("scheduleStart").value="09:00";document.getElementById("scheduleEnd").value="18:00";document.getElementById("scheduleForm").requestSubmit()')
        wait('document.getElementById("scheduleList").textContent.includes("03/11/2026")')
        open_page('work-schedules.html','report-ia')
        wait('document.getElementById("scheduleList").textContent.includes("03/11/2026")')
        assert evaluate('document.getElementById("scheduleForm").hidden'),'Intern schedule read only'
        command('Page.reload');wait('document.getElementById("scheduleList")?.textContent.includes("03/11/2026")')
        for width in (390,1440):
            command('Emulation.setDeviceMetricsOverride',{'width':width,'height':1000,'deviceScaleFactor':1,'mobile':width<640})
            assert evaluate('document.documentElement.scrollWidth<=innerWidth'),'Schedule overflow'
        print('PASS: live US23 create/read/reload, role navigation, desktop/mobile')
        print('PASS: live US16-US18 UI, navigation, persistence, feedback, foreign Mentor 403')
    finally:
        if ws:
            ws.close()
        process.terminate()
        process.wait(timeout=15)
