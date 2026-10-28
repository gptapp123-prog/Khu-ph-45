-- Old schema fixture for migration test; no resident data.

CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS accounts(
 id TEXT PRIMARY KEY, username TEXT NOT NULL UNIQUE, display_name TEXT NOT NULL,
 password_hash TEXT NOT NULL, role TEXT NOT NULL CHECK(role IN ('viewer','reviewer','admin_kp','admin_root','super_admin')),
 team_id INTEGER CHECK(team_id BETWEEN 1 AND 15), active INTEGER NOT NULL DEFAULT 1,
 must_change_password INTEGER NOT NULL DEFAULT 0, failed_attempts INTEGER NOT NULL DEFAULT 0,
 locked_until TEXT, created_at TEXT NOT NULL,
 phone TEXT, zalo_id TEXT, email TEXT, last_login_at TEXT
);
CREATE TABLE IF NOT EXISTS devices(
 id TEXT PRIMARY KEY, user_id TEXT NOT NULL REFERENCES accounts(id),
 device_label TEXT NOT NULL, status TEXT NOT NULL CHECK(status IN ('pending','approved','blocked')),
 seen_at TEXT NOT NULL, UNIQUE(user_id,id)
);
CREATE TABLE IF NOT EXISTS households(
 id TEXT PRIMARY KEY, code TEXT NOT NULL UNIQUE, address TEXT NOT NULL,
 team_id INTEGER NOT NULL CHECK(team_id BETWEEN 1 AND 15), note TEXT NOT NULL DEFAULT '', created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS residents(
 id TEXT PRIMARY KEY, full_name TEXT NOT NULL, cccd TEXT UNIQUE,
 dob TEXT, gender TEXT, hometown TEXT, permanent_address TEXT,
 team_id INTEGER NOT NULL CHECK(team_id BETWEEN 1 AND 15), phone TEXT, health_insurance TEXT,
 occupation TEXT, note TEXT NOT NULL DEFAULT '', household_id TEXT REFERENCES households(id) ON DELETE SET NULL,
 created_at TEXT NOT NULL, updated_at TEXT NOT NULL, deleted_at TEXT
);
CREATE INDEX IF NOT EXISTS idx_residents_name ON residents(full_name);
CREATE INDEX IF NOT EXISTS idx_residents_team ON residents(team_id);
CREATE INDEX IF NOT EXISTS idx_residents_household ON residents(household_id);
CREATE TABLE IF NOT EXISTS field_definitions(
 id TEXT PRIMARY KEY, name TEXT NOT NULL UNIQUE, label TEXT NOT NULL UNIQUE,
 type TEXT NOT NULL CHECK(type IN ('text','number','date','dropdown','checkbox')),
 options_json TEXT NOT NULL DEFAULT '[]', created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS field_values(
 resident_id TEXT NOT NULL REFERENCES residents(id) ON DELETE CASCADE,
 field_id TEXT NOT NULL REFERENCES field_definitions(id) ON DELETE CASCADE,
 value TEXT,PRIMARY KEY(resident_id,field_id)
);
CREATE TABLE IF NOT EXISTS field_permissions(
 role TEXT NOT NULL, field TEXT NOT NULL,
 mode TEXT NOT NULL CHECK(mode IN ('required','optional','hidden','masked')),
 PRIMARY KEY(role,field)
);
CREATE TABLE IF NOT EXISTS templates(id TEXT PRIMARY KEY,name TEXT NOT NULL,fields_json TEXT NOT NULL,mapping_json TEXT NOT NULL,created_at TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS filter_rules(id TEXT PRIMARY KEY,name TEXT NOT NULL,signature_json TEXT NOT NULL,fields_json TEXT NOT NULL,created_at TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS audit_log(
 seq INTEGER PRIMARY KEY AUTOINCREMENT, at TEXT NOT NULL, actor TEXT NOT NULL,
 action TEXT NOT NULL, entity_type TEXT NOT NULL, entity_id TEXT NOT NULL,
 details_json TEXT NOT NULL, prev_hash TEXT NOT NULL, hash TEXT NOT NULL
);
CREATE TRIGGER IF NOT EXISTS audit_block_update BEFORE UPDATE ON audit_log BEGIN SELECT RAISE(ABORT,'Nhật ký không thể sửa'); END;
CREATE TRIGGER IF NOT EXISTS audit_block_delete BEFORE DELETE ON audit_log BEGIN SELECT RAISE(ABORT,'Nhật ký không thể xóa'); END;
