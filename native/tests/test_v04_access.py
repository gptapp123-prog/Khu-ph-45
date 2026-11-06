"""V0.4 SQL execution + WPF source contracts. Does NOT compile .NET or execute WPF."""
import pathlib, sqlite3, unittest, re, xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[1]
read=lambda f: (ROOT/f).read_text(encoding='utf-8')
schema=read('database/schema.sql')
core=read('src/QLKP.Desktop/Core/DataStore.cs')

class PermissionTest(unittest.TestCase):
    def setUp(self):
        self.c=sqlite3.connect(':memory:')
        self.c.execute('PRAGMA foreign_keys=ON')
        self.c.executescript(schema)
        self.c.execute("INSERT INTO areas(id,code,name,created_utc) VALUES('area','KP01','KP 01','2026-11-05')")
        self.c.execute("INSERT INTO areas(id,code,name,created_utc) VALUES('other','KP02','KP 02','2026-11-05')")
        for id,username,role,area in [('root','r','ROOT_ADMIN',None),('admin','a','KP_ADMIN','area'),('user','u','ASSIGNED_USER','area'),('outsider','o','ASSIGNED_USER','other')]:
            self.c.execute('INSERT INTO accounts(id,username,hash_b64,salt_b64,role,area_id,created_utc) VALUES(?,?,?,?,?,?,?)',(id,username,'hash','salt',role,area,'2026-11-05'))
        for actor,target in [('root','admin'),('admin','user')]:
            self.c.execute("INSERT INTO permissions(account_id,feature,action,area_id,granted_by) VALUES(?,?,?,? ,?)",(target,'B01','read','area',actor))
        self.c.execute("INSERT INTO permissions(account_id,feature,action,area_id,granted_by) VALUES('admin','I01','manage','area','root')")
        self.c.execute("INSERT INTO delegation_ceiling(admin_id,feature,action,area_id) VALUES('admin','B01','read','area')")
        self.c.commit()
    def tearDown(self):self.c.close()
    def test_revoke_delegation_cascades_to_child(self):
        self.c.execute("DELETE FROM delegation_ceiling WHERE admin_id='admin' AND feature IN ('B01','B02')")
        affected=[x[0] for x in self.c.execute("""SELECT DISTINCT p.account_id FROM permissions p WHERE p.granted_by='admin' AND p.feature IN ('B01','B02') AND NOT EXISTS (SELECT 1 FROM delegation_ceiling d WHERE d.admin_id='admin' AND d.area_id=p.area_id AND d.feature=p.feature AND d.action=p.action)""")]
        self.c.execute("""DELETE FROM permissions WHERE granted_by='admin' AND feature IN ('B01','B02') AND NOT EXISTS (SELECT 1 FROM delegation_ceiling d WHERE d.admin_id='admin' AND d.area_id=permissions.area_id AND d.feature=permissions.feature AND d.action=permissions.action)""")
        for a in affected:self.c.execute("UPDATE accounts SET policy_version=policy_version+1 WHERE id=?",(a,))
        self.assertEqual(affected,['user'])
        self.assertEqual(self.c.execute("SELECT COUNT(*) FROM permissions WHERE account_id='user' AND feature='B01'").fetchone()[0],0)
        self.assertEqual(self.c.execute("SELECT policy_version FROM accounts WHERE id='user'").fetchone()[0],2)
        self.assertEqual(self.c.execute("SELECT COUNT(*) FROM permissions WHERE account_id='admin' AND feature='I01'").fetchone()[0],1)
    def test_cannot_cross_area_by_permission_lookup(self):
        query="""SELECT COUNT(*) FROM permissions WHERE account_id=? AND feature=? AND action=? AND area_id=?"""
        self.assertEqual(self.c.execute(query,('user','B01','read','other')).fetchone()[0],0)
        self.assertEqual(self.c.execute(query,('user','B01','read','area')).fetchone()[0],1)
    def test_cannot_grant_nonexistent_area(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.c.execute("INSERT INTO permissions(account_id,feature,action,area_id,granted_by) VALUES('user','B01','create','nonexistent','admin')")
    def test_change_rights_invalidates_policy_version(self):
        old=self.c.execute("SELECT policy_version FROM accounts WHERE id='user'").fetchone()[0]
        self.c.execute("UPDATE accounts SET policy_version=policy_version+1 WHERE id='user'")
        self.assertNotEqual(old,self.c.execute("SELECT policy_version FROM accounts WHERE id='user'").fetchone()[0])
    def test_lockout_counter_and_reset(self):
        self.c.execute("INSERT OR IGNORE INTO login_throttle(account_id,failed_count,updated_utc) VALUES('user',0,'2026-11-05')")
        for i in range(5):
            self.c.execute("UPDATE login_throttle SET failed_count=failed_count+1,blocked_until_utc=CASE WHEN failed_count+1>=5 THEN '2099-01-01T00:00:00Z' ELSE NULL END WHERE account_id='user'")
        attempts,until=self.c.execute("SELECT failed_count,blocked_until_utc FROM login_throttle WHERE account_id='user'").fetchone()
        self.assertEqual(attempts,5);self.assertTrue(until)
        self.c.execute("DELETE FROM login_throttle WHERE account_id='user'")
        self.assertEqual(self.c.execute("SELECT COUNT(*) FROM login_throttle").fetchone()[0],0)
    def test_login_counter_cannot_be_negative(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.c.execute("INSERT INTO login_throttle(account_id,failed_count,updated_utc) VALUES('user',-1,'2026')")
    def test_audit_query_scoped_to_area(self):
        self.c.execute("INSERT INTO audit_log(id,actor_id,event_code,entity_type,entity_id,utc,result) VALUES('a','user','CHANGE_ACCOUNT_RIGHTS','account','user','2026','OK')")
        self.c.execute("INSERT INTO audit_log(id,actor_id,event_code,entity_type,entity_id,utc,result) VALUES('b','outsider','CHANGE_ACCOUNT_RIGHTS','account','outsider','2026','OK')")
        sql="""SELECT l.utc,a.username,l.event_code,l.entity_type,l.result FROM audit_log l JOIN accounts a ON a.id=l.actor_id WHERE (?=1 OR a.area_id=?) ORDER BY l.utc DESC,l.id DESC LIMIT 200"""
        self.assertEqual([x[1] for x in self.c.execute(sql,(0,'area'))],['u'])
        self.assertEqual(set(x[1] for x in self.c.execute(sql,(1,None))),{'u','o'})
    def test_account_status_revoke_increments_policy(self):
        self.c.execute("UPDATE accounts SET active=0,policy_version=policy_version+1 WHERE id='user'")
        self.assertEqual(self.c.execute("SELECT active,policy_version FROM accounts WHERE id='user'").fetchone(),(0,2))

class SourceTest(unittest.TestCase):
    def test_three_tier_account_scope(self):
        self.assertIn('actor.Id==targetId',core)
        self.assertIn('role=="KP_ADMIN"',core)
        self.assertIn('role=="ASSIGNED_USER" && area==actor.AreaId',core)
        self.assertIn('SetAccountRights',core)
        self.assertIn('SetAccountStatus',core)
    def test_no_root_implicit_citizen_read(self):
        s=re.search(r'if\(s.Role=="ROOT_ADMIN"\)return (.*?);',core).group(1)
        self.assertNotIn('B01',s);self.assertNotIn('B02',s)
    def test_new_screens_xaml_valid_and_codebehind(self):
        for name in ['AccessControlView','AuditTrailView']:
            xaml=read(f'src/QLKP.Desktop/Views/{name}.xaml')
            cs=read(f'src/QLKP.Desktop/Views/{name}.xaml.cs')
            ET.fromstring(xaml)
            self.assertIn(f'partial class {name}',cs)
            events=re.findall(r'(?:Click|SelectionChanged)="([A-Za-z_][A-Za-z0-9_]*)"',xaml)
            for event in events:self.assertRegex(cs,r'void\s+'+re.escape(event)+r'\s*\(')
    def test_iam_registered_as_real_navigation(self):
        s=read('src/QLKP.Desktop/Views/MainWindow.xaml.cs')
        for name in ('I01','I02','I03'):
            self.assertIn(f'f.Code=="{name}"',s)
        self.assertIn('new AccessControlView(session)',s)
        self.assertIn('new AuditTrailView(session)',s)
    def test_build_output_is_deterministic(self):
        build=read('build-on-windows.ps1')
        installer=read('installer/QLKP-PILOT.iss')
        self.assertIn('build-output\\Release\\x64',build)
        self.assertIn('OutDir=$outDir',build)
        self.assertIn(r'build-output\Release\x64\*',installer)
        self.assertIn('SmokeTest',build)
    def test_lockout_in_login(self):
        self.assertIn('DateTime.UtcNow.AddMinutes(15)',core)
        self.assertIn('LOGIN_DENIED',core)
        self.assertIn('LOGIN_SUCCESS',core)
        self.assertIn('blocked_until_utc',core)
    def test_permissions_version_on_change(self):
        self.assertIn('UPDATE accounts SET policy_version=policy_version+1 WHERE id=@id',core)
        self.assertIn('affected.Add(r.GetString(0))',core)
    def test_release_channel_remains_explicit(self):
        self.assertIn('PILOT',read('release.properties'))
        self.assertNotIn('chưa mã hóa',read('src/QLKP.Desktop/Views/MainWindow.xaml').lower())
        self.assertIn('Kho dữ liệu mã hóa',read('src/QLKP.Desktop/Views/MainWindow.xaml'))

if __name__=='__main__':unittest.main()
