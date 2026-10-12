"""Verify real CSS viewport sizes via CDP (Chrome window sizing has a minimum).
Requires the websocket-client package, available in the development environment.
All pages use mocked API fixtures and an isolated temporary browser profile.
"""
from pathlib import Path
from tempfile import TemporaryDirectory
from urllib.request import urlopen
import base64,json,os,subprocess,time,sys
import websocket

repo=Path(__file__).resolve().parents[1]
chrome=Path(os.environ.get('PROGRAMFILES','C:/Program Files'))/'Google/Chrome/Application/chrome.exe'
with TemporaryDirectory(prefix='sprint1-viewport-') as directory:
 directory=Path(directory)
 process=subprocess.Popen([str(chrome),'--headless','--disable-gpu','--no-sandbox','--allow-file-access-from-files','--remote-debugging-port=0','--user-data-dir='+str(directory/'profile'),'about:blank'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
 try:
  port_file=directory/'profile/DevToolsActivePort'
  deadline=time.monotonic()+15
  while not port_file.exists():
   assert time.monotonic()<deadline,'Chrome did not start';time.sleep(.05)
  port=port_file.read_text().splitlines()[0]
  pages=json.load(urlopen('http://127.0.0.1:'+port+'/json/list'))
  ws=websocket.create_connection(next(p['webSocketDebuggerUrl'] for p in pages if p['type']=='page'),suppress_origin=True,timeout=10)
  sequence=0
  def command(method,params=None):
   global sequence
   sequence+=1;ws.send(json.dumps(dict(id=sequence,method=method,params=params or {})))
   while True:
    response=json.loads(ws.recv())
    if response.get('id')==sequence:
     assert 'error' not in response,response;return response.get('result',{})
  def evaluate(code):return command('Runtime.evaluate',{'expression':code,'returnByValue':True}).get('result',{}).get('value')
  command('Page.enable')
  cases = [('check_role_cards_ui.py','permissions.html'),('check_documents_ui.py','intern-upload-cv.html'),('check_document_reviews_ui.py','document-reviews.html')]
  if '--matrix-only' in sys.argv: cases = cases[:1]
  if '--accounts' in sys.argv: cases = [('check_api_ui.py',name) for name in ('api','accounts','list')]
  if '--dashboard' in sys.argv: cases = [('check_dashboard_ui.py','dashboard.html')]
  for test,page in cases:
   namespace={'__file__':str(repo/'tests'/test)}
   source=(repo/'tests'/test).read_text(encoding='utf-8')
   exec(source.split('# RUN_BROWSER')[0].split('with TemporaryDirectory')[0],namespace)
   root=repo/'Frontend/pages'
   if test=='check_api_ui.py':
    setup=namespace['bootstrap'];actions=namespace[{'api':'api_checks','accounts':'account_checks','list':'list_checks'}[page]]
    checks='<pre id="result"></pre><script>(async()=>{try{'+namespace['common']+actions+'document.getElementById("result").textContent="PASS"}catch(e){document.getElementById("result").textContent="FAIL "+e.message}})();</script>'
    template = '<!doctype html><html><head></head><body><script src="../js/api.js"></script></body></html>' if page=='api' else (root/('user-create.html' if page=='accounts' else 'user-manage.html')).read_text(encoding='utf-8')
   else:
    setup=namespace['setup'];checks=namespace['checks'];template=(root/page).read_text(encoding='utf-8')
   html=template.replace('<head>','<head><base href="'+root.as_uri()+'/"><style>#result{display:none}</style>'+setup).replace('</body>',checks+'</body>')
   target=directory/(page if page.endswith('.html') else page+'.html');target.write_text(html,encoding='utf-8')
   for width in ((1100,) if test=='check_api_ui.py' else (320,390,768,1440)):
    command('Emulation.setDeviceMetricsOverride',dict(width=width,height=1100,deviceScaleFactor=1,mobile=False))
    url=target.as_uri()+'?width='+str(width)
    command('Page.navigate',{'url':url})
    deadline=time.monotonic()+15;status=None
    while time.monotonic()<deadline:
     status=evaluate('location.href==='+json.dumps(url)+' ? document.getElementById("result")?.textContent : null')
     if status:break
     time.sleep(.05)
    assert status and status.startswith('PASS'),(test,width,status)
    actual=evaluate('({width:innerWidth,scroll:document.documentElement.scrollWidth})')
    assert actual['width']==width and actual['scroll']<=width,(test,width,actual)
    print(test,width,'PASS (real CSS viewport)')
    if test=='check_role_cards_ui.py' and width in (390,1440):
     evaluate('document.getElementById("toastRegion")?.remove()')
     screenshot=command('Page.captureScreenshot',{'format':'png','captureBeyondViewport':False})
     (repo/'docs'/('sprint1-rbac-'+('mobile' if width==390 else 'desktop')+'.png')).write_bytes(base64.b64decode(screenshot['data']))
 finally:
  if process.poll() is None:
   try: ws.send(json.dumps(dict(id=sequence+1,method='Browser.close')))
   except Exception: pass
   try: process.wait(timeout=10)
   except subprocess.TimeoutExpired:
    if os.name=='nt': subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    else: process.kill()
    process.wait(timeout=10)
  try: ws.close()
  except Exception: pass
