"""I04 source contracts and an independent SQLite/AES-GCM reference fixture.
This is NOT C# compiled or WPF E2E verification.
"""
import os, sqlite3, struct, tempfile, unittest
from pathlib import Path
from xml.etree import ElementTree as ET
from hashlib import pbkdf2_hmac
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

ROOT=Path(__file__).resolve().parents[1]
CS=(ROOT/'src/QLKP.Desktop/Core/EncryptedBackup.cs').read_text(encoding='utf-8')
DATA=(ROOT/'src/QLKP.Desktop/Core/DataStore.cs').read_text(encoding='utf-8')
UI=(ROOT/'src/QLKP.Desktop/Views/BackupView.xaml').read_text(encoding='utf-8')
CONTROLLER=(ROOT/'src/QLKP.Desktop/Views/BackupView.xaml.cs').read_text(encoding='utf-8')
APP=(ROOT/'src/QLKP.Desktop/Views/MainWindow.xaml.cs').read_text(encoding='utf-8')
SCHEMA=(ROOT/'database/schema.sql').read_text(encoding='utf-8')


def seal(data,passphrase):
    salt=os.urandom(16);nonce=os.urandom(12)
    header=b'QLKPBK01'+salt+nonce+struct.pack('>I',310000)
    key=pbkdf2_hmac('sha256',passphrase.encode('utf-8'),salt,310000,32)
    return header+AESGCM(key).encrypt(nonce,data,header)


def unseal(data,passphrase):
    assert data[:8]==b'QLKPBK01' and len(data)>56
    assert struct.unpack('>I',data[36:40])[0]==310000
    key=pbkdf2_hmac('sha256',passphrase.encode('utf-8'),data[8:24],310000,32)
    return AESGCM(key).decrypt(data[24:36],data[40:],data[:40])


class SourceContract(unittest.TestCase):
    def test_valid_xaml_and_handlers(self):
        ET.fromstring(UI)
        for handler in ['Backup_Click','ChooseRestore_Click','Validate_Click','Restore_Click']:
            self.assertIn(f'Click="{handler}"',UI)
            self.assertIn('void '+handler,CONTROLLER)

    def test_role_check_at_service_not_only_ui(self):
        self.assertIn('RequireRootMaintenance(admin)',CS)
        self.assertIn('Require(c,null,s,"I04","manage"',DATA)
        self.assertIn('feature=="I04"',DATA)
        self.assertIn('new BackupView(session)',APP)

    def test_crypto_scheme_is_authenticated(self):
        self.assertIn('GcmBlockCipher(',CS)
        self.assertIn('new AeadParameters(new KeyParameter(key),128,header.Skip(24).Take(12).ToArray(),header)',CS)
        self.assertIn('Rfc2898DeriveBytes(phrase,salt,Iterations,HashAlgorithmName.SHA256)',CS)
        self.assertIn('Iterations=310000',CS)
        self.assertIn('"QLKPBK01"',CS)
        self.assertIn('"QLKPBK02"',CS)
        self.assertIn('gcm.DoFinal(output,n)',CS)

    def test_atomic_backup_and_restore_guards(self):
        for phrase in ('VaultStorage.Snapshot(stage,key)', 'File.Move(temp,path)',
                       'File.Replace(stage,DataStore.DatabasePath,null)',
                       'CheckSnapshot(c)', 'File.Exists(DataStore.DatabasePath+"-wal")'):
            self.assertIn(phrase,CS)
        self.assertLess(CS.index('stage=Materialize(path,phrase)'),CS.index('File.Replace(stage,DataStore.DatabasePath,null)'))
        vault=(ROOT/'src/QLKP.Desktop/Core/VaultStorage.cs').read_text()
        self.assertIn('PRAGMA integrity_check',vault)
        self.assertIn('PRAGMA foreign_key_check',vault)
        self.assertNotIn('VACUUM INTO',CS)

    def test_vault_and_backup_disclosures_match_implementation(self):
        self.assertIn('Kho đang dùng và bản sao đều được mã hóa',UI)
        self.assertIn('khóa Windows hiện tại',UI)
        self.assertIn('BouncyCastle.Cryptography', (ROOT/'src/QLKP.Desktop/QLKP.Desktop.csproj').read_text())
        self.assertIn('SQLite3MC.PCLRaw.lib', (ROOT/'src/QLKP.Desktop/QLKP.Desktop.csproj').read_text())
        self.assertIn('BouncyCastle.Cryptography.dll',(ROOT/'build-on-windows.ps1').read_text())

    def test_wpf_syntax_braces_balanced(self):
        # lexical approximation only, not a substitute for csc/MSBuild
        for p in (ROOT/'src/QLKP.Desktop').rglob('*.cs'):
            s=p.read_text(encoding='utf-8')
            depth=0
            # ignore braces inside basic C# quoted strings and line comments
            import re
            # Strip C# char literals, then strings, then comments. XML namespace URLs contain //.
            s=re.sub(r"'(?:\\.|[^'\\])'", "''", s)
            s=re.sub(r'@"(?:[^"]|"")*"','""',s)
            s=re.sub(r'"(?:\\.|[^"\\])*"','""',s)
            s=re.sub(r'//[^\n]*','',s)
            for ch in s:
                if ch=='{':depth+=1
                elif ch=='}':depth-=1
                self.assertGreaterEqual(depth,0,str(p))
            self.assertEqual(depth,0,str(p))


class ReferenceCryptoTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp=tempfile.TemporaryDirectory()
        cls.db=Path(cls.tmp.name)/'data.db'
        con=sqlite3.connect(cls.db)
        con.executescript(SCHEMA)
        con.execute("INSERT INTO areas(id,code,name,created_utc) VALUES('a','KPT','Demo','2026-11-05T00:00:00Z')")
        con.execute("INSERT INTO accounts(id,username,hash_b64,salt_b64,role,created_utc) VALUES('root','owner','TEST','TEST','ROOT_ADMIN','2026-11-05T00:00:00Z')")
        con.execute("INSERT INTO households(id,area_id,code,address,created_utc,updated_utc) VALUES('h1','a','M001','Dia chi demo','t','t')")
        con.commit();con.execute('PRAGMA user_version=1');con.commit();con.close()
        cls.payload=cls.db.read_bytes()
        cls.passphrase='recovery-real-example-do-not-use!'
        cls.cipher=seal(cls.payload,cls.passphrase)

    @classmethod
    def tearDownClass(cls):cls.tmp.cleanup()

    def test_reference_round_trip(self):
        self.assertEqual(self.payload,unseal(self.cipher,self.passphrase))

    def test_reference_wrong_password(self):
        with self.assertRaises(Exception):unseal(self.cipher,'wrong-secret-no-valid-backup')

    def test_reference_tamper_header(self):
        altered=bytearray(self.cipher);altered[10]^=0x11
        with self.assertRaises(Exception):unseal(bytes(altered),self.passphrase)

    def test_reference_tamper_ciphertext(self):
        altered=bytearray(self.cipher);altered[-9]^=0x11
        with self.assertRaises(Exception):unseal(bytes(altered),self.passphrase)

    def test_reference_truncate(self):
        with self.assertRaises(Exception):unseal(self.cipher[:-5],self.passphrase)

    def test_sqlite_snapshot_and_rollback(self):
        snapshot=Path(self.tmp.name)/'snapshot.db'
        c=sqlite3.connect(self.db)
        c.execute('VACUUM INTO ?', (str(snapshot),))
        self.assertEqual(sqlite3.connect(snapshot).execute('PRAGMA integrity_check').fetchone()[0], 'ok')
        c.execute("INSERT INTO households(id,area_id,code,address,created_utc,updated_utc) VALUES('h2','a','M002','new','t','t')")
        c.commit();c.close()
        self.assertEqual(sqlite3.connect(self.db).execute('SELECT count(*) FROM households').fetchone()[0],2)
        stage=Path(self.tmp.name)/'stage.db';stage.write_bytes(snapshot.read_bytes())
        old=Path(self.tmp.name)/'old.db'
        os.replace(self.db,old)
        os.replace(stage,self.db)
        self.assertEqual(sqlite3.connect(self.db).execute('SELECT count(*) FROM households').fetchone()[0],1)


if __name__=='__main__':unittest.main()
