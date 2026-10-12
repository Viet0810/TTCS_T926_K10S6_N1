"""Verify editable RBAC using the actual backend catalog, in isolated Chrome."""
from pathlib import Path
from tempfile import TemporaryDirectory
import subprocess,re,json,os
repo=Path(__file__).resolve().parents[1]
source=(repo/'Backend/InternManagement/Services/RolePermissionService.cs').read_text(encoding='utf-8')
names=dict(re.findall(r'public const string (\w+) = "([A-Z_]+)"',source))
roles={role:[names[key] for key in re.findall(r'PermissionNames\.(\w+)',body)] for role,body in re.findall(r'\["(ADMIN|HR|MENTOR|INTERN)"\] =\s*\[(.*?)\]',source,re.S)}
catalog_source=(repo/'Backend/InternManagement/Services/PermissionCatalog.cs').read_text(encoding='utf-8')
aliases={'Staff':['ADMIN','HR','MENTOR','INTERN'],'Managers':['ADMIN','HR']}
catalog=[]
for code,module,label,action,allowed,fixed in re.findall(r'new\(PermissionNames\.(\w+), "([\w-]+)", "([^"]+)", "(\w+)", (Staff|Managers|\[[^\]]+\])(, false)?\)',catalog_source):
 catalog.append(dict(code=names[code],module=module,label=label,action=action,roles=aliases.get(allowed,re.findall(r'"(\w+)"',allowed)),editable=not fixed))
for children,parent in re.findall(r'((?:PermissionNames\.\w+)(?: or PermissionNames\.\w+)*) => \[PermissionNames\.(\w+)\]',catalog_source):
 for child in re.findall(r'PermissionNames\.(\w+)',children):
  next(item for item in catalog if item['code']==names[child])['requires']=[names[parent]]
