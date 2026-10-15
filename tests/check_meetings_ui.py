"""Headless Chrome UI checks with mocked API; never sends real email."""
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.request import urlopen
import json, os, subprocess, time
import websocket

repo = Path(__file__).resolve().parents[1]
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
bootstrap = r'''<script>
localStorage.setItem('token','meeting-ui-test');
window.testSession={user:{id:1,role:'HR',fullName:'HR test'},permissions:['MEETING_READ','MEETING_CREATE']};
window.testMeetings=[];window.testPosts=[];window.testFail=false;
window.fetch=async(url,options={})=>{
 const path=new URL(url).pathname;
 const ok=data=>new Response(JSON.stringify(data),{status:200,headers:{'Content-Type':'application/json'}});
 if(path.endsWith('/auth/me'))return ok(window.testSession);
 if(path.endsWith('/meetings/participants'))return ok([{id:2,fullName:'Intern tiếng Việt',email:'intern@company.vn',role:'INTERN'},{id:3,fullName:'Mentor',email:'mentor@gmail.com',role:'MENTOR'}]);
 if(path.endsWith('/meetings')&&options.method==='POST'){
   const body=JSON.parse(options.body);window.testPosts.push(body);
   await new Promise(resolve=>setTimeout(resolve,200));
   const result={...body,id:1,emailsSent:body.participantUserIds.length,emailsFailed:0};window.testMeetings.push(result);
   return new Response(JSON.stringify(result),{status:201,headers:{'Content-Type':'application/json'}});
 }
 if(path.endsWith('/meetings'))return window.testFail?new Response('{}',{status:503}):ok(window.testMeetings);
 return ok([]);
};
</script>'''

