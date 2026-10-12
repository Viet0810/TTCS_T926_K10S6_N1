"""Exercise the shared toast in Chromium: geometry, top layer, timers and safety."""
from pathlib import Path
from tempfile import TemporaryDirectory
import os, re, subprocess

root = Path(__file__).resolve().parents[1] / 'Frontend'
chrome = Path(os.environ.get('PROGRAMFILES', 'C:/Program Files')) / 'Google/Chrome/Application/chrome.exe'
checks = r'''
(async()=>{const checks=[];const assert=(ok,name)=>{if(!ok)throw Error(name);checks.push(name);document.getElementById('result').textContent='RUNNING: '+name};
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
try{
for(const type of ['success','error','warning','info']){const item=AppUI.toast(type,type);
assert(item.classList.contains('is-'+type)&&item.querySelector('.ui-toast-icon'),'status '+type);
assert(item.getAttribute('role')===(type==='error'?'alert':'status'),'accessible '+type);}
const region=document.getElementById('toastRegion');
assert(region.children.length===3&&region.firstElementChild.textContent.includes('info'),'bounded newest-first stack');
const feedback=document.createElement('p');feedback.id='action-feedback';feedback.textContent='Old inline message';document.body.append(feedback);
AppUI.feedback(feedback,'Saved','success');AppUI.feedback(feedback,'Saved but refresh failed','warning');
assert(feedback.hidden&&feedback.textContent===''&&region.querySelectorAll('[data-feedback-key=action-feedback]').length===1,'one action feedback without inline duplicate');
assert(region.firstElementChild.classList.contains('is-warning'),'partial success replaces initial success');
const colors=new Set(['success','error','warning','info'].map(type=>getComputedStyle(AppUI.toast(type,type)).backgroundColor));
assert(colors.size===4,'distinct tinted status backgrounds');
const first=region.getBoundingClientRect(), header=document.querySelector('header').getBoundingClientRect();
assert(Math.abs(first.left+first.width/2-document.documentElement.clientWidth/2)<2&&first.top>=20&&first.top<=28,'top-center 24px from viewport');
assert(first.left>=15&&first.right<=innerWidth-15,'responsive margins');
assert(getComputedStyle(region).position==='fixed'&&getComputedStyle(region).pointerEvents==='none','viewport anchored and click-through');
scrollTo(0,500);await delay(30);assert(Math.abs(region.getBoundingClientRect().top-first.top)<2,'stable after scroll');
AppUI.toast('duplicate');AppUI.toast('duplicate');
assert([...region.children].filter(item=>item.querySelector('.ui-toast-text').textContent==='duplicate').length===1,'deduplicated');
const long=AppUI.toast('<script>unsafe<'+ '/script>'+(' long message'.repeat(70)),'error');
assert(!long.querySelector('script')&&long.getBoundingClientRect().width<=innerWidth-32,'escaped long text without overflow');
const modal=AppUI.confirm('Confirm remains centered');await delay(20);
const dialog=document.querySelector('dialog');const rect=dialog.getBoundingClientRect();
assert(Math.abs(rect.left+rect.width/2-document.documentElement.clientWidth/2)<2,'confirmation layout preserved');
const above=AppUI.toast('Above dialog','warning');await delay(20);
const hit=above.getBoundingClientRect();const target=document.elementFromPoint(hit.left+20,hit.top+20);assert(above.contains(target),'toast above modal top layer '+JSON.stringify({top:hit.top,left:hit.left,target:target?.tagName,className:target?.className,popover:region.matches(':popover-open')}));
dialog.close('cancel');await delay(50);
above.querySelector('button').click();await delay(180);assert(!above.isConnected,'manual close');
for(const item of [...region.children])item.querySelector('button').click();await delay(180);
const timed=AppUI.toast('Auto close','info');await delay(4300);assert(!timed.isConnected,'automatic close');
assert(document.documentElement.scrollWidth<=innerWidth,'no horizontal page overflow');
document.getElementById('result').textContent='PASS: '+checks.join('; ');
}catch(error){document.getElementById('result').textContent='FAIL: '+error.message}})();
'''
with TemporaryDirectory(prefix='intern-toasts-') as directory:
    temp = Path(directory)
    for width in (1280, 768, 390):
        page=temp/f'toast-{width}.html'
        page.write_text(f'''<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<link rel="stylesheet" href="{(root/'css/ui.css').as_uri()}"><style>*{{box-sizing:border-box}}body{{margin:0;min-height:2000px}}header{{position:fixed;top:0;height:{88 if width>560 else 132}px;width:100%;background:#eee}}dialog{{margin:auto}}</style></head>
<body><header class="dashboard-header">Topbar</header><pre id="result"></pre><script src="{(root/'js/ui.js').as_uri()}"></script><script>{checks}</script></body></html>''',encoding='utf-8')
        result=subprocess.run([str(chrome),'--headless','--disable-gpu','--no-sandbox',f'--user-data-dir={temp/str(width)}',f'--window-size={width},900','--virtual-time-budget=6500','--dump-dom',page.as_uri()],capture_output=True,timeout=35)
        match=re.search(r'<pre id="result">(.*?)</pre>',result.stdout.decode('utf-8',errors='replace'),re.S)
        assert match and match.group(1).startswith('PASS:'),(width,match.group(1) if match else 'No browser result')
        print(f'{width}px '+match.group(1))
