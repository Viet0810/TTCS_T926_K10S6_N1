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
            while not evaluate('(()=>{try{return ('+expression+')}catch{return false}})()'):
                assert time.monotonic() < until, 'UI wait failed: ' + expression
                time.sleep(.1)


        for name in ('user-manage.html','permissions.html'):
            html=(repo/'Frontend/pages'/name).read_text(encoding='utf-8')
            page=directory/name
            page.write_text(html.replace('<head>','<head>'+bootstrap),encoding='utf-8')
            command('Page.navigate',{'url':page.as_uri()})
            wait('!!document.querySelector('+json.dumps('#roleMenu a[href="user-manage.html"]')+') && !!document.querySelector('+json.dumps('#roleMenu a[href="permissions.html"]')+')')
            if name=='permissions.html':
                wait('typeof matrix!=="undefined" && matrix && !busy')
                assert evaluate('[...document.querySelectorAll("[data-role]")].map(x=>x.dataset.role).join(",")') == 'ADMIN,HR,MENTOR,INTERN', 'RBAC must use four role tabs'
            else:
                wait('document.querySelectorAll("#userTableBody tr").length>0')
                assert not evaluate('document.getElementById("userTableBody").textContent.includes("s3-admin@example.org")'), 'ADMIN account leaked into business account list'
            links=evaluate('[...document.querySelectorAll("#roleMenu a")].map(x=>x.getAttribute("href"))')
            assert links == ['dashboard.html','user-manage.html','permissions.html','profile.html'], 'ADMIN sidebar must contain exactly four administration links'
            command('Page.reload')
            wait('!!document.querySelector('+json.dumps('#roleMenu a[href="permissions.html"]')+')')
            print('PASS: ADMIN navigation, page and reload',name)
    finally:
        if ws: ws.close()
        process.terminate()
        process.wait(timeout=15)
