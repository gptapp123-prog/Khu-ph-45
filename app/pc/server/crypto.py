"""PC-only encrypted portable SQLite vault.

KPHO2 uses an AES-GCM-encrypted SQLite SQL dump, never a plaintext disk DB.
Unlike sqlite3.Connection.serialize/deserialize (Python 3.11+), SQLite's
iterdump/executescript are available on Python 3.8 (Windows 7 branch).
KPHO1 is readable on modern Python only for in-place migration to KPHO2.
"""
from __future__ import annotations
import os
import hashlib
import sqlite3
import tempfile
from pathlib import Path
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.exceptions import InvalidTag

MAGIC_OLD=b'KPHO1\0'
MAGIC=b'KPHO2\0'


def derive_key(passphrase:str,salt:bytes)->bytes:
    if len(passphrase)<12:
        raise ValueError('Mật khẩu kho dữ liệu phải có ít nhất 12 ký tự.')
    return hashlib.scrypt(passphrase.encode('utf-8'),salt=salt,n=2**15,r=8,p=1,
                          dklen=32,maxmem=128*1024*1024)


class Vault:
    def __init__(self,path:Path,passphrase:str):
        self.path=Path(path)
        self.path.parent.mkdir(parents=True,exist_ok=True)
        if self.path.is_symlink():
            raise ValueError('Không cho phép đường dẫn dữ liệu dạng liên kết.')
        self.conn=sqlite3.connect(':memory:',check_same_thread=False)
        self.conn.row_factory=sqlite3.Row
        self._was_legacy=False
        try:
            if self.path.exists():
                raw=self.path.read_bytes()
                header=raw[:len(MAGIC)]
                if header not in (MAGIC,MAGIC_OLD) or len(raw)<len(MAGIC)+16+12+16:
                    raise ValueError('Sai định dạng kho dữ liệu.')
                salt=raw[len(MAGIC):len(MAGIC)+16]
                nonce=raw[len(MAGIC)+16:len(MAGIC)+28]
                self._salt=salt
                self.key=derive_key(passphrase,salt)
                try:
                    plain=AESGCM(self.key).decrypt(nonce,raw[len(MAGIC)+28:],header)
                except InvalidTag as e:
                    raise ValueError('Sai mật khẩu kho dữ liệu hoặc tệp đã bị chỉnh sửa.') from e
                if header==MAGIC_OLD:
                    if not hasattr(self.conn,'deserialize'):
                        raise ValueError('Kho dữ liệu 7.2 cần được chuyển đổi một lần trên PC Windows 10/11 trước khi dùng Windows 7.')
                    self.conn.deserialize(plain)
                    self._was_legacy=True
                else:
                    try:sql=plain.decode('utf-8')
                    except UnicodeDecodeError as e:raise ValueError('Dữ liệu không đúng UTF-8.') from e
                    self.conn.executescript(sql)
            else:
                self._salt=os.urandom(16)
                self.key=derive_key(passphrase,self._salt)
            self.conn.execute('PRAGMA foreign_keys=ON')
            self.conn.execute('PRAGMA secure_delete=ON')
            self.conn.execute('PRAGMA trusted_schema=OFF')
        except Exception:
            self.conn.close()
            raise

    def persist(self):
        if self.path.is_symlink():
            raise ValueError('Không cho phép đường dẫn liên kết.')
        # Durable output is encrypted; SQL dump exists in memory only.
        data='\n'.join(self.conn.iterdump()).encode('utf-8')
        nonce=os.urandom(12)
        blob=MAGIC+self._salt+nonce+AESGCM(self.key).encrypt(nonce,data,MAGIC)
        fd,tmp=tempfile.mkstemp(prefix='.quanlykp-',dir=str(self.path.parent))
        try:
            os.chmod(tmp,0o600)
            with os.fdopen(fd,'wb') as f:
                f.write(blob)
                f.flush()
                os.fsync(f.fileno())
            # atomic replacement; never truncate source before successful write
            os.replace(tmp,str(self.path))
            self._was_legacy=False
        finally:
            if os.path.exists(tmp):os.unlink(tmp)

    def backup(self,backup_dir:Path)->Path:
        backup_dir=Path(backup_dir)
        backup_dir.mkdir(parents=True,exist_ok=True)
        from datetime import datetime,timezone
        stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
        path=backup_dir/('quanlykp_'+stamp+'.kpbak')
        if path.exists():raise ValueError('Bản sao lưu đã tồn tại.')
        self.persist()
        path.write_bytes(self.path.read_bytes())
        os.chmod(path,0o600)
        backups=sorted(backup_dir.glob('quanlykp_*.kpbak'))
        for old in backups[:-3]:old.unlink()
        return path
