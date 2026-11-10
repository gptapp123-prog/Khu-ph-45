"""V0.19 focused regressions. SQLite executes; WPF source invariants are static until Windows compile."""
from hashlib import sha256
from pathlib import Path
import lzma, re, shutil, sqlite3, tempfile, unittest
from xml.etree import ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
def read(path):return (ROOT/path).read_text(encoding='utf-8')

class V019GuardrailTests(unittest.TestCase):
    def test_01_report_aggregate_small_counts_are_masked_in_ui(self):
        src=read('src/QLKP.Desktop/Core/ReportStore.cs')
        xaml=read('src/QLKP.Desktop/Views/ReportsView.xaml')
        self.assertIn('public string CountDisplay',src)
        self.assertIn('String.IsNullOrEmpty(RecordId) && Count>0 && Count<5',src)
        self.assertIn('"Ẩn (1–4)"',src)
        self.assertIn('Binding="{Binding CountDisplay}"',xaml)
        self.assertNotIn('Binding="{Binding Count}"',xaml)
    def test_02_real_detail_rows_keep_identifier_and_count(self):
        src=read('src/QLKP.Desktop/Core/ReportStore.cs')
        # No replacement with a masked/sanitized record ID; detail reads remain independently authorized.
        self.assertIn('Feature=r.GetString(0),RecordId=',src)
        self.assertIn('return Count.ToString(CultureInfo.InvariantCulture);',src)
        self.assertIn('Require(c,tx,s,reportFeature,"read"',src)
    def test_03_export_also_masks_low_counts(self):
        src=read('src/QLKP.Desktop/Core/ReportStore.cs')
        self.assertIn('long count=r.GetInt64(2)',src)
        self.assertIn('count<5?(object)"Ẩn (1–4)"',src)
        self.assertEqual(src.count("AggregateRows(s,from,to,sourceFilter,statusFilter)"),2)
        self.assertIn('Require(c,tx,s,"H05","export"',src)
    def test_04_windows_runtime_selftest_checks_privacy(self):
        src=read('src/QLKP.Desktop/Core/SelfTest.cs')
        self.assertIn('new ReportLine{Feature="H03",RecordId="",Count=2}.CountDisplay',src)
        self.assertIn('new ReportLine{Feature="H03",RecordId="",Count=5}.CountDisplay',src)
        self.assertIn('new ReportLine{Feature="H02",RecordId="valid-id",Count=1}.CountDisplay',src)
    def test_05_no_automatic_deletion_of_leftover_plaintext(self):
        code=read('src/QLKP.Desktop/Core/StartupDiagnostics.cs')
        function=code.split('public static void CheckPlaintextMaintenanceArtifacts()')[1].split('public static void CheckDatabase(')[0]
        self.assertIn('Directory.GetFiles(',function)
        self.assertIn('throw new InvalidDataException',function)
        self.assertNotIn('File.Delete(',function)
        self.assertNotIn('Directory.Delete(',function)
    def test_06_guard_covers_root_and_maintenance_sqlite_journals(self):
        code=read('src/QLKP.Desktop/Core/StartupDiagnostics.cs')
        for pat in ('qlkp-restore-stage-*.db','qlkp-rollback-stage-*.db',
                    'qlkp-*.db','qlkp-*.db-journal','qlkp-*.db-wal','qlkp-*.db-shm'):
            self.assertIn(pat,code)
    def test_07_startup_checks_before_initializing_db(self):
        code=read('src/QLKP.Desktop/App.xaml.cs')
        self.assertLess(code.index('StartupDiagnostics.CheckPlaintextMaintenanceArtifacts();'),code.index('DataStore.Initialize();'))
        self.assertIn('StartupDiagnostics.CheckApplicationFiles();',code)
    def test_08_plaintext_stage_cleanup_failures_do_not_silently_pass(self):
        code=read('src/QLKP.Desktop/Core/EncryptedBackup.cs')
        self.assertIn('new SqliteMemoryImage(p.Data)',code)
        self.assertNotIn('VACUUM INTO',code)
        self.assertIn('if(File.Exists(temp))File.Delete(temp);',code)
        self.assertIn('if(File.Exists(stage))File.Delete(stage);',code)
        self.assertNotIn('catch{}',code)
    def test_09_windows_build_and_setup_version_match(self):
        csproj=read('src/QLKP.Desktop/QLKP.Desktop.csproj')
        ps=read('build-on-windows.ps1')
        iss=read('installer/QLKP-PILOT.iss')
        cmd=read('BUILD_SETUP_1_CLICK.cmd')
        versions = dict(line.split('=', 1) for line in read('release.properties').splitlines() if '=' in line)
        name = versions['desktopVersionName']
        assembly = versions['desktopAssemblyVersion']
        self.assertIn(f'<Version>{assembly}</Version>', csproj)
        self.assertIn(f'<FileVersion>{assembly}</FileVersion>', csproj)
        self.assertIn('V' + name.split('-')[0], ps)
        self.assertIn(f'#define MyAppVersion "{name}"', iss)
        self.assertIn('V' + name.split('-')[0], cmd)
        self.assertIn(f"-ne '{assembly}'", read('test-windows-install.ps1'))
    def test_10_build_requires_real_wpf_selftest_before_installer(self):
        ps=read('build-on-windows.ps1')
        self.assertIn('WaitForExit(120000)',ps)
        self.assertIn('if($check.ExitCode -ne 0)',ps)
        self.assertLess(ps.index('if($check.ExitCode -ne 0)'),ps.index("& $isccPath 'installer\\QLKP-PILOT.iss'"))
        self.assertIn('System.Data.SQLite.dll',ps)
    def test_11_xaml_parses_after_binding_update(self):
        for path in ROOT.glob('src/**/*.xaml'):ET.parse(path)
    def test_12_real_fixture_has_zero_foreign_key_violations(self):
        fixture=ROOT/'tests/fixtures/qlkp_demo_fictional_v016_G.sqlite.xz'
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'fixture.sqlite'
            with lzma.open(fixture,'rb') as source, path.open('wb') as target:
                shutil.copyfileobj(source,target)
            self.assertEqual(sha256(path.read_bytes()).hexdigest(),
                'e59114aba8fae1b7526854bac2c8e3bb73df8e1f3066e842dcbabdc5d53a783e')
            con=sqlite3.connect(path.as_uri()+'?mode=ro',uri=True)
            try:
                self.assertEqual(con.execute('PRAGMA quick_check').fetchone()[0],'ok')
                self.assertEqual(con.execute('PRAGMA foreign_key_check').fetchall(),[])
                self.assertEqual(con.execute('SELECT COUNT(*) FROM households').fetchone()[0],2120)
                self.assertEqual(con.execute('SELECT COUNT(*) FROM people').fetchone()[0],9540)
                self.assertEqual(con.execute('SELECT COUNT(*) FROM module_records').fetchone()[0],24050)
            finally:con.close()
    def test_13_group_privacy_low_count_1_to_4_is_nonempty(self):
        # Real SQLite query determines threshold cases; C# masking is validated structurally above.
        con=sqlite3.connect(':memory:')
        try:
            con.execute('CREATE TABLE cases(code TEXT, status TEXT)')
            con.executemany('INSERT INTO cases VALUES(?,?)',[('H01','A')]*1+[('H01','B')]*2+[('H01','C')]*3+[('H01','D')]*4+[('H01','E')]*5)
            rows=con.execute('SELECT status,COUNT(*) FROM cases GROUP BY status ORDER BY status').fetchall()
            self.assertEqual(rows,[('A',1),('B',2),('C',3),('D',4),('E',5)])
            masked=[('Ẩn (1–4)' if 0<n<5 else str(n)) for _,n in rows]
            self.assertEqual(masked,['Ẩn (1–4)']*4+['5'])
        finally:con.close()
    def test_14_windows_selftest_detects_maintenance_artifact(self):
        txt=read('src/QLKP.Desktop/Core/SelfTest.cs')
        for phrase in ('qlkp-plaintext-check.db','CheckPlaintextMaintenanceArtifacts();',
                       'leftoversBlocked','File.Delete(leftover)'):
            self.assertIn(phrase,txt)
    def test_15_main_store_uses_encryption_runtime(self):
        src=read('src/QLKP.Desktop/Core/EncryptedBackup.cs')
        self.assertIn('VaultStorage.Snapshot(stage,key)',src)
        store=read('src/QLKP.Desktop/Core/DataStore.cs')
        self.assertIn('VaultStorage.OpenCurrent()',store)
        self.assertIn('VaultStorage.EnsureCurrent()',store)

if __name__=='__main__': unittest.main()
