"""Windows OS/runtime selection; pure Python 3.8 syntax with no dependencies.

This checks documented prerequisites; it does not prove the whole program has
been installed/tested on a real Windows 7 PC.
"""
import argparse
import os
import sys


def assess(platform_name=None, windows_version=None, python_version=None, service_pack=None):
    platform_name=platform_name if platform_name is not None else os.name
    python_version=python_version if python_version is not None else sys.version_info[:2]
    if platform_name!='nt':
        return {'ok':True,'track':'development','requirements':'requirements.txt',
                'message':'Chỉ đánh giá cú pháp trên hệ không phải Windows.'}
    windows_version=windows_version if windows_version is not None else sys.getwindowsversion()[:2]
    major,minor=windows_version[:2]
    if service_pack is None:
        if os.name=='nt':
            service_pack=getattr(sys.getwindowsversion(),'service_pack_major',0)
        else:
            service_pack=1  # simulated platform for unit tests
    if (major,minor)==(6,1) and service_pack<1:
        return {'ok':False,'track':'unsupported','requirements':'',
                'message':'Windows 7 phải có Service Pack 1 (SP1).'}
    if (major,minor)<(6,1):
        return {'ok':False,'track':'unsupported','requirements':'',
                'message':'Chỉ hỗ trợ từ Windows 7 SP1 (NT 6.1) trở lên.'}
    if (major,minor)<(6,3):
        legacy=(3,8)<=tuple(python_version)<(3,9)
        return {'ok':legacy,'track':'legacy','requirements':'requirements-win7-legacy.txt',
                'message':('Yêu cầu Python 3.8 trên Windows 7/8. '
                           'Đây là môi trường hết bảo trì; không dùng dữ liệu dân cư thật khi chưa nghiệm thu và cô lập mạng.')}
    modern=tuple(python_version)>=(3,11) and tuple(python_version)<(3,16)
    return {'ok':modern,'track':'modern','requirements':'requirements.txt',
            'message':'Yêu cầu Python 3.11 đến 3.15 và Windows 8.1 trở lên.'}


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--requirements',action='store_true')
    args=parser.parse_args()
    x=assess()
    if not x['ok']:
        print('KHONG TUONG THICH: '+x['message'],file=sys.stderr)
        return 3
    if args.requirements:print(x['requirements'])
    else:print('Nhánh: '+x['track']+'; '+x['message'])
    return 0

if __name__=='__main__':sys.exit(main())
