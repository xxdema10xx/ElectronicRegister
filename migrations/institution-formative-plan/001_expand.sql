-- Fase 4 additive MySQL 5.7.24. DDL commits implicitly; preflight and backup required.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,NO_ENGINE_SUBSTITUTION';
CREATE TABLE formative_plan (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 study_path_id char(36) CHARACTER SET ascii NOT NULL,
 code varchar(64) NOT NULL,
 name varchar(200) NOT NULL,
 description text DEFAULT NULL,
 revision bigint NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_formative_plan_owner(institution_id,id),
 UNIQUE KEY uq_fp_code(institution_id,code),
 UNIQUE KEY uq_fp_path(institution_id,study_path_id,id),
 CONSTRAINT fk_fp_path FOREIGN KEY(institution_id,study_path_id) REFERENCES study_path(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE formative_plan_version (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 study_path_id char(36) CHARACTER SET ascii NOT NULL,
 plan_id char(36) CHARACTER SET ascii NOT NULL,
 version_number int NOT NULL,
 name varchar(200) NOT NULL,
 status enum('Draft','Published') NOT NULL,
 revision bigint NOT NULL,
 theory_planned_minutes int NOT NULL,
 internship_planned_minutes int NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 published_at_utc datetime(6) DEFAULT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_formative_plan_version_owner(institution_id,id),
 UNIQUE KEY uq_fpv_number(institution_id,plan_id,version_number),
 UNIQUE KEY uq_fpv_path(institution_id,study_path_id,id),
 CONSTRAINT fk_fpv_plan FOREIGN KEY(institution_id,study_path_id,plan_id) REFERENCES formative_plan(institution_id,study_path_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE formative_unit (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 version_id char(36) CHARACTER SET ascii NOT NULL,
 code varchar(64) DEFAULT NULL,
 name varchar(200) NOT NULL,
 description text DEFAULT NULL,
 display_order int NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_formative_unit_owner(institution_id,id),
 UNIQUE KEY uq_fu_version(institution_id,version_id,id),
 UNIQUE KEY uq_fu_order(institution_id,version_id,display_order),
 UNIQUE KEY uq_fu_code(institution_id,version_id,code),
 CONSTRAINT fk_fu_version FOREIGN KEY(institution_id,version_id) REFERENCES formative_plan_version(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE formative_module (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 version_id char(36) CHARACTER SET ascii NOT NULL,
 unit_id char(36) CHARACTER SET ascii NOT NULL,
 subject_id char(36) CHARACTER SET ascii DEFAULT NULL,
 code varchar(64) DEFAULT NULL,
 name varchar(200) NOT NULL,
 description text DEFAULT NULL,
 display_order int NOT NULL,
 planned_minutes int NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_formative_module_owner(institution_id,id),
 UNIQUE KEY uq_fm_version(institution_id,version_id,id),
 UNIQUE KEY uq_fm_order(institution_id,unit_id,display_order),
 UNIQUE KEY uq_fm_code(institution_id,unit_id,code),
 CONSTRAINT fk_fm_unit FOREIGN KEY(institution_id,version_id,unit_id) REFERENCES formative_unit(institution_id,version_id,id),
 CONSTRAINT fk_fm_subject FOREIGN KEY(institution_id,subject_id) REFERENCES subject(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE formative_module_competency (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 version_id char(36) CHARACTER SET ascii NOT NULL,
 module_id char(36) CHARACTER SET ascii NOT NULL,
 description varchar(4000) NOT NULL,
 display_order int NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_formative_module_competency_owner(institution_id,id),
 UNIQUE KEY uq_fmc_order(institution_id,module_id,display_order),
 CONSTRAINT fk_fmc_module FOREIGN KEY(institution_id,version_id,module_id) REFERENCES formative_module(institution_id,version_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE formative_module_content (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 version_id char(36) CHARACTER SET ascii NOT NULL,
 module_id char(36) CHARACTER SET ascii NOT NULL,
 description varchar(4000) NOT NULL,
 display_order int NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_formative_module_content_owner(institution_id,id),
 UNIQUE KEY uq_fmt_order(institution_id,module_id,display_order),
 CONSTRAINT fk_fmt_module FOREIGN KEY(institution_id,version_id,module_id) REFERENCES formative_module(institution_id,version_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE course_edition_formative_plan (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 study_path_id char(36) CHARACTER SET ascii NOT NULL,
 course_edition_id char(36) CHARACTER SET ascii NOT NULL,
 version_id char(36) CHARACTER SET ascii NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_course_edition_formative_plan_owner(institution_id,id),
 UNIQUE KEY uq_cefp_edition(institution_id,course_edition_id),
 CONSTRAINT fk_cefp_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id),
 CONSTRAINT fk_cefp_version FOREIGN KEY(institution_id,study_path_id,version_id) REFERENCES formative_plan_version(institution_id,study_path_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE formative_plan_event (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 plan_id char(36) CHARACTER SET ascii NOT NULL,
 version_id char(36) CHARACTER SET ascii DEFAULT NULL,
 course_edition_id char(36) CHARACTER SET ascii DEFAULT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 operation varchar(40) NOT NULL,
 revision_before bigint DEFAULT NULL,
 revision_after bigint NOT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 before_json json DEFAULT NULL,
 after_json json NOT NULL,
 reason varchar(500) NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_formative_plan_event_owner(institution_id,id),
 KEY idx_fpe_history(institution_id,plan_id,occurred_at_utc,id),
 CONSTRAINT fk_fpe_plan FOREIGN KEY(institution_id,plan_id) REFERENCES formative_plan(institution_id,id),
 CONSTRAINT fk_fpe_version FOREIGN KEY(institution_id,version_id) REFERENCES formative_plan_version(institution_id,id),
 CONSTRAINT fk_fpe_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id),
 CONSTRAINT fk_fpe_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
DELIMITER $$
CREATE TRIGGER fp_insert BEFORE INSERT ON formative_plan FOR EACH ROW BEGIN
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR CHAR_LENGTH(TRIM(NEW.code))=0 OR CHAR_LENGTH(TRIM(NEW.name))=0 OR NEW.revision<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative plan'; END IF;
END$$
CREATE TRIGGER fp_update BEFORE UPDATE ON formative_plan FOR EACH ROW BEGIN
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.study_path_id<>OLD.study_path_id OR BINARY NEW.code<>BINARY OLD.code OR NEW.created_at_utc<>OLD.created_at_utc OR NEW.revision<>OLD.revision+1 OR CHAR_LENGTH(TRIM(NEW.name))=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Plan identity or revision immutable'; END IF;
END$$
CREATE TRIGGER fp_delete BEFORE DELETE ON formative_plan FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Formative plan retained';
END$$
CREATE TRIGGER fpv_insert BEFORE INSERT ON formative_plan_version FOR EACH ROW BEGIN
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.status<>'Draft' OR NEW.version_number<1 OR NEW.revision<>0 OR NEW.published_at_utc IS NOT NULL OR NEW.theory_planned_minutes<1 OR NEW.theory_planned_minutes>10000000 OR NEW.internship_planned_minutes<0 OR NEW.internship_planned_minutes>10000000 OR CHAR_LENGTH(TRIM(NEW.name))=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid draft'; END IF;
END$$
CREATE TRIGGER fpv_update BEFORE UPDATE ON formative_plan_version FOR EACH ROW BEGIN
 IF OLD.status<>'Draft' OR NEW.status NOT IN ('Draft','Published') OR NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.study_path_id<>OLD.study_path_id OR NEW.plan_id<>OLD.plan_id OR NEW.version_number<>OLD.version_number OR NEW.created_at_utc<>OLD.created_at_utc OR NEW.revision<>OLD.revision+1 OR NEW.theory_planned_minutes NOT BETWEEN 1 AND 10000000 OR NEW.internship_planned_minutes NOT BETWEEN 0 AND 10000000 OR CHAR_LENGTH(TRIM(NEW.name))=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published snapshot or identity immutable'; END IF;
 IF NEW.status='Published' THEN
 IF NEW.published_at_utc IS NULL OR
 (SELECT COUNT(*) FROM formative_unit WHERE institution_id=OLD.institution_id AND version_id=OLD.id) NOT BETWEEN 1 AND 500 OR
 EXISTS(SELECT 1 FROM formative_unit u WHERE u.institution_id=OLD.institution_id AND u.version_id=OLD.id AND (SELECT COUNT(*) FROM formative_module m WHERE m.institution_id=u.institution_id AND m.unit_id=u.id) NOT BETWEEN 1 AND 500) OR
 (SELECT COALESCE(SUM(planned_minutes),0) FROM formative_module WHERE institution_id=OLD.institution_id AND version_id=OLD.id)<>NEW.theory_planned_minutes OR
 EXISTS(SELECT 1 FROM formative_module m WHERE m.institution_id=OLD.institution_id AND m.version_id=OLD.id AND (m.subject_id IS NULL OR (SELECT COUNT(*) FROM formative_module_competency c WHERE c.institution_id=m.institution_id AND c.module_id=m.id) NOT BETWEEN 1 AND 200 OR (SELECT COUNT(*) FROM formative_module_content c WHERE c.institution_id=m.institution_id AND c.module_id=m.id) NOT BETWEEN 1 AND 200))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Incomplete formative plan publication'; END IF;
 ELSE IF NEW.published_at_utc IS NOT NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Draft publication date invalid'; END IF; END IF;
END$$
CREATE TRIGGER fpv_delete BEFORE DELETE ON formative_plan_version FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Version retained';
END$$
CREATE TRIGGER fu_insert BEFORE INSERT ON formative_unit FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.name))=0 OR (NEW.code IS NOT NULL AND CHAR_LENGTH(TRIM(NEW.code))=0) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fu_update BEFORE UPDATE ON formative_unit FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.version_id<>OLD.version_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Child parent immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.name))=0 OR (NEW.code IS NOT NULL AND CHAR_LENGTH(TRIM(NEW.code))=0) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fu_delete BEFORE DELETE ON formative_unit FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=OLD.institution_id AND id=OLD.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
END$$
CREATE TRIGGER fm_insert BEFORE INSERT ON formative_module FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.name))=0 OR (NEW.code IS NOT NULL AND CHAR_LENGTH(TRIM(NEW.code))=0) OR NEW.planned_minutes NOT BETWEEN 1 AND 10000000 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fm_update BEFORE UPDATE ON formative_module FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.version_id<>OLD.version_id OR NEW.unit_id<>OLD.unit_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Child parent immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.name))=0 OR (NEW.code IS NOT NULL AND CHAR_LENGTH(TRIM(NEW.code))=0) OR NEW.planned_minutes NOT BETWEEN 1 AND 10000000 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fm_delete BEFORE DELETE ON formative_module FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=OLD.institution_id AND id=OLD.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
END$$
CREATE TRIGGER fmc_insert BEFORE INSERT ON formative_module_competency FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.description))=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fmc_update BEFORE UPDATE ON formative_module_competency FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.version_id<>OLD.version_id OR NEW.module_id<>OLD.module_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Child parent immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.description))=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fmc_delete BEFORE DELETE ON formative_module_competency FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=OLD.institution_id AND id=OLD.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
END$$
CREATE TRIGGER fmt_insert BEFORE INSERT ON formative_module_content FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.description))=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fmt_update BEFORE UPDATE ON formative_module_content FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.version_id<>OLD.version_id OR NEW.module_id<>OLD.module_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Child parent immutable'; END IF;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.display_order<0 OR CHAR_LENGTH(TRIM(NEW.description))=0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative element'; END IF;
END$$
CREATE TRIGGER fmt_delete BEFORE DELETE ON formative_module_content FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=OLD.institution_id AND id=OLD.version_id FOR UPDATE;
 IF version_state IS NULL OR version_state<>'Draft' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Published children immutable'; END IF;
