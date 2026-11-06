"""Verify the delivered release, its identity and equality to the release built from this source."""
import hashlib,json,os,re,subprocess,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
STAGING=ROOT/'release-staging'
APK=STAGING/'QLKP-Mobile-0.20.0-PILOT.apk'
EXPECTED=json.loads((STAGING/'android-signing-receipt.json').read_text())
OUT=ROOT/'build-logs/android-release';OUT.mkdir(parents=True,exist_ok=True)
SDK=Path(os.environ.get('ANDROID_HOME') or os.environ['ANDROID_SDK_ROOT'])/'build-tools/35.0.0'
digest=hashlib.sha256(APK.read_bytes()).hexdigest()
if digest!=EXPECTED['apk_sha256']:raise ValueError('Release APK hash mismatch')
signature=subprocess.run([str(SDK/'apksigner'),'verify','--verbose','--print-certs',str(APK)],check=True,capture_output=True,text=True).stdout
(OUT/'signature.txt').write_text(signature)
if 'certificate SHA-256 digest: '+EXPECTED['certificate_sha256'] not in signature:raise ValueError('Signing certificate mismatch')
metadata=subprocess.run([str(SDK/'aapt'),'dump','badging',str(APK)],check=True,capture_output=True,text=True).stdout
(OUT/'apk-metadata.txt').write_text(metadata)
if "name='vn.qlkp.companion' versionCode='20' versionName='0.20.0-PILOT'" not in metadata:raise ValueError('APK identity/version mismatch')
if 'application-debuggable' in metadata:raise ValueError('Release APK must not be debuggable')
fresh=ROOT/'android-companion/app/build/outputs/apk/release/app-release-unsigned.apk'
with zipfile.ZipFile(fresh) as source,zipfile.ZipFile(APK) as signed:
    if source.testzip() is not None or signed.testzip() is not None:raise ValueError('Invalid APK ZIP CRC')
    for name in source.namelist():
        if source.read(name)!=signed.read(name):raise ValueError('Signed release differs from compiled source: '+name)
    extras=set(signed.namelist())-set(source.namelist())
    if any(not x.startswith('META-INF/') for x in extras):raise ValueError('Unexpected release content')
report=dict(format='QLKP_SIGNED_RELEASE_VERIFICATION_V1',status='PASS',apk_sha256=digest,certificate_sha256=EXPECTED['certificate_sha256'],compiled_content_match=True,debuggable=False,application_id=EXPECTED['application_id'],version_name=EXPECTED['version_name'])
(OUT/'release-verification.json').write_text(json.dumps(report,indent=2))
print('SIGNED_RELEASE_SIGNATURE_IDENTITY_AND_COMPILED_CONTENT_PASS')
