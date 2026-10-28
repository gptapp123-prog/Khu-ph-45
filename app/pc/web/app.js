'use strict';
// No localStorage, IndexedDB, offline cache, cookies or persistent mobile state.
// All session secrets, previews and form values live in RAM only.
const S={token:null,me:null,page:'dashboard',preview:null,residents:[],households:[],editing:null,isNative:!!window.KPDevice,
  isPC:(['localhost','127.0.0.1','::1'].includes(location.hostname)),pendingLogin:null,fields:null};
const $=id=>document.getElementById(id);
const esc=v=>String(v??'').replace(/[&<>"']/g,x=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[x]));
const roles={viewer:'Chỉ xem',reviewer:'Rà soát',admin_kp:'Quản trị khu phố',admin_root:'Quản trị gốc',super_admin:'Quản trị hệ thống'};
const fieldLabels={full_name:'Họ tên',cccd:'Số CCCD',dob:'Ngày sinh',gender:'Giới tính',hometown:'Quê quán',permanent_address:'Thường trú',team_id:'Tổ',phone:'Số điện thoại',health_insurance:'BHYT',occupation:'Nghề nghiệp',note:'Ghi chú'};
const baseKeys=Object.keys(fieldLabels),allKeys=[...baseKeys];
function syncFields(data){
 S.fields=data;
 Object.keys(fieldLabels).filter(k=>!baseKeys.includes(k)).forEach(k=>delete fieldLabels[k]);
 allKeys.splice(0,allKeys.length,...baseKeys);
 (data?.custom||[]).forEach(f=>{const key='custom:'+f.name;fieldLabels[key]=f.label;if(!allKeys.includes(key))allKeys.push(key)});
}
async function ensureFields(){if(!S.fields)syncFields(await api('/fields'));return S.fields}
let toastTimer;
function toast(msg,error=false){const x=$('toast');x.textContent=msg;x.className='show'+(error?' error':'');clearTimeout(toastTimer);toastTimer=setTimeout(()=>x.className='',6500)}
function dateTime(s){try{return new Date(s).toLocaleString('vi-VN')}catch{return s||''}}
function isAdmin(){return ['admin_kp','admin_root','super_admin'].includes(S.me?.role)}
function isRoot(){return ['admin_root','super_admin'].includes(S.me?.role)}
function canEdit(){return S.me?.role!=='viewer'}
function deviceId(){
  if(window.KPDevice&&typeof window.KPDevice.deviceId==='function')return window.KPDevice.deviceId();
  // Browser mobile fallback: ephemeral and must be approved each session. Nothing saved.
  if(!S.browserID)S.browserID='web-'+Array.from(crypto.getRandomValues(new Uint8Array(16)),b=>b.toString(16).padStart(2,'0')).join('');
  return S.isPC?'pc':S.browserID;
}
async function api(path,opt={}){
  let headers={'Accept':'application/json',...(opt.body instanceof FormData?{}:{'Content-Type':'application/json'}),...(opt.headers||{})};
  if(S.token)headers.Authorization='Bearer '+S.token;
  let res;
  try{res=await fetch('/api'+path,{method:opt.method||'GET',headers,body:opt.body instanceof FormData?opt.body:opt.body?JSON.stringify(opt.body):undefined,cache:'no-store',credentials:'omit',redirect:'error'})}
  catch(e){throw Error('Không kết nối được PC chính. Kiểm tra kết nối mạng và dịch vụ PC.');}
  const contentType=res.headers.get('content-type')||'';
  const data=opt.raw?await res.blob():contentType.includes('json')?await res.json():await res.text();
  if(!res.ok)throw Error(typeof data?.detail==='string'?data.detail:'Yêu cầu không thành công ('+res.status+').');
  return data;
}
function authScreen(on=true){$('authScreen').classList.toggle('hidden',!on);$('appScreen').classList.toggle('hidden',on)}
async function boot(){
  try{let h=await api('/health');if(h.setup_required){
    $('authTitle').textContent='Thiết lập lần đầu trên PC';$('authHelp').textContent='Tạo quản trị viên hệ thống lần đầu. Chỉ được thực hiện trực tiếp tại PC chính.';
    $('setupNameWrap').classList.remove('hidden');$('loginButton').textContent='Tạo quản trị viên';$('loginForm').dataset.setup='true';
    if(!S.isPC){$('loginButton').disabled=true;toast('Thiết lập lần đầu phải thao tác tại PC chính.',true)}
  }}catch(e){toast(e.message,true)}
}
$('loginForm').addEventListener('submit',async e=>{
 e.preventDefault();const user=$('username').value.trim(),password=$('password').value;
 if(!user||!password){toast('Điền đủ thông tin.',true);return}
 const btn=$('loginButton');btn.disabled=true;
 try{
   if($('loginForm').dataset.setup){await api('/setup',{method:'POST',body:{username:user,full_name:$('setupName').value.trim(),password}});
     $('loginForm').dataset.setup='';$('setupNameWrap').classList.add('hidden');$('authTitle').textContent='Đăng nhập hệ thống';$('loginButton').textContent='Đăng nhập';toast('Tạo quản trị viên thành công. Đăng nhập để bắt đầu.');return;}
   const label=window.KPDevice?.label?.()|| (S.isPC?'PC chính':'Trình duyệt điện thoại');
   const r=await api('/login',{method:'POST',body:{username:user,password,device_id:deviceId(),device_label:label}});
   if(r.status==='pending_approval'){
     S.pendingLogin={request_id:r.request_id,proof:r.proof};$('password').value='';
     $('pendingApproval').classList.remove('hidden');toast('Đã gửi yêu cầu. Cần duyệt phiên tại PC chính.');
     pollApprovalSoon();return;
   }
   finishLogin(r);
 }catch(err){toast(err.message,true)}finally{btn.disabled=false}
});
function finishLogin(r){
   S.pendingLogin=null;$('pendingApproval').classList.add('hidden');
   S.token=r.token;S.me={id:r.id,role:r.role,name:r.name,team_id:r.team_id,must_change_password:r.must_change_password};
   $('password').value='';$('userDisplay').textContent=r.name+' · '+(roles[r.role]||r.role);
   document.querySelectorAll('.admin-only').forEach(x=>x.classList.toggle('hidden',!isAdmin()));
   document.querySelectorAll('.root-only').forEach(x=>x.classList.toggle('hidden',!isRoot()));
   document.querySelectorAll('.super-only').forEach(x=>x.classList.toggle('hidden',S.me.role!=='super_admin'));
   document.querySelectorAll('.edit-only').forEach(x=>x.classList.toggle('hidden',!canEdit()));
   document.querySelectorAll('.pc-only').forEach(x=>x.classList.toggle('hidden',!S.isPC));
   if(!canEdit())document.querySelector('[data-mobile-page="import"]').classList.add('hidden');
   authScreen(false);if(r.must_change_password){forcePasswordChange();}else{go('dashboard');}
}
let approvalPolling=false;
async function checkMobileApproval(){
 if(!S.pendingLogin)return;
 try{
  let r=await api('/mobile/login-status',{method:'POST',body:S.pendingLogin});
  if(r.status==='approved'){finishLogin(r);return}
  $('pendingApproval').classList.remove('hidden');
 }catch(err){S.pendingLogin=null;$('pendingApproval').classList.add('hidden');toast(err.message,true)}
}
function pollApprovalSoon(){
 if(approvalPolling)return;approvalPolling=true;
 let ticks=0;
 const tick=async()=>{
  if(!S.pendingLogin||ticks++>=50){approvalPolling=false;return}
  await checkMobileApproval();
  if(S.pendingLogin)setTimeout(tick,3500);else approvalPolling=false;
 };
 setTimeout(tick,3500);
}
$('checkApproval').addEventListener('click',checkMobileApproval);
function passwordDialog(forced=false){
 modal(forced?'Bắt buộc đổi mật khẩu lần đầu':'Đổi mật khẩu tài khoản',
  `<p class="muted">Mật khẩu mới từ 12 ký tự; đăng nhập lại sau khi thay đổi.</p>
   <label>Mật khẩu hiện tại<input type="password" name="old_password" required minlength="1" autocomplete="current-password"></label>
   <label>Mật khẩu mới<input type="password" name="new_password" required minlength="12" autocomplete="new-password"></label>`,
  async values=>{await api('/account/change-password',{method:'POST',body:values});S.token=null;S.me=null;authScreen();toast('Mật khẩu đã thay đổi. Vui lòng đăng nhập lại.');});
 if(forced){let b=document.querySelector('#cancelModal');if(b){b.textContent='Đăng xuất';b.onclick=()=>{S.token=null;S.me=null;document.querySelector('.modal-cover')?.remove();authScreen()};}}
}
function forcePasswordChange(){passwordDialog(true)}
$('changePasswordBtn').addEventListener('click',()=>passwordDialog(false));
$('logoutBtn').addEventListener('click',async()=>{
 try{await api('/logout',{method:'POST'})}catch(_){}
 S.token=null;S.me=null;S.pendingLogin=null;S.preview=null;S.residents=[];S.households=[];authScreen();
});
window.addEventListener('pagehide',()=>{S.token=null;S.pendingLogin=null;S.preview=null;S.residents=[];S.households=[]});
function go(page){
 S.page=page;const titles={dashboard:'Tổng quan',residents:'Danh sách nhân khẩu',households:'Hộ gia đình',import:'Quét & nhập dữ liệu',
 export:'Xuất báo cáo',accounts:'Tài khoản',permissions:'Ma trận phân quyền',devices:'Quản lý thiết bị',fields:'Trường dữ liệu',templates:'Mẫu dữ liệu',audit:'Nhật ký hệ thống'};
 $('pageTitle').textContent=titles[page]||page;$('pageBread').textContent=titles[page]||page;
 document.querySelectorAll('[data-page]').forEach(x=>x.classList.toggle('active',x.dataset.page===page));
 document.querySelectorAll('[data-mobile-page]').forEach(x=>x.classList.toggle('active',x.dataset.mobilePage===page));
 $('content').innerHTML='<div class="card"><p class="muted">Đang tải từ PC chính…</p></div>';
 const fn={dashboard,residents,households,importPage,exportPage,accounts,permissions,devices,fields,templates,audit};
 const key=page==='import'?'importPage':page==='export'?'exportPage':page;
 fn[key]?.().catch(err=>{if(err.message.includes('hết hạn')){S.token=null;authScreen();}else $('content').innerHTML='<div class="card"><p class="muted">'+esc(err.message)+'</p><button class="outline" id="retry">Thử lại</button></div>';toast(err.message,true);$('retry')?.addEventListener('click',()=>go(page))});
}
document.querySelectorAll('[data-page]').forEach(x=>x.addEventListener('click',()=>go(x.dataset.page)));
document.querySelectorAll('[data-mobile-page]').forEach(x=>x.addEventListener('click',()=>go(x.dataset.mobilePage)));
function note(text){return '<div class="hint">'+esc(text)+'</div>'}
function table(headers,rows){return '<div class="table-wrap"><table><thead><tr>'+headers.map(x=>'<th>'+esc(x)+'</th>').join('')+'</tr></thead><tbody>'+rows.join('')+'</tbody></table></div>'}
function cell(x){return '<td>'+esc(x)+'</td>'}
async function dashboard(){
 const d=await api('/stats');$('content').innerHTML=`<div class="cards">
  <div class="metric"><div class="metric-icon">♙</div><div><strong>${d.residents}</strong><span>Nhân khẩu</span></div></div>
  <div class="metric"><div class="metric-icon">⌂</div><div><strong>${d.households}</strong><span>Hộ gia đình</span></div></div>
  <div class="metric"><div class="metric-icon">♧</div><div><strong>${d.accounts===null?'—':d.accounts}</strong><span>Tài khoản hoạt động</span></div></div>
  <div class="metric"><div class="metric-icon">▣</div><div><strong>${d.audit_events===null?'—':d.audit_events}</strong><span>Sự kiện hệ thống</span></div></div>
  </div><div class="two-col"><div class="card"><div class="card-heading"><h3>Thao tác nhanh</h3></div><div class="toolbar"><button class="secondary" data-quick="residents">Xem nhân khẩu</button><button class="secondary" data-quick="households">Xem hộ</button>${canEdit()?'<button class="secondary" data-quick="import">Nhập dữ liệu</button>':''}</div>
  <p class="footnote">Mọi thao tác được xử lý tại PC chính. Không có bộ nhớ dữ liệu cư dân trên thiết bị di động.</p></div>
  <div class="card"><div class="card-heading"><h3>Trạng thái kết nối</h3><span class="chip success">Đã kết nối PC</span></div>
  ${note(S.isPC?'Bạn đang thao tác trên PC chính. Chức năng xuất dữ liệu chỉ có trên PC.':'Bạn đang thao tác từ thiết bị khách. Chức năng xuất tệp và sao lưu đã bị vô hiệu hóa trên mobile.')}
  </div></div>`;
 document.querySelectorAll('[data-quick]').forEach(b=>b.addEventListener('click',()=>go(b.dataset.quick)));
}
function formValues(form){const obj={};new FormData(form).forEach((v,k)=>{obj[k]=String(v).trim()||null});return obj}
function modal(title,html,onSave){
 document.querySelector('.modal-cover')?.remove();let wrap=document.createElement('div');wrap.className='modal-cover';wrap.innerHTML=`<section class="modal" role="dialog" aria-modal="true"><h3>${esc(title)}</h3><form id="modalForm">${html}<div class="modal-footer"><button type="button" class="outline" id="cancelModal">Hủy</button><button class="primary" type="submit">Lưu dữ liệu</button></div></form></section>`;
 document.body.append(wrap);wrap.querySelector('#cancelModal').addEventListener('click',()=>wrap.remove());
 wrap.querySelector('#modalForm').addEventListener('submit',async e=>{e.preventDefault();let b=e.submitter;if(b)b.disabled=true;try{await onSave(formValues(e.target));wrap.remove();toast('Đã lưu lên PC chính.')}catch(err){toast(err.message,true)}finally{if(b)b.disabled=false}});
}
function residentForm(row={}){const fields=[
 ['full_name','Họ tên',true],['cccd','Số CCCD (12 số)'],['dob','Ngày sinh (DD-MM-YYYY)'],['gender','Giới tính'],
 ['team_id','Tổ (1–15)',true],['phone','Số điện thoại'],['hometown','Quê quán'],['permanent_address','Thường trú'],
 ['health_insurance','BHYT'],['occupation','Nghề nghiệp'],['note','Ghi chú']];
 const standard=fields.map(([k,label,required])=>`<label>${label}<input name="${k}" value="${esc(row[k]||'')}" ${required?'required':''} ${k==='cccd'?'inputmode="numeric" pattern="[0-9]{12}"':''} ${k==='dob'?'inputmode="numeric" placeholder="31-12-2000" pattern="[0-9]{2}-[0-9]{2}-[0-9]{4}"':''} ${k==='team_id'?'type="number" min="1" max="15"':''} maxlength="${k==='note'?'3000':'300'}"></label>`).join('');
 const custom=(S.fields?.custom||[]).map(f=>{const value=row.custom_values?.[f.name]??'',name='custom__'+f.name,required=f.mode==='required'?'required':'';
  if(f.type==='dropdown'){let options=[];try{options=JSON.parse(f.options_json||'[]')}catch{}return `<label>${esc(f.label)}<select name="${esc(name)}" ${required}><option value="">Chưa chọn</option>${options.map(x=>`<option value="${esc(x)}" ${String(value)===String(x)?'selected':''}>${esc(x)}</option>`).join('')}</select></label>`}
  if(f.type==='checkbox')return `<label class="checkline"><input type="checkbox" name="${esc(name)}" value="1" ${String(value)==='1'?'checked':''}> ${esc(f.label)}</label>`;
  return `<label>${esc(f.label)}<input name="${esc(name)}" value="${esc(value)}" ${required} ${f.type==='number'?'inputmode="decimal"':''} ${f.type==='date'?'inputmode="numeric" placeholder="31-12-2026" pattern="[0-9]{2}-[0-9]{2}-[0-9]{4}"':''}></label>`}).join('');
 return '<div class="form-grid">'+standard+custom+
 `<label>Hộ gia đình<select name="household_id"><option value="">Chưa gắn hộ</option>${S.households.filter(h=>!row.team_id||h.team_id==row.team_id).map(h=>`<option value="${esc(h.id)}" ${row.household_id===h.id?'selected':''}>${esc(h.code+' - '+h.address)}</option>`).join('')}</select></label></div>`;
}
function cleanResident(p){p.team_id=Number(p.team_id);p.custom_values={};(S.fields?.custom||[]).forEach(f=>{const k='custom__'+f.name;p.custom_values[f.name]=p[k]??(f.type==='checkbox'?'0':null);delete p[k]});return p}
async function residents(){
 await Promise.all([ensureFields(),api('/households').then(x=>S.households=x.items),api('/residents?limit=200').then(x=>{S.residents=x.items;S.total=x.total})]);
 renderResidents();
}
function renderResidents(){
 const body=S.residents.map((r,i)=>'<tr>'+cell(i+1)+cell(r.full_name)+cell(r.cccd)+cell(r.dob)+cell(r.team_id)+cell(r.phone)+
 `<td>${canEdit()?`<button class="outline small" data-edit="${esc(r.id)}">Sửa</button>`:''}${isAdmin()?` <button class="danger small" data-delete="${esc(r.id)}">Xóa</button>`:''}</td></tr>`);
 $('content').innerHTML=`<div class="card"><div class="card-heading"><div><h3>Danh sách nhân khẩu</h3><p class="muted small">${S.total} bản ghi trên PC · đang hiển thị ${S.residents.length} dòng</p></div><div class="actions">${isAdmin()?'<button class="secondary" id="reviewDuplicates">Rà soát trùng</button>':''}${canEdit()?'<button class="primary" id="newResident">+ Thêm nhân khẩu</button>':''}</div></div>
 <div class="toolbar"><input id="resSearch" placeholder="Tìm họ tên, CCCD, địa chỉ…" aria-label="Tìm nhân khẩu"><select id="resTeam"><option value="">Tất cả tổ</option>${Array.from({length:15},(_,i)=>`<option value="${i+1}">Tổ ${i+1}</option>`).join('')}</select><button class="secondary" id="searchResident">Tìm</button></div>
 ${body.length?table(['STT','Họ tên','CCCD','Ngày sinh','Tổ','Số điện thoại','Thao tác'],body):'<div class="empty">Chưa có dữ liệu dân cư trong hệ thống.</div>'}<p class="footnote">Tìm kiếm có giới hạn 200 dòng/lượt để bảo vệ hiệu năng. Dữ liệu được lọc và kiểm tra quyền trên PC.</p></div>`;
 $('newResident')?.addEventListener('click',()=>modal('Tạo hồ sơ nhân khẩu',residentForm({team_id:S.me.team_id||1}),async form=>{await api('/residents',{method:'POST',body:cleanResident(form)});await residents()}));
 $('reviewDuplicates')?.addEventListener('click',duplicateReview);
 $('searchResident').addEventListener('click',async()=>{const q=$('resSearch').value,t=$('resTeam').value;let res=await api('/residents?limit=200&q='+encodeURIComponent(q)+(t?'&team='+t:''));S.residents=res.items;S.total=res.total;renderResidents();$('resSearch').value=q;$('resTeam').value=t});
 $('resSearch').addEventListener('keydown',e=>{if(e.key==='Enter')$('searchResident').click()});
 document.querySelectorAll('[data-edit]').forEach(b=>b.addEventListener('click',()=>{const row=S.residents.find(x=>x.id===b.dataset.edit);modal('Chỉnh sửa nhân khẩu',residentForm(row),async form=>{await api('/residents/'+row.id,{method:'PUT',body:cleanResident(form)});await residents()})}));
 document.querySelectorAll('[data-delete]').forEach(b=>b.addEventListener('click',async()=>{if(!confirm('Xóa mềm hồ sơ này? Nhật ký thao tác vẫn được giữ.'))return;try{await api('/residents/'+b.dataset.delete,{method:'DELETE'});await residents();toast('Đã đánh dấu xóa.')}catch(e){toast(e.message,true)}}));
}
async function duplicateReview(){
 const data=await api('/residents/duplicate-candidates');
 const cards=data.items.map(x=>`<div class="card"><div class="card-heading"><div><h3>${esc(x.left.full_name)} ↔ ${esc(x.right.full_name)}</h3><p class="muted small">${esc(x.rule)}</p></div><button class="outline small" data-not-duplicate="${esc(x.candidate_id)}">Không phải cùng người</button></div>${table(['Thuộc tính','Hồ sơ A','Hồ sơ B'],[
  `<tr>${cell('CCCD')}${cell(x.left.cccd||'Chưa có')}${cell(x.right.cccd||'Chưa có')}</tr>`,
  `<tr>${cell('Ngày sinh')}${cell(x.left.dob||'—')}${cell(x.right.dob||'—')}</tr>`,
  `<tr>${cell('Tổ')}${cell(x.left.team_id)}${cell(x.right.team_id)}</tr>`,
  `<tr>${cell('Điện thoại')}${cell(x.left.phone||'—')}${cell(x.right.phone||'—')}</tr>`,
  `<tr>${cell('Địa chỉ')}${cell(x.left.permanent_address||'—')}${cell(x.right.permanent_address||'—')}</tr>`])}</div>`).join('');
 $('content').innerHTML=`<div class="card"><div class="card-heading"><div><h3>Rà soát hồ sơ nghi trùng</h3><p class="muted small">${data.count} cặp cần xem xét${data.limited?' · giới hạn 500 cặp':''}</p></div><button class="outline" id="backResidents">Quay lại</button></div>${note(data.note)}</div>${cards||'<div class="card empty">Không còn cặp hồ sơ nào cần rà soát.</div>'}`;
 $('backResidents').addEventListener('click',residents);
 document.querySelectorAll('[data-not-duplicate]').forEach(b=>b.addEventListener('click',async()=>{if(!confirm('Xác nhận hai hồ sơ này không phải cùng một người?'))return;try{await api('/residents/duplicate-candidates/'+encodeURIComponent(b.dataset.notDuplicate)+'/decision',{method:'POST',body:{decision:'not_duplicate'}});await duplicateReview();toast('Đã ghi nhận quyết định và lưu nhật ký.')}catch(e){toast(e.message,true)}}));
}
async function households(){
 const d=await api('/households');S.households=d.items;
 const rows=d.items.map(h=>'<tr>'+cell(h.code)+cell(h.address)+cell('Tổ '+h.team_id)+cell(h.note)+'</tr>');
 $('content').innerHTML=`<div class="card"><div class="card-heading"><div><h3>Hồ sơ hộ gia đình</h3><p class="muted small">Mỗi hộ có một mã duy nhất, gắn được với nhân khẩu.</p></div>${canEdit()?'<button id="addHouse" class="primary">+ Tạo hộ</button>':''}</div>${rows.length?table(['Mã hộ','Địa chỉ','Tổ','Ghi chú'],rows):'<div class="empty">Chưa có hộ gia đình.</div>'}</div>`;
 $('addHouse')?.addEventListener('click',()=>modal('Tạo hồ sơ hộ',`<div class="form-grid"><label>Mã hộ<input required name="code"></label><label>Địa chỉ<input required name="address"></label><label>Tổ<input type="number" min="1" max="15" value="${S.me.team_id||1}" name="team_id" required></label><label>Ghi chú<input name="note"></label></div>`,async f=>{f.team_id=Number(f.team_id);await api('/households',{method:'POST',body:f});await households()}));
}
async function importPage(){
 await ensureFields();
 $('content').innerHTML=`<div class="card"><h3>Nhập dữ liệu vào PC chính</h3>${note('Đọc file XLSX, CSV, PDF văn bản hoặc ảnh CCCD trên PC. Tệp nguồn được xử lý trong RAM, không ghi ra ổ đĩa hay đưa lên máy chủ ngoài.')}
 <div class="toolbar" style="margin-top:16px"><input id="fileIn" type="file" accept=".xlsx,.csv,.pdf,.png,.jpg,.jpeg,.webp"><button class="primary" id="previewFile">Đọc & xem trước</button></div><p class="footnote">Giới hạn 10 MB, 5.000 dòng/lần. PDF dạng quét và OCR tiếng Việt cần gói Tesseract với dữ liệu ngôn ngữ Việt trên PC.</p></div><div id="importResult"></div>`;
 $('previewFile').addEventListener('click',async()=>{
  let f=$('fileIn').files?.[0];if(!f){toast('Chọn tệp trước khi đọc.',true);return}
  const fd=new FormData();fd.append('file',f);try{S.preview=await api('/import/preview',{method:'POST',body:fd});showPreview()}catch(e){toast(e.message,true)}
 });
}
function showPreview(){
 const p=S.preview;const headers=p.headers;
 const checkboxes=headers.map(h=>`<label class="checkline"><input class="includeHeader" type="checkbox" data-header="${esc(h)}" ${p.mapping[h]?'checked':''}> ${esc(h)}</label>`).join('');
 const selector=headers.map(h=>`<tr><td>${esc(h)}</td><td><select class="mapSelect" data-header="${esc(h)}"><option value="">Không nhập</option>${allKeys.map(k=>`<option value="${k}" ${p.mapping[h]===k?'selected':''}>${esc(fieldLabels[k])}</option>`).join('')}</select></td></tr>`);
 const preview=p.preview.map(r=>'<tr>'+headers.slice(0,7).map(h=>cell(String(r[h]||'').slice(0,85))).join('')+'</tr>');
 $('importResult').innerHTML=`<div class="card"><div class="card-heading"><h3>Đã đọc ${p.count} dòng · ${headers.length} cột</h3><span class="chip success">Xử lý tại PC</span></div><h3>1. Chọn cột nguồn</h3><div class="checkgrid">${checkboxes}</div></div>
 <div class="two-col"><div class="card"><h3>2. Ánh xạ vào trường chuẩn</h3>${table(['Cột nguồn','Trường đích'],selector)}</div><div class="card"><h3>3. Xem trước (${Math.min(10,p.count)} dòng)</h3>${headers.length?table(headers.slice(0,7),preview):'<p>Không có trường.</p>'}</div></div>
 <div class="card"><div class="toolbar"><button id="commitImport" class="primary">Nhập dữ liệu vào kho PC</button></div><div class="footnote">Yêu cầu trường Họ tên và Tổ; CCCD trùng được báo và bỏ qua. Bản nhập không tự ghi đè hồ sơ cũ.</div></div>`;
 $('commitImport').addEventListener('click',async()=>{
  const checked=Array.from(document.querySelectorAll('.includeHeader:checked')).map(x=>x.dataset.header);
  const mapping={};document.querySelectorAll('.mapSelect').forEach(x=>{if(checked.includes(x.dataset.header)&&x.value)mapping[x.dataset.header]=x.value});
  const selected=[...new Set(Object.values(mapping))];
  if(!selected.includes('full_name')||!selected.includes('team_id')){toast('Cần ánh xạ Họ tên và Tổ trước khi nhập.',true);return}
  if(!confirm('Xác nhận nhập dữ liệu vào PC chính? Dữ liệu trùng CCCD sẽ bỏ qua.'))return;
  try{const r=await api('/import/commit',{method:'POST',body:{rows:p.rows,mapping,selected}});
   $('importResult').innerHTML=`<div class="card"><h3>Kết quả nhập</h3><div class="cards"><div class="metric"><strong>${r.inserted}</strong>&nbsp;thành công</div><div class="metric"><strong>${r.duplicates}</strong>&nbsp;trùng</div><div class="metric"><strong>${r.invalid}</strong>&nbsp;không hợp lệ</div></div>${r.errors?.length?table(['Dòng','Lý do'],r.errors.map(x=>'<tr>'+cell(x.line)+cell(x.reason)+'</tr>')):''}</div>`;
   S.preview=null;toast('Đã hoàn tất xử lý nhập dữ liệu.');
  }catch(e){toast(e.message,true)}
 });
}
async function exportPage(){
 await ensureFields();
 if(!S.isPC){$('content').innerHTML=note('Xuất file chỉ được phép trên PC chính.');return}
 $('content').innerHTML=`<div class="card"><h3>Xuất file theo trường và phạm vi</h3>${note('Chỉ PC chính được tạo tệp xuất. Người dùng mobile không được tải dữ liệu cư dân xuống thiết bị.')}<h3 style="margin-top:20px">Chọn trường xuất</h3><div class="checkgrid">${allKeys.map(k=>`<label class="checkline"><input class="exportField" type="checkbox" value="${k}" ${['full_name','cccd','team_id'].includes(k)?'checked':''}>${esc(fieldLabels[k])}</label>`).join('')}</div>
 <div class="form-grid" style="margin-top:20px"><label>Định dạng<select id="exportFormat"><option value="xlsx">Excel (.xlsx)</option><option value="csv">CSV UTF-8 BOM</option><option value="json">JSON UTF-8</option></select></label><label>Lọc tổ<select id="exportTeam"><option value="">Tất cả tổ được phép</option>${Array.from({length:15},(_,i)=>`<option value="${i+1}">Tổ ${i+1}</option>`).join('')}</select></label></div>
 <div class="toolbar" style="margin-top:18px"><button class="primary" id="doExport">Xuất file tại PC</button></div></div>`;
 $('doExport').addEventListener('click',async()=>{
 const fields=Array.from(document.querySelectorAll('.exportField:checked')).map(x=>x.value);
 if(!fields.length){toast('Chọn ít nhất một trường.',true);return}
 const format=$('exportFormat').value;let uri='/export?format='+format+'&fields='+fields.join(',')+($('exportTeam').value?'&team='+$('exportTeam').value:'');
 try{const blob=await api(uri,{raw:true});let url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download='QuanLyKhuPho_'+new Date().toISOString().slice(0,10)+'.'+format;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);toast('Đã tạo tệp xuất từ PC.')}catch(e){toast(e.message,true)}
 });
}
async function accounts(){
 const d=await api('/accounts');
 const rows=d.items.map(r=>`<tr>${cell(r.name)}${cell(r.username)}${cell(r.phone||'—')}${cell(roles[r.role]||r.role)}${cell(r.team_id||'—')}
 <td><span class="chip ${r.active?'success':'bad'}">${!r.active?'Đã khóa':r.must_change_password?'Chưa đổi mật khẩu':'Hoạt động'}</span></td>
 <td><div class="actions">${S.isPC&&r.id!==S.me.id&&roles[r.role]&&(
 ['super_admin','admin_root','admin_kp'].includes(S.me.role))?
 `<button class="outline small" data-lock="${esc(r.id)}" data-active="${r.active}">${r.active?'Khóa':'Mở khóa'}</button>
 <button class="outline small" data-reset="${esc(r.id)}">Đặt lại MK</button>
 <button class="outline small" data-edit-account="${esc(r.id)}">Sửa</button>`:''}</div></td></tr>`);
 $('content').innerHTML=`<div class="card"><div class="card-heading"><div><h3>Tài khoản cán bộ</h3><p class="muted small">Quản lý vai trò, tổ, liên hệ, tình trạng và mật khẩu; toàn bộ dữ liệu trên PC.</p></div>${S.isPC?'<button class="primary" id="addAccount">+ Tạo tài khoản</button>':''}</div>
 <div class="toolbar"><input id="accountSearch" placeholder="Lọc họ tên / đăng nhập / SĐT" aria-label="Tìm tài khoản"><select id="accountRole"><option value="">Mọi vai trò</option>${Object.entries(roles).map(([k,v])=>`<option value="${k}">${esc(v)}</option>`).join('')}</select></div>
 ${table(['Họ tên','Tài khoản','SĐT','Vai trò','Tổ','Trạng thái','Hành động'],rows)}</div>`;
 let trs=Array.from(document.querySelectorAll('#content tbody tr'));
 function applyFilter(){const q=$('accountSearch').value.toLocaleLowerCase('vi'),role=$('accountRole').value;
   trs.forEach((tr,i)=>{const r=d.items[i];tr.hidden=!(r.name.toLocaleLowerCase('vi').includes(q)||r.username.includes(q)||(r.phone||'').includes(q))||(!!role&&r.role!==role)});}
 $('accountSearch').addEventListener('input',applyFilter);$('accountRole').addEventListener('change',applyFilter);
 $('addAccount')?.addEventListener('click',()=>modal('Tạo tài khoản cán bộ',`<div class="form-grid">
 <label>Họ tên có dấu<input required maxlength="120" name="full_name"></label>
 <label>Tên đăng nhập<input required name="username" pattern="[a-z][a-z0-9._-]{2,49}" placeholder="vidu.tuvan"></label>
 <label>Mật khẩu cấp ban đầu<input type="password" required minlength="12" name="password" autocomplete="new-password"></label>
 <label>Vai trò<select name="role"><option value="viewer">Chỉ xem</option><option value="reviewer">Rà soát</option>${S.me.role!=='admin_kp'?'<option value="admin_kp">Quản trị khu phố</option>':''}${S.me.role==='super_admin'?'<option value="admin_root">Quản trị gốc</option>':''}</select></label>
 <label>Tổ phụ trách (1–15)<input type="number" min="1" max="15" name="team_id" value="${S.me.role==='admin_kp'?esc(S.me.team_id):''}"></label>
 <label>Số điện thoại<input name="phone" type="tel" pattern="0[0-9]{9}" maxlength="10"></label>
 <label>Zalo ID<input name="zalo_id" maxlength="120"></label><label>Email<input name="email" type="email" maxlength="120"></label></div>`,
 async x=>{if(x.team_id)x.team_id=Number(x.team_id);await api('/accounts',{method:'POST',body:x});await accounts()}));
 document.querySelectorAll('[data-lock]').forEach(b=>b.addEventListener('click',async()=>{if(!confirm('Xác nhận thay đổi trạng thái tài khoản?'))return;
   try{await api('/accounts/'+encodeURIComponent(b.dataset.lock),{method:'PATCH',body:{active:b.dataset.active!=='true'}});await accounts();toast('Đã cập nhật trên PC.')}catch(e){toast(e.message,true)}}));
 document.querySelectorAll('[data-reset]').forEach(b=>b.addEventListener('click',()=>modal('Đặt lại mật khẩu',
 `<p class="muted">Phiên cũ bị thu hồi; tài khoản phải đổi mật khẩu khi đăng nhập lại.</p><label>Mật khẩu ban đầu mới<input type="password" name="password" minlength="12" required autocomplete="new-password"></label>`,
 async x=>{await api('/accounts/'+encodeURIComponent(b.dataset.reset),{method:'PATCH',body:{password:x.password}});await accounts()})));
 document.querySelectorAll('[data-edit-account]').forEach(b=>b.addEventListener('click',()=>{let r=d.items.find(v=>v.id===b.dataset.editAccount);
 modal('Cập nhật hồ sơ tài khoản',`<div class="form-grid"><label>Họ và tên<input name="full_name" required value="${esc(r.name)}"></label>
 <label>Số điện thoại<input name="phone" type="tel" pattern="0[0-9]{9}" maxlength="10" value="${esc(r.phone||'')}"></label>
 <label>Email<input type="email" name="email" value="${esc(r.email||'')}"></label>
 <label>Zalo ID<input name="zalo_id" value="${esc(r.zalo_id||'')}"></label>
 <label>Tổ (1–15)<input type="number" name="team_id" min="1" max="15" value="${r.team_id||''}"></label></div>`,
 async x=>{if(x.team_id)x.team_id=Number(x.team_id);await api('/accounts/'+encodeURIComponent(r.id),{method:'PATCH',body:x});await accounts()});}));
}
async function devices(){
 const [d,requests]=await Promise.all([api('/devices'),S.isPC?api('/mobile/login-requests'):Promise.resolve({items:[]})]);
 const rows=d.items.map(x=>`<tr>${cell(x.user_name)}${cell(x.device_label)}${cell(x.id.slice(0,22))}<td><span class="chip ${x.status==='approved'?'success':x.status==='blocked'?'bad':'warn'}">${esc(x.status)}</span></td><td>${S.isPC&&x.status!=='approved'?`<button class="secondary small" data-device="${esc(x.id)}" data-status="approved">Duyệt</button>`:''}${S.isPC?`<button class="danger small" data-device="${esc(x.id)}" data-status="blocked">Chặn</button>`:''}</td></tr>`);
 const pending=requests.items.filter(x=>!x.approved);
 const approvalRows=pending.map(x=>`<tr>${cell(x.user_name)}${cell(x.device_label)}${cell(x.device_status==='approved'?'Đã duyệt':'Chưa duyệt')}${cell(dateTime(x.created_at))}<td><div class="actions"><button class="secondary small" data-approval="${esc(x.request_id)}" data-answer="yes" ${x.device_status!=='approved'?'disabled title="Duyệt thiết bị trước"':''}>Cho phép phiên</button><button class="danger small" data-approval="${esc(x.request_id)}" data-answer="no">Từ chối</button></div></td></tr>`);
 $('content').innerHTML=`<div class="card"><div class="card-heading"><h3>Thiết bị được quản lý</h3><button class="outline small" id="refreshDevices">Làm mới</button></div>${note('Mobile cần được duyệt thiết bị và phải có phê duyệt PC ở MỖI phiên. Mã thiết bị không tự chứng minh danh tính; xác nhận thông tin trực tiếp với cán bộ.')}${table(['Tài khoản','Tên thiết bị','Định danh','Trạng thái','Thao tác'],rows)}</div>${S.isPC?`<div class="card"><div class="card-heading"><h3>Yêu cầu đăng nhập mobile chờ duyệt</h3><span class="chip warn">${pending.length} yêu cầu</span></div>${pending.length?table(['Người dùng','Thiết bị tự khai báo','Duyệt thiết bị','Thời điểm','Quyết định'],approvalRows):'<p class="muted">Không có yêu cầu đăng nhập mới.</p>'}</div>`:''}`;
 $('refreshDevices')?.addEventListener('click',devices);
 document.querySelectorAll('[data-device]').forEach(b=>b.addEventListener('click',async()=>{
  if(!confirm('Xác nhận '+(b.dataset.status==='approved'?'duyệt':'chặn')+' thiết bị?'))return;
  try{await api('/devices/'+encodeURIComponent(b.dataset.device),{method:'PATCH',body:{status:b.dataset.status}});await devices();toast('Đã cập nhật thiết bị.')}catch(e){toast(e.message,true)}
 }));
 document.querySelectorAll('[data-approval]').forEach(b=>b.addEventListener('click',async()=>{
  if(!confirm('Xác nhận '+(b.dataset.answer==='yes'?'cho phép':'từ chối')+' phiên đăng nhập này?'))return;
  try{await api('/mobile/login-requests/'+encodeURIComponent(b.dataset.approval)+'/decision',{method:'POST',body:{approved:b.dataset.answer==='yes'}});await devices();toast('Đã cập nhật phê duyệt phiên.')}catch(e){toast(e.message,true)}
 }));
}
async function permissions(){
 if(S.me.role!=='super_admin'||!S.isPC)throw Error('Chỉ Super Admin tại PC chính được quản lý ma trận.');
 const m=await api('/permissions');
 const col=m.roles.map(r=>`<th>${esc(roles[r])}</th>`).join('');
 const rows=Object.entries(m.capabilities).map(([c,label])=>`<tr><td>${esc(label)}</td>${m.roles.map(r=>{
  const allow=!!m.role_grants[r][c],inBaseline=m.default_capabilities[r].includes(c),locked=r==='super_admin'||!inBaseline;
  return `<td><label class="checkline"><input type="checkbox" data-role="${esc(r)}" data-cap="${esc(c)}" ${allow?'checked':''} ${locked?'disabled':''}><span>${allow?'Có':'Không'}</span></label></td>`;
 }).join('')}</tr>`).join('');
 const fieldRows=Object.entries(m.field_labels).map(([field,label])=>`<tr><td>${esc(label)}</td>${m.roles.map(r=>{
 const mode=m.field_grants[r][field];const opts=[['required','Bắt buộc'],['optional','Tùy chọn'],['hidden','Ẩn'],['masked','Che']];
 return `<td><select data-field="${esc(field)}" data-field-role="${esc(r)}" ${r==='super_admin'?'disabled':''}>${opts.map(([v,t])=>`<option value="${v}" ${mode===v?'selected':''}>${t}</option>`).join('')}</select></td>`;
 }).join('')}</tr>`).join('');
 $('content').innerHTML=`<div class="card"><h3>Ma trận quyền thao tác</h3>${note('Chỉ thu hồi hoặc khôi phục quyền mặc định. Không cho nâng vượt vai trò. Mỗi thay đổi có nhật ký và hiệu lực ngay trên API.')}${table(['Chức năng',...m.roles.map(r=>roles[r])],rows)}</div><div class="card"><h3>Quyền truy cập từng trường</h3>${note('Quyền Ẩn và Che được kiểm tra khi API trả dữ liệu. Viewer không thể nhận CCCD hoặc SĐT đầy đủ.')}${table(['Trường',...m.roles.map(r=>roles[r])],fieldRows)}</div>`;
 document.querySelectorAll('[data-cap]').forEach(x=>x.addEventListener('change',async()=>{
  const old=!x.checked;
  try{await api('/permissions/capability',{method:'PUT',body:{role:x.dataset.role,capability:x.dataset.cap,allowed:x.checked}});toast('Đã cập nhật quyền và ghi nhật ký.');}
  catch(e){x.checked=old;toast(e.message,true)}
 }));
 document.querySelectorAll('[data-field]').forEach(x=>x.addEventListener('change',async()=>{
  let next=x.value;
  try{await api('/permissions/field',{method:'PUT',body:{role:x.dataset.fieldRole,field:x.dataset.field,mode:next}});toast('Đã lưu phân quyền dữ liệu.');}
  catch(e){toast(e.message,true);await permissions()}
 }));
}
async function fields(){
 const d=await api('/fields?include_inactive=true');syncFields({standard:d.standard,custom:d.custom.filter(x=>x.active)});
 const typeName={text:'Văn bản',number:'Số',date:'Ngày',dropdown:'Danh sách',checkbox:'Đánh dấu'};
 const rows=d.standard.map(x=>'<tr>'+cell(x.id)+cell(x.label)+cell('Hệ thống')+cell('—')+cell('—')+cell(x.mode)+cell('—')+'</tr>').concat(d.custom.map(x=>`<tr>${cell(x.name)}${cell(x.label)}${cell(typeName[x.type]||x.type)}${cell(x.display_order)}${cell(x.usage_count)}<td><span class="chip ${x.active?'success':'bad'}">${x.active?'Đang dùng':'Đã ẩn'}</span></td><td><div class="actions"><button class="outline small" data-move-field="${esc(x.id)}" data-direction="up" title="Đưa lên">↑</button><button class="outline small" data-move-field="${esc(x.id)}" data-direction="down" title="Đưa xuống">↓</button><button class="outline small" data-edit-field="${esc(x.id)}">Sửa</button>${S.isPC&&isRoot()&&x.type==='dropdown'?`<button class="outline small" data-migrate-field="${esc(x.id)}">Chuyển giá trị</button><button class="outline small" data-migration-history="${esc(x.id)}">Lịch sử</button>`:''}</div></td></tr>`));
 $('content').innerHTML=`<div class="card"><div class="card-heading"><h3>Danh mục trường dữ liệu</h3><button class="primary" id="newField">+ Thêm trường</button></div>${table(['Mã','Tên trường','Kiểu','Thứ tự','Đang dùng','Trạng thái','Thao tác'],rows)}<p class="footnote">Không thể bỏ lựa chọn danh sách đang có hồ sơ sử dụng. Ẩn trường không xóa dữ liệu đã nhập.</p></div>`;
 const fieldForm=(row={})=>`<div class="form-grid">${row.id?'':`<label>Mã không dấu (snake_case)<input required name="name" pattern="[a-z][a-z0-9_]{1,40}"></label>`}<label>Tên hiển thị có dấu<input required name="label" value="${esc(row.label||'')}"></label>${row.id?`<label>Kiểu dữ liệu<input value="${esc(typeName[row.type]||row.type)}" disabled></label><label>Thứ tự hiển thị<input type="number" min="0" max="10000" name="display_order" value="${row.display_order||0}"></label>`:`<label>Kiểu dữ liệu<select name="type"><option value="text">Văn bản</option><option value="number">Số</option><option value="date">Ngày</option><option value="dropdown">Danh sách</option><option value="checkbox">Đánh dấu</option></select></label>`}<label style="grid-column:1/-1">Các lựa chọn, mỗi dòng một giá trị (chỉ dùng với kiểu Danh sách)<textarea name="options" rows="5">${esc((()=>{try{return JSON.parse(row.options_json||'[]').join('\n')}catch{return ''}})())}</textarea></label>${row.id?`<label class="checkline"><input type="checkbox" name="active" value="1" ${row.active?'checked':''}> Đang sử dụng</label>`:''}</div>`;
 const prepare=x=>{x.options=String(x.options||'').split(/\r?\n/).map(v=>v.trim()).filter(Boolean);if(x.display_order!==undefined&&x.display_order!==null)x.display_order=Number(x.display_order);return x};
 $('newField').addEventListener('click',()=>modal('Thêm trường dữ liệu',fieldForm(),async x=>{await api('/fields',{method:'POST',body:prepare(x)});S.fields=null;await fields()}));
 document.querySelectorAll('[data-edit-field]').forEach(b=>b.addEventListener('click',()=>{const row=d.custom.find(x=>x.id===b.dataset.editField);modal('Sửa cấu hình trường',fieldForm(row),async x=>{x.active=x.active==='1';await api('/fields/'+encodeURIComponent(row.id),{method:'PATCH',body:prepare(x)});S.fields=null;await fields()})}));
 document.querySelectorAll('[data-move-field]').forEach(b=>b.addEventListener('click',async()=>{try{await api('/fields/'+encodeURIComponent(b.dataset.moveField)+'/move',{method:'POST',body:{direction:b.dataset.direction}});S.fields=null;await fields()}catch(e){toast(e.message,true)}}));
 document.querySelectorAll('[data-migrate-field]').forEach(b=>b.addEventListener('click',()=>{const row=d.custom.find(x=>x.id===b.dataset.migrateField);let options=[];try{options=JSON.parse(row.options_json||'[]')}catch{}
  modal('Chuyển hàng loạt lựa chọn',`<p class="muted">Thao tác thực hiện trên toàn bộ kho PC và có nhật ký. Dữ liệu sẽ không đổi trước khi xem trước và xác nhận.</p><div class="form-grid"><label>Lựa chọn đang dùng<select name="from_value" required><option value="">Chọn nguồn</option>${options.map(x=>`<option value="${esc(x)}">${esc(x)}</option>`).join('')}</select></label><label>Chuyển thành<input name="to_value" required maxlength="100"></label></div>`,async x=>{
   const preview=await api('/fields/'+encodeURIComponent(row.id)+'/option-migration/preview',{method:'POST',body:x});
   if(!preview.affected_count)throw Error('Không có hồ sơ nào đang dùng lựa chọn nguồn.');
   const names=preview.sample.slice(0,10).map(v=>`• ${v.full_name} — Tổ ${v.team_id}${v.dob?' — '+v.dob:''}`).join('\n');
   const more=preview.sample_limited?'\n… danh sách xem trước chỉ hiển thị tối đa 50 hồ sơ.':'';
   if(!confirm(`Sẽ chuyển ${preview.affected_count} hồ sơ từ “${preview.from_value}” sang “${preview.to_value}”.\n\n${names}${more}\n\nTiếp tục?`))throw Error('Đã hủy chuyển dữ liệu.');
   const result=await api('/fields/'+encodeURIComponent(row.id)+'/option-migration/commit',{method:'POST',body:{...x,expected_count:preview.affected_count,confirmation:'CHUYEN DU LIEU'}});
   S.fields=null;await fields();toast('Đã chuyển '+result.updated+' hồ sơ và ghi nhật ký.');
  })}));
 document.querySelectorAll('[data-migration-history]').forEach(b=>b.addEventListener('click',async()=>{try{
  const history=await api('/fields/'+encodeURIComponent(b.dataset.migrationHistory)+'/option-migrations');
  const active=history.items.filter(x=>!x.undone_at);
  const rows=history.items.map(x=>`<tr><td><input type="radio" name="migration_id" value="${esc(x.id)}" ${x.undone_at?'disabled':''}></td>${cell(x.from_value+' → '+x.to_value)}${cell(x.affected_count)}${cell(dateTime(x.created_at))}${cell(x.undone_at?'Đã hoàn tác':'Có thể hoàn tác')}</tr>`);
  modal('Lịch sử chuyển lựa chọn',history.items.length?table(['Chọn','Chuyển đổi','Hồ sơ','Thời điểm','Trạng thái'],rows):'<p>Chưa có giao dịch chuyển đổi.</p>',async x=>{
   if(!x.migration_id)throw Error(active.length?'Chọn một giao dịch chưa hoàn tác.':'Không có giao dịch có thể hoàn tác.');
   if(!confirm('Hoàn tác giao dịch đã chọn? Hệ thống sẽ dừng nếu hồ sơ hoặc cấu hình đã thay đổi.'))throw Error('Đã hủy hoàn tác.');
   const result=await api('/fields/'+encodeURIComponent(b.dataset.migrationHistory)+'/option-migrations/'+encodeURIComponent(x.migration_id)+'/undo',{method:'POST',body:{confirmation:'HOAN TAC'}});
   S.fields=null;await fields();toast('Đã hoàn tác '+result.restored+' hồ sơ.');
  });
 }catch(e){toast(e.message,true)}}));
}
async function templates(){
 await ensureFields();
 const d=await api('/templates');let rows=d.items.map(x=>'<tr>'+cell(x.name)+cell(x.fields_json)+cell(dateTime(x.created_at))+'</tr>');
 $('content').innerHTML=`<div class="card"><div class="card-heading"><h3>Mẫu trường xuất/nhập</h3><button class="primary" id="newTemplate">+ Lưu mẫu</button></div>${rows.length?table(['Tên mẫu','Trường đã lưu','Ngày tạo'],rows):'<p class="muted">Chưa có mẫu dữ liệu.</p>'}</div>`;
 $('newTemplate').addEventListener('click',()=>modal('Tạo mẫu trường',`<label>Tên mẫu<input required name="name"></label><p>Chọn trường cần lưu</p><div class="checkgrid">${allKeys.map(k=>`<label class="checkline"><input type="checkbox" name="selected_${k}" value="1">${esc(fieldLabels[k])}</label>`).join('')}</div>`,async x=>{const selected=allKeys.filter(k=>x['selected_'+k]);await api('/templates',{method:'POST',body:{name:x.name,fields:selected,mapping:{}}});await templates()}));
}
async function audit(){
 const [r,v]=await Promise.all([api('/audit'),S.me.role==='super_admin'?api('/audit/verify'):Promise.resolve(null)]);
 const rows=r.items.map(x=>'<tr>'+cell(x.seq)+cell(dateTime(x.at))+cell(x.action)+cell(x.entity_type)+cell(x.entity_id.slice(0,15))+'</tr>');
 $('content').innerHTML=`<div class="card"><div class="card-heading"><h3>Nhật ký hành động</h3>${v?`<span class="chip ${v.valid?'success':'bad'}">${v.valid?'Chuỗi hash hợp lệ ('+v.count+')':'LỖI CHUỖI HASH'}</span>`:''}</div>${note('Nhật ký chỉ cho ghi thêm, không có nút xóa. Chuỗi hash chỉ phát hiện sửa đổi ở mức dữ liệu, không thay thế bảo vệ vật lý của PC.')}${table(['STT','Thời điểm','Hành động','Đối tượng','Mã nội bộ'],rows)}</div>${S.isPC&&S.me.role==='super_admin'?'<div class="card"><h3>Sao lưu nội bộ PC</h3><p class="muted">Sao lưu mã hóa trong thư mục Data/backups trên PC chính. Không đồng bộ đám mây.</p><button class="secondary" id="backupNow">Tạo bản sao lưu PC</button></div>':''}`;
 $('backupNow')?.addEventListener('click',async()=>{if(!confirm('Tạo một bản sao lưu mã hóa trên chính PC?'))return;try{let d=await api('/backup',{method:'POST'});toast('Đã sao lưu: '+d.location)}catch(e){toast(e.message,true)}});
}
boot();
