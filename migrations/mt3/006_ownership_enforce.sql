-- MT-3A / MySQL 5.7. Run only through guarded test tooling.
ALTER TABLE `biennium` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `biennium` ADD UNIQUE KEY uq_mt3a_biennium_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `biennium` ADD CONSTRAINT fk_mt3a_biennium_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `study_area` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `study_area` ADD UNIQUE KEY uq_mt3a_study_area_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `study_area` ADD CONSTRAINT fk_mt3a_study_area_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `study_path` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `study_path` ADD UNIQUE KEY uq_mt3a_study_path_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `study_path` ADD CONSTRAINT fk_mt3a_study_path_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `subject` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `subject` ADD UNIQUE KEY uq_mt3a_subject_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `subject` ADD CONSTRAINT fk_mt3a_subject_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `teacher` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `teacher` ADD UNIQUE KEY uq_mt3a_teacher_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `teacher` ADD CONSTRAINT fk_mt3a_teacher_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `student` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `student` ADD UNIQUE KEY uq_mt3a_student_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `student` ADD CONSTRAINT fk_mt3a_student_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `biennium_study_area` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `biennium_study_area` ADD UNIQUE KEY uq_mt3a_biennium_study_area_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `biennium_study_area` ADD CONSTRAINT fk_mt3a_biennium_study_area_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `biennium_study_path` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `biennium_study_path` ADD UNIQUE KEY uq_mt3a_biennium_study_path_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `biennium_study_path` ADD CONSTRAINT fk_mt3a_biennium_study_path_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `class` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `class` ADD UNIQUE KEY uq_mt3a_class_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `class` ADD CONSTRAINT fk_mt3a_class_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `class_subject` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `class_subject` ADD UNIQUE KEY uq_mt3a_class_subject_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `class_subject` ADD CONSTRAINT fk_mt3a_class_subject_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `exam` ADD UNIQUE KEY uq_mt3a_exam_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `exam` ADD CONSTRAINT fk_mt3a_exam_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_student` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `exam_student` ADD CONSTRAINT fk_mt3a_exam_student_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `grade` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `grade` ADD UNIQUE KEY uq_mt3a_grade_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `grade` ADD CONSTRAINT fk_mt3a_grade_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_submission` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `exam_submission` ADD UNIQUE KEY uq_mt3a_exam_submission_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `exam_submission` ADD CONSTRAINT fk_mt3a_exam_submission_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `question` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `question` ADD UNIQUE KEY uq_mt3a_question_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `question` ADD CONSTRAINT fk_mt3a_question_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `question_option` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `question_option` ADD UNIQUE KEY uq_mt3a_question_option_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `question_option` ADD CONSTRAINT fk_mt3a_question_option_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `submission_answer` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `submission_answer` ADD UNIQUE KEY uq_mt3a_submission_answer_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `submission_answer` ADD CONSTRAINT fk_mt3a_submission_answer_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `submission_answer_option` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `submission_answer_option` ADD CONSTRAINT fk_mt3a_submission_answer_option_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_violation` MODIFY institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE `exam_violation` ADD UNIQUE KEY uq_mt3a_exam_violation_owner (institution_id,id);
-- MT3A-BATCH
ALTER TABLE `exam_violation` ADD CONSTRAINT fk_mt3a_exam_violation_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE audit_event ADD KEY idx_mt3a_audit_owner_created (institution_id,created_at), ADD CONSTRAINT fk_mt3a_audit_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE submission_answer MODIFY exam_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE submission_answer_option MODIFY question_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
-- MT3A-BATCH
ALTER TABLE exam_submission ADD UNIQUE KEY uq_mt3a_submission_exam (institution_id,exam_id,id);
-- MT3A-BATCH
ALTER TABLE question ADD UNIQUE KEY uq_mt3a_question_exam (institution_id,exam_id,id);
-- MT3A-BATCH
ALTER TABLE submission_answer ADD UNIQUE KEY uq_mt3a_answer_question (institution_id,question_id,id);
-- MT3A-BATCH
ALTER TABLE question_option ADD UNIQUE KEY uq_mt3a_option_question (institution_id,question_id,id);
-- MT3A-BATCH
ALTER TABLE `biennium_study_area` ADD KEY idx_mt3a_bsa_biennium (institution_id,biennium_id), ADD CONSTRAINT fk_mt3a_bsa_biennium FOREIGN KEY (institution_id,biennium_id) REFERENCES `biennium` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `biennium_study_area` ADD KEY idx_mt3a_bsa_area (institution_id,study_area_id), ADD CONSTRAINT fk_mt3a_bsa_area FOREIGN KEY (institution_id,study_area_id) REFERENCES `study_area` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `biennium_study_path` ADD KEY idx_mt3a_bsp_bsa (institution_id,biennium_study_area_id), ADD CONSTRAINT fk_mt3a_bsp_bsa FOREIGN KEY (institution_id,biennium_study_area_id) REFERENCES `biennium_study_area` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `biennium_study_path` ADD KEY idx_mt3a_bsp_path (institution_id,study_path_id), ADD CONSTRAINT fk_mt3a_bsp_path FOREIGN KEY (institution_id,study_path_id) REFERENCES `study_path` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `class` ADD KEY idx_mt3a_class_bsp (institution_id,biennium_study_path_id), ADD CONSTRAINT fk_mt3a_class_bsp FOREIGN KEY (institution_id,biennium_study_path_id) REFERENCES `biennium_study_path` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `class_subject` ADD KEY idx_mt3a_cs_class (institution_id,class_id), ADD CONSTRAINT fk_mt3a_cs_class FOREIGN KEY (institution_id,class_id) REFERENCES `class` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `class_subject` ADD KEY idx_mt3a_cs_subject (institution_id,subject_id), ADD CONSTRAINT fk_mt3a_cs_subject FOREIGN KEY (institution_id,subject_id) REFERENCES `subject` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `class_subject` ADD KEY idx_mt3a_cs_teacher (institution_id,teacher_id), ADD CONSTRAINT fk_mt3a_cs_teacher FOREIGN KEY (institution_id,teacher_id) REFERENCES `teacher` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `student` ADD KEY idx_mt3a_student_class (class_institution_id,class_id), ADD CONSTRAINT fk_mt3a_student_class FOREIGN KEY (class_institution_id,class_id) REFERENCES `class` (institution_id,id) ON DELETE SET NULL ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam` ADD KEY idx_mt3a_exam_class_subject (institution_id,class_subject_id), ADD CONSTRAINT fk_mt3a_exam_class_subject FOREIGN KEY (institution_id,class_subject_id) REFERENCES `class_subject` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam` ADD KEY idx_mt3a_exam_retake_of (retake_institution_id,retake_of_exam_id), ADD CONSTRAINT fk_mt3a_exam_retake_of FOREIGN KEY (retake_institution_id,retake_of_exam_id) REFERENCES `exam` (institution_id,id) ON DELETE SET NULL ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_student` ADD KEY idx_mt3a_exam_student_exam (institution_id,exam_id), ADD CONSTRAINT fk_mt3a_exam_student_exam FOREIGN KEY (institution_id,exam_id) REFERENCES `exam` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_student` ADD KEY idx_mt3a_exam_student_student (institution_id,student_id), ADD CONSTRAINT fk_mt3a_exam_student_student FOREIGN KEY (institution_id,student_id) REFERENCES `student` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `grade` ADD KEY idx_mt3a_grade_student (institution_id,student_id), ADD CONSTRAINT fk_mt3a_grade_student FOREIGN KEY (institution_id,student_id) REFERENCES `student` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `grade` ADD KEY idx_mt3a_grade_class_subject (institution_id,class_subject_id), ADD CONSTRAINT fk_mt3a_grade_class_subject FOREIGN KEY (institution_id,class_subject_id) REFERENCES `class_subject` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_submission` ADD KEY idx_mt3a_exam_submission_exam (institution_id,exam_id), ADD CONSTRAINT fk_mt3a_exam_submission_exam FOREIGN KEY (institution_id,exam_id) REFERENCES `exam` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_submission` ADD KEY idx_mt3a_exam_submission_student (institution_id,student_id), ADD CONSTRAINT fk_mt3a_exam_submission_student FOREIGN KEY (institution_id,student_id) REFERENCES `student` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_submission` ADD KEY idx_mt3a_exam_submission_grade (grade_institution_id,grade_id), ADD CONSTRAINT fk_mt3a_exam_submission_grade FOREIGN KEY (grade_institution_id,grade_id) REFERENCES `grade` (institution_id,id) ON DELETE SET NULL ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `question` ADD KEY idx_mt3a_question_exam (institution_id,exam_id), ADD CONSTRAINT fk_mt3a_question_exam FOREIGN KEY (institution_id,exam_id) REFERENCES `exam` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `question_option` ADD KEY idx_mt3a_question_option_question (institution_id,question_id), ADD CONSTRAINT fk_mt3a_question_option_question FOREIGN KEY (institution_id,question_id) REFERENCES `question` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `submission_answer` ADD KEY idx_mt3a_submission_answer_submission (institution_id,exam_id,submission_id), ADD CONSTRAINT fk_mt3a_submission_answer_submission FOREIGN KEY (institution_id,exam_id,submission_id) REFERENCES `exam_submission` (institution_id,exam_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `submission_answer` ADD KEY idx_mt3a_submission_answer_question (institution_id,exam_id,question_id), ADD CONSTRAINT fk_mt3a_submission_answer_question FOREIGN KEY (institution_id,exam_id,question_id) REFERENCES `question` (institution_id,exam_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `submission_answer_option` ADD KEY idx_mt3a_submission_answer_option_answer (institution_id,question_id,submission_answer_id), ADD CONSTRAINT fk_mt3a_submission_answer_option_answer FOREIGN KEY (institution_id,question_id,submission_answer_id) REFERENCES `submission_answer` (institution_id,question_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `submission_answer_option` ADD KEY idx_mt3a_submission_answer_option_option (institution_id,question_id,option_id), ADD CONSTRAINT fk_mt3a_submission_answer_option_option FOREIGN KEY (institution_id,question_id,option_id) REFERENCES `question_option` (institution_id,question_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `exam_violation` ADD KEY idx_mt3a_exam_violation_submission (institution_id,submission_id), ADD CONSTRAINT fk_mt3a_exam_violation_submission FOREIGN KEY (institution_id,submission_id) REFERENCES `exam_submission` (institution_id,id) ON DELETE CASCADE ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE institution_student_profile ADD KEY idx_mt3a_student_profile (institution_id,student_id), ADD CONSTRAINT fk_mt3a_student_profile_owner FOREIGN KEY (institution_id,student_id) REFERENCES `student` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE institution_teacher_profile ADD KEY idx_mt3a_teacher_profile (institution_id,teacher_id), ADD CONSTRAINT fk_mt3a_teacher_profile_owner FOREIGN KEY (institution_id,teacher_id) REFERENCES `teacher` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
-- MT3A-BATCH
ALTER TABLE `biennium` DROP INDEX uq_biennium, ADD UNIQUE KEY uq_biennium (institution_id,start_year,end_year);
-- MT3A-BATCH
ALTER TABLE `study_area` DROP INDEX uq_study_area_name, ADD UNIQUE KEY uq_study_area_name (institution_id,name);
-- MT3A-BATCH
ALTER TABLE `study_path` DROP INDEX uq_study_path_name, ADD UNIQUE KEY uq_study_path_name (institution_id,name);
-- MT3A-BATCH
ALTER TABLE `subject` DROP INDEX uq_subject_name, ADD UNIQUE KEY uq_subject_name (institution_id,name);
