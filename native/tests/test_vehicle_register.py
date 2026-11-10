from pathlib import Path
import sqlite3
import unittest
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src/QLKP.Desktop"
SCHEMA = (ROOT / "database/schema.sql").read_text(encoding="utf-8")


class VehicleTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        self.db.executescript(SCHEMA)
        self.db.executescript("""
          INSERT INTO areas VALUES('a','A','A','2026'),('b','B','B','2026');
          INSERT INTO accounts(id,username,hash_b64,salt_b64,role,created_utc)
            VALUES('u','qa','fake','fake','ROOT_ADMIN','2026');
          INSERT INTO people(id,area_id,full_name,created_utc,updated_utc)
            VALUES('p','a','QA A','2026','2026'),('q','b','QA B','2026','2026');
        """)

    def insert(self, key, plate, kind, seats=None, payload=None, owner="p"):
        self.db.execute("INSERT INTO vehicles VALUES(?,'a',?,?,'Hãng',?,?,?,1,1,'u','2026')",
                        (key, owner, plate, kind, seats, payload))

    def test_capacity_matches_vehicle_type(self):
        self.insert("one", "00QA1234", "passenger", seats=7)
        self.insert("two", "00QA1235", "truck", payload=1500)
        for kind, seats, payload in [("passenger", None, None), ("truck", 3, None),
                                     ("motorcycle", 2, None), ("truck", None, 1.5)]:
            with self.assertRaises(sqlite3.IntegrityError):
                self.insert("bad", "00QA1236", kind, seats, payload)

    def test_plate_must_be_normalized_and_unique(self):
        self.insert("one", "00QA1234", "motorcycle")
        for plate in ("00QA1234", "00-QA-1235", "00qa1235"):
            with self.assertRaises(sqlite3.IntegrityError):
                self.insert("two", plate, "motorcycle")

    def test_scope_is_checked_on_insert_update_and_owner_move(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert("bad", "00QA1234", "motorcycle", owner="q")
        self.insert("one", "00QA1234", "motorcycle")
        for sql in ("UPDATE vehicles SET owner_id='q'", "UPDATE people SET area_id='b' WHERE id='p'"):
            with self.assertRaises(sqlite3.IntegrityError):
                self.db.execute(sql)

    def test_wpf_handlers_exist(self):
        for view in ("VehicleView", "VehicleEditWindow"):
            xaml = SOURCE / "Views" / (view + ".xaml")
            code = xaml.with_suffix(".xaml.cs").read_text(encoding="utf-8")
            for element in ElementTree.parse(xaml).iter():
                for event in ("Click", "SelectionChanged"):
                    if event in element.attrib:
                        self.assertIn("void " + element.attrib[event] + "(", code)
