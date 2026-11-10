-- QLKP native schema. Database encryption and keys are enforced by VaultStorage.
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
 code TEXT NOT NULL COLLATE NOCASE, address TEXT NOT NULL, search_address TEXT NOT NULL DEFAULT '', status TEXT NOT NULL DEFAULT 'Đang theo dõi',
 version INTEGER NOT NULL DEFAULT 1,
 created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL,
 UNIQUE(area_id,code)
);
CREATE INDEX IF NOT EXISTS household_by_area ON households(area_id, code);
CREATE TABLE IF NOT EXISTS people (
 id TEXT PRIMARY KEY, area_id TEXT NOT NULL REFERENCES areas(id),
 full_name TEXT NOT NULL, search_name TEXT NOT NULL DEFAULT '', date_of_birth TEXT, gender TEXT,
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
 relationship TEXT NOT NULL, reason TEXT NOT NULL, search_reason TEXT NOT NULL DEFAULT '',
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

-- Address periods are independent of household membership and of legal verification.
-- NULL end means open until a later period of the same type starts.
CREATE TABLE IF NOT EXISTS person_residence_periods (
 id TEXT PRIMARY KEY NOT NULL,
 area_id TEXT NOT NULL REFERENCES areas(id),
 person_id TEXT NOT NULL REFERENCES people(id) ON DELETE RESTRICT,
 kind TEXT NOT NULL CHECK(kind IN ('thuong_tru','tam_tru')),
 address TEXT NOT NULL CHECK(length(trim(address)) BETWEEN 1 AND 255),
 valid_from TEXT NOT NULL CHECK(length(valid_from)=10 AND date(valid_from,'+0 days') IS valid_from),
 valid_until TEXT CHECK(valid_until IS NULL OR
   (length(valid_until)=10 AND date(valid_until,'+0 days') IS valid_until AND valid_until>valid_from)),
 actor_id TEXT NOT NULL REFERENCES accounts(id) ON DELETE RESTRICT,
 recorded_utc TEXT NOT NULL,
 UNIQUE(person_id,kind,valid_from)
);
CREATE TRIGGER IF NOT EXISTS person_residence_period_scope_insert
 BEFORE INSERT ON person_residence_periods
 WHEN NEW.area_id IS NOT (SELECT area_id FROM people WHERE id=NEW.person_id)
 BEGIN SELECT RAISE(ABORT, 'CROSS_AREA_RESIDENCE_PERIOD'); END;
CREATE TRIGGER IF NOT EXISTS person_residence_period_overlap_insert
 BEFORE INSERT ON person_residence_periods
 WHEN EXISTS (
   SELECT 1 FROM person_residence_periods p
   WHERE p.person_id=NEW.person_id AND p.kind=NEW.kind
     AND ((p.valid_from<NEW.valid_from AND p.valid_until>NEW.valid_from)
       OR (p.valid_from>NEW.valid_from AND NEW.valid_until>p.valid_from)))
 BEGIN SELECT RAISE(ABORT, 'OVERLAPPING_RESIDENCE_PERIOD'); END;
CREATE TRIGGER IF NOT EXISTS person_residence_period_immutable_update
 BEFORE UPDATE ON person_residence_periods
 BEGIN SELECT RAISE(ABORT, 'IMMUTABLE_RESIDENCE_PERIOD'); END;
CREATE TRIGGER IF NOT EXISTS person_residence_period_immutable_delete
 BEFORE DELETE ON person_residence_periods
 BEGIN SELECT RAISE(ABORT, 'IMMUTABLE_RESIDENCE_PERIOD'); END;
CREATE TRIGGER IF NOT EXISTS person_residence_period_person_scope_update
 BEFORE UPDATE OF area_id ON people
 WHEN NEW.area_id<>OLD.area_id AND EXISTS (SELECT 1 FROM person_residence_periods WHERE person_id=OLD.id)
 BEGIN SELECT RAISE(ABORT, 'MOVE_PERSON_REQUIRES_RESIDENCE_WORKFLOW'); END;

-- V0.8: normalized search indexes are created by DataStore.Initialize after ALTER/backfill migration.

-- V0.10: explicit, evidence-aware classification. Empty profile means NOT CLASSIFIED.
-- Source tags do not grant permissions and are NEVER legal determinations.
CREATE TABLE IF NOT EXISTS household_profiles (
 household_id TEXT PRIMARY KEY REFERENCES households(id) ON DELETE CASCADE,
 housing_type TEXT NOT NULL CHECK(housing_type IN ('nha_rieng','nha_cap4','can_ho','phong_tro','nha_thue','nha_tap_the','o_cung_than_nhan','chua_xac_minh')),
 residency_context TEXT NOT NULL CHECK(residency_context IN ('chu_so_huu','thue','o_nho','chua_doi_chieu')),
 source_kind TEXT NOT NULL CHECK(source_kind IN ('SYNTHETIC_DEMO','USER_DECLARED','DOCUMENT_CHECKED')),
 review_status TEXT NOT NULL CHECK(review_status IN ('DEMO_ONLY','PENDING_REVIEW','VERIFIED')),
 CHECK((source_kind='SYNTHETIC_DEMO' AND review_status='DEMO_ONLY') OR
       (source_kind<>'SYNTHETIC_DEMO' AND review_status<>'DEMO_ONLY'))
);
CREATE INDEX IF NOT EXISTS idx_household_profiles_type ON household_profiles(housing_type);
CREATE TABLE IF NOT EXISTS person_profiles (
 person_id TEXT PRIMARY KEY REFERENCES people(id) ON DELETE CASCADE,
 residency_group TEXT NOT NULL CHECK(residency_group IN ('thuong_tru','tam_tru','luu_tru','tam_vang','chua_xac_minh')),
 occupation_group TEXT NOT NULL CHECK(occupation_group IN ('hoc_sinh','lao_dong','noi_tro','huu_tri','chua_khai_bao')),
 source_kind TEXT NOT NULL CHECK(source_kind IN ('SYNTHETIC_DEMO','USER_DECLARED','DOCUMENT_CHECKED')),
 review_status TEXT NOT NULL CHECK(review_status IN ('DEMO_ONLY','PENDING_REVIEW','VERIFIED')),
 CHECK((source_kind='SYNTHETIC_DEMO' AND review_status='DEMO_ONLY') OR
       (source_kind<>'SYNTHETIC_DEMO' AND review_status<>'DEMO_ONLY'))
);
CREATE INDEX IF NOT EXISTS idx_person_profiles_residency ON person_profiles(residency_group);

-- V0.11: metadata-only provenance for approved batches. Never persist raw unreviewed PII.
CREATE TABLE IF NOT EXISTS intake_batches (
 request_id TEXT PRIMARY KEY, actor_id TEXT NOT NULL REFERENCES accounts(id),
 area_id TEXT NOT NULL REFERENCES areas(id),
 mode TEXT NOT NULL CHECK(mode IN ('CREATE_HOUSEHOLD','ADD_MEMBER','ENRICH_PERSON')),
 source_kind TEXT NOT NULL CHECK(source_kind IN ('CSV_UTF8','XLSX_LOCAL','CLIPBOARD','MANUAL','OCR_DRAFT','QR_DRAFT')),
 source_label TEXT NOT NULL, payload_sha256 TEXT NOT NULL,
 row_count INTEGER NOT NULL CHECK(row_count BETWEEN 1 AND 5000),
 created_utc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS intake_batches_area ON intake_batches(area_id,created_utc);

-- V0.12: scoped, strongly identified operational registers for Group A and B04-B07.
-- A01 and A05 are calculated views: no duplicate counters or hand-written counts.
CREATE TABLE IF NOT EXISTS module_records (
 id TEXT PRIMARY KEY,
 area_id TEXT NOT NULL REFERENCES areas(id),
 feature TEXT NOT NULL CHECK(feature IN ('A02','A03','A04','A06','B04','B05','B06','B07','C01','C02','C03','C04','C05','C06','D01','D02','D03','D04','D05','E01','E02','E03','E04','E05','E06','E07','F01','F02','F03','F04','F05','F06','F07','G01','G02','G03','G04')),
 record_code TEXT NOT NULL COLLATE NOCASE,
 title TEXT NOT NULL,
 search_title TEXT NOT NULL DEFAULT '',
 notes TEXT NOT NULL DEFAULT '',
 status TEXT NOT NULL,
 due_date TEXT NOT NULL DEFAULT '',
 related_feature TEXT NOT NULL DEFAULT '',
 related_id TEXT NOT NULL DEFAULT '',
 version INTEGER NOT NULL DEFAULT 1 CHECK(version>=1),
 created_by TEXT NOT NULL REFERENCES accounts(id),
 updated_by TEXT NOT NULL REFERENCES accounts(id),
 created_utc TEXT NOT NULL,
 updated_utc TEXT NOT NULL,
 UNIQUE(area_id,feature,record_code),
 CHECK(related_id <> id),
 CHECK((related_feature='' AND related_id='') OR
       (related_feature!='' AND related_id!=''))
);
CREATE INDEX IF NOT EXISTS ix_module_records_scope ON module_records(area_id,feature,record_code,id);
CREATE INDEX IF NOT EXISTS ix_module_search ON module_records(area_id,feature,search_title);
CREATE INDEX IF NOT EXISTS ix_module_records_due ON module_records(area_id,feature,due_date,status);
-- V0.17 report period indexes (idempotent, no schema version bump).
CREATE INDEX IF NOT EXISTS ix_report_module_period ON module_records(area_id,feature,created_utc,status);
CREATE INDEX IF NOT EXISTS ix_report_household_period ON households(area_id,created_utc,status);
CREATE INDEX IF NOT EXISTS ix_report_person_period ON people(area_id,created_utc);
CREATE TABLE IF NOT EXISTS module_field_values (
 record_id TEXT NOT NULL REFERENCES module_records(id) ON DELETE CASCADE,
 field_key TEXT NOT NULL,
 field_value TEXT NOT NULL DEFAULT '',
 PRIMARY KEY(record_id,field_key)
);
CREATE TABLE IF NOT EXISTS module_record_history (
 id TEXT PRIMARY KEY,
 record_id TEXT NOT NULL REFERENCES module_records(id) ON DELETE RESTRICT,
 actor_id TEXT NOT NULL REFERENCES accounts(id),
 old_version INTEGER NOT NULL,
 new_version INTEGER NOT NULL,
 old_status TEXT NOT NULL,
 new_status TEXT NOT NULL,
 occurred_utc TEXT NOT NULL,
 CHECK(new_version=old_version+1)
);
CREATE INDEX IF NOT EXISTS ix_module_record_history ON module_record_history(record_id,occurred_utc);
-- A typed reference must exist IN THE SAME AREA, including direct SQL writes.
CREATE TRIGGER IF NOT EXISTS module_reference_insert BEFORE INSERT ON module_records
WHEN NEW.related_feature!='' AND (
 (NEW.related_feature='B01' AND NOT EXISTS(SELECT 1 FROM households h WHERE h.id=NEW.related_id AND h.area_id=NEW.area_id)) OR
 (NEW.related_feature='B02' AND NOT EXISTS(SELECT 1 FROM people p WHERE p.id=NEW.related_id AND p.area_id=NEW.area_id)) OR
 (NEW.related_feature NOT IN ('B01','B02') AND NOT EXISTS(SELECT 1 FROM module_records m WHERE m.id=NEW.related_id AND m.area_id=NEW.area_id AND m.feature=NEW.related_feature))
)
BEGIN SELECT RAISE(ABORT,'INVALID_SCOPED_MODULE_REFERENCE'); END;
CREATE TRIGGER IF NOT EXISTS module_reference_update BEFORE UPDATE OF related_feature,related_id ON module_records
WHEN NEW.related_feature!='' AND (
 (NEW.related_feature='B01' AND NOT EXISTS(SELECT 1 FROM households h WHERE h.id=NEW.related_id AND h.area_id=NEW.area_id)) OR
 (NEW.related_feature='B02' AND NOT EXISTS(SELECT 1 FROM people p WHERE p.id=NEW.related_id AND p.area_id=NEW.area_id)) OR
 (NEW.related_feature NOT IN ('B01','B02') AND NOT EXISTS(SELECT 1 FROM module_records m WHERE m.id=NEW.related_id AND m.area_id=NEW.area_id AND m.feature=NEW.related_feature))
)
BEGIN SELECT RAISE(ABORT,'INVALID_SCOPED_MODULE_REFERENCE'); END;
CREATE TRIGGER IF NOT EXISTS module_immutable_scope BEFORE UPDATE OF area_id,feature,id,record_code ON module_records
WHEN NEW.area_id!=OLD.area_id OR NEW.feature!=OLD.feature OR NEW.id!=OLD.id OR NEW.record_code!=OLD.record_code
BEGIN SELECT RAISE(ABORT,'IMMUTABLE_MODULE_IDENTITY'); END;
-- Prevent stale polymorphic links after changes made outside the UI service.
CREATE TRIGGER IF NOT EXISTS module_target_delete_guard BEFORE DELETE ON module_records
WHEN EXISTS(SELECT 1 FROM module_records x WHERE x.related_feature=OLD.feature AND x.related_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_MODULE_DELETE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS household_module_target_delete_guard BEFORE DELETE ON households
WHEN EXISTS(SELECT 1 FROM module_records x WHERE x.related_feature='B01' AND x.related_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_HOUSEHOLD_DELETE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS person_module_target_delete_guard BEFORE DELETE ON people
WHEN EXISTS(SELECT 1 FROM module_records x WHERE x.related_feature='B02' AND x.related_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_PERSON_DELETE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS household_module_target_scope_guard BEFORE UPDATE OF area_id ON households
WHEN NEW.area_id != OLD.area_id AND EXISTS(SELECT 1 FROM module_records x WHERE x.related_feature='B01' AND x.related_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_HOUSEHOLD_MOVE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS person_module_target_scope_guard BEFORE UPDATE OF area_id ON people
WHEN NEW.area_id != OLD.area_id AND EXISTS(SELECT 1 FROM module_records x WHERE x.related_feature='B02' AND x.related_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_PERSON_MOVE_DENIED'); END;

-- V0.13: extra typed links: one document can reference several files, tasks or meetings.
-- No name-based merging, and the database validates both record types and territorial scope.
CREATE TABLE IF NOT EXISTS module_relations (
 id TEXT PRIMARY KEY,
 area_id TEXT NOT NULL REFERENCES areas(id),
 source_id TEXT NOT NULL REFERENCES module_records(id) ON DELETE RESTRICT,
 target_feature TEXT NOT NULL,
 target_id TEXT NOT NULL,
 relation_kind TEXT NOT NULL CHECK(relation_kind IN ('THAM_CHIEU','NGUON','TRA_LOI','PHAN_CONG','BIEN_BAN','PHOI_HOP')),
 created_by TEXT NOT NULL REFERENCES accounts(id),
 created_utc TEXT NOT NULL,
 UNIQUE(source_id,target_feature,target_id,relation_kind),
 CHECK(source_id<>target_id)
);
CREATE INDEX IF NOT EXISTS ix_module_relations_source ON module_relations(area_id,source_id,created_utc);
CREATE TRIGGER IF NOT EXISTS module_relations_guard_insert BEFORE INSERT ON module_relations
WHEN NOT EXISTS(SELECT 1 FROM module_records s WHERE s.id=NEW.source_id AND s.area_id=NEW.area_id)
 OR (NEW.target_feature='B01' AND NOT EXISTS(SELECT 1 FROM households h WHERE h.id=NEW.target_id AND h.area_id=NEW.area_id))
 OR (NEW.target_feature='B02' AND NOT EXISTS(SELECT 1 FROM people p WHERE p.id=NEW.target_id AND p.area_id=NEW.area_id))
 OR (NEW.target_feature NOT IN ('B01','B02') AND NOT EXISTS(SELECT 1 FROM module_records t WHERE t.id=NEW.target_id AND t.area_id=NEW.area_id AND t.feature=NEW.target_feature))
BEGIN SELECT RAISE(ABORT,'INVALID_SCOPED_RELATION'); END;
CREATE TRIGGER IF NOT EXISTS module_relations_guard_update BEFORE UPDATE ON module_relations
BEGIN SELECT RAISE(ABORT,'IMMUTABLE_RELATION'); END;
CREATE TRIGGER IF NOT EXISTS module_relation_target_delete BEFORE DELETE ON module_records
WHEN EXISTS(SELECT 1 FROM module_relations r WHERE r.target_feature=OLD.feature AND r.target_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_RELATION_TARGET_DELETE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS module_relation_household_delete BEFORE DELETE ON households
WHEN EXISTS(SELECT 1 FROM module_relations r WHERE r.target_feature='B01' AND r.target_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_RELATION_HOUSEHOLD_DELETE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS module_relation_person_delete BEFORE DELETE ON people
WHEN EXISTS(SELECT 1 FROM module_relations r WHERE r.target_feature='B02' AND r.target_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_RELATION_PERSON_DELETE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS module_relation_household_move BEFORE UPDATE OF area_id ON households
WHEN NEW.area_id<>OLD.area_id AND EXISTS(SELECT 1 FROM module_relations r WHERE r.target_feature='B01' AND r.target_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_RELATION_HOUSEHOLD_MOVE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS module_relation_person_move BEFORE UPDATE OF area_id ON people
WHEN NEW.area_id<>OLD.area_id AND EXISTS(SELECT 1 FROM module_relations r WHERE r.target_feature='B02' AND r.target_id=OLD.id)
BEGIN SELECT RAISE(ABORT,'LINKED_RELATION_PERSON_MOVE_DENIED'); END;

-- E06: never accept individual references through generic columns or manual SQL.
CREATE TRIGGER IF NOT EXISTS e06_no_person_link_insert BEFORE INSERT ON module_records
WHEN NEW.feature='E06' AND (NEW.related_feature<>'' OR NEW.related_id<>'')
BEGIN SELECT RAISE(ABORT,'E06_AGGREGATE_ONLY'); END;
CREATE TRIGGER IF NOT EXISTS e06_no_person_link_update BEFORE UPDATE OF related_feature,related_id ON module_records
WHEN NEW.feature='E06' AND (NEW.related_feature<>'' OR NEW.related_id<>'')
BEGIN SELECT RAISE(ABORT,'E06_AGGREGATE_ONLY'); END;
CREATE TRIGGER IF NOT EXISTS e06_relation_source_guard BEFORE INSERT ON module_relations
WHEN EXISTS(SELECT 1 FROM module_records r WHERE r.id=NEW.source_id AND r.feature='E06')
BEGIN SELECT RAISE(ABORT,'E06_AGGREGATE_ONLY'); END;

-- V0.15 – Group F relation policy enforced in SQLite, not only via UI.
CREATE TRIGGER IF NOT EXISTS f_typed_primary_relation_insert BEFORE INSERT ON module_records
WHEN NEW.related_feature<>'' AND (
 (NEW.feature='F01' AND NEW.related_feature NOT IN ('B04','A02')) OR
 (NEW.feature='F02' AND NEW.related_feature NOT IN ('B05','B06','B07')) OR
 (NEW.feature='F03' AND NEW.related_feature NOT IN ('B04','B05','A02')) OR
 (NEW.feature='F04' AND NEW.related_feature NOT IN ('B04','B05')) OR
 (NEW.feature='F05' AND NEW.related_feature NOT IN ('B04','B05','A02')) OR
 (NEW.feature='F06') OR
 (NEW.feature='F07' AND NEW.related_feature NOT IN ('A04','C04')))
BEGIN SELECT RAISE(ABORT,'F_RELATION_TYPE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS f_typed_primary_relation_update BEFORE UPDATE OF related_feature,related_id ON module_records
WHEN NEW.related_feature<>'' AND (
 (NEW.feature='F01' AND NEW.related_feature NOT IN ('B04','A02')) OR
 (NEW.feature='F02' AND NEW.related_feature NOT IN ('B05','B06','B07')) OR
 (NEW.feature='F03' AND NEW.related_feature NOT IN ('B04','B05','A02')) OR
 (NEW.feature='F04' AND NEW.related_feature NOT IN ('B04','B05')) OR
 (NEW.feature='F05' AND NEW.related_feature NOT IN ('B04','B05','A02')) OR
 (NEW.feature='F06') OR
 (NEW.feature='F07' AND NEW.related_feature NOT IN ('A04','C04')))
BEGIN SELECT RAISE(ABORT,'F_RELATION_TYPE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS f_typed_multi_relation_insert BEFORE INSERT ON module_relations
WHEN EXISTS (SELECT 1 FROM module_records src WHERE src.id=NEW.source_id AND (
 (src.feature='F01' AND NEW.target_feature NOT IN ('B04','A02')) OR
 (src.feature='F02' AND NEW.target_feature NOT IN ('B05','B06','B07')) OR
 (src.feature='F03' AND NEW.target_feature NOT IN ('B04','B05','A02')) OR
 (src.feature='F04' AND NEW.target_feature NOT IN ('B04','B05')) OR
 (src.feature='F05' AND NEW.target_feature NOT IN ('B04','B05','A02')) OR
 (src.feature='F06') OR
 (src.feature='F07' AND NEW.target_feature NOT IN ('A04','C04'))))
BEGIN SELECT RAISE(ABORT,'F_RELATION_TYPE_DENIED'); END;
-- F06 has no person links, not even anonymous generic links without a dedicated workflow.
CREATE TRIGGER IF NOT EXISTS f06_no_person_link_insert BEFORE INSERT ON module_records
WHEN NEW.feature='F06' AND (NEW.related_feature<>'' OR NEW.related_id<>'')
BEGIN SELECT RAISE(ABORT,'F06_AGGREGATE_ONLY'); END;
CREATE TRIGGER IF NOT EXISTS f06_no_person_link_update BEFORE UPDATE OF related_feature,related_id ON module_records
WHEN NEW.feature='F06' AND (NEW.related_feature<>'' OR NEW.related_id<>'')
BEGIN SELECT RAISE(ABORT,'F06_AGGREGATE_ONLY'); END;
CREATE TRIGGER IF NOT EXISTS f06_relation_source_guard BEFORE INSERT ON module_relations
WHEN EXISTS(SELECT 1 FROM module_records r WHERE r.id=NEW.source_id AND r.feature='F06')
BEGIN SELECT RAISE(ABORT,'F06_AGGREGATE_ONLY'); END;


-- V0.16: typed links for G01–G04 enforced for direct SQL and multi-link writes.
-- These are internal coordination records, not validated administrative decisions.
CREATE TRIGGER IF NOT EXISTS g_typed_primary_insert BEFORE INSERT ON module_records
WHEN NEW.related_feature<>'' AND (
 (NEW.feature='G01' AND NEW.related_feature NOT IN ('A02','B04','B05','F04','F05','G04')) OR
 (NEW.feature='G02' AND NEW.related_feature NOT IN ('G01','C04','C05','A02')) OR
 (NEW.feature='G03' AND NEW.related_feature NOT IN ('A04','C04','C06','F07')) OR
 (NEW.feature='G04' AND NEW.related_feature NOT IN ('G01','A02','C01','C02','F04','F05')))
BEGIN SELECT RAISE(ABORT,'G_RELATION_TYPE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS g_typed_primary_update BEFORE UPDATE OF related_feature,related_id ON module_records
WHEN NEW.related_feature<>'' AND (
 (NEW.feature='G01' AND NEW.related_feature NOT IN ('A02','B04','B05','F04','F05','G04')) OR
 (NEW.feature='G02' AND NEW.related_feature NOT IN ('G01','C04','C05','A02')) OR
 (NEW.feature='G03' AND NEW.related_feature NOT IN ('A04','C04','C06','F07')) OR
 (NEW.feature='G04' AND NEW.related_feature NOT IN ('G01','A02','C01','C02','F04','F05')))
BEGIN SELECT RAISE(ABORT,'G_RELATION_TYPE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS g_typed_multi_insert BEFORE INSERT ON module_relations
WHEN EXISTS (SELECT 1 FROM module_records src WHERE src.id=NEW.source_id AND (
 (src.feature='G01' AND NEW.target_feature NOT IN ('A02','B04','B05','F04','F05','G04')) OR
 (src.feature='G02' AND NEW.target_feature NOT IN ('G01','C04','C05','A02')) OR
 (src.feature='G03' AND NEW.target_feature NOT IN ('A04','C04','C06','F07')) OR
 (src.feature='G04' AND NEW.target_feature NOT IN ('G01','A02','C01','C02','F04','F05'))))
BEGIN SELECT RAISE(ABORT,'G_RELATION_TYPE_DENIED'); END;


-- V0.18: area-scoped / global catalog. No deletion of referenced code labels;
-- optimistic updates and audit events are enforced by C# service on authenticated writes.
CREATE TABLE IF NOT EXISTS shared_catalog_items (
 id TEXT PRIMARY KEY,
 scope_area TEXT NOT NULL,
 category_code TEXT NOT NULL,
 item_code TEXT NOT NULL,
 item_label TEXT NOT NULL,
 search_label TEXT NOT NULL DEFAULT '',
 details TEXT NOT NULL DEFAULT '',
 active INTEGER NOT NULL DEFAULT 1 CHECK(active IN (0,1)),
 version INTEGER NOT NULL DEFAULT 1 CHECK(version>=1),
 changed_by TEXT NOT NULL REFERENCES accounts(id),
 updated_utc TEXT NOT NULL,
 UNIQUE(scope_area,category_code,item_code),
 CHECK(length(category_code) BETWEEN 2 AND 32),
 CHECK(length(item_code) BETWEEN 2 AND 32),
 CHECK(length(item_label) BETWEEN 1 AND 150),
 CHECK(length(details)<=500)
);
CREATE INDEX IF NOT EXISTS ix_shared_catalog_lookup ON shared_catalog_items(scope_area,category_code,active,item_label,id);
CREATE TRIGGER IF NOT EXISTS shared_catalog_immutable_id BEFORE UPDATE OF id,scope_area,category_code,item_code ON shared_catalog_items
 WHEN OLD.id<>NEW.id OR OLD.scope_area<>NEW.scope_area OR OLD.category_code<>NEW.category_code OR OLD.item_code<>NEW.item_code
 BEGIN SELECT RAISE(ABORT,'CATALOG_IDENTITY_IMMUTABLE'); END;
CREATE TRIGGER IF NOT EXISTS shared_catalog_restrict_delete BEFORE DELETE ON shared_catalog_items
 BEGIN SELECT RAISE(ABORT,'CATALOG_DELETE_DENIED'); END;
CREATE TRIGGER IF NOT EXISTS shared_catalog_scope_guard BEFORE INSERT ON shared_catalog_items
 WHEN NEW.scope_area!='*' AND NOT EXISTS(SELECT 1 FROM areas WHERE id=NEW.scope_area)
 BEGIN SELECT RAISE(ABORT,'CATALOG_UNKNOWN_AREA'); END;

-- Local merge provenance. Original actor IDs are retained as source identifiers only;
-- imported records never grant permissions or replace application accounts.
CREATE TABLE IF NOT EXISTS data_merge_runs (
 id TEXT PRIMARY KEY, area_id TEXT NOT NULL REFERENCES areas(id),
 actor_id TEXT NOT NULL REFERENCES accounts(id), source_sha256 TEXT NOT NULL,
 row_count INTEGER NOT NULL CHECK(row_count>=0), applied_utc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS data_merge_items (
 run_id TEXT NOT NULL REFERENCES data_merge_runs(id), source_table TEXT NOT NULL,
 source_id TEXT NOT NULL, source_actor_id TEXT NOT NULL DEFAULT '',
 source_created_by TEXT NOT NULL DEFAULT '', source_updated_by TEXT NOT NULL DEFAULT '',
 PRIMARY KEY(run_id,source_table,source_id)
);
CREATE INDEX IF NOT EXISTS ix_data_merge_origin ON data_merge_items(source_table,source_id);

-- One-time default business policy migration; no password or identity is stored here.
CREATE TABLE IF NOT EXISTS system_policy_migrations (code TEXT PRIMARY KEY, applied_utc TEXT NOT NULL);
