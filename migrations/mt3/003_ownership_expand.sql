-- MT-3A / MySQL 5.7. Run only through guarded test tooling.
ALTER TABLE `biennium` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `study_area` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `study_path` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `subject` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `teacher` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `student` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `biennium_study_area` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `biennium_study_path` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `class` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `class_subject` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `exam` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `exam_student` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `grade` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `exam_submission` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `question` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `question_option` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `submission_answer` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `submission_answer_option` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `exam_violation` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `audit_event` ADD COLUMN institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `student` ADD COLUMN class_institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `exam` ADD COLUMN retake_institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE `exam_submission` ADD COLUMN grade_institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE submission_answer ADD COLUMN exam_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
-- MT3A-BATCH
ALTER TABLE submission_answer_option ADD COLUMN question_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL;
