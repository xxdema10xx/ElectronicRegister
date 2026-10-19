-- MT4C EXPAND. Guarded test runner only; no destructive DOWN.
ALTER TABLE student_enrollment ADD COLUMN revision BIGINT NOT NULL DEFAULT 0;
ALTER TABLE institution_membership ADD UNIQUE KEY uq_mt4c_membership_actor (institution_id,id,user_id);
CREATE TABLE course_administration_event (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 actor_user_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 actor_membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 course_edition_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 enrollment_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 campus_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 class_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 action VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 previous_status VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NULL,
 new_status VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NULL,
 previous_completion_date DATE NULL,
 new_completion_date DATE NULL,
 revision_before BIGINT NULL,
 revision_after BIGINT NULL,
 reason VARCHAR(1000) NULL,
 request_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
 before_json LONGTEXT NULL,
 after_json LONGTEXT NOT NULL,
 occurred_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY (institution_id,id),
 KEY idx_mt4c_enrollment_history (institution_id,enrollment_id,revision_after,occurred_at_utc,id),
 KEY idx_mt4c_edition_history (institution_id,course_edition_id,occurred_at_utc,id),
 CONSTRAINT fk_mt4c_actor FOREIGN KEY (actor_user_id) REFERENCES user(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_mt4c_membership FOREIGN KEY (institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_mt4c_edition FOREIGN KEY (institution_id,course_edition_id) REFERENCES course_edition(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_mt4c_enrollment FOREIGN KEY (institution_id,enrollment_id) REFERENCES student_enrollment(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_mt4c_campus FOREIGN KEY (institution_id,campus_id) REFERENCES campus(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_mt4c_class FOREIGN KEY (institution_id,class_id) REFERENCES class(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
DELIMITER $$
CREATE TRIGGER mt4c_enrollment_revision_insert BEFORE INSERT ON student_enrollment FOR EACH ROW BEGIN
 IF NEW.revision<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C initial revision required'; END IF;
END$$
CREATE TRIGGER mt4c_enrollment_revision_update BEFORE UPDATE ON student_enrollment FOR EACH ROW BEGIN
 IF NEW.revision<OLD.revision OR NEW.revision>OLD.revision+1 OR
 ((NOT(NEW.status <=> OLD.status) OR NOT(NEW.completion_date <=> OLD.completion_date)) AND NEW.revision<>OLD.revision+1)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C revision mismatch'; END IF;
END$$
CREATE TRIGGER mt4c_course_event_insert BEFORE INSERT ON course_administration_event FOR EACH ROW BEGIN
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
 WHERE m.id=NEW.actor_membership_id AND m.institution_id=NEW.institution_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C verified actor required'; END IF;
 IF NOT JSON_VALID(NEW.after_json) OR (NEW.before_json IS NOT NULL AND NOT JSON_VALID(NEW.before_json)) OR
 NEW.action NOT IN ('course-edition.create','course-edition.update','campus.link','class.assign','enrollment.create','enrollment.transition','enrollment.completion-date.correct')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C event shape invalid'; END IF;
 IF NEW.action LIKE 'enrollment.%' THEN
  IF NEW.enrollment_id IS NULL OR NEW.campus_id IS NOT NULL OR NEW.class_id IS NOT NULL OR NOT EXISTS(
   SELECT 1 FROM student_enrollment e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.enrollment_id AND e.course_edition_id=NEW.course_edition_id
   AND e.status=NEW.new_status AND e.completion_date <=> NEW.new_completion_date AND e.revision=NEW.revision_after)
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C enrollment snapshot invalid'; END IF;
 ELSEIF NEW.enrollment_id IS NOT NULL OR NEW.previous_status IS NOT NULL OR NEW.new_status IS NOT NULL OR NEW.revision_before IS NOT NULL OR NEW.revision_after IS NOT NULL OR NEW.reason IS NOT NULL OR NEW.request_hash IS NOT NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C catalog shape invalid';
 END IF;
 IF NEW.action IN ('enrollment.transition','enrollment.completion-date.correct') THEN
  IF NEW.reason IS NULL OR NEW.reason NOT REGEXP '[^[:space:]]' OR NEW.request_hash IS NULL OR NEW.request_hash NOT REGEXP '^[0-9A-F]{64}$'
   OR NEW.revision_before IS NULL OR NEW.revision_before<0 OR NEW.revision_after<>NEW.revision_before+1 OR NEW.new_completion_date IS NULL
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C lifecycle shape invalid'; END IF;
  IF NEW.action='enrollment.transition' AND (NOT(NEW.previous_status <=> 'enrolled') OR NEW.new_status NOT IN ('completed','withdrawn','cancelled')) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C transition invalid'; END IF;
  IF NEW.action='enrollment.completion-date.correct' AND (NEW.new_status NOT IN ('completed','withdrawn','cancelled') OR NOT(NEW.previous_status <=> NEW.new_status)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C correction invalid'; END IF;
 END IF;
 IF NEW.action='enrollment.create' AND (NEW.previous_status IS NOT NULL OR NEW.revision_before IS NOT NULL OR NEW.revision_after<>0) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C creation shape invalid'; END IF;
 IF (NEW.action='campus.link') <> (NEW.campus_id IS NOT NULL) OR (NEW.action='class.assign') <> (NEW.class_id IS NOT NULL) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C link shape invalid'; END IF;
 IF NEW.action='campus.link' AND NOT EXISTS(SELECT 1 FROM course_edition_campus c WHERE c.institution_id=NEW.institution_id AND c.course_edition_id=NEW.course_edition_id AND c.campus_id=NEW.campus_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C campus snapshot invalid'; END IF;
 IF NEW.action='class.assign' AND NOT EXISTS(SELECT 1 FROM class c WHERE c.institution_id=NEW.institution_id AND c.course_edition_id=NEW.course_edition_id AND c.id=NEW.class_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C class snapshot invalid'; END IF;
 SET NEW.occurred_at_utc=UTC_TIMESTAMP(6);
END$$
CREATE TRIGGER mt4c_course_event_update BEFORE UPDATE ON course_administration_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C append only';
END$$
CREATE TRIGGER mt4c_course_event_delete BEFORE DELETE ON course_administration_event FOR EACH ROW BEGIN
 -- Privileged maintenance ONLY for disposable integration fixtures in the authorized test DB.
 -- Never usable for holding/real institutions; no HTTP code sets this variable.
 IF NOT(DATABASE()='electronic_register_test' AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND COALESCE(@er_mt4c_purge_owner,'')=OLD.institution_id AND EXISTS(
   SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND i.code REGEXP '^MT4B_[0-9a-f]{32}$'))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C append only'; END IF;
END$$
DELIMITER ;
