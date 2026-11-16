"""Finite APK install/launch/chooser smoke test on a dedicated emulator; no real user data."""
import hashlib, json, os, re, subprocess, time
from run_tests_with_history import Journal
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
OUT=Path(os.environ.get('QLKP_SMOKE_EVIDENCE') or ROOT/'build-logs/android')
OUT.mkdir(parents=True,exist_ok=True)
SDK=Path(os.environ.get('ANDROID_HOME') or os.environ['ANDROID_SDK_ROOT'])
ADB=SDK/'platform-tools/adb'
SERIAL='emulator-5554'
APP=os.environ.get('QLKP_SMOKE_APP','vn.qlkp.companion')
APK=Path(os.environ.get('QLKP_SMOKE_APK') or ROOT/'dist/android/QLKP-Mobile-0.20.0-PILOT-debug.apk').resolve()
journal=Journal(OUT,kind='android_release_smoke' if os.environ.get('QLKP_SMOKE_APK') else 'android_debug_smoke',layer='device',source_hash=hashlib.sha256(APK.read_bytes()).hexdigest())
report=journal.report;report['hash_kind']='apk_sha256'
def save():
    p=OUT/'device-session.json'
    temp=p.with_suffix('.tmp');temp.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');temp.replace(p)
def call(*args,timeout=45):
    return subprocess.run([str(ADB),'-s',SERIAL,*args],check=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=timeout).stdout
def step(name,action):
    journal.begin(name);save()
    try: action();journal.end('PASS')
    except BaseException as ex:journal.end('FAIL',type(ex).__name__);raise
    finally:save()
def snapshot(name):
    call('shell','uiautomator','dump','/sdcard/window.xml')
    call('pull','/sdcard/window.xml',str(OUT/(name+'.xml')))
    (OUT/(name+'.png')).write_bytes(call('exec-out','screencap','-p'))
    return ET.parse(OUT/(name+'.xml')).getroot()
def launch():
    call('shell','am','start','-W','-n',APP+'/vn.qlkp.companion.MainActivity')
    if not call('shell','pidof',APP).strip():raise RuntimeError('Activity stopped')
    screen=snapshot('main-screen')
    texts=[node.attrib.get('text','') for node in screen.iter()]
    if 'QUẢN LÝ KHU PHỐ' not in texts:raise RuntimeError('Native title not visible')
    button=next((n for n in screen.iter() if n.attrib.get('resource-id','')==APP+':id/import_catalog'),None)
    if button is None or button.attrib.get('enabled')!='true' or button.attrib.get('clickable')!='true':raise RuntimeError('File picker button unavailable')
def chooser():
    screen=ET.parse(OUT/'main-screen.xml').getroot()
    node=next(n for n in screen.iter() if n.attrib.get('resource-id','')==APP+':id/import_catalog')
    x1,y1,x2,y2=map(int,re.findall(r'\d+',node.attrib['bounds']))
    call('shell','input','tap',str((x1+x2)//2),str((y1+y2)//2))
    deadline=time.monotonic()+20
    while True:
        ui=snapshot('file-chooser')
        if any(n.attrib.get('package','').startswith('com.google.android.documentsui') or n.attrib.get('package','').startswith('com.android.documentsui') for n in ui.iter()):break
        if time.monotonic()>=deadline:raise RuntimeError('Android document chooser did not open')
        time.sleep(0.5)
    call('shell','input','keyevent','4')
    if not call('shell','pidof',APP).strip():raise RuntimeError('Application stopped after chooser')
def wait_for_text(name,label,timeout=15):
    deadline=time.monotonic()+timeout
    while True:
        screen=snapshot(name)
        if any(n.attrib.get('text')==label for n in screen.iter()):return screen
        if time.monotonic()>=deadline:raise RuntimeError('Missing screen element: '+label)
        time.sleep(0.5)

def tap_text(screen,label):
    node=next(n for n in screen.iter() if n.attrib.get('text')==label and n.attrib.get('clickable')=='true')
    x1,y1,x2,y2=map(int,re.findall(r'\d+',node.attrib['bounds']))
    if x2-x1<24 or y2-y1<24:raise RuntimeError('Action is clipped')
    call('shell','input','tap',str((x1+x2)//2),str((y1+y2)//2))

def compact_rotate_back():
    call('shell','wm','size','720x1280')
    call('shell','wm','density','320')
    call('shell','settings','put','system','font_scale','1.3')
    call('shell','cmd','window','user-rotation','lock','0')
    call('shell','am','start','-W','-n',APP+'/vn.qlkp.companion.MainActivity')
    tap_text(wait_for_text('compact-home','Đọc ảnh và mã QR'),'Đọc ảnh và mã QR')
    tap_text(wait_for_text('compact-scan','QR hồ sơ'),'QR hồ sơ')
    call('shell','cmd','window','user-rotation','lock','1')
    deadline=time.monotonic()+15
    while True:
        rotated=snapshot('landscape-scan')
        if any(n.attrib.get('text')=='QR hồ sơ' and n.attrib.get('checked')=='true' for n in rotated.iter()):
            size=(OUT/'landscape-scan.png').read_bytes()[16:24]
            if int.from_bytes(size[:4],'big')>int.from_bytes(size[4:],'big'):break
        if time.monotonic()>=deadline:raise RuntimeError('Rotation lost the selected scanning mode')
        time.sleep(0.5)
    tap_text(rotated,'Quay lại')
    deadline=time.monotonic()+12
    while True:
        home=snapshot('return-from-scanner')
        if any(n.attrib.get('text')=='QUẢN LÝ KHU PHỐ' for n in home.iter()):break
        if time.monotonic()>=deadline:raise RuntimeError('Could not return to companion home')
        time.sleep(0.5)
    call('shell','cmd','window','user-rotation','lock','0')
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
                if call('shell','getprop','sys.boot_completed',timeout=10).strip()==b'1':
                    release=call('shell','getprop','ro.build.version.release').decode().strip()
                    api=call('shell','getprop','ro.build.version.sdk').decode().strip()
                    abi=call('shell','getprop','ro.product.cpu.abi').decode().strip()
                    report['environment']='Android emulator '+release+' API '+api+' '+abi
                    return
            except (subprocess.CalledProcessError,subprocess.TimeoutExpired):pass
            time.sleep(2)
        raise TimeoutError('Android boot timed out')
    step('EMULATOR_BOOT',boot)
    call('shell','input','keyevent','82')
    step('APK_INSTALL',lambda:call('install','-r',str(APK),timeout=90))
    step('ACTIVITY_LAUNCH_AND_NATIVE_SCREEN',launch)
    step('FILE_CHOOSER_BUTTON',chooser)
    step('COMPACT_LARGE_FONT_ROTATION_AND_BACK',compact_rotate_back)
    step('APK_UNINSTALL',lambda:call('uninstall',APP))
    journal.finish('PASS')
except BaseException:
    journal.finish('FAIL');raise
finally:
    if not journal.closed:journal.finish('INTERRUPTED')
    save()
    if emulator is not None:
        try: call('emu','kill',timeout=10)
        except (subprocess.SubprocessError,OSError):pass
        try:emulator.wait(timeout=10)
        except subprocess.TimeoutExpired:emulator.kill();emulator.wait(timeout=10)
    log.close()
