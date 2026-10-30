-- Additive attributed ledger for explicitly requested historical profile acquisition.
CREATE TABLE institution_profile_acquisition (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 target_user_id char(36) CHARACTER SET ascii NOT NULL,
 target_membership_id char(36) CHARACTER SET ascii NOT NULL,
 student_id char(36) CHARACTER SET ascii DEFAULT NULL,
 teacher_id char(36) CHARACTER SET ascii DEFAULT NULL,
 status varchar(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 reason varchar(1000) COLLATE utf8mb4_unicode_ci NOT NULL,
 created_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 PRIMARY KEY(institution_id,id),
 CONSTRAINT fk_profile_acquisition_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id) ON DELETE RESTRICT,
 CONSTRAINT fk_profile_acquisition_target FOREIGN KEY(institution_id,target_membership_id,target_user_id) REFERENCES institution_membership(institution_id,id,user_id) ON DELETE RESTRICT,
 CONSTRAINT fk_profile_acquisition_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id) ON DELETE RESTRICT,
 CONSTRAINT fk_profile_acquisition_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
DELIMITER $$
CREATE TRIGGER profile_acquisition_insert BEFORE INSERT ON institution_profile_acquisition FOR EACH ROW
BEGIN
 IF (NEW.student_id IS NULL)=(NEW.teacher_id IS NULL) OR NEW.status NOT IN ('linked','requires-review') OR CHAR_LENGTH(TRIM(NEW.reason))=0 OR NEW.request_hash NOT REGEXP '^[A-F0-9]{64}$' THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid profile acquisition event';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM institution i JOIN institution_membership m ON m.institution_id=i.id JOIN institution_membership_role r ON r.membership_id=m.id WHERE i.id=NEW.institution_id AND i.status='active' AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND r.role='InstitutionAdmin') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unauthorized profile acquisition actor';
 END IF;
 IF NEW.status='linked' AND NOT (
  (NEW.student_id IS NOT NULL AND EXISTS(SELECT 1 FROM institution_student_profile p JOIN `user` u ON u.id=NEW.target_user_id WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.target_membership_id AND p.student_id=NEW.student_id AND u.student_id=NEW.student_id)) OR
  (NEW.teacher_id IS NOT NULL AND EXISTS(SELECT 1 FROM institution_teacher_profile p JOIN `user` u ON u.id=NEW.target_user_id WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.target_membership_id AND p.teacher_id=NEW.teacher_id AND u.teacher_id=NEW.teacher_id))
 ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unverified historical profile link'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id WHERE m.institution_id=NEW.institution_id AND m.id=NEW.target_membership_id AND m.user_id=NEW.target_user_id AND m.status='active' AND r.role=IF(NEW.student_id IS NOT NULL,'Student','Teacher')) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Unauthorized profile acquisition target';
 END IF;
END$$
CREATE TRIGGER profile_acquisition_no_update BEFORE UPDATE ON institution_profile_acquisition FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Profile acquisition is append-only'; END$$
CREATE TRIGGER profile_acquisition_no_delete BEFORE DELETE ON institution_profile_acquisition FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Profile acquisition is append-only'; END$$
DELIMITER ;
