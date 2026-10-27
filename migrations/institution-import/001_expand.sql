-- 20261027.3. Additive MySQL5.7.24; attested isolated target, stopped writers and backup required.
-- DDL implicitly commits. Never automatically replay or promise transactional DDL rollback.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';
CREATE TABLE institution_import_batch (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 category varchar(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 state enum('Preview','Completed','Expired') NOT NULL,
 revision bigint NOT NULL,
 file_sha256 char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 preview_digest char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 total_rows int NOT NULL,valid_rows int NOT NULL,invalid_rows int NOT NULL,
 payload json DEFAULT NULL,
 result_json json DEFAULT NULL,
 warnings_json json NOT NULL,
 policy_revision bigint DEFAULT NULL,
 created_at_utc datetime(6) NOT NULL,
 expires_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_import_batch_owner(institution_id,id),
 UNIQUE KEY uq_import_batch_category(institution_id,id,category),
 KEY idx_import_batch_actor(institution_id,actor_membership_id,actor_user_id,created_at_utc,id),
 KEY idx_import_batch_digest(institution_id,category,file_sha256,state),
 KEY idx_import_batch_expiry(state,expires_at_utc,id),
 CONSTRAINT fk_import_batch_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_import_batch_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id),
 CONSTRAINT fk_import_batch_policy FOREIGN KEY(institution_id,category,policy_revision) REFERENCES institution_email_policy(institution_id,category,revision)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_import_external_reference (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 category varchar(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 reference varchar(100) COLLATE utf8mb4_bin NOT NULL,
 student_id char(36) CHARACTER SET ascii DEFAULT NULL,
 teacher_id char(36) CHARACTER SET ascii DEFAULT NULL,
 PRIMARY KEY(institution_id,category,reference),
 CONSTRAINT fk_import_external_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_import_external_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_import_external_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_import_item (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 batch_id char(36) CHARACTER SET ascii NOT NULL,
 row_number int NOT NULL,
 category varchar(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 student_id char(36) CHARACTER SET ascii DEFAULT NULL,
 teacher_id char(36) CHARACTER SET ascii DEFAULT NULL,
 email_assignment_id char(36) CHARACTER SET ascii DEFAULT NULL,
 external_reference varchar(100) COLLATE utf8mb4_bin DEFAULT NULL,
 PRIMARY KEY(institution_id,batch_id,row_number),
 CONSTRAINT fk_import_item_batch FOREIGN KEY(institution_id,batch_id,category) REFERENCES institution_import_batch(institution_id,id,category),
 CONSTRAINT fk_import_item_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_import_item_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id),
 CONSTRAINT fk_import_item_assignment FOREIGN KEY(institution_id,email_assignment_id) REFERENCES institution_email_assignment(institution_id,id),
 CONSTRAINT fk_import_item_external FOREIGN KEY(institution_id,category,external_reference) REFERENCES institution_import_external_reference(institution_id,category,reference)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_import_event (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 id char(36) CHARACTER SET ascii NOT NULL,
 batch_id char(36) CHARACTER SET ascii NOT NULL,
 revision bigint NOT NULL,
 action varchar(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_kind enum('User','Retention') NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii DEFAULT NULL,
 actor_membership_id char(36) CHARACTER SET ascii DEFAULT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 result_json json NOT NULL,
 reason varchar(500) NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(institution_id,id),
 UNIQUE KEY uq_import_event_revision(institution_id,batch_id,revision),
 CONSTRAINT fk_import_event_batch FOREIGN KEY(institution_id,batch_id) REFERENCES institution_import_batch(institution_id,id),
 CONSTRAINT fk_import_event_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
DELIMITER $$
CREATE TRIGGER import_batch_insert BEFORE INSERT ON institution_import_batch FOR EACH ROW BEGIN
 DECLARE parent char(36) CHARACTER SET ascii;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.category NOT IN ('Student','Teacher') OR NEW.state<>'Preview' OR NEW.revision<>1 OR NEW.payload IS NULL OR JSON_TYPE(NEW.payload)<>'OBJECT' OR NEW.result_json IS NOT NULL OR JSON_TYPE(NEW.warnings_json)<>'ARRAY' OR NEW.total_rows<1 OR NEW.total_rows>10000 OR NEW.valid_rows+NEW.invalid_rows<>NEW.total_rows OR NEW.expires_at_utc<=NEW.created_at_utc OR NOT(BINARY NEW.file_sha256 REGEXP '^[0-9A-F]{64}$') OR NOT(BINARY NEW.preview_digest REGEXP '^[0-9A-F]{64}$') OR
 NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN institution i ON i.id=m.institution_id WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role IN ('InstitutionAdmin','Staff')) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import preview ownership or state invalid'; END IF;
END$$
CREATE TRIGGER import_batch_update BEFORE UPDATE ON institution_import_batch FOR EACH ROW BEGIN
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.actor_user_id<>OLD.actor_user_id OR NEW.actor_membership_id<>OLD.actor_membership_id OR NEW.category<>OLD.category OR NEW.file_sha256<>OLD.file_sha256 OR NEW.created_at_utc<>OLD.created_at_utc OR NEW.expires_at_utc<>OLD.expires_at_utc OR NEW.revision<>OLD.revision+1 OR OLD.state<>'Preview' OR NEW.total_rows<>OLD.total_rows OR NEW.valid_rows+NEW.invalid_rows<>NEW.total_rows OR JSON_TYPE(NEW.warnings_json)<>'ARRAY' OR
 (NEW.state='Preview' AND (NEW.payload IS NULL OR NEW.result_json IS NOT NULL)) OR
 (NEW.state='Completed' AND (NEW.payload IS NOT NULL OR NEW.result_json IS NULL OR OLD.expires_at_utc<=UTC_TIMESTAMP(6) OR OLD.invalid_rows<>0 OR (SELECT COUNT(*) FROM institution_import_item WHERE institution_id=NEW.institution_id AND batch_id=NEW.id)<>OLD.total_rows)) OR
 (NEW.state='Expired' AND (NEW.payload IS NOT NULL OR NEW.result_json IS NOT NULL OR OLD.expires_at_utc>UTC_TIMESTAMP(6))) OR
 (NEW.state<>'Expired' AND NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN institution i ON i.id=m.institution_id WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role IN ('InstitutionAdmin','Staff'))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import transition or immutable fields invalid'; END IF;
END$$
CREATE TRIGGER import_batch_delete BEFORE DELETE ON institution_import_batch FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import history retained'; END$$
CREATE TRIGGER import_external_insert BEFORE INSERT ON institution_import_external_reference FOR EACH ROW BEGIN
 IF CHAR_LENGTH(TRIM(NEW.reference))=0 OR NEW.reference<>TRIM(NEW.reference) OR NEW.reference REGEXP '[[:cntrl:]]' OR NOT((NEW.category='Student' AND NEW.student_id IS NOT NULL AND NEW.teacher_id IS NULL) OR (NEW.category='Teacher' AND NEW.student_id IS NULL AND NEW.teacher_id IS NOT NULL)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import external reference invalid'; END IF;
END$$
CREATE TRIGGER import_external_update BEFORE UPDATE ON institution_import_external_reference FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import reference immutable'; END$$
CREATE TRIGGER import_external_delete BEFORE DELETE ON institution_import_external_reference FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import reference retained'; END$$
CREATE TRIGGER import_item_insert BEFORE INSERT ON institution_import_item FOR EACH ROW BEGIN
 IF NEW.row_number<1 OR NOT((NEW.category='Student' AND NEW.student_id IS NOT NULL AND NEW.teacher_id IS NULL) OR (NEW.category='Teacher' AND NEW.student_id IS NULL AND NEW.teacher_id IS NOT NULL)) OR
 NOT EXISTS(SELECT 1 FROM institution_import_batch b WHERE b.institution_id=NEW.institution_id AND b.id=NEW.batch_id AND b.state='Preview' AND b.expires_at_utc>UTC_TIMESTAMP(6)) OR
 (NEW.email_assignment_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND a.id=NEW.email_assignment_id AND a.category=NEW.category AND (a.student_id<=>NEW.student_id) AND (a.teacher_id<=>NEW.teacher_id))) OR
 (NEW.external_reference IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_import_external_reference x WHERE x.institution_id=NEW.institution_id AND x.category=NEW.category AND x.reference=NEW.external_reference AND (x.student_id<=>NEW.student_id) AND (x.teacher_id<=>NEW.teacher_id))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import item ownership or target invalid'; END IF;
END$$
CREATE TRIGGER import_item_update BEFORE UPDATE ON institution_import_item FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import item immutable'; END$$
CREATE TRIGGER import_item_delete BEFORE DELETE ON institution_import_item FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import item retained'; END$$
CREATE TRIGGER import_event_insert BEFORE INSERT ON institution_import_event FOR EACH ROW BEGIN
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$') OR JSON_TYPE(NEW.result_json)<>'OBJECT' OR NEW.reason REGEXP '[[:cntrl:]]' OR CHAR_LENGTH(TRIM(NEW.reason))=0 OR
 NOT EXISTS(SELECT 1 FROM institution_import_batch b WHERE b.institution_id=NEW.institution_id AND b.id=NEW.batch_id AND b.revision=NEW.revision AND
 ((NEW.actor_kind='User' AND NEW.actor_user_id=b.actor_user_id AND NEW.actor_membership_id=b.actor_membership_id AND ((NEW.action='Previewed' AND b.state='Preview' AND b.revision=1) OR (NEW.action='Remapped' AND b.state='Preview' AND b.revision>1) OR (NEW.action='Confirmed' AND b.state='Completed'))) OR (NEW.actor_kind='Retention' AND NEW.actor_user_id IS NULL AND NEW.actor_membership_id IS NULL AND NEW.action='Expired' AND b.state='Expired' AND b.expires_at_utc<=UTC_TIMESTAMP(6)))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import audit invalid'; END IF;
END$$
CREATE TRIGGER import_event_update BEFORE UPDATE ON institution_import_event FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import audit append only'; END$$
CREATE TRIGGER import_event_delete BEFORE DELETE ON institution_import_event FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import audit retained'; END$$
DELIMITER ;
