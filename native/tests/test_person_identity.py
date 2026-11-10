from pathlib import Path
import re
import sqlite3
import unittest
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / "src/QLKP.Desktop"
SCHEMA = (ROOT / "database/schema.sql").read_text(encoding="utf-8")
LOOKUP = re.search(r'Scalar\(c,tx,@"(SELECT i\.person_id.*?)",', (CODE / "Core/PersonIdentity.cs").read_text(encoding="utf-8"), re.S)[1]


class PersonIdentityTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        self.db.executescript(SCHEMA)
        self.db.executescript("""
            INSERT INTO areas VALUES('a','A','Khu A','2026'),('b','B','Khu B','2026');
            INSERT INTO accounts(id,username,hash_b64,salt_b64,role,created_utc)
              VALUES('u','qa','fake','fake','ROOT_ADMIN','2026');
            INSERT INTO people(id,area_id,full_name,created_utc,updated_utc)
              VALUES('p','a','QA','2026','2026'),('q','a','QA trùng tên','2026','2026'),('r','b','QA khác','2026','2026');
        """)

    def put(self, person, area, number):
        self.db.execute("INSERT INTO person_identity_numbers VALUES(?,?,?,'u','2026')", (person, area, number))

    def test_exact_lookup_preserves_leading_zero_and_scopes_area(self):
        self.put("p", "a", "000000123456")
        for area, number, want in [("a", "000000123456", [("p",)]), ("b", "000000123456", []), ("a", "123456", [])]:
            self.assertEqual(self.db.execute(LOOKUP, {"area": area, "number": number}).fetchall(), want)

    def test_same_area_duplicate_rejected(self):
        self.put("p", "a", "000000123456")
        with self.assertRaises(sqlite3.IntegrityError):
            self.put("q", "a", "000000123456")
        self.put("r", "b", "000000123456")

    def test_invalid_digits_and_wrong_area_rejected(self):
        for area, number in [("a", "123456"), ("a", "１２３４５６７８９０１２"), ("b", "000000123456")]:
            with self.assertRaises(sqlite3.IntegrityError):
                self.put("p", area, number)

    def test_existing_identity_blocks_change_to_wrong_person_or_area(self):
        self.put("p", "a", "000000123456")
        for sql in ["UPDATE person_identity_numbers SET person_id='q'", "UPDATE people SET area_id='b' WHERE id='p'"]:
            with self.assertRaises(sqlite3.IntegrityError):
                self.db.execute(sql)

    def test_wpf_click_handlers_exist(self):
        for name in ["PersonIdentityWindow", "PeopleView"]:
            xaml = CODE / "Views" / (name + ".xaml")
            code = xaml.with_suffix(".xaml.cs").read_text(encoding="utf-8")
            for e in ElementTree.parse(xaml).iter():
                if "Click" in e.attrib:
                    self.assertIn("void " + e.attrib["Click"] + "(", code)
