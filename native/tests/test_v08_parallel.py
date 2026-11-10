"""V0.8: SQL execution + source contract tests, NOT a Windows WPF build test."""
import pathlib
import re
import sqlite3
import tempfile
import unittest
import unicodedata
import xml.etree.ElementTree as ET

ROOT=pathlib.Path(__file__).resolve().parents[1]
STORE=(ROOT/'src/QLKP.Desktop/Core/DataStore.cs').read_text(encoding='utf-8')
BACKUP=(ROOT/'src/QLKP.Desktop/Core/EncryptedBackup.cs').read_text(encoding='utf-8')
MAIN=(ROOT/'src/QLKP.Desktop/Views/MainWindow.xaml.cs').read_text(encoding='utf-8')
SCHEMA=(ROOT/'database/schema.sql').read_text(encoding='utf-8')

def normalized(text):
    text=text.replace('đ','d').replace('Đ','D').strip()
    return ''.join(c.lower() for c in unicodedata.normalize('NFD',text) if unicodedata.category(c)!='Mn')

def clip(start,end):
    return STORE[STORE.index(start):STORE.index(end,STORE.index(start)+len(start))]

class SQLV08(unittest.TestCase):
    def setUp(self):
        self.db=sqlite3.connect(':memory:')
        self.db.execute('PRAGMA foreign_keys=ON')
        self.db.executescript(SCHEMA)
        self.db.executemany('INSERT INTO areas(id,code,name,created_utc) VALUES(?,?,?,?)', [('a','A','Khu A','now'),('b','B','Khu B','now')])
        for i,area,code,address in [('h1','a','H01','Đường Nguyễn Huệ'),('h2','a','H02','Đường Lê Lợi'),('h3','b','H03','Đường Nguyễn Huệ')]:
            self.db.execute('INSERT INTO households(id,area_id,code,address,search_address,created_utc,updated_utc) VALUES(?,?,?,?,?,?,?)', (i,area,code,address,normalized(address),'now','now'))
        for i,area,name in [('p1','a','Đặng Minh Anh'),('p2','a','Đặng Minh Anh'),('p3','a','Trần Bích'),('p4','b','Đặng Minh Anh')]:
            self.db.execute('INSERT INTO people(id,area_id,full_name,search_name,created_utc,updated_utc) VALUES(?,?,?,?,?,?)',(i,area,name,normalized(name),'now','now'))
        for i,h,p,role in [('m1','h1','p1','Chủ hộ'),('m2','h2','p2','Con'),('m3','h3','p4','Chủ hộ')]:
            self.db.execute('INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(?,?,?,?,?,?)',(i,h,p,role,'now','now'))
        self.db.execute("INSERT INTO accounts(id,username,hash_b64,salt_b64,role,area_id,created_utc) VALUES('admin','admin','h','s','KP_ADMIN','a','now')")
        self.db.execute("INSERT INTO accounts(id,username,hash_b64,salt_b64,role,area_id,created_utc) VALUES('root','root','h','s','ROOT_ADMIN',NULL,'now')")
    def tearDown(self):self.db.close()

    def test_normalization_vietnamese_and_d_are_equivalent(self):
        for a,b in [('Đường Nguyễn Huệ','duong nguyen hue'),('Đặng Minh Anh','dang minh anh'),('Thành phố Hồ Chí Minh','thanh pho ho chi minh')]:
            self.assertEqual(normalized(a),b)
        self.assertIn("Replace('đ','d').Replace('Đ','D')",STORE)
        self.assertIn('UnicodeCategory.NonSpacingMark',STORE)

    def test_households_unaccented_query_executes_with_real_scope(self):
        sql=re.search(r'const string sql=@"(.*?)";',clip('public static List<HouseholdRow> FindHouseholds','public static HouseholdRow GetHousehold'),re.S).group(1)
        result=self.db.execute(sql,{'a':'a','q':'%duong nguyen hue%'}).fetchall()
        self.assertEqual([r[0] for r in result], ['h1'])

    def test_people_unaccented_query_not_merge_names(self):
        sql=re.search(r'const string sql=@"(.*?)";',clip('public static List<PersonRow> FindPeople','public static List<PersonRow> HouseholdMembers'),re.S).group(1)
        rows=self.db.execute(sql,{'a':'a','q':'%dang minh anh%'}).fetchall()
        self.assertEqual({r[0] for r in rows},{'p1','p2'})
        self.assertEqual({r[6] for r in rows},{'h1','h2'})

    def test_i08_quality_sql_scoped_and_no_automerge(self):
        code=clip('public static List<DataQualityIssue> ListDataQualityIssues','public static List<ResidencyChangeRow> ListPersonResidencyHistory')
        sqls=re.findall(r'new SQLiteCommand\(@"(.*?)",c\)',code,re.S)
        self.assertEqual(len(sqls),3)
        no_head=self.db.execute(sqls[0],{'area':'a'}).fetchall()
        unlinked=self.db.execute(sqls[1],{'area':'a'}).fetchall()
        dupe=self.db.execute(sqls[2],{'area':'a'}).fetchall()
        self.assertEqual([x[0] for x in no_head],['h2'])
        self.assertEqual([x[0] for x in unlinked],['p3'])
        self.assertEqual({x[0] for x in dupe},{'p1','p2'})
        self.assertIn('Require(c,null,actor,"I08","read"',code)
        self.assertNotIn('DELETE FROM people',code)
        self.assertNotIn('UPDATE people',code)

    def test_person_history_exact_id_and_area(self):
        self.db.execute('UPDATE household_memberships SET end_utc=? WHERE id=?',('z','m2'))
        self.db.execute('INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES(?,?,?,?,?,?)',('m4','h1','p2','Con','z','z'))
        self.db.execute('INSERT INTO residency_changes(id,area_id,person_id,from_household_id,to_household_id,old_membership_id,new_membership_id,relationship,reason,search_reason,actor_id,occurred_utc) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',('evt','a','p2','h2','h1','m2','m4','Con','Chuyển hợp lệ',normalized('Chuyển hợp lệ'),'admin','z'))
        code=clip('public static List<ResidencyChangeRow> ListPersonResidencyHistory','public static List<ResidencyChangeRow> ListResidencyChanges')
        sql=re.search(r'new SQLiteCommand\(@"(.*?)",c\)',code,re.S).group(1)
        self.assertEqual(len(self.db.execute(sql,{'area':'a','person':'p2'}).fetchall()),1)
        self.assertEqual(len(self.db.execute(sql,{'area':'a','person':'p1'}).fetchall()),0)
        self.assertEqual(len(self.db.execute(sql,{'area':'b','person':'p2'}).fetchall()),0)
        self.assertIn('Require(c,null,actor,"B02","read"',code)
        self.assertIn('Require(c,null,actor,"B03","read"',code)

    def test_history_cannot_create_invalid_relation(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.db.execute('INSERT INTO residency_changes(id,area_id,person_id,from_household_id,to_household_id,old_membership_id,new_membership_id,relationship,reason,actor_id,occurred_utc) VALUES(?,?,?,?,?,?,?,?,?,?,?)',('bad','a','p4','h2','h1','m2','m1','Con','Sai','admin','now'))

    def test_migration_search_keys_atomic_on_failure(self):
        # SQL migration mirror: rollback restores missing columns and original user_version.
        # Actual C# transaction remains to be executed on Windows.
        db=sqlite3.connect(':memory:');db.execute('CREATE TABLE people(id TEXT PRIMARY KEY,full_name TEXT NOT NULL)')
        db.execute("INSERT INTO people VALUES('p','Nguyễn Văn A')")
        db.execute('PRAGMA user_version=3');db.commit()
        db.execute('BEGIN IMMEDIATE')
        try:
            db.execute("ALTER TABLE people ADD COLUMN search_name TEXT NOT NULL DEFAULT ''")
            db.execute("UPDATE people SET search_name=? WHERE id='p'",(normalized('Nguyễn Văn A'),))
            db.execute('PRAGMA user_version=4')
            raise RuntimeError('simulate disk failure')
        except RuntimeError:db.rollback()
        self.assertEqual(db.execute('PRAGMA user_version').fetchone()[0],3)
        self.assertEqual([c[1] for c in db.execute('PRAGMA table_info(people)').fetchall()],['id','full_name'])
        db.close()

    def test_full_existing_v07_schema_migration_mirror_preserves_records(self):
        # Execute a V0.7 database schema, then the exact required V0.8 ALTER/backfill steps.
        # This is SQLite integration, NOT execution of the C# migration method.
        old_schema=(ROOT/'tests/fixtures/schema_v0_7.sql').read_text(encoding='utf-8')
        con=sqlite3.connect(':memory:')
        con.execute('PRAGMA foreign_keys=ON')
        con.executescript(old_schema)
        con.execute("INSERT INTO areas VALUES('a','A','Khu A','now')")
        con.execute("INSERT INTO households(id,area_id,code,address,created_utc,updated_utc) VALUES('h','a','H01','Đường Nguyễn Huệ','now','now')")
        con.execute("INSERT INTO people(id,area_id,full_name,created_utc,updated_utc) VALUES('p','a','Đặng Văn Minh','now','now')")
        con.execute("INSERT INTO household_memberships(id,household_id,person_id,relationship,start_utc,created_utc) VALUES('m','h','p','Chủ hộ','now','now')")
        con.execute('PRAGMA user_version=3');con.commit()
        con.execute('BEGIN IMMEDIATE')
        for table,col in [('people','search_name'),('households','search_address'),('residency_changes','search_reason')]:
            con.execute(f"ALTER TABLE {table} ADD COLUMN {col} TEXT NOT NULL DEFAULT ''")
            source={'people':'full_name','households':'address','residency_changes':'reason'}[table]
            for ident,raw in con.execute(f'SELECT id,{source} FROM {table}').fetchall():
                con.execute(f'UPDATE {table} SET {col}=? WHERE id=?',(normalized(raw),ident))
        con.execute('CREATE INDEX idx_people_search_area ON people(area_id,search_name)')
        con.execute('CREATE INDEX idx_households_search_area ON households(area_id,search_address)')
        con.execute('CREATE INDEX idx_residency_reason_area ON residency_changes(area_id,search_reason)')
        con.execute('PRAGMA user_version=4')
        con.commit()
        self.assertEqual(con.execute('PRAGMA user_version').fetchone()[0],4)
        self.assertEqual(con.execute("SELECT search_name FROM people WHERE id='p'").fetchone()[0], 'dang van minh')
        self.assertEqual(con.execute("SELECT search_address FROM households WHERE id='h'").fetchone()[0], 'duong nguyen hue')
        self.assertEqual(con.execute("SELECT COUNT(*) FROM household_memberships WHERE person_id='p'").fetchone()[0],1)
        self.assertEqual(con.execute('PRAGMA foreign_key_check').fetchall(),[])
        con.close()

    def test_schema_allows_no_automatic_merge(self):
        self.assertEqual(self.db.execute('SELECT COUNT(*) FROM people WHERE search_name=? AND area_id=?',('dang minh anh','a')).fetchone()[0],2)
        self.assertEqual(self.db.execute('PRAGMA foreign_key_check').fetchall(),[])

class SecurityAndPackaging(unittest.TestCase):
    def test_backup_creates_encrypted_rollback_before_atomic_replace(self):
        self.assertIn('VaultStorage.Snapshot(rollback,key)',BACKUP)
        self.assertIn('File.Replace(stage,DataStore.DatabasePath,null)',BACKUP)
        self.assertIn('File.Replace(repair,DataStore.DatabasePath,null)',BACKUP)
        self.assertNotIn('File.Replace(stage,DataStore.DatabasePath,rollback)',BACKUP)
        self.assertLess(BACKUP.index('VaultStorage.Snapshot(rollback,key)'),BACKUP.index('File.Replace(stage,DataStore.DatabasePath,null)'))

    def test_backup_refuses_foreign_key_corruption(self):
        vault=(ROOT/'src/QLKP.Desktop/Core/VaultStorage.cs').read_text()
        self.assertIn('VaultStorage.Check(c)',BACKUP)
        self.assertIn('PRAGMA foreign_key_check',vault)
        self.assertIn('PRAGMA integrity_check',vault)
        # SQL itself detects dangling FK when integrity_check alone can be 'ok'.
        con=sqlite3.connect(':memory:')
        con.execute('PRAGMA foreign_keys=OFF');con.execute('CREATE TABLE parent(id INTEGER PRIMARY KEY)');con.execute('CREATE TABLE child(parent_id INTEGER REFERENCES parent(id))');con.execute('INSERT INTO child VALUES (99)')
        self.assertEqual(con.execute('PRAGMA integrity_check').fetchone()[0],'ok')
        self.assertNotEqual(con.execute('PRAGMA foreign_key_check').fetchall(),[])
        con.close()

    def test_data_quality_navigation_protects_target_by_id(self):
        x=(ROOT/'src/QLKP.Desktop/Views/DataQualityView.xaml.cs').read_text(encoding='utf-8')
        for phrase in ['DataStore.GetHousehold(session,row.TargetId)','DataStore.GetPerson(session,row.TargetId)', 'DataStore.Can(session,"B01","read")', 'DataStore.Can(session,"B02","read")']:
            self.assertIn(phrase,x)
        self.assertIn('new DataQualityView(session,NavigateToHousehold,NavigateToPerson)',MAIN)

    def test_i08_cannot_leak_rows_without_source_module_read_right(self):
        code=clip('public static List<DataQualityIssue> ListDataQualityIssues','public static List<ResidencyChangeRow> ListPersonResidencyHistory')
        for token in ['bool canReadHouseholds=HasPermission(c,null,actor,"B01","read"',
                      'bool canReadPeople=HasPermission(c,null,actor,"B02","read"',
                      'if(canReadHouseholds)', 'if(canReadPeople)']:
            self.assertIn(token,code)
        self.assertIn('Require(c,null,actor,"I08","read"',code)

    def test_i08_default_read_is_scoped_and_checked_at_service_and_ui(self):
        policy=(ROOT/'src/QLKP.Desktop/Core/BusinessDefaults.cs').read_text(encoding='utf-8')
        defaults, grant=policy.split('public static List<string> BusinessDefaultRights()',1)[1].split('static void GrantBusinessDefaults(',1)
        self.assertIn('"I08:read"',defaults)
        self.assertIn('foreach(string right in ceiling?BusinessDefaultRights():OrdinaryAccountDefaults())',grant)
        self.assertIn('Grant(c,tx,id,p[0],p[1],area,root)',grant)
        root_create=clip('if(actor.Role=="ROOT_ADMIN") {','} else if(actor.Role=="KP_ADMIN")')
        self.assertIn('GrantBusinessDefaults(c,tx,target,area,actor.Id,true)',root_create)
        root_rule=clip('if(s.Role=="ROOT_ADMIN") {','if(feature=="I04"')
        before_scope,after_scope=root_rule.split('areaId!=s.AreaId',1)
        self.assertNotIn('"I08"',before_scope)
        self.assertIn('BusinessDefaultRights().Contains(feature+":"+action)',after_scope)
        service=clip('public static List<DataQualityIssue> ListDataQualityIssues(', 'var result=new List<DataQualityIssue>();')
        self.assertIn('Require(c,null,actor,"I08","read",actor==null?null:actor.AreaId)',service)
        self.assertIn('DataStore.Can(session,"I08","read")?new DataQualityView(session,NavigateToHousehold,NavigateToPerson)',MAIN)

    def test_schema_v4_and_existing_migration_contract(self):
        self.assertIn('if(installed>SchemaVersion)',STORE)
        self.assertIn('PRAGMA user_version=',STORE)
        self.assertIn('EnsureSearchColumn(c,tx,"people","search_name")',STORE)
        self.assertIn('EnsureSearchColumn(c,tx,"households","search_address")',STORE)
        self.assertIn('EnsureSearchColumn(c,tx,"residency_changes","search_reason")',STORE)
        self.assertIn('BackfillSearchKeys(c,tx)',STORE)
        self.assertIn('using(var tx=c.BeginTransaction())',STORE)

    def test_xaml_and_ui_event_handlers_including_new_screen(self):
        import re
        for f in (ROOT/'src/QLKP.Desktop/Views').glob('*.xaml'):
            tree=ET.parse(f);code=f.with_suffix('.xaml.cs').read_text(encoding='utf-8')
            for elem in tree.iter():
                for attr in ['Click','TextChanged','SelectionChanged']:
                    if attr in elem.attrib:
                        self.assertRegex(code,r'\bvoid\s+'+re.escape(elem.attrib[attr])+r'\s*\(')

if __name__=='__main__': unittest.main()
