"""Relational schema on PC. All user data is stored inside the encrypted Vault."""
from __future__ import annotations
import sqlite3
DDL='''
CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY,value TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS role_permissions(
 role TEXT NOT NULL, capability TEXT NOT NULL, allowed INTEGER NOT NULL DEFAULT 0 CHECK(allowed IN (0,1)),
 PRIMARY KEY(role,capability)
);
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
 options_json TEXT NOT NULL DEFAULT '[]', created_at TEXT NOT NULL,
 display_order INTEGER NOT NULL DEFAULT 0, active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1))
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
CREATE TABLE IF NOT EXISTS field_option_migrations(
 id TEXT PRIMARY KEY, field_id TEXT NOT NULL REFERENCES field_definitions(id),
 from_value TEXT NOT NULL, to_value TEXT NOT NULL, affected_count INTEGER NOT NULL,
 options_before_json TEXT NOT NULL, options_after_json TEXT NOT NULL,
 actor TEXT NOT NULL, created_at TEXT NOT NULL, undone_at TEXT, undone_by TEXT
);
CREATE TABLE IF NOT EXISTS field_option_migration_items(
 migration_id TEXT NOT NULL REFERENCES field_option_migrations(id) ON DELETE CASCADE,
 resident_id TEXT NOT NULL REFERENCES residents(id), PRIMARY KEY(migration_id,resident_id)
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
'''
CAPABILITIES={
 'read_residents':'Xem hồ sơ nhân khẩu',
 'edit_residents':'Thêm / sửa nhân khẩu',
 'delete_residents':'Xóa mềm nhân khẩu',
 'read_households':'Xem hộ gia đình',
 'edit_households':'Tạo hộ gia đình',
 'import':'Nhập tài liệu / OCR',
 'export':'Xuất báo cáo trên PC',
 'read_accounts':'Xem tài khoản',
 'manage_accounts':'Tạo / sửa / khóa tài khoản trên PC',
 'read_devices':'Xem thiết bị',
 'manage_devices':'Duyệt / chặn thiết bị trên PC',
 'read_audit':'Xem nhật ký',
 'verify_audit':'Kiểm tra tính toàn vẹn nhật ký',
 'manage_fields':'Tạo trường dữ liệu',
 'manage_templates':'Tạo mẫu dữ liệu',
 'backup':'Sao lưu nội bộ trên PC'
}
DEFAULT_CAPABILITIES={
 'viewer': {'read_residents','read_households'},
 'reviewer': {'read_residents','edit_residents','read_households','edit_households','import','export'},
 'admin_kp': {'read_residents','edit_residents','delete_residents','read_households','edit_households','import','export','read_accounts','manage_accounts','read_devices','manage_devices','manage_fields','manage_templates'},
 'admin_root': {'read_residents','edit_residents','delete_residents','read_households','edit_households','import','export','read_accounts','manage_accounts','read_devices','manage_devices','read_audit','manage_fields','manage_templates'},
 'super_admin': set(CAPABILITIES)
}
BASE_FIELDS={
 'cccd':'Số CCCD','full_name':'Họ tên','dob':'Ngày sinh',
 'gender':'Giới tính','hometown':'Quê quán','permanent_address':'Thường trú',
 'team_id':'Tổ','phone':'Số điện thoại','health_insurance':'BHYT',
 'occupation':'Nghề nghiệp','note':'Ghi chú'}

def initialize(conn:sqlite3.Connection):
    conn.executescript(DDL)
    # Upgrade existing PC vaults without deleting credentials or citizen data.
    account_columns = {row[1] for row in conn.execute('PRAGMA table_info(accounts)')}
    for name in ('phone', 'zalo_id', 'email', 'last_login_at'):
        if name not in account_columns:
            conn.execute(f'ALTER TABLE accounts ADD COLUMN {name} TEXT')
    field_columns = {row[1] for row in conn.execute('PRAGMA table_info(field_definitions)')}
    if 'display_order' not in field_columns:
        conn.execute('ALTER TABLE field_definitions ADD COLUMN display_order INTEGER NOT NULL DEFAULT 0')
    if 'active' not in field_columns:
        conn.execute('ALTER TABLE field_definitions ADD COLUMN active INTEGER NOT NULL DEFAULT 1')
    # Normalize legacy/default duplicate positions to a stable 1..N sequence.
    ordered=conn.execute('SELECT id FROM field_definitions ORDER BY display_order,created_at,label').fetchall()
    for position,row in enumerate(ordered,1):
        conn.execute('UPDATE field_definitions SET display_order=? WHERE id=?',(position,row[0]))
    if not conn.execute("SELECT 1 FROM meta WHERE key='schema_version'").fetchone():
        conn.execute("INSERT INTO meta(key,value) VALUES('schema_version','7.1.0')")
    for role in ['viewer','reviewer','admin_kp','admin_root','super_admin']:
        for capability in CAPABILITIES:
            conn.execute('INSERT OR IGNORE INTO role_permissions(role,capability,allowed) VALUES(?,?,?)',
                         (role,capability,int(capability in DEFAULT_CAPABILITIES[role])))
        for k in BASE_FIELDS:
            mode='masked' if (role=='viewer' and k in ('cccd','phone')) else 'optional'
            conn.execute('INSERT OR IGNORE INTO field_permissions(role,field,mode) VALUES(?,?,?)',(role,k,mode))
    # Existing custom definitions receive fail-closed defaults during upgrade.
    for field in conn.execute('SELECT name FROM field_definitions'):
        for role in ['viewer','reviewer','admin_kp','admin_root','super_admin']:
            mode='hidden' if role=='viewer' else 'optional'
            conn.execute('INSERT OR IGNORE INTO field_permissions(role,field,mode) VALUES(?,?,?)',
                         (role,field[0],mode))
    # Update only schema version; no account, permission or resident data is overwritten.
    conn.execute("UPDATE meta SET value='7.3.0' WHERE key='schema_version'")
    conn.commit()
