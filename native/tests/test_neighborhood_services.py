from pathlib import Path
import sqlite3
import unittest
from xml.etree import ElementTree


ROOT = Path(__file__).resolve().parents[1]
SCHEMA = (ROOT / "database/schema.sql").read_text(encoding="utf-8")
VIEWS = ROOT / "src/QLKP.Desktop/Views"


class NeighborhoodServicesTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        self.db.executescript(SCHEMA)
        self.db.executescript("""
          INSERT INTO areas VALUES('a','A','A','2026'),('b','B','B','2026');
          INSERT INTO people(id,area_id,full_name,created_utc,updated_utc)
            VALUES('p','a','QA A','2026','2026'),('q','b','QA B','2026','2026');
        """)

    def insert(self, person, area, feature):
        self.db.execute("INSERT INTO personal_service_records VALUES(?,?,?,'<record/>',1,'2026')",
                        (person, area, feature))

    def test_each_service_may_be_recorded_once_for_the_same_person(self):
        for feature in ("E02", "E04", "E06", "F06"):
            self.insert("p", "a", feature)
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert("p", "a", "E06")
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert("p", "a", "A02")
        self.assertEqual(self.db.execute("SELECT count(*) FROM personal_service_records").fetchone()[0], 4)

    def test_person_link_cannot_cross_areas_or_be_orphaned(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.insert("q", "a", "E06")
        self.insert("p", "a", "E06")
        for query in ("UPDATE personal_service_records SET person_id='q'",
                      "UPDATE people SET area_id='b' WHERE id='p'",
                      "DELETE FROM people WHERE id='p'"):
            with self.assertRaises(sqlite3.IntegrityError):
                self.db.execute(query)
        self.assertEqual(self.db.execute("PRAGMA foreign_key_check").fetchall(), [])

    def test_schema_reapplication_preserves_existing_rows(self):
        self.insert("p", "a", "E06")
        self.db.executescript(SCHEMA)
        self.assertEqual(self.db.execute("SELECT feature,version FROM personal_service_records").fetchall(),
                         [("E06", 1)])

    def test_excel_and_personal_record_handlers_are_connected(self):
        cases = {"PeopleView": {"ReviewResidentWorkbook_Click", "PersonalServices_Click"},
                 "ModuleRecordsView": {"NeighborhoodImport_Click"}}
        for view, required in cases.items():
            xaml = VIEWS / (view + ".xaml")
            code = xaml.with_suffix(".xaml.cs").read_text(encoding="utf-8")
            handlers = {element.get("Click") for element in ElementTree.parse(xaml).iter()}
            self.assertTrue(required.issubset(handlers))
            for handler in required:
                self.assertIn("void " + handler + "(", code)
