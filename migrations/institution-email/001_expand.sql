-- 20261027.2. MySQL 5.7.24. Maintenance window + attested instance + backup REQUIRED.
-- DDL implicitly commits. Never replay. No login/microsoft/invitation alias is created.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

CREATE TABLE institution_email_mutex (
 id int NOT NULL, PRIMARY KEY(id)
) ENGINE=InnoDB;
INSERT INTO institution_email_mutex(id) VALUES(1);

CREATE TABLE institution_email_policy (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 policy_id char(36) CHARACTER SET ascii NOT NULL,
 category varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 revision bigint NOT NULL,
 state varchar(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 configuration json NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(institution_id,category,revision),
 UNIQUE KEY uq_email_policy_version(institution_id,policy_id,revision),
 CONSTRAINT fk_email_policy_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_email_policy_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id)
 REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_email_assignment (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 student_id char(36) CHARACTER SET ascii DEFAULT NULL,
 teacher_id char(36) CHARACTER SET ascii DEFAULT NULL,
 membership_id char(36) CHARACTER SET ascii DEFAULT NULL,
 category varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 policy_revision bigint NOT NULL,
 email varchar(254) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 state enum('reserved','provisioned','verified','retired') NOT NULL,
 revision bigint NOT NULL,
 first_name_snapshot varchar(256) NOT NULL,
 last_name_snapshot varchar(256) NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 updated_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_email_assignment_owner(institution_id,id),
 UNIQUE KEY uq_email_assignment_student(institution_id,student_id),
 UNIQUE KEY uq_email_assignment_teacher(institution_id,teacher_id),
 UNIQUE KEY uq_email_assignment_member(institution_id,membership_id),
 KEY idx_email_assignment_page(institution_id,created_at_utc,id),
 CONSTRAINT fk_email_assignment_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_email_assignment_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_email_assignment_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id),
 CONSTRAINT fk_email_assignment_member FOREIGN KEY(institution_id,membership_id) REFERENCES institution_membership(institution_id,id),
 CONSTRAINT fk_email_assignment_policy FOREIGN KEY(institution_id,category,policy_revision) REFERENCES institution_email_policy(institution_id,category,revision),
 CONSTRAINT fk_email_assignment_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Global coordination namespace, intentionally not a tenant directory. Collation
-- matches user.email, including case/accent equivalence. All writers register here.
CREATE TABLE institution_email_reservation (
 address varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
 user_id char(36) CHARACTER SET ascii DEFAULT NULL,
 institution_id char(36) CHARACTER SET ascii DEFAULT NULL,
 assignment_id char(36) CHARACTER SET ascii DEFAULT NULL,
 PRIMARY KEY(address),
 KEY idx_email_reservation_user(user_id),
 CONSTRAINT fk_email_reservation_user FOREIGN KEY(user_id) REFERENCES user(id) ON DELETE CASCADE,
 CONSTRAINT fk_email_reservation_assignment FOREIGN KEY(institution_id,assignment_id) REFERENCES institution_email_assignment(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_email_event (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 operation varchar(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 category varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 policy_revision bigint NOT NULL,
 assignment_id char(36) CHARACTER SET ascii DEFAULT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 before_json json DEFAULT NULL,
 after_json json NOT NULL,
 reason varchar(500) NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(institution_id,id),
 KEY idx_email_event_assignment(institution_id,assignment_id,occurred_at_utc,id),
 KEY idx_email_event_policy(institution_id,category,policy_revision),
 CONSTRAINT fk_email_event_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_email_event_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id),
 CONSTRAINT fk_email_event_policy FOREIGN KEY(institution_id,category,policy_revision) REFERENCES institution_email_policy(institution_id,category,revision),
 CONSTRAINT fk_email_event_assignment FOREIGN KEY(institution_id,assignment_id) REFERENCES institution_email_assignment(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER email_reservation_insert BEFORE INSERT ON institution_email_reservation FOR EACH ROW BEGIN
 IF BINARY NEW.address<>BINARY LOWER(NEW.address) OR
  NOT((NEW.user_id IS NOT NULL AND NEW.institution_id IS NULL AND NEW.assignment_id IS NULL) OR
      (NEW.user_id IS NULL AND NEW.institution_id IS NOT NULL AND NEW.assignment_id IS NOT NULL)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email reservation shape invalid';
 END IF;
 IF NEW.user_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM user WHERE id=NEW.user_id AND email=NEW.address) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email identity reservation mismatch'; END IF;
 IF NEW.assignment_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_email_assignment WHERE institution_id=NEW.institution_id AND id=NEW.assignment_id AND email=NEW.address) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email assignment reservation mismatch'; END IF;
END$$
CREATE TRIGGER email_reservation_update BEFORE UPDATE ON institution_email_reservation FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email reservation immutable';
END$$
CREATE TRIGGER email_reservation_delete BEFORE DELETE ON institution_email_reservation FOR EACH ROW BEGIN
 IF OLD.assignment_id IS NOT NULL OR EXISTS(SELECT 1 FROM user WHERE id=OLD.user_id AND email=OLD.address) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email reservation retained'; END IF;
END$$
CREATE TRIGGER email_user_insert AFTER INSERT ON user FOR EACH ROW BEGIN
 INSERT INTO institution_email_reservation(address,user_id) VALUES(LOWER(NEW.email),NEW.id);
END$$
CREATE TRIGGER email_user_update AFTER UPDATE ON user FOR EACH ROW BEGIN
 IF NOT(NEW.email <=> OLD.email) THEN
  DELETE FROM institution_email_reservation WHERE user_id=OLD.id;
  INSERT INTO institution_email_reservation(address,user_id) VALUES(LOWER(NEW.email),NEW.id);
 END IF;
END$$
CREATE TRIGGER email_policy_insert BEFORE INSERT ON institution_email_policy FOR EACH ROW BEGIN
 DECLARE latest BIGINT;
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT COALESCE(MAX(revision),0) INTO latest FROM institution_email_policy WHERE institution_id=NEW.institution_id AND category=NEW.category;
 IF NEW.category NOT IN ('Student','Teacher','Staff','InstitutionAdmin') OR NEW.state NOT IN ('Draft','Published','Disabled') OR
  NEW.revision<>latest+1 OR NEW.created_at_utc IS NULL OR JSON_TYPE(NEW.configuration)<>'OBJECT' OR
  (latest>0 AND NOT EXISTS(SELECT 1 FROM institution_email_policy WHERE institution_id=NEW.institution_id AND category=NEW.category AND revision=latest AND policy_id=NEW.policy_id)) OR
  NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
    WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy transition or actor invalid'; END IF;
END$$
CREATE TRIGGER email_policy_update BEFORE UPDATE ON institution_email_policy FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy versions append only';
END$$
CREATE TRIGGER email_policy_delete BEFORE DELETE ON institution_email_policy FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy versions retained';
END$$
CREATE TRIGGER email_assignment_insert BEFORE INSERT ON institution_email_assignment FOR EACH ROW BEGIN
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 DECLARE mutex INT;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT id INTO mutex FROM institution_email_mutex WHERE id=1 FOR UPDATE;
 IF NEW.revision<>1 OR NEW.state<>'reserved' OR NEW.email<>LOWER(TRIM(NEW.email)) OR CHAR_LENGTH(NEW.email)>254 OR
  (NEW.student_id IS NOT NULL AND NEW.teacher_id IS NOT NULL) OR
  (NEW.student_id IS NULL AND NEW.teacher_id IS NULL AND NEW.membership_id IS NULL) OR
  (NEW.category='Student' AND NEW.student_id IS NULL) OR (NEW.category='Teacher' AND NEW.teacher_id IS NULL) OR
  (NEW.category IN ('Staff','InstitutionAdmin') AND (NEW.membership_id IS NULL OR NEW.student_id IS NOT NULL OR NEW.teacher_id IS NOT NULL)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email assignment shape invalid'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_email_policy WHERE institution_id=NEW.institution_id AND category=NEW.category AND revision=NEW.policy_revision AND state='Published') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy not published'; END IF;
 IF NEW.membership_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_membership WHERE institution_id=NEW.institution_id AND id=NEW.membership_id AND status='active') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email recipient membership unavailable'; END IF;
 IF NEW.student_id IS NOT NULL AND NEW.membership_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_student_profile WHERE institution_id=NEW.institution_id AND student_id=NEW.student_id AND membership_id=NEW.membership_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email student membership mismatch'; END IF;
 IF NEW.teacher_id IS NOT NULL AND NEW.membership_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_teacher_profile WHERE institution_id=NEW.institution_id AND teacher_id=NEW.teacher_id AND membership_id=NEW.membership_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email teacher membership mismatch'; END IF;
 IF NEW.membership_id IS NULL AND (EXISTS(SELECT 1 FROM institution_student_profile WHERE institution_id=NEW.institution_id AND student_id=NEW.student_id) OR EXISTS(SELECT 1 FROM institution_teacher_profile WHERE institution_id=NEW.institution_id AND teacher_id=NEW.teacher_id)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email linked recipient requires canonical membership'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
   WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active'
   AND (r.role='InstitutionAdmin' OR r.role='Staff' AND NEW.category IN ('Student','Teacher'))) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email actor not authorized'; END IF;
 IF EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND
  (a.student_id=NEW.student_id OR a.teacher_id=NEW.teacher_id OR a.membership_id=NEW.membership_id OR
   EXISTS(SELECT 1 FROM institution_student_profile p WHERE p.institution_id=NEW.institution_id AND
    (p.membership_id=NEW.membership_id AND p.student_id=a.student_id OR p.student_id=NEW.student_id AND p.membership_id=a.membership_id)) OR
   EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=NEW.institution_id AND
    (p.membership_id=NEW.membership_id AND p.teacher_id=a.teacher_id OR p.teacher_id=NEW.teacher_id AND p.membership_id=a.membership_id)))) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email recipient already assigned'; END IF;
END$$
CREATE TRIGGER email_assignment_reserve AFTER INSERT ON institution_email_assignment FOR EACH ROW BEGIN
 INSERT INTO institution_email_reservation(address,institution_id,assignment_id) VALUES(LOWER(NEW.email),NEW.institution_id,NEW.id);
END$$
CREATE TRIGGER email_assignment_update BEFORE UPDATE ON institution_email_assignment FOR EACH ROW BEGIN
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NOT(NEW.student_id<=>OLD.student_id) OR NOT(NEW.teacher_id<=>OLD.teacher_id) OR
  NOT(NEW.membership_id<=>OLD.membership_id) OR NEW.category<>OLD.category OR NEW.created_at_utc<>OLD.created_at_utc OR
  NEW.first_name_snapshot<>OLD.first_name_snapshot OR NEW.last_name_snapshot<>OLD.last_name_snapshot OR NEW.revision<>OLD.revision+1 OR
  NEW.state NOT IN ('reserved','retired') OR NEW.email<>LOWER(TRIM(NEW.email)) OR
  (NEW.email=OLD.email AND NOT(OLD.state='reserved' AND NEW.state='retired')) OR
  NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
   WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email override or retirement invalid'; END IF;
END$$
CREATE TRIGGER email_assignment_override_reserve AFTER UPDATE ON institution_email_assignment FOR EACH ROW BEGIN
 IF NOT(BINARY NEW.email <=> BINARY OLD.email) THEN
  INSERT INTO institution_email_reservation(address,institution_id,assignment_id) VALUES(LOWER(NEW.email),NEW.institution_id,NEW.id);
 END IF;
END$$
CREATE TRIGGER email_assignment_delete BEFORE DELETE ON institution_email_assignment FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email assignment history retained';
END$$
CREATE TRIGGER email_event_insert BEFORE INSERT ON institution_email_event FOR EACH ROW BEGIN
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.operation NOT IN ('PolicyWritten','Assigned','PersonCreatedAndAssigned','Overridden','Retired') OR
  NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$') OR NEW.occurred_at_utc IS NULL OR JSON_TYPE(NEW.after_json)<>'OBJECT' OR
  CHAR_LENGTH(TRIM(NEW.reason))=0 OR NEW.reason REGEXP '[[:cntrl:]]' OR
  (NEW.operation='PolicyWritten' AND NEW.assignment_id IS NOT NULL) OR (NEW.operation<>'PolicyWritten' AND NEW.assignment_id IS NULL) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email event invalid'; END IF;
END$$
CREATE TRIGGER email_event_update BEFORE UPDATE ON institution_email_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email audit append only';
END$$
CREATE TRIGGER email_event_delete BEFORE DELETE ON institution_email_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email audit retained';
END$$
DELIMITER ;

-- Current-state namespace registration, not invented historical events. The
-- maintenance runner must stop all writers before DDL/backfill and verify afterward.
INSERT INTO institution_email_reservation(address,user_id) SELECT LOWER(email),id FROM user;

DELIMITER $$
CREATE TRIGGER email_student_profile_link BEFORE INSERT ON institution_student_profile FOR EACH ROW BEGIN
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 DECLARE mutex INT;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT id INTO mutex FROM institution_email_mutex WHERE id=1 FOR UPDATE;
 IF EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND a.student_id=NEW.student_id AND a.membership_id IS NOT NULL AND a.membership_id<>NEW.membership_id) OR
 EXISTS(SELECT 1 FROM institution_email_assignment a JOIN institution_email_assignment b ON b.institution_id=a.institution_id AND b.id<>a.id
  AND (b.membership_id=NEW.membership_id OR EXISTS(SELECT 1 FROM institution_student_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.student_id=b.student_id) OR EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.teacher_id=b.teacher_id))
  WHERE a.institution_id=NEW.institution_id AND a.student_id=NEW.student_id) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email identity reconciliation required before profile linkage'; END IF;
END$$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER email_teacher_profile_link BEFORE INSERT ON institution_teacher_profile FOR EACH ROW BEGIN
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 DECLARE mutex INT;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT id INTO mutex FROM institution_email_mutex WHERE id=1 FOR UPDATE;
 IF EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND a.teacher_id=NEW.teacher_id AND a.membership_id IS NOT NULL AND a.membership_id<>NEW.membership_id) OR
 EXISTS(SELECT 1 FROM institution_email_assignment a JOIN institution_email_assignment b ON b.institution_id=a.institution_id AND b.id<>a.id
  AND (b.membership_id=NEW.membership_id OR EXISTS(SELECT 1 FROM institution_student_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.student_id=b.student_id) OR EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.teacher_id=b.teacher_id))
  WHERE a.institution_id=NEW.institution_id AND a.teacher_id=NEW.teacher_id) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email identity reconciliation required before profile linkage'; END IF;
END$$
DELIMITER ;
