-- QLKP V0.1 / PILOT DATA ONLY - DATABASE IS NOT ENCRYPTED.
-- Tables for first functional vertical slice. No preconfigured accounts/passwords.
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS areas (
 id TEXT PRIMARY KEY, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL,
 created_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS accounts (
 id TEXT PRIMARY KEY, username TEXT NOT NULL UNIQUE COLLATE NOCASE,
 hash_b64 TEXT NOT NULL, salt_b64 TEXT NOT NULL,
 role TEXT NOT NULL CHECK(role IN ('ROOT_ADMIN','KP_ADMIN','ASSIGNED_USER')),
 area_id TEXT REFERENCES areas(id), active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
 policy_version INTEGER NOT NULL DEFAULT 1, created_utc TEXT NOT NULL,
 CHECK((role='ROOT_ADMIN' AND area_id IS NULL) OR (role!='ROOT_ADMIN' AND area_id IS NOT NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS one_root_admin ON accounts(role) WHERE role='ROOT_ADMIN';
-- Temporary login throttling state; no passwords or IP/user-provided details are logged.
CREATE TABLE IF NOT EXISTS login_throttle (
 account_id TEXT PRIMARY KEY NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
 failed_count INTEGER NOT NULL DEFAULT 0 CHECK(failed_count>=0),
 blocked_until_utc TEXT, updated_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS permissions (
 account_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
 feature TEXT NOT NULL, action TEXT NOT NULL,
 area_id TEXT NOT NULL REFERENCES areas(id),
 granted_by TEXT NOT NULL REFERENCES accounts(id),
 PRIMARY KEY (account_id,feature,action,area_id)
);
CREATE TABLE IF NOT EXISTS delegation_ceiling (
 admin_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
 feature TEXT NOT NULL, action TEXT NOT NULL,
 area_id TEXT NOT NULL REFERENCES areas(id),
 PRIMARY KEY(admin_id,feature,action,area_id)
);
CREATE TABLE IF NOT EXISTS households (
 id TEXT PRIMARY KEY, area_id TEXT NOT NULL REFERENCES areas(id),
 code TEXT NOT NULL COLLATE NOCASE, address TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'Đang theo dõi',
 version INTEGER NOT NULL DEFAULT 1,
 created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL,
 UNIQUE(area_id,code)
);
CREATE INDEX IF NOT EXISTS household_by_area ON households(area_id, code);
CREATE TABLE IF NOT EXISTS people (
 id TEXT PRIMARY KEY, area_id TEXT NOT NULL REFERENCES areas(id),
 full_name TEXT NOT NULL, date_of_birth TEXT, gender TEXT,
 version INTEGER NOT NULL DEFAULT 1,
 created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS people_by_area_name ON people(area_id, full_name);
CREATE TABLE IF NOT EXISTS household_memberships (
 id TEXT PRIMARY KEY,
 household_id TEXT NOT NULL REFERENCES households(id) ON DELETE RESTRICT,
 person_id TEXT NOT NULL REFERENCES people(id) ON DELETE RESTRICT,
 relationship TEXT NOT NULL, start_utc TEXT NOT NULL, end_utc TEXT,
 created_utc TEXT NOT NULL,
 CHECK(end_utc IS NULL OR end_utc >= start_utc)
);
CREATE UNIQUE INDEX IF NOT EXISTS one_active_household_per_person ON household_memberships(person_id) WHERE end_utc IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS one_active_household_head ON household_memberships(household_id) WHERE relationship='Chủ hộ' AND end_utc IS NULL;
CREATE INDEX IF NOT EXISTS membership_by_household ON household_memberships(household_id,end_utc);
CREATE TABLE IF NOT EXISTS audit_log (
 id TEXT PRIMARY KEY, actor_id TEXT NOT NULL REFERENCES accounts(id),
 event_code TEXT NOT NULL, entity_type TEXT NOT NULL, entity_id TEXT NOT NULL,
 utc TEXT NOT NULL, result TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS audit_by_actor ON audit_log(actor_id,utc);

-- Prevent cross-area person–household relations even when an internal caller is faulty.
CREATE TRIGGER IF NOT EXISTS membership_area_guard_insert
 BEFORE INSERT ON household_memberships
 WHEN (SELECT area_id FROM households WHERE id=NEW.household_id) != (SELECT area_id FROM people WHERE id=NEW.person_id)
 BEGIN SELECT RAISE(ABORT, 'CROSS_AREA_MEMBERSHIP'); END;
CREATE TRIGGER IF NOT EXISTS membership_area_guard_update
 BEFORE UPDATE OF household_id,person_id ON household_memberships
 WHEN (SELECT area_id FROM households WHERE id=NEW.household_id) != (SELECT area_id FROM people WHERE id=NEW.person_id)
 BEGIN SELECT RAISE(ABORT, 'CROSS_AREA_MEMBERSHIP'); END;
CREATE TRIGGER IF NOT EXISTS household_area_move_guard
 BEFORE UPDATE OF area_id ON households
 WHEN NEW.area_id != OLD.area_id AND EXISTS(SELECT 1 FROM household_memberships WHERE household_id=OLD.id)
 BEGIN SELECT RAISE(ABORT, 'MOVE_HOUSEHOLD_REQUIRES_WORKFLOW'); END;
CREATE TRIGGER IF NOT EXISTS person_area_move_guard
 BEFORE UPDATE OF area_id ON people
 WHEN NEW.area_id != OLD.area_id AND EXISTS(SELECT 1 FROM household_memberships WHERE person_id=OLD.id)
 BEGIN SELECT RAISE(ABORT, 'MOVE_PERSON_REQUIRES_WORKFLOW'); END;

-- V0.6 migration: repeated delivery of the SAME approved import must not create duplicate people.
CREATE TABLE IF NOT EXISTS member_import_batches (
 idempotency_key TEXT PRIMARY KEY,
 household_id TEXT NOT NULL REFERENCES households(id) ON DELETE RESTRICT,
 actor_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
 payload_hash TEXT NOT NULL,
 record_count INTEGER NOT NULL CHECK(record_count > 0 AND record_count <= 1000),
 created_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS member_import_batches_household ON member_import_batches(household_id,created_utc);

-- V0.7: business event log for household movement; FK-bound to source/destination people and actor.
CREATE TABLE IF NOT EXISTS residency_changes (
 id TEXT PRIMARY KEY,
 area_id TEXT NOT NULL REFERENCES areas(id),
 person_id TEXT NOT NULL REFERENCES people(id) ON DELETE RESTRICT,
 from_household_id TEXT NOT NULL REFERENCES households(id) ON DELETE RESTRICT,
 to_household_id TEXT NOT NULL REFERENCES households(id) ON DELETE RESTRICT,
 old_membership_id TEXT NOT NULL UNIQUE REFERENCES household_memberships(id) ON DELETE RESTRICT,
 new_membership_id TEXT NOT NULL UNIQUE REFERENCES household_memberships(id) ON DELETE RESTRICT,
 relationship TEXT NOT NULL, reason TEXT NOT NULL,
 actor_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
 occurred_utc TEXT NOT NULL,
 CHECK(from_household_id != to_household_id)
);
CREATE INDEX IF NOT EXISTS residency_changes_by_area ON residency_changes(area_id,occurred_utc);
CREATE INDEX IF NOT EXISTS residency_changes_by_person ON residency_changes(person_id,occurred_utc);
CREATE TRIGGER IF NOT EXISTS residency_change_scope_insert
 BEFORE INSERT ON residency_changes
 WHEN NEW.area_id != (SELECT area_id FROM people WHERE id=NEW.person_id)
   OR NEW.area_id != (SELECT area_id FROM households WHERE id=NEW.from_household_id)
   OR NEW.area_id != (SELECT area_id FROM households WHERE id=NEW.to_household_id)
   OR NEW.person_id != (SELECT person_id FROM household_memberships WHERE id=NEW.old_membership_id)
   OR NEW.person_id != (SELECT person_id FROM household_memberships WHERE id=NEW.new_membership_id)
   OR NEW.from_household_id != (SELECT household_id FROM household_memberships WHERE id=NEW.old_membership_id)
   OR NEW.to_household_id != (SELECT household_id FROM household_memberships WHERE id=NEW.new_membership_id)
 BEGIN SELECT RAISE(ABORT, 'INVALID_RESIDENCY_CHANGE_RELATION'); END;
CREATE TRIGGER IF NOT EXISTS residency_change_immutable_update
 BEFORE UPDATE ON residency_changes
 BEGIN SELECT RAISE(ABORT, 'IMMUTABLE_RESIDENCY_EVENT'); END;
CREATE TRIGGER IF NOT EXISTS residency_change_immutable_delete
 BEFORE DELETE ON residency_changes
 BEGIN SELECT RAISE(ABORT, 'IMMUTABLE_RESIDENCY_EVENT'); END;
