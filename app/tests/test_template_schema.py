import sqlite3
from server.schema import initialize


def test_template_fields_upgrade_is_repeatable_and_private():
    connection = sqlite3.connect(':memory:')
    initialize(connection)
    initialize(connection)
    fields = connection.execute("SELECT name FROM field_definitions WHERE id LIKE 'template_%'").fetchall()
    assert len(fields) == 6
    for (name,) in fields:
        assert connection.execute("SELECT mode FROM field_permissions WHERE role='viewer' AND field=?", (name,)).fetchone()[0] == 'hidden'
    connection.close()
