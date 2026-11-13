-- MT-3A / MySQL 5.7. Run only through guarded test tooling.
DROP TRIGGER mt3a_audit_insert;
-- MT3A-BATCH
CREATE TRIGGER mt3a_audit_insert BEFORE INSERT ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;

 IF NEW.entity_type='platform' THEN
 IF NEW.institution_id IS NOT NULL OR NEW.entity_id IS NOT NULL OR NEW.before_json IS NOT NULL OR NEW.after_json IS NOT NULL OR NEW.action NOT LIKE 'platform.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A platform shape required'; END IF;
 ELSE
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'INSERT'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END IF;
 END;
-- MT3A-BATCH
DROP TRIGGER mt3a_audit_update;
-- MT3A-BATCH
CREATE TRIGGER mt3a_audit_update BEFORE UPDATE ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;
 IF NOT(NEW.institution_id <=> OLD.institution_id) OR NOT(NEW.entity_type <=> OLD.entity_type) OR NOT(NEW.entity_id <=> OLD.entity_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit attribution immutable'; END IF;
 IF NEW.entity_type='platform' THEN
 IF NEW.institution_id IS NOT NULL OR NEW.entity_id IS NOT NULL OR NEW.before_json IS NOT NULL OR NEW.after_json IS NOT NULL OR NEW.action NOT LIKE 'platform.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A platform shape required'; END IF;
 ELSE
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'UPDATE'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END IF;
 END;
