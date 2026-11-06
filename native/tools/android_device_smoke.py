"""Finite APK install/launch/chooser smoke test on a dedicated emulator; no real user data."""
import json, os, re, subprocess, time
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'build-logs/android'
OUT.mkdir(parents=True,exist_ok=True)
SDK=Path(os.environ.get('ANDROID_HOME') or os.environ['ANDROID_SDK_ROOT'])
ADB=SDK/'platform-tools/adb'
SERIAL='emulator-5554'
APP='vn.qlkp.companion'
APK=ROOT/'dist/android/QLKP-Mobile-0.19.0-PILOT-debug.apk'
report={'format':'QLKP_ANDROID_DEVICE_SMOKE_V1','layer':'device','status':'RUNNING','steps':[]}
def save():
    p=OUT/'device-session.json'
    temp=p.with_suffix('.tmp');temp.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');temp.replace(p)
def call(*args,timeout=45):
    return subprocess.run([str(ADB),'-s',SERIAL,*args],check=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=timeout).stdout
def step(name,action):
    row={'code':name,'status':'RUNNING'};report['steps'].append(row);save()
    start=time.monotonic()
    try: action();row['status']='PASS'
    except BaseException as ex:row['status']='FAIL';row['error_type']=type(ex).__name__;raise
    finally:row['duration_ms']=round((time.monotonic()-start)*1000);save()
def snapshot(name):
    call('shell','uiautomator','dump','/sdcard/window.xml')
    call('pull','/sdcard/window.xml',str(OUT/(name+'.xml')))
    (OUT/(name+'.png')).write_bytes(call('exec-out','screencap','-p'))
    return ET.parse(OUT/(name+'.xml')).getroot()
def launch():
    call('shell','am','start','-W','-n',APP+'/.MainActivity')
    if not call('shell','pidof',APP).strip():raise RuntimeError('Activity stopped')
    screen=snapshot('main-screen')
    texts=[node.attrib.get('text','') for node in screen.iter()]
    if 'QUẢN LÝ KHU PHỐ' not in texts:raise RuntimeError('Native title not visible')
    button=next((n for n in screen.iter() if n.attrib.get('text','')=='Chọn gói .qlkpsync từ máy tính'),None)
    if button is None or button.attrib.get('enabled')!='true':raise RuntimeError('File picker button unavailable')
def chooser():
    screen=ET.parse(OUT/'main-screen.xml').getroot()
    node=next(n for n in screen.iter() if n.attrib.get('text','')=='Chọn gói .qlkpsync từ máy tính')
    x1,y1,x2,y2=map(int,re.findall(r'\d+',node.attrib['bounds']))
    call('shell','input','tap',str((x1+x2)//2),str((y1+y2)//2))
    ui=snapshot('file-chooser')
    if not any(n.attrib.get('package','').startswith('com.google.android.documentsui') or n.attrib.get('package','').startswith('com.android.documentsui') for n in ui.iter()):
        raise RuntimeError('Android document chooser did not open')
    call('shell','input','keyevent','4')
    if not call('shell','pidof',APP).strip():raise RuntimeError('Application stopped after chooser')
log=(OUT/'emulator.log').open('wb')
emulator=None
save()
try:
    emulator=subprocess.Popen([str(SDK/'emulator/emulator'),'-avd','qlkp_smoke','-port','5554','-no-window','-no-audio','-no-snapshot','-no-boot-anim','-gpu','swiftshader_indirect'],stdout=log,stderr=subprocess.STDOUT)
    def boot():
        deadline=time.monotonic()+240
        while time.monotonic()<deadline:
            if emulator.poll() is not None:raise RuntimeError('Emulator exited')
            try:
                if call('shell','getprop','sys.boot_completed',timeout=10).strip()==b'1':return
            except (subprocess.CalledProcessError,subprocess.TimeoutExpired):pass
            time.sleep(2)
        raise TimeoutError('Android boot timed out')
    step('EMULATOR_BOOT',boot)
    call('shell','input','keyevent','82')
    step('APK_INSTALL',lambda:call('install','-r',str(APK),timeout=90))
    step('ACTIVITY_LAUNCH_AND_NATIVE_SCREEN',launch)
    step('FILE_CHOOSER_BUTTON',chooser)
    step('APK_UNINSTALL',lambda:call('uninstall',APP))
    report['status']='PASS'
except BaseException:
    report['status']='FAIL';raise
finally:
    save()
    if emulator is not None:
        try: call('emu','kill',timeout=10)
        except (subprocess.SubprocessError,OSError):pass
        try:emulator.wait(timeout=10)
        except subprocess.TimeoutExpired:emulator.kill();emulator.wait(timeout=10)
    log.close()
