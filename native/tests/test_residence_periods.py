from pathlib import Path
import re
import sqlite3
import unittest
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parents[1]
SCHEMA = (ROOT / "database/schema.sql").read_text()
CODE = ROOT / "src/QLKP.Desktop"
PAGE = re.search(r'const string sql=@"(.*?)";', (CODE / "Core/ResidencePeriods.cs").read_text(), re.S)[1]


class ResidencePeriodsTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        self.db.executescript(SCHEMA)
        self.db.executescript("""
            INSERT INTO areas VALUES('a','A','Khu A','2026'),('b','B','Khu B','2026');
            INSERT INTO accounts(id,username,hash_b64,salt_b64,role,created_utc)
              VALUES('u','qa','fake','fake','ROOT_ADMIN','2026');
            INSERT INTO people(id,area_id,full_name,created_utc,updated_utc)
              VALUES('p','a','QA','2026','2026'),('q','b','QA khác','2026','2026');
        """)

    def insert(self, key, start, end=None, kind="thuong_tru", area="a"):
        self.db.execute(
            "INSERT INTO person_residence_periods VALUES(?,?,'p',?,?,?,?, 'u','2026')",
            (key, area, kind, f"Địa chỉ {key}", start, end),
        )

    def test_actual_query_keeps_old_address_and_derives_end_per_kind(self):
        self.insert("one", "2020-01-01")
        self.insert("two", "2021-01-01")
        self.insert("temp", "2020-04-01", kind="tam_tru")
        rows = {row[0]: row for row in self.db.execute(PAGE, {"person": "p", "area": "a"})}
        self.assertEqual(rows["one"][2:5], ("Địa chỉ one", "2020-01-01", "2021-01-01"))
        self.assertIsNone(rows["two"][4])
        self.assertIsNone(rows["temp"][4])
        self.assertEqual(list(self.db.execute(PAGE, {"person": "p", "area": "b"})), [])

    def test_finite_periods_must_not_overlap_in_either_insertion_order(self):
        self.insert("one", "2020-01-01", "2021-01-01")
        for start, end in [("2020-06-01", None), ("2019-01-01", "2020-06-01")]:
            with self.assertRaisesRegex(sqlite3.IntegrityError, "OVERLAPPING_RESIDENCE_PERIOD"):
                self.insert("bad", start, end)
        self.insert("adjacent", "2021-01-01")

    def test_cross_area_duplicate_and_invalid_date_are_rejected(self):
        self.insert("one", "2020-01-01")
        for key, start, end, area in [
            ("wrong-area", "2021-01-01", None, "b"),
            ("duplicate", "2020-01-01", None, "a"),
            ("invalid", "2021-02-31", None, "a"),
            ("empty", "2021-01-01", "2021-01-01", "a"),
        ]:
            with self.assertRaises(sqlite3.IntegrityError):
                self.insert(key, start, end, area=area)

    def test_original_address_and_person_reference_are_protected(self):
        self.insert("one", "2020-01-01")
        for sql in [
            "UPDATE person_residence_periods SET address='x'",
            "DELETE FROM person_residence_periods",
            "DELETE FROM people WHERE id='p'",
            "UPDATE people SET area_id='b' WHERE id='p'",
        ]:
            with self.assertRaises(sqlite3.IntegrityError):
                self.db.execute(sql)

    def test_new_window_xaml_events_are_connected(self):
        for name in ("ResidencePeriodsWindow", "PeopleView"):
            path = CODE / "Views" / (name + ".xaml")
            root = ElementTree.parse(path)
            implementation = path.with_suffix(".xaml.cs").read_text()
            for element in root.iter():
                for event in ("Click", "SelectionChanged", "TextChanged"):
                    if event in element.attrib:
                        self.assertIn("void " + element.attrib[event] + "(", implementation)
