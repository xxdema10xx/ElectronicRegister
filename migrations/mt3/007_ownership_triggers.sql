-- MT-3A / MySQL 5.7. Run only through guarded test tooling.
CREATE TABLE mt3a_legacy_scope (scope ENUM('legacy') NOT NULL PRIMARY KEY, institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL UNIQUE, CONSTRAINT fk_mt3a_legacy_scope FOREIGN KEY(institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT) ENGINE=InnoDB;
-- MT3A-BATCH
CREATE TRIGGER mt3a_biennium_insert BEFORE INSERT ON `biennium` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_biennium_update BEFORE UPDATE ON `biennium` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_study_area_insert BEFORE INSERT ON `study_area` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_study_area_update BEFORE UPDATE ON `study_area` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_study_path_insert BEFORE INSERT ON `study_path` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_study_path_update BEFORE UPDATE ON `study_path` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_subject_insert BEFORE INSERT ON `subject` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_subject_update BEFORE UPDATE ON `subject` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_teacher_insert BEFORE INSERT ON `teacher` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_teacher_update BEFORE UPDATE ON `teacher` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_student_insert BEFORE INSERT ON `student` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=COALESCE((SELECT institution_id FROM class WHERE id=NEW.class_id),(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy')); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.class_institution_id=IF(NEW.class_id IS NULL,NULL,NEW.institution_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_student_update BEFORE UPDATE ON `student` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.class_institution_id=IF(NEW.class_id IS NULL,NULL,NEW.institution_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_biennium_study_area_insert BEFORE INSERT ON `biennium_study_area` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `biennium` WHERE id=NEW.biennium_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_biennium_study_area_update BEFORE UPDATE ON `biennium_study_area` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_biennium_study_path_insert BEFORE INSERT ON `biennium_study_path` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `biennium_study_area` WHERE id=NEW.biennium_study_area_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_biennium_study_path_update BEFORE UPDATE ON `biennium_study_path` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_class_insert BEFORE INSERT ON `class` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `biennium_study_path` WHERE id=NEW.biennium_study_path_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_class_update BEFORE UPDATE ON `class` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_class_subject_insert BEFORE INSERT ON `class_subject` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `class` WHERE id=NEW.class_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_class_subject_update BEFORE UPDATE ON `class_subject` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_insert BEFORE INSERT ON `exam` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `class_subject` WHERE id=NEW.class_subject_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.retake_institution_id=IF(NEW.retake_of_exam_id IS NULL,NULL,NEW.institution_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_update BEFORE UPDATE ON `exam` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.retake_institution_id=IF(NEW.retake_of_exam_id IS NULL,NULL,NEW.institution_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_student_insert BEFORE INSERT ON `exam_student` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam` WHERE id=NEW.exam_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_student_update BEFORE UPDATE ON `exam_student` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_grade_insert BEFORE INSERT ON `grade` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `student` WHERE id=NEW.student_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_grade_update BEFORE UPDATE ON `grade` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_submission_insert BEFORE INSERT ON `exam_submission` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam` WHERE id=NEW.exam_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.grade_institution_id=IF(NEW.grade_id IS NULL,NULL,NEW.institution_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_submission_update BEFORE UPDATE ON `exam_submission` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.grade_institution_id=IF(NEW.grade_id IS NULL,NULL,NEW.institution_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_question_insert BEFORE INSERT ON `question` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam` WHERE id=NEW.exam_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_question_update BEFORE UPDATE ON `question` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_question_option_insert BEFORE INSERT ON `question_option` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `question` WHERE id=NEW.question_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_question_option_update BEFORE UPDATE ON `question_option` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_submission_answer_insert BEFORE INSERT ON `submission_answer` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam_submission` WHERE id=NEW.submission_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.exam_id=(SELECT exam_id FROM exam_submission WHERE id=NEW.submission_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_submission_answer_update BEFORE UPDATE ON `submission_answer` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.exam_id=(SELECT exam_id FROM exam_submission WHERE id=NEW.submission_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_submission_answer_option_insert BEFORE INSERT ON `submission_answer_option` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `submission_answer` WHERE id=NEW.submission_answer_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.question_id=(SELECT question_id FROM submission_answer WHERE id=NEW.submission_answer_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_submission_answer_option_update BEFORE UPDATE ON `submission_answer_option` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.question_id=(SELECT question_id FROM submission_answer WHERE id=NEW.submission_answer_id);
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_violation_insert BEFORE INSERT ON `exam_violation` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam_submission` WHERE id=NEW.submission_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_exam_violation_update BEFORE UPDATE ON `exam_violation` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_audit_insert BEFORE INSERT ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;

 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'INSERT'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END;
-- MT3A-BATCH
CREATE TRIGGER mt3a_audit_update BEFORE UPDATE ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;
 IF NOT(NEW.institution_id <=> OLD.institution_id) OR NOT(NEW.entity_type <=> OLD.entity_type) OR NOT(NEW.entity_id <=> OLD.entity_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit attribution immutable'; END IF;
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'UPDATE'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END;
