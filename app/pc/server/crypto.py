"""PC-only encrypted portable SQLite vault.

KPHO2 uses an AES-GCM-encrypted SQLite SQL dump, never a plaintext disk DB.
Unlike sqlite3.Connection.serialize/deserialize (Python 3.11+), SQLite's
iterdump/executescript are available on Python 3.8 (Windows 7 branch).
KPHO1 is readable on modern Python only for in-place migration to KPHO2.
"""
from __future__ import annotations
import os
import hashlib
import hmac
import sqlite3
import tempfile
from datetime import datetime,timezone
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
            # Atomic replacement; never truncate source before successful write.
            self._replace_and_sync(tmp,self.path)
            self._was_legacy=False
        finally:
            if os.path.exists(tmp):os.unlink(tmp)


    @staticmethod
    def _replace_and_sync(source:str,destination:Path):
        """Replace atomically, then best-effort sync the containing directory.

        The file contents are fsynced by the caller before replacement. POSIX
        filesystems additionally need the directory entry synced for the rename
        to survive abrupt power loss. Windows does not expose directory fsync
        through the same API, so that step is intentionally best-effort there.
        """
        destination=Path(destination)
        os.replace(source,str(destination))
        directory_fd=None
        try:
            flags=os.O_RDONLY|getattr(os,'O_DIRECTORY',0)
            directory_fd=os.open(str(destination.parent),flags)
            os.fsync(directory_fd)
        except (OSError,AttributeError):
            pass
        finally:
            if directory_fd is not None:os.close(directory_fd)

    @staticmethod
    def _atomic_write(path:Path,data:bytes):
        path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
        if path.is_symlink():raise ValueError('Không cho phép đường dẫn dạng liên kết.')
        fd,tmp=tempfile.mkstemp(prefix='.quanlykp-',dir=str(path.parent))
        try:
            os.chmod(tmp,0o600)
            with os.fdopen(fd,'wb') as f:f.write(data);f.flush();os.fsync(f.fileno())
            Vault._replace_and_sync(tmp,path)
        finally:
            if os.path.exists(tmp):os.unlink(tmp)

    def _validate_blob(self,raw:bytes)->str:
        if len(raw)<len(MAGIC)+44 or raw[:len(MAGIC)]!=MAGIC:raise ValueError('Bản sao lưu không đúng định dạng KPHO2.')
        salt=raw[len(MAGIC):len(MAGIC)+16]
        if not hmac.compare_digest(salt,self._salt):raise ValueError('Bản sao lưu không thuộc kho dữ liệu đang mở.')
        nonce=raw[len(MAGIC)+16:len(MAGIC)+28]
        try:plain=AESGCM(self.key).decrypt(nonce,raw[len(MAGIC)+28:],MAGIC)
        except InvalidTag as e:raise ValueError('Bản sao lưu sai khóa hoặc đã bị chỉnh sửa.') from e
        try:sql=plain.decode('utf-8')
        except UnicodeDecodeError as e:raise ValueError('Bản sao lưu không chứa dữ liệu UTF-8 hợp lệ.') from e
        check=sqlite3.connect(':memory:')
        try:
            check.executescript(sql);result=check.execute('PRAGMA integrity_check').fetchone()
            if not result or result[0]!='ok':raise ValueError('Kiểm tra toàn vẹn bản sao lưu không đạt.')
        except sqlite3.Error as e:raise ValueError('Cấu trúc bản sao lưu không hợp lệ.') from e
        finally:check.close()
        return sql

    def backup(self,backup_dir:Path)->Path:
        backup_dir=Path(backup_dir)
        backup_dir.mkdir(parents=True,exist_ok=True)
        stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
        path=backup_dir/('quanlykp_'+stamp+'.kpbak')
        if path.exists():raise ValueError('Bản sao lưu đã tồn tại.')
        checksum_path=Path(str(path)+'.sha256')
        self.persist()
        raw=self.path.read_bytes();self._validate_blob(raw)
        try:
            self._atomic_write(path,raw)
            digest=hashlib.sha256(raw).hexdigest()
            self._atomic_write(checksum_path,(digest+'  '+path.name+'\\n').encode('ascii'))
            if not hmac.compare_digest(hashlib.sha256(path.read_bytes()).hexdigest(),digest):
                raise ValueError('Đọc lại bản sao lưu không khớp checksum.')
        except Exception:
            # Never leave an apparently usable backup without its verified pair.
            if path.exists():path.unlink()
            if checksum_path.exists():checksum_path.unlink()
            raise
        backups=sorted(backup_dir.glob('quanlykp_*.kpbak'))
        for old in backups[:-3]:
            old.unlink();sidecar=Path(str(old)+'.sha256')
            if sidecar.exists():sidecar.unlink()
        return path

    def restore(self,backup_path:Path,rollback_dir:Path)->Path:
        backup_path=Path(backup_path)
        if backup_path.is_symlink() or not backup_path.is_file():raise ValueError('Không tìm thấy bản sao lưu hợp lệ.')
        raw=backup_path.read_bytes();checksum_path=Path(str(backup_path)+'.sha256')
        if not checksum_path.is_file():raise ValueError('Thiếu tệp checksum của bản sao lưu.')
        expected=checksum_path.read_text(encoding='ascii').split()[0].lower();actual=hashlib.sha256(raw).hexdigest()
        if len(expected)!=64 or not hmac.compare_digest(expected,actual):raise ValueError('Checksum bản sao lưu không khớp.')
        sql=self._validate_blob(raw);rollback=self.backup(Path(rollback_dir));self._atomic_write(self.path,raw)
        replacement=sqlite3.connect(':memory:',check_same_thread=False);replacement.row_factory=sqlite3.Row
        try:
            replacement.executescript(sql);replacement.execute('PRAGMA foreign_keys=ON');replacement.execute('PRAGMA secure_delete=ON');replacement.execute('PRAGMA trusted_schema=OFF')
        except Exception:
            replacement.close();self._atomic_write(self.path,rollback.read_bytes());raise
        old=self.conn;self.conn=replacement;old.close();self._was_legacy=False
        return rollback