END$$
CREATE TRIGGER cefp_insert BEFORE INSERT ON course_edition_formative_plan FOR EACH ROW BEGIN
 DECLARE version_state varchar(10); DECLARE edition_path char(36) CHARACTER SET ascii; SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE; SELECT study_path_id INTO edition_path FROM course_edition WHERE institution_id=NEW.institution_id AND id=NEW.course_edition_id FOR UPDATE; IF version_state IS NULL OR version_state<>'Published' OR edition_path IS NULL OR edition_path<>NEW.study_path_id THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Edition plan binding invalid'; END IF;
END$$
CREATE TRIGGER cefp_update BEFORE UPDATE ON course_edition_formative_plan FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Formative history immutable';
END$$
CREATE TRIGGER cefp_delete BEFORE DELETE ON course_edition_formative_plan FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Formative history immutable';
END$$
CREATE TRIGGER fpe_update BEFORE UPDATE ON formative_plan_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Formative history immutable';
END$$
CREATE TRIGGER fpe_delete BEFORE DELETE ON formative_plan_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Formative history immutable';
END$$
CREATE TRIGGER fpe_insert BEFORE INSERT ON formative_plan_event FOR EACH ROW BEGIN
 IF NEW.revision_after<0 OR NEW.revision_before<0 OR CHAR_LENGTH(TRIM(NEW.reason))=0 OR NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$') OR NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN institution i ON i.id=m.institution_id WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin') OR (NEW.version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM formative_plan_version v WHERE v.institution_id=NEW.institution_id AND v.id=NEW.version_id AND v.plan_id=NEW.plan_id)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid formative audit actor or resource'; END IF;
END$$
DELIMITER ;
