"""Sign an aligned Android release APK without sending the keystore to GitHub."""
import argparse
import getpass
import os
import pathlib
import re
import subprocess

EXPECTED='490D6E73A8C0826C766BD57E39E779F383E5625C10D8D7B1EF65081A6199C077'
ALIAS='qlkp-release'


def certificate_digest(output):
    match=re.search(r'(?:SHA256:|certificate SHA-256 digest:)\s*([0-9a-fA-F:]{64,95})',output)
    return match.group(1).replace(':','').upper() if match else None


def main():
    parser=argparse.ArgumentParser(description='Ký APK release bằng khóa Quản lý Khu phố đã ấn định.')
    parser.add_argument('--keystore',type=pathlib.Path,required=True)
    parser.add_argument('--input',type=pathlib.Path,required=True)
    parser.add_argument('--output',type=pathlib.Path,required=True)
    parser.add_argument('--apksigner',type=pathlib.Path,required=True)
    args=parser.parse_args()
    if not all(path.is_file() for path in (args.keystore,args.input,args.apksigner)):
        parser.error('Thiếu keystore, APK đã căn chỉnh hoặc apksigner.jar.')
    if args.output.exists() or args.output.resolve()==args.input.resolve():
        parser.error('File kết quả đã tồn tại hoặc trùng APK nguồn; không ghi đè.')
    password=os.environ.get('QLKP_SIGN_STORE_PASSWORD') or getpass.getpass('Mật khẩu keystore: ')
    key_password=os.environ.get('QLKP_SIGN_KEY_PASSWORD') or getpass.getpass('Mật khẩu khóa: ')
    environment={**os.environ,'QLKP_SIGN_STORE_PASSWORD':password,'QLKP_SIGN_KEY_PASSWORD':key_password}
    keytool=['keytool','-list','-v','-keystore',str(args.keystore),'-storetype','JKS',
             '-storepass:env','QLKP_SIGN_STORE_PASSWORD','-alias',ALIAS]
    info=subprocess.run(keytool,env=environment,text=True,capture_output=True,check=True)
    if certificate_digest(info.stdout)!=EXPECTED:
        raise SystemExit('Sai chứng chỉ phát hành; không ký APK bằng khóa khác.')
    command=['java','-jar',str(args.apksigner),'sign','--ks',str(args.keystore),
             '--ks-type','JKS','--ks-key-alias',ALIAS,'--ks-pass','env:QLKP_SIGN_STORE_PASSWORD',
             '--key-pass','env:QLKP_SIGN_KEY_PASSWORD','--out',str(args.output),str(args.input)]
    subprocess.run(command,env=environment,check=True)
    try:
        verified=subprocess.run(['java','-jar',str(args.apksigner),'verify','--verbose','--print-certs',str(args.output)],
                                text=True,capture_output=True,check=True)
        if certificate_digest(verified.stdout)!=EXPECTED or 'Verified using v2 scheme (APK Signature Scheme v2): true' not in verified.stdout:
            raise RuntimeError('Chữ ký APK hoặc chứng chỉ không hợp lệ.')
    except Exception:
        args.output.unlink(missing_ok=True)
        raise
    print('Đã ký và xác minh APK bằng chứng chỉ đã ấn định:',args.output)


if __name__=='__main__':main()
