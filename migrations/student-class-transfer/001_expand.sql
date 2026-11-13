-- 20261022.4: additive current-membership revision and immutable transfer decision ledger.
-- Apply ONCE, only after isolated-target attestation, preflight and verified backup.
ALTER TABLE student ADD COLUMN transfer_revision bigint(20) NOT NULL DEFAULT 0;
CREATE TABLE student_class_transfer_event (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 id char(36) CHARACTER SET ascii NOT NULL,
 student_id char(36) CHARACTER SET ascii NOT NULL,
 from_class_id char(36) CHARACTER SET ascii NOT NULL,
 to_class_id char(36) CHARACTER SET ascii NOT NULL,
 from_course_edition_id char(36) CHARACTER SET ascii NOT NULL,
 to_course_edition_id char(36) CHARACTER SET ascii NOT NULL,
 source_enrollment_id char(36) CHARACTER SET ascii NOT NULL,
 destination_enrollment_id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 effective_date date NOT NULL,
 source_course_start_date date NOT NULL,
 destination_course_start_date date NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 applied_limit_months int NOT NULL,
 revision_before bigint(20) NOT NULL,
 revision_after bigint(20) NOT NULL,
 reason varchar(1000) NOT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 from_class_name varchar(200) NOT NULL,
 to_class_name varchar(200) NOT NULL,
 from_course_name varchar(200) NOT NULL,
 to_course_name varchar(200) NOT NULL,
 PRIMARY KEY(institution_id,id),
 UNIQUE KEY uq_transfer_revision(institution_id,student_id,revision_after),
 KEY idx_transfer_history(institution_id,student_id,occurred_at_utc,id),
 CONSTRAINT fk_transfer_owner FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_transfer_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id),
 CONSTRAINT fk_transfer_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_transfer_from_class FOREIGN KEY(institution_id,from_class_id) REFERENCES class(institution_id,id),
 CONSTRAINT fk_transfer_to_class FOREIGN KEY(institution_id,to_class_id) REFERENCES class(institution_id,id),
 CONSTRAINT fk_transfer_from_course FOREIGN KEY(institution_id,from_course_edition_id) REFERENCES course_edition(institution_id,id),
 CONSTRAINT fk_transfer_to_course FOREIGN KEY(institution_id,to_course_edition_id) REFERENCES course_edition(institution_id,id),
 CONSTRAINT fk_transfer_source_enrollment FOREIGN KEY(institution_id,source_enrollment_id) REFERENCES student_enrollment(institution_id,id),
 CONSTRAINT fk_transfer_destination_enrollment FOREIGN KEY(institution_id,destination_enrollment_id) REFERENCES student_enrollment(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET SESSION character_set_client='utf8mb4';
SET SESSION collation_connection='utf8mb4_unicode_ci';
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER transfer_event_insert BEFORE INSERT ON student_class_transfer_event FOR EACH ROW BEGIN
 IF NEW.from_class_id=NEW.to_class_id OR NEW.revision_before<0 OR NEW.revision_after<>NEW.revision_before+1
 OR NEW.applied_limit_months<1 OR NEW.applied_limit_months>120 OR NEW.reason NOT REGEXP '[^[:space:]]'
 OR NEW.request_hash NOT REGEXP '^[0-9A-F]{64}$' OR YEAR(NEW.effective_date)<1000
 OR NEW.effective_date>DATE_ADD(NEW.source_course_start_date,INTERVAL NEW.applied_limit_months MONTH)
 OR NEW.effective_date>DATE_ADD(NEW.destination_course_start_date,INTERVAL NEW.applied_limit_months MONTH)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer decision shape required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
 WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer actor required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM student s WHERE s.institution_id=NEW.institution_id AND s.id=NEW.student_id AND s.class_id=NEW.to_class_id AND s.transfer_revision=NEW.revision_after)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer current membership required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM class a JOIN class b ON b.institution_id=a.institution_id WHERE a.institution_id=NEW.institution_id AND a.id=NEW.from_class_id AND b.id=NEW.to_class_id AND a.course_edition_id=NEW.from_course_edition_id AND b.course_edition_id=NEW.to_course_edition_id)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer class course required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM student_enrollment a JOIN student_enrollment b ON b.institution_id=a.institution_id AND b.student_id=a.student_id
 WHERE a.institution_id=NEW.institution_id AND a.student_id=NEW.student_id AND a.id=NEW.source_enrollment_id AND a.course_edition_id=NEW.from_course_edition_id
 AND b.id=NEW.destination_enrollment_id AND b.course_edition_id=NEW.to_course_edition_id AND b.status='enrolled'
 AND ((NEW.from_course_edition_id=NEW.to_course_edition_id AND a.id=b.id AND a.status='enrolled') OR
 (NEW.from_course_edition_id<>NEW.to_course_edition_id AND a.id<>b.id AND a.status='withdrawn' AND a.completion_date=NEW.effective_date AND b.enrollment_date=NEW.effective_date)))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer enrollment decision required'; END IF;
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER transfer_event_update BEFORE UPDATE ON student_class_transfer_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer audit append only';
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER transfer_event_delete BEFORE DELETE ON student_class_transfer_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer audit append only';
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER student_transfer_revision_update BEFORE UPDATE ON student FOR EACH ROW BEGIN
 IF NEW.transfer_revision<0 OR (NEW.transfer_revision<>OLD.transfer_revision AND
 (NEW.transfer_revision<>OLD.transfer_revision+1 OR NEW.class_id<=>OLD.class_id))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer revision invalid'; END IF;
END$$
DELIMITER ;
