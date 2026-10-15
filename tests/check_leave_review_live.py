from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.error import HTTPError
from urllib.request import Request, urlopen
import json, os, subprocess, sys, time, websocket

repo = Path(__file__).resolve().parents[1]
base = json.loads(sys.stdin.readline())['base'].rstrip('/')
assert base.startswith('http://127.0.0.1:'), 'Only isolated loopback API is accepted'

def api(path, method='GET', payload=None, token=None, status=False):
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    request=Request(base+path,data=None if payload is None else json.dumps(payload).encode(),headers=headers,method=method)
    try:
        with urlopen(request,timeout=20) as response:
            return response.status if status else json.load(response)
    except HTTPError as error:
        if status: return error.code
        raise

def login(username):
    return api('/api/auth/login','POST',{'username':username,'password':'Ownership-test-9'})['token']

chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='leave-review-live-') as directory:
    directory=Path(directory); ws=None; sequence=0
    process=subprocess.Popen([str(chrome),'--headless','--disable-gpu','--no-sandbox','--allow-file-access-from-files','--remote-debugging-port=0','--user-data-dir='+str(directory/'profile'),'about:blank'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    try:
        port_file=directory/'profile/DevToolsActivePort'; deadline=time.monotonic()+20
        while not port_file.exists():
            assert time.monotonic()<deadline,'Chrome startup timeout';time.sleep(.1)
        port=port_file.read_text().splitlines()[0]
        targets=json.load(urlopen('http://127.0.0.1:'+port+'/json/list'))
        ws=websocket.create_connection(next(p['webSocketDebuggerUrl'] for p in targets if p['type']=='page'),suppress_origin=True,timeout=20)
        def command(method,params=None):
            nonlocal_sequence=None
            global sequence
            sequence+=1;ws.send(json.dumps({'id':sequence,'method':method,'params':params or {}}))
            while True:
                result=json.loads(ws.recv())
                if result.get('id')==sequence:
                    assert 'error' not in result,'CDP command failed';return result.get('result',{})
        def evaluate(script):
            result=command('Runtime.evaluate',{'expression':script,'returnByValue':True,'awaitPromise':True})
            assert 'exceptionDetails' not in result,'UI JavaScript exception';return result.get('result',{}).get('value')
        def wait(expression):
            until=time.monotonic()+25
            while not evaluate(expression):
                assert time.monotonic()<until,'UI wait failed: '+expression;time.sleep(.1)
        def open_page(name,username):
            token=login(username);html=(repo/'Frontend/pages'/name).read_text(encoding='utf-8')
            bootstrap='<base href="'+(repo/'Frontend/pages').as_uri()+'/"><script>window.INTERN_API_BASE_URL='+json.dumps(base+'/api')+';localStorage.setItem("token",'+json.dumps(token)+');</script>'
            page=directory/(username+'-'+name);page.write_text(html.replace('<head>','<head>'+bootstrap),encoding='utf-8')
            command('Page.navigate',{'url':page.as_uri()});wait('!!document.querySelector("#roleMenu a")');return token

        intern=open_page('intern-leave-request.html','ownership-intern-a')
        wait('!document.getElementById("submitLeaveBtn").disabled')
        evaluate('document.getElementById("startDate").value="2026-12-15";document.getElementById("startDate").dispatchEvent(new Event("change"));document.getElementById("reason").value="Chrome Mentor review request";document.getElementById("leaveRequestForm").requestSubmit()')
        wait('document.getElementById("leaveRequestsTableBody")?.textContent.includes("Chrome Mentor review request")')
        command('Page.reload');wait('document.getElementById("leaveRequestsTableBody")?.textContent.includes("Chrome Mentor review request")')

        wrong=login('ownership-mentor-a')
        request_id=next(item['id'] for item in api('/api/interns/me/leave-requests',token=intern)['items'] if item['reason']=='Chrome Mentor review request')
        assert api('/api/mentor/leave-requests/'+str(request_id),token=wrong,status=True)==403,'Wrong Mentor detail must return 403'
        assert api('/api/mentor/leave-requests/'+str(request_id)+'/review','POST',{'decision':'APPROVED','note':'forged'},wrong,True)==403,'Wrong Mentor review must return 403'

        open_page('mentor-leave-requests.html','ownership-mentor-b')
        wait('document.getElementById("mentorLeaveBody")?.textContent.includes("Chrome Mentor review request")')
        evaluate('[...document.querySelectorAll("#mentorLeaveBody tr")].find(r=>r.textContent.includes("Chrome Mentor review request")).querySelector("[data-detail]").click()')
        wait('!document.getElementById("reviewModal").hidden && !document.getElementById("approveLeaveBtn").hidden')
        evaluate('document.getElementById("reviewNote").value="Approved in Chrome";document.getElementById("approveLeaveBtn").click()')
        wait('document.getElementById("mentorLeaveBody")?.textContent.includes("Chrome Mentor review request") && !document.getElementById("mentorLeaveBody").textContent.includes("Chờ duyệt")')
        command('Page.reload');wait('document.getElementById("mentorLeaveBody")?.textContent.includes("Chrome Mentor review request")')

        open_page('intern-leave-request.html','ownership-intern-a')
        wait('document.getElementById("leaveRequestsTableBody")?.textContent.includes("Chrome Mentor review request") && document.getElementById("leaveRequestsTableBody").textContent.includes("Approved in Chrome")')
        print('PASS: Chrome US24 Intern create, assigned Mentor approve/reload, Intern result, wrong Mentor 403')
    finally:
        if ws: ws.close()
        process.terminate();process.wait(timeout=15)