assert len(catalog)==len(names),'all real backend permissions represented'
essential=['MANAGE_USERS','CREATE_USER','DELETE_USER','RBAC_READ','MANAGE_PERMISSIONS']
root=repo/'Frontend/pages'
setup='''<script>localStorage.setItem('token','mock');const defaults='''+json.dumps(roles)+''';let roles=structuredClone(defaults);const catalog='''+json.dumps(catalog)+''';const essential='''+json.dumps(essential)+''';let saves=0,hold=false,release,failSave=false,failReadback=false;window.confirm=()=>true;
window.fetch=async(url,options={})=>{let data;if(options.method==='PUT'&&failSave){failSave=false;throw Error('simulated save failure');}if(url.endsWith('/permissions')&&failReadback){failReadback=false;throw Error('simulated readback failure');}
if(url.endsWith('/auth/me'))data={user:{role:'ADMIN'},permissions:roles.ADMIN};
else if(url.endsWith('/defaults'))data={permissions:defaults[url.split('/').at(-2)]};
else if(options.method==='PUT'){saves++;if(hold)await new Promise(r=>release=r);const role=url.split('/').at(-1);roles[role]=JSON.parse(options.body).permissions;data={permissions:roles[role]};}
else data={roles:structuredClone(roles),catalog,essentialAdminPermissions:essential};return{ok:true,status:200,json:async()=>data};};</script>'''
checks='''<pre id="result"></pre><script>(async()=>{try{const assert=(v,m)=>{if(!v)throw Error(m)},tick=()=>new Promise(r=>setTimeout(r,0)),settle=async()=>{for(let i=0;i<5;i++)await tick()},modal=async(accept=true)=>{await tick();const dialog=document.querySelector('.shared-confirm[open]');assert(dialog,'custom confirmation dialog');const closed=new Promise(r=>dialog.addEventListener('close',r,{once:true}));dialog.querySelector('[value="'+(accept?'confirm':'cancel')+'"]').click();await closed;await settle()};await settle();
assert(document.querySelectorAll('.permission-matrix-table tbody tr').length===new Set(catalog.map(p=>p.module)).size,'actual catalog modules');
for(const code of essential)assert(document.querySelector('[data-permission="'+code+'"]').disabled,'essential Admin locked '+code);
document.querySelector('[data-role=HR]').click();
assert(!document.querySelector('[data-permission=CREATE_USER]').checked,'HR has no default account grant but Admin can delegate it');
const approve=document.querySelector('[data-permission=APPROVE_DOCUMENTS]');approve.click();assert(!document.getElementById('savePermissionsBtn').disabled&&saves===0,'changes staged');
hold=true;document.getElementById('savePermissionsBtn').click();document.getElementById('savePermissionsBtn').click();assert(saves===1&&document.getElementById('savePermissionsBtn').disabled,'one save while busy');release();hold=false;await tick();await tick();await tick();
assert(!roles.HR.includes('APPROVE_DOCUMENTS')&&roles.HR.includes('DOCUMENT_REVIEW_READ'),'read remains after approve revoked');
document.getElementById('resetPermissionsBtn').click();await modal();assert(document.querySelector('[data-permission=APPROVE_DOCUMENTS]').checked&&saves===1&&!roles.HR.includes('APPROVE_DOCUMENTS'),'defaults staged without persistence '+JSON.stringify({saves,checked:document.querySelector('[data-permission=APPROVE_DOCUMENTS]').checked,stored:roles.HR,message:document.getElementById('permissionsMessage').textContent}));
document.getElementById('savePermissionsBtn').click();await tick();await tick();await tick();assert(saves===2&&roles.HR.includes('APPROVE_DOCUMENTS'),'explicit save restores defaults');
await loadRolePermissions();assert(document.querySelector('[data-permission=APPROVE_DOCUMENTS]').checked,'reload reads persisted grants');
document.querySelector('[data-permission=VIEW_INTERNS]').click();assert(!document.querySelector('[data-permission=EDIT_INTERNS]').checked&&!document.querySelector('[data-permission=MANAGE_INTERNS]').checked,'read revocation clears dependent actions');
document.querySelector('[data-role=ADMIN]').click();await modal();document.querySelector('[data-permission=VIEW_INTERNS]').click();document.getElementById('savePermissionsBtn').click();await settle();
assert(!document.querySelector('#roleMenu a[href="intern-manage.html"]'),'navigation reflects saved READ revocation');
document.getElementById('resetPermissionsBtn').click();await modal();document.getElementById('savePermissionsBtn').click();await settle();
assert(document.querySelector('#roleMenu a[href="intern-manage.html"]'),'navigation returns after regrant');
assert(document.querySelector('#toastRegion .is-success'),'successful save uses toast');
document.querySelector('[data-permission=EDIT_INTERNS]').click();assert(document.getElementById('permissionDirtyStatus').textContent.includes('1'),'dirty change count');document.getElementById('cancelPermissionsBtn').click();assert(document.querySelector('[data-permission=EDIT_INTERNS]').checked&&document.getElementById('savePermissionsBtn').disabled,'cancel restores persisted state');
const filter=document.getElementById('permissionSearch');filter.value='not-a-module';filter.dispatchEvent(new Event('input'));assert(document.querySelector('.permission-empty'),'search empty state');filter.value='';filter.dispatchEvent(new Event('input'));
document.querySelector('[data-role=MENTOR]').click();const mentorEdit=document.querySelector('[data-permission=EDIT_INTERNS]');assert(mentorEdit&&!mentorEdit.checked&&!mentorEdit.disabled,'Admin can add permission outside Mentor defaults');mentorEdit.click();document.getElementById('resetPermissionsBtn').click();await modal(false);assert(mentorEdit.checked,'cancel reset retains draft');document.getElementById('cancelPermissionsBtn').click();document.querySelector('[data-role=ADMIN]').click();
document.getElementById('toastRegion')?.remove();
document.querySelector('[data-permission=EDIT_INTERNS]').click();failSave=true;document.getElementById('savePermissionsBtn').click();await settle();
assert(dirty()&&!document.getElementById('retryPermissionsBtn').hidden&&!document.querySelector('.ui-toast.is-success'),'failed PUT preserves draft without fake success');
document.getElementById('cancelPermissionsBtn').click();
document.querySelector('[data-permission=EDIT_INTERNS]').click();failReadback=true;document.getElementById('savePermissionsBtn').click();await settle();
assert(!dirty()&&!roles.ADMIN.includes('EDIT_INTERNS')&&!document.getElementById('retryPermissionsBtn').hidden&&!document.querySelector('.ui-toast.is-success'),'failed GET after successful PUT reports persisted state without fake success');
document.getElementById('retryPermissionsBtn').click();await settle();
assert(!document.querySelector('[data-permission=EDIT_INTERNS]').checked&&document.getElementById('retryPermissionsBtn').hidden,'retry reads actual persisted state');
document.querySelector('[data-permission=EDIT_INTERNS]').click();document.getElementById('savePermissionsBtn').click();await settle();
assert(document.documentElement.scrollWidth<=innerWidth,'no horizontal page scroll');
assert(getComputedStyle(document.querySelector('.permission-matrix-table tr')).display===(innerWidth<=760?'grid':'table-row'),'mobile cards and desktop matrix');
document.getElementById('result').textContent='PASS';}catch(e){document.getElementById('result').textContent='FAIL '+e.message}})();</script>'''
# RUN_BROWSER
if __name__ == '__main__':
 import sys
 subprocess.run([sys.executable,str(repo/'tests/check_sprint1_viewports.py'),'--matrix-only'],check=True)
