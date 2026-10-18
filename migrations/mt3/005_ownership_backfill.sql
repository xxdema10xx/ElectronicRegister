-- MT-3A / MySQL 5.7. Run only through guarded test tooling.
UPDATE `biennium` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `study_area` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `study_path` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `subject` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `teacher` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `student` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `biennium_study_area` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `biennium_study_path` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `class` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `class_subject` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `exam` SET institution_id=@legacy_id, updated_at=updated_at WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `exam_student` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `grade` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `exam_submission` SET institution_id=@legacy_id, updated_at=updated_at WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `question` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `question_option` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `submission_answer` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `submission_answer_option` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `exam_violation` SET institution_id=@legacy_id WHERE institution_id IS NULL;
-- MT3A-BATCH
UPDATE `student` SET class_institution_id=IF(class_id IS NULL,NULL,institution_id);
-- MT3A-BATCH
UPDATE `exam` SET retake_institution_id=IF(retake_of_exam_id IS NULL,NULL,institution_id), updated_at=updated_at;
-- MT3A-BATCH
UPDATE `exam_submission` SET grade_institution_id=IF(grade_id IS NULL,NULL,institution_id), updated_at=updated_at;
-- MT3A-BATCH
UPDATE submission_answer a JOIN exam_submission s ON s.id=a.submission_id SET a.exam_id=s.exam_id;
-- MT3A-BATCH
UPDATE submission_answer_option b JOIN submission_answer a ON a.id=b.submission_answer_id SET b.question_id=a.question_id;
-- MT3A-BATCH
UPDATE audit_event a LEFT JOIN exam e ON a.entity_type='exam' AND e.id=a.entity_id LEFT JOIN exam_submission s ON a.entity_type='exam_submission' AND s.id=a.entity_id SET a.institution_id=COALESCE(e.institution_id,s.institution_id) WHERE a.institution_id IS NULL;