with TemporaryDirectory(prefix='meeting-ui-') as directory:
    directory = Path(directory)
    page = directory / 'meetings.html'
    html = (repo / 'Frontend/pages/meetings.html').read_text(encoding='utf-8')
    page.write_text(html.replace('<head>', '<head><base href="' + (repo / 'Frontend/pages').as_uri() + '/">' + bootstrap), encoding='utf-8')
    process = subprocess.Popen([str(chrome), '--headless', '--disable-gpu', '--no-sandbox', '--allow-file-access-from-files', '--remote-debugging-port=0', '--user-data-dir=' + str(directory / 'profile'), 'about:blank'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    ws = None
    sequence = 0
    try:
        port_file = directory / 'profile/DevToolsActivePort'
        deadline = time.monotonic() + 15
        while not port_file.exists():
            assert time.monotonic() < deadline, 'Chrome startup timeout'
            time.sleep(.05)
        port = port_file.read_text().splitlines()[0]
        pages = json.load(urlopen('http://127.0.0.1:' + port + '/json/list'))
        ws = websocket.create_connection(next(p['webSocketDebuggerUrl'] for p in pages if p['type'] == 'page'), suppress_origin=True, timeout=15)

        def command(method, params=None):
            global sequence
            sequence += 1
            ws.send(json.dumps(dict(id=sequence, method=method, params=params or {})))
            while True:
                result = json.loads(ws.recv())
                if result.get('id') == sequence:
                    assert 'error' not in result, result
                    return result.get('result', {})

        def evaluate(expression):
            result = command('Runtime.evaluate', dict(expression=expression, returnByValue=True, awaitPromise=True))
            assert 'exceptionDetails' not in result, result
            return result.get('result', {}).get('value')

        def until(expression):
            deadline = time.monotonic() + 10
            while not evaluate(expression):
                assert time.monotonic() < deadline, expression
                time.sleep(.05)

        def click(selector):
            expression=json.dumps(selector)
            evaluate('document.querySelector('+expression+').scrollIntoView({block:"center",behavior:"instant"})')
            target=evaluate('''(() => {const r=document.querySelector('''+expression+''').getBoundingClientRect();return {x:r.left+r.width/2,y:r.top+r.height/2};})()''')
            command('Input.dispatchMouseEvent',dict(type='mousePressed',button='left',clickCount=1,**target))
            command('Input.dispatchMouseEvent',dict(type='mouseReleased',button='left',clickCount=1,**target))

        def type_time(field, part, value):
            click('#'+field+'Picker [data-'+part+']')
            command('Input.dispatchKeyEvent',dict(type='keyDown',key='a',code='KeyA',windowsVirtualKeyCode=65,modifiers=2))
            command('Input.dispatchKeyEvent',dict(type='keyUp',key='a',code='KeyA',windowsVirtualKeyCode=65,modifiers=2))
            command('Input.insertText',dict(text=value))

        command('Emulation.setDeviceMetricsOverride', dict(width=1440, height=1000, deviceScaleFactor=1, mobile=False))
        command('Page.navigate', {'url': page.as_uri()})
        until('!!document.getElementById("meetingSave") && !document.getElementById("meetingSave").disabled')
        assert evaluate('document.getElementById("meetingState").textContent.includes("Chưa có")')
        print('PASS: HR page loads participant options and empty state')
        assert evaluate('''(() => {const left=document.getElementById('meetingCreate').getBoundingClientRect(),right=document.querySelector('.meeting-guide').getBoundingClientRect();return right.left>left.right&&Math.abs(left.width/right.width-7/3)<.05;})()''')
        assert evaluate('Math.abs(document.querySelector("input[name=title]").offsetHeight-document.querySelector(".meeting-datetime").offsetHeight)<=2')
        print('PASS: desktop composition is 70/30 and input heights match')
        evaluate('document.querySelector("#participantOptions input").click()')
        assert evaluate('document.getElementById("participantCount").textContent==="Đã chọn 1 người" && !!document.querySelector(".meeting-person.is-selected .meeting-person-avatar")')
        evaluate('''(() => {const s=document.getElementById('participantSearch');s.value='Mentor';s.dispatchEvent(new Event('input'));})()''')
        assert evaluate('document.querySelectorAll(".meeting-person").length===1 && document.querySelector(".meeting-person-role").textContent==="MENTOR"')
        evaluate('''(() => {const s=document.getElementById('participantSearch');s.value='';s.dispatchEvent(new Event('input'));})()''')
        assert evaluate('document.querySelector("#participantOptions input").checked')
        if os.environ.get('MEETING_UI_SCREENSHOT'):
            import base64
            screenshot=command('Page.captureScreenshot',dict(format='png',captureBeyondViewport=True))['data']
            Path(os.environ['MEETING_UI_SCREENSHOT']).write_bytes(base64.b64decode(screenshot))
        evaluate('document.querySelector("#meetingForm button[type=reset]").click()')
        until('!document.querySelector(".meeting-person.is-selected")')
        assert evaluate('document.getElementById("participantCount").textContent==="Đã chọn 0 người" && window.testPosts.length===0')
        print('PASS: participant badge, selected avatar row, search persistence and cancel reset work')
        evaluate('''(() => {const i=document.getElementById('meetingStart');i.value='2026-10-01T08:00';i.dispatchEvent(new Event('change'));})()''')
        click('#meetingStartDisplay')
        assert evaluate('new Promise(resolve=>setTimeout(()=>resolve(!document.getElementById("meetingStartPicker").hidden),200))')
        click('#meetingStartPicker .meeting-calendar-heading strong')
        assert evaluate('!document.getElementById("meetingStartPicker").hidden')
        click('#meetingStartPicker [data-day="15"]')
        assert evaluate('!document.getElementById("meetingStartPicker").hidden')
        click('#meetingStartPicker .meeting-time-row>span:first-child')
        assert evaluate('!document.getElementById("meetingStartPicker").hidden')
        type_time('meetingStart','hours','08')
        type_time('meetingStart','minutes','30')
        click('#meetingStartPicker [data-apply]')
        assert evaluate('document.getElementById("meetingStartPicker").hidden && document.getElementById("meetingStartDisplay").value==="15/10/2026 08:30"')
        click('#meetingStartDisplay')
        assert evaluate('!document.getElementById("meetingStartPicker").hidden')
        type_time('meetingStart','hours','14')
        type_time('meetingStart','minutes','45')
        click('input[name=title]')
        assert evaluate('document.getElementById("meetingStartPicker").hidden && document.getElementById("meetingStart").value==="2026-10-15T14:45"')
        evaluate('''(() => {const i=document.getElementById('meetingEnd');i.value='2026-10-15T16:00';i.dispatchEvent(new Event('change'));})()''')
        click('#meetingEndDisplay')
        assert evaluate('!document.getElementById("meetingEndPicker").hidden && document.getElementById("meetingStartPicker").hidden')
        type_time('meetingEnd','minutes','30')
        click('#meetingEndPicker [data-apply]')
        assert evaluate('document.getElementById("meetingEnd").value==="2026-10-15T16:30" && document.getElementById("meetingStart").value==="2026-10-15T14:45"')
        print('PASS: real mouse clicks open and keep both pickers open; calendar/time/header clicks stay inside; Done/outside closes without value loss')
        evaluate('''(() => {const input=document.getElementById('meetingStart');input.value='2030-01-15T09:30';input.dispatchEvent(new Event('change'));document.querySelector('#meetingStartPicker').parentElement.querySelector('.meeting-datetime-trigger').click();})()''')
        assert evaluate('document.getElementById("meetingStartDisplay").value==="15/01/2030 09:30"')
        evaluate('''(() => {const p=document.getElementById('meetingStartPicker');p.querySelector('[data-day="16"]').click();const h=p.querySelector('[data-hours]'),m=p.querySelector('[data-minutes]');h.value='10';h.dispatchEvent(new Event('change'));m.value='45';m.dispatchEvent(new Event('change'));p.querySelector('[data-apply]').click();})()''')
        assert evaluate('document.getElementById("meetingStart").value==="2030-01-16T10:45" && document.getElementById("meetingStartDisplay").value==="16/01/2030 10:45" && document.getElementById("meetingStartPicker").hidden')
        print('PASS: custom calendar and 24-hour time controls produce dd/MM/yyyy HH:mm and preserve ISO form value')
        evaluate('document.querySelector("#meetingStartPicker").parentElement.querySelector(".meeting-datetime-clock").click()')
        assert evaluate('document.activeElement.matches("[data-hours]")')
        type_time('meetingStart','hours','08')
        type_time('meetingStart','minutes','30')
        assert evaluate('document.getElementById("meetingStartDisplay").value==="16/01/2030 08:30"')
        type_time('meetingStart','hours','14')
        type_time('meetingStart','minutes','45')
        evaluate("""document.querySelector('#meetingStartPicker [data-day="17"]').click()""")
        evaluate('document.querySelector("#meetingStartPicker [data-apply]").click()')
        assert evaluate('document.getElementById("meetingStartDisplay").value==="17/01/2030 14:45" && document.getElementById("meetingStart").value==="2030-01-17T14:45"')
        evaluate('document.querySelector("#meetingEndPicker").parentElement.querySelector(".meeting-datetime-clock").click()')
        type_time('meetingEnd','hours','18')
        type_time('meetingEnd','minutes','15')
        evaluate('document.body.dispatchEvent(new PointerEvent("pointerdown",{bubbles:true}))')
        assert evaluate('document.getElementById("meetingEnd").value.endsWith("T18:15") && document.getElementById("meetingStart").value==="2030-01-17T14:45"')
        print('PASS: real keyboard edits 08:30 to 14:45; changing day preserves time; End edits and blur leave Start unchanged')

        # Click the hour segment of the visible text field, then type without clearing the date.
        evaluate('document.getElementById("meetingStartDisplay").scrollIntoView({block:"center"})')
        target=evaluate('''(() => {const i=document.getElementById('meetingStartDisplay'),r=i.getBoundingClientRect(),s=getComputedStyle(i),canvas=document.createElement('canvas'),c=canvas.getContext('2d');c.font=s.fontSize+' '+s.fontFamily;return {x:r.left+parseFloat(s.paddingLeft)+c.measureText(i.value.slice(0,11)).width+3,y:r.top+r.height/2};})()''')
        command('Input.dispatchMouseEvent',dict(type='mousePressed',button='left',clickCount=1,**target))
        command('Input.dispatchMouseEvent',dict(type='mouseReleased',button='left',clickCount=1,**target))
        assert evaluate('document.getElementById("meetingStartDisplay").selectionStart===11 && document.getElementById("meetingStartDisplay").selectionEnd===13'), evaluate('''(() => {const i=document.getElementById('meetingStartDisplay');return {value:i.value,start:i.selectionStart,end:i.selectionEnd,active:document.activeElement.id};})()''')
        command('Input.insertText',dict(text='15'))
        evaluate('document.getElementById("meetingTitle")?.focus();document.getElementById("meetingForm").elements.title.focus()')
        assert evaluate('document.getElementById("meetingStart").value==="2030-01-17T15:45"')
        print('PASS: clicking and typing directly in hour segment preserves date/minutes and survives blur')
        evaluate('''(() => {const i=document.getElementById('meetingStartDisplay');i.value='31/02/2030 10:45';i.dispatchEvent(new Event('input'));})()''')
        assert evaluate('!document.getElementById("meetingStartDisplay").checkValidity() && document.getElementById("meetingStart").value===""')
        evaluate('''(() => {const i=document.getElementById('meetingStartDisplay');i.value='01/01/2030 09:00';i.dispatchEvent(new Event('input'));i.focus();i.dispatchEvent(new KeyboardEvent('keydown',{key:'ArrowDown',altKey:true,bubbles:true}));})()''')
        assert evaluate('!document.getElementById("meetingStartPicker").hidden')
        evaluate('document.activeElement.dispatchEvent(new KeyboardEvent("keydown",{key:"ArrowRight",bubbles:true}))')
        assert evaluate('document.activeElement.dataset.day==="2"')
        evaluate('document.activeElement.dispatchEvent(new KeyboardEvent("keydown",{key:"Escape",bubbles:true}))')
        assert evaluate('document.getElementById("meetingStartPicker").hidden')
        print('PASS: impossible dates rejected; keyboard opens calendar, navigates days, and closes with Escape')
        evaluate('''(() => {const f=document.getElementById('meetingForm');f.elements.title.value='Họp tiếng Việt';for(const [name,value] of [['start','2030-01-01T09:00'],['end','2030-01-01T08:00']]){f.elements[name].value=value;f.elements[name].dispatchEvent(new Event('change'));}document.getElementById('meetingSave').click();})()''')
        assert evaluate('window.testPosts.length===0 && document.querySelector(".ui-toast-text").textContent.includes("bắt đầu")')
        print('PASS: start-before-end validation rejects reversed range before API request')
        evaluate('''(() => {const f=document.getElementById('meetingForm'); f.elements.end.value='2030-01-01T10:00';f.elements.end.dispatchEvent(new Event('change'));f.elements.location.value='Phòng họp';document.querySelector('#participantOptions input').click();document.getElementById('meetingSave').click();document.getElementById('meetingSave').click();})()''')
        until('window.testPosts.length===1 && !document.getElementById("meetingSave").disabled')
        assert evaluate('window.testPosts[0].participantUserIds.length===1 && window.testPosts[0].participantUserIds[0]===2 && !!window.testPosts[0].clientRequestId')
        assert evaluate('window.testPosts[0].startTime==="2030-01-01T02:00:00.000Z" && window.testPosts[0].endTime==="2030-01-01T03:00:00.000Z"')
        assert evaluate('document.querySelectorAll("#meetingRows tr").length===1 && document.getElementById("meetingRows").textContent.includes("Họp tiếng Việt")')
        assert evaluate('document.querySelectorAll(".ui-toast").length===1')
        print('PASS: submit sends selected participants only, renders meeting, and prevents duplicate submit/toast')
        command('Emulation.setDeviceMetricsOverride', dict(width=390, height=844, deviceScaleFactor=1, mobile=True))
        evaluate('document.querySelector("#meetingEndPicker").parentElement.querySelector(".meeting-datetime-trigger").click()')
        assert evaluate('document.documentElement.scrollWidth <= document.documentElement.clientWidth')
        assert evaluate('document.getElementById("meetingEndPicker").getBoundingClientRect().right<=window.innerWidth')
        evaluate('document.body.dispatchEvent(new PointerEvent("pointerdown",{bubbles:true}))')
        assert evaluate('document.getElementById("meetingEndPicker").hidden')
        print('PASS: mobile has no whole-page horizontal overflow')
        command('Emulation.setDeviceMetricsOverride', dict(width=768, height=1024, deviceScaleFactor=1, mobile=False))
        assert evaluate('document.documentElement.scrollWidth <= document.documentElement.clientWidth')
        print('PASS: tablet layout has no whole-page horizontal overflow')
        evaluate('window.testFail=true;document.getElementById("meetingReload").click()')
        until('document.getElementById("meetingState").textContent.includes("Chưa tải")')
        print('PASS: reload failure shows retry state and existing toast')
        evaluate('document.dispatchEvent(new CustomEvent("session-permissions-updated",{detail:{user:{role:"HR"},permissions:["MEETING_READ"]}}))')
        assert evaluate('document.getElementById("meetingCreate").hidden')
        evaluate('document.dispatchEvent(new CustomEvent("session-permissions-updated",{detail:{user:{role:"INTERN"},permissions:[]}}))')
        assert evaluate('document.getElementById("meetingList").hidden && document.getElementById("meetingCreate").hidden')
        print('PASS: revoked create permission and non-HR session hide corresponding UI')
    finally:
        if ws:
            try:
                command('Browser.close')
            except websocket.WebSocketException:
                pass
            ws.close()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.terminate()
            process.wait(timeout=10)
