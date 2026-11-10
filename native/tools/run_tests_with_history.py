"""Run unittest with durable per-case checkpoints. Logs IDs/status/error classes, never exception values."""
from __future__ import annotations
import argparse,datetime as dt,hashlib,json,os,platform,sys,time,unittest,uuid
from pathlib import Path
FORMAT='QLKP_TEST_RUN_V1'
def utc():return dt.datetime.now(dt.timezone.utc).isoformat()
class Journal:
 def __init__(self,root:Path,kind='regression',layer='source_sql',source_version='0.21.2-PILOT',source_hash=''):
  self.id=uuid.uuid4().hex;self.directory=Path(root)/self.id;self.directory.mkdir(parents=True,exist_ok=False)
  self.started=time.monotonic_ns();self.active=None;self.closed=False
  self.report=dict(format=FORMAT,session_id=self.id,parent_session_id='',kind=kind,layer=layer,status='RUNNING',started_utc=utc(),finished_utc='',duration_ms=0,
    source_version=source_version,source_hash=source_hash,hash_kind='source_tree_sha256' if source_hash else '',environment=f'{platform.system()} {platform.release()} {platform.machine()}',
    origin='LOCAL',original_session_id='',import_sha256='',last_sequence=0,total=0,passed=0,failed=0,skipped=0,steps=[])
  self.events=(self.directory/'events.jsonl').open('x',encoding='utf-8');self.progress=(self.directory/'progress.log').open('x',encoding='utf-8')
  self._save('SESSION_START')
 def _save(self,event,step=None):
  r=self.report;r['last_sequence']+=1;r['total']=len(r['steps'])
  for key,state in [('passed','PASS'),('failed','FAIL'),('skipped','SKIP')]:r[key]=sum(s['status']==state for s in r['steps'])
  self.events.write(json.dumps(dict(session_id=self.id,sequence=r['last_sequence'],event=event,at_utc=utc(),step=step),ensure_ascii=False)+'\n');self.events.flush();os.fsync(self.events.fileno())
  temp=self.directory/(uuid.uuid4().hex+'.tmp')
  try:
   with temp.open('x',encoding='utf-8') as f:json.dump(r,f,ensure_ascii=False,indent=2);f.flush();os.fsync(f.fileno())
   os.replace(temp,self.directory/'session.json')
  finally:
   if temp.exists():temp.unlink()
 def begin(self,code):
  if self.closed or self.active is not None:raise ValueError('session closed or step still active')
  if not isinstance(code,str) or not code.strip() or len(code)>256 or len(self.report['steps'])>=5000:raise ValueError('invalid step')
  self.active=dict(code=code,status='RUNNING',started_utc=utc(),finished_utc='',duration_ms=0,error_type='');self.step_started=time.monotonic_ns()
  self.report['steps'].append(self.active);self._save('STEP_START',self.active)
 def end(self,status,error_type=''):
  if status not in ('PASS','FAIL','SKIP','INTERRUPTED'):raise ValueError('invalid step status')
  if self.active is None:return
  s=self.active;s.update(status=status,finished_utc=utc(),duration_ms=(time.monotonic_ns()-self.step_started)//1_000_000,error_type=error_type)
  self.active=None;self._save('STEP_END',s)
  self.progress.write(f"{s['status']} {s['code']} {s['error_type']}\n");self.progress.flush();os.fsync(self.progress.fileno())
 def finish(self,status):
  if self.closed:return
  if status not in ('PASS','FAIL','INTERRUPTED','SKIP'):raise ValueError('invalid session status')
  if self.active is not None:self.end('INTERRUPTED' if status=='PASS' else status)
  if any(s['status']=='FAIL' for s in self.report['steps']):status='FAIL'
  elif any(s['status']=='INTERRUPTED' for s in self.report['steps']):status='INTERRUPTED'
  if status=='PASS' and all(s['status']=='SKIP' for s in self.report['steps']):status='SKIP'
  self.report.update(status=status,finished_utc=utc(),duration_ms=(time.monotonic_ns()-self.started)//1_000_000)
  self._save('SESSION_END');self.closed=True;self.events.close();self.progress.close()
class RecordedResult(unittest.TestResult):
 def __init__(self,journal):super().__init__();self.journal=journal;self.pending=None;self.error_type=''
 def startTest(self,test):super().startTest(test);self.pending=None;self.error_type='';self.journal.begin(test.id())
 def addSuccess(self,test):super().addSuccess(test);self.pending='PASS'
 def addFailure(self,test,err):super().addFailure(test,err);self.pending='FAIL';self.error_type=err[0].__name__
 def addError(self,test,err):super().addError(test,err);self.pending='FAIL';self.error_type=err[0].__name__
 def addSkip(self,test,reason):super().addSkip(test,reason);self.pending='SKIP';self.error_type='SKIPPED'
 def addExpectedFailure(self,test,err):super().addExpectedFailure(test,err);self.pending='SKIP';self.error_type='EXPECTED_FAILURE'
 def addUnexpectedSuccess(self,test):super().addUnexpectedSuccess(test);self.pending='FAIL';self.error_type='UNEXPECTED_SUCCESS'
 def addSubTest(self,test,subtest,err):
  super().addSubTest(test,subtest,err)
  if err is not None:self.pending='FAIL';self.error_type=err[0].__name__
 def stopTest(self,test):
  self.journal.end(self.pending or 'INTERRUPTED',self.error_type);super().stopTest(test)
  if self.testsRun%25==0:print(f'Đã ghi {self.testsRun} ca kiểm thử...',flush=True)
def source_hash(root):
 h=hashlib.sha256()
 for top in ('src','database','tests','tools','android-companion'):
  for f in sorted((root/top).rglob('*')):
   if f.is_file() and '__pycache__' not in f.parts and f.suffix in ('.cs','.xaml','.csproj','.sql','.py','.kt','.kts','.xml','.xz'):
    h.update(f.relative_to(root).as_posix().encode());h.update(b'\0');h.update(f.read_bytes())
 for f in sorted(root.glob('*.ps1')):h.update(f.name.encode());h.update(f.read_bytes())
 return h.hexdigest()
def run(suite,journal):
 result=RecordedResult(journal)
 if suite.countTestCases()==0:
  journal.begin('DISCOVERY_EMPTY');journal.end('SKIP','NO_TESTS');journal.finish('SKIP');return 2
 try:
  suite.run(result);journal.finish('PASS' if result.wasSuccessful() else 'FAIL');return 0 if journal.report['status'] in ('PASS','SKIP') else 1
 except KeyboardInterrupt:journal.finish('INTERRUPTED');return 130
 except BaseException as error:
  if journal.active is None:journal.begin('RUNNER_ERROR')
  journal.end('FAIL',type(error).__name__);journal.finish('FAIL');raise
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--start-dir',type=Path,default=Path(__file__).resolve().parents[1]/'tests');p.add_argument('--pattern',default='test*.py');p.add_argument('--output',type=Path);a=p.parse_args()
 root=Path(__file__).resolve().parents[1];out=a.output or (Path(os.environ['LOCALAPPDATA'])/'QLKP-V2-PILOT/test-runs' if os.name=='nt' and os.environ.get('LOCALAPPDATA') else root/'test-runs')
 j=Journal(out,source_hash=source_hash(root))
 try:suite=unittest.defaultTestLoader.discover(str(a.start_dir),pattern=a.pattern)
 except BaseException as error:j.begin('DISCOVERY');j.end('FAIL',type(error).__name__);j.finish('FAIL');raise
 code=run(suite,j);print(f"{j.report['status']}: {j.report['total']} ca, đạt={j.report['passed']}, lỗi={j.report['failed']}, bỏ qua={j.report['skipped']}");print('Hồ sơ:',j.directory/'session.json');sys.exit(code)
