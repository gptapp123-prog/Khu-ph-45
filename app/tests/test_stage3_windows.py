"""Giai đoạn 3: phiên bản Windows, di trú kho cũ, Unicode và mã hóa."""
import ast
import hashlib
import os
from pathlib import Path
import sqlite3
import pytest
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from server.crypto import Vault,derive_key,MAGIC,MAGIC_OLD
from win_compat import assess

ROOT=Path(__file__).resolve().parents[1]
PASS='MatKhauKho-DuLieu#2026'


def test_os_tracks_and_mismatched_python():
    def check(win,py):return assess('nt',win,py)
    assert check((6,0),(3,8))['ok'] is False  # Vista: unsupported
    assert check((6,1),(3,8))['ok'] is True
    assert assess('nt',(6,1),(3,8),service_pack=0)['ok'] is False
    assert check((6,1),(3,11))['ok'] is False
    assert check((6,2),(3,8))['requirements']=='requirements-win7-legacy.txt'
    assert check((6,3),(3,11))['ok'] is True
    assert check((10,0),(3,13))['requirements']=='requirements.txt'
    assert check((10,0),(3,8))['ok'] is False
    assert check((10,0),(3,16))['ok'] is False


def test_python38_source_syntax_compatibility():
    for path in list((ROOT/'pc').rglob('*.py')):
        ast.parse(path.read_text(encoding='utf8'),filename=str(path),feature_version=(3,8))


def test_portable_vault_v2_encrypted_reopen_unicode_and_tamper(tmp_path):
    path=tmp_path/'chinh.vault'
    vault=Vault(path,PASS)
    vault.conn.execute('CREATE TABLE nhan_khau(id INTEGER PRIMARY KEY, ten TEXT)')
    vault.conn.execute('INSERT INTO nhan_khau(ten) VALUES(?)',('Nguyễn Thị Ánh – Tổ 5',))
    vault.conn.commit()
    vault.persist()
    contents=path.read_bytes()
    assert contents.startswith(MAGIC)
    assert b'Nguy' not in contents and b'CREATE TABLE' not in contents
    assert not list(tmp_path.glob('*.sqlite'))
    vault.conn.close()
    reread=Vault(path,PASS)
    assert reread.conn.execute('SELECT ten FROM nhan_khau').fetchone()[0]=='Nguyễn Thị Ánh – Tổ 5'
    reread.conn.close()
    with pytest.raises(ValueError):Vault(path,'SaiMatKhauDuLieu#2026')
    bad=bytearray(contents);bad[-1]^=0x20
    path.write_bytes(bad)
    with pytest.raises(ValueError):Vault(path,PASS)


def test_old_v1_vault_in_place_migration_no_lost_records(tmp_path):
    path=tmp_path/'cu.vault'
    old=sqlite3.connect(':memory:')
    old.execute('CREATE TABLE doi_chieu(id TEXT PRIMARY KEY, ten TEXT)')
    old.execute('INSERT INTO doi_chieu VALUES(?,?)',('KH.0001','Đỗ Thị Thúy'))
    old.commit()
    salt=os.urandom(16);nonce=os.urandom(12)
    data=MAGIC_OLD+salt+nonce+AESGCM(derive_key(PASS,salt)).encrypt(nonce,old.serialize(),MAGIC_OLD)
    path.write_bytes(data);old.close()
    upgraded=Vault(path,PASS)
    assert upgraded._was_legacy is True
    assert upgraded.conn.execute('SELECT ten FROM doi_chieu').fetchone()[0]=='Đỗ Thị Thúy'
    upgraded.persist()
    assert path.read_bytes().startswith(MAGIC)
    upgraded.conn.close()
    restored=Vault(path,PASS)
    assert restored.conn.execute('SELECT COUNT(*) FROM doi_chieu').fetchone()[0]==1
    restored.conn.close()


def test_installer_and_runtime_pin_have_both_tracks():
    installer=(ROOT/'Cai_dat_PC.bat').read_text(encoding='utf8')
    assert '--requirements' in installer
    assert 'win_compat.py' in installer
    assert 'requirements-win7-legacy.txt' in installer
    assert '.venv' in installer
    legacy=(ROOT/'requirements-win7-legacy.txt').read_text(encoding='utf8')
    assert 'Python 3.8' in legacy
    assert 'fastapi==' in legacy


def test_pydantic_v1_v2_compat_helpers():
    # Exercise the API surface used in Python 3.8's Pydantic v1 dependency lane.
    from pydantic.v1 import BaseModel as LegacyModel
    from server.model_compat import fields_set,copy_with,declared_fields,validate_model
    class SimpleOld(LegacyModel):
        ho_ten:str
        to:int=1
    record=validate_model(SimpleOld,{'ho_ten':'Lê Thị Bé'})
    assert fields_set(record)=={'ho_ten'}
    assert declared_fields(SimpleOld)=={'ho_ten','to'}
    assert copy_with(record,{'to':4}).to==4
    assert record.to==1


def test_backup_checksum_restore_and_rollback(tmp_path):
    path=tmp_path/'primary.vault';vault=Vault(path,PASS)
    vault.conn.execute('CREATE TABLE records(id INTEGER PRIMARY KEY, value TEXT)');vault.conn.execute('INSERT INTO records(value) VALUES(?)',('bản gốc',));vault.conn.commit();vault.persist()
    backup=vault.backup(tmp_path/'backups');assert Path(str(backup)+'.sha256').is_file()
    vault.conn.execute('UPDATE records SET value=?',('đã thay đổi',));vault.conn.commit();vault.persist()
    rollback=vault.restore(backup,tmp_path/'rollbacks')
    assert vault.conn.execute('SELECT value FROM records').fetchone()[0]=='bản gốc'
    changed=Vault(rollback,PASS);assert changed.conn.execute('SELECT value FROM records').fetchone()[0]=='đã thay đổi';changed.conn.close();vault.conn.close()

def test_restore_rejects_bad_checksum_without_changing_data(tmp_path):
    path=tmp_path/'primary.vault';vault=Vault(path,PASS)
    vault.conn.execute('CREATE TABLE records(value TEXT)');vault.conn.execute('INSERT INTO records VALUES(?)',('an toàn',));vault.conn.commit();vault.persist()
    backup=vault.backup(tmp_path/'backups');original=path.read_bytes();Path(str(backup)+'.sha256').write_text('0'*64+'  '+backup.name+'\\n',encoding='ascii')
    with pytest.raises(ValueError,match='Checksum'):vault.restore(backup,tmp_path/'rollbacks')
    assert path.read_bytes()==original;vault.conn.close()
