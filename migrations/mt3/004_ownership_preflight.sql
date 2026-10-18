-- MT-3A / MySQL 5.7. Run only through guarded test tooling.
SELECT 'biennium_institution_exists' AS rule,COUNT(*) AS violations FROM `biennium` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'study_area_institution_exists' AS rule,COUNT(*) AS violations FROM `study_area` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'study_path_institution_exists' AS rule,COUNT(*) AS violations FROM `study_path` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'subject_institution_exists' AS rule,COUNT(*) AS violations FROM `subject` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'teacher_institution_exists' AS rule,COUNT(*) AS violations FROM `teacher` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'student_institution_exists' AS rule,COUNT(*) AS violations FROM `student` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'biennium_study_area_institution_exists' AS rule,COUNT(*) AS violations FROM `biennium_study_area` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'biennium_study_path_institution_exists' AS rule,COUNT(*) AS violations FROM `biennium_study_path` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'class_institution_exists' AS rule,COUNT(*) AS violations FROM `class` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'class_subject_institution_exists' AS rule,COUNT(*) AS violations FROM `class_subject` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'exam_institution_exists' AS rule,COUNT(*) AS violations FROM `exam` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'exam_student_institution_exists' AS rule,COUNT(*) AS violations FROM `exam_student` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'grade_institution_exists' AS rule,COUNT(*) AS violations FROM `grade` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'exam_submission_institution_exists' AS rule,COUNT(*) AS violations FROM `exam_submission` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'question_institution_exists' AS rule,COUNT(*) AS violations FROM `question` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'question_option_institution_exists' AS rule,COUNT(*) AS violations FROM `question_option` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'submission_answer_institution_exists' AS rule,COUNT(*) AS violations FROM `submission_answer` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'submission_answer_option_institution_exists' AS rule,COUNT(*) AS violations FROM `submission_answer_option` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'exam_violation_institution_exists' AS rule,COUNT(*) AS violations FROM `exam_violation` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'audit_event_institution_exists' AS rule,COUNT(*) AS violations FROM `audit_event` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;
-- MT3A-BATCH
SELECT 'fk_bsa_biennium' AS rule, COUNT(*) AS violations FROM `biennium_study_area` c LEFT JOIN `biennium` p ON p.id=c.biennium_id WHERE c.biennium_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_bsa_area' AS rule, COUNT(*) AS violations FROM `biennium_study_area` c LEFT JOIN `study_area` p ON p.id=c.study_area_id WHERE c.study_area_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_bsp_bsa' AS rule, COUNT(*) AS violations FROM `biennium_study_path` c LEFT JOIN `biennium_study_area` p ON p.id=c.biennium_study_area_id WHERE c.biennium_study_area_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_bsp_path' AS rule, COUNT(*) AS violations FROM `biennium_study_path` c LEFT JOIN `study_path` p ON p.id=c.study_path_id WHERE c.study_path_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_class_bsp' AS rule, COUNT(*) AS violations FROM `class` c LEFT JOIN `biennium_study_path` p ON p.id=c.biennium_study_path_id WHERE c.biennium_study_path_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_cs_class' AS rule, COUNT(*) AS violations FROM `class_subject` c LEFT JOIN `class` p ON p.id=c.class_id WHERE c.class_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_cs_subject' AS rule, COUNT(*) AS violations FROM `class_subject` c LEFT JOIN `subject` p ON p.id=c.subject_id WHERE c.subject_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_cs_teacher' AS rule, COUNT(*) AS violations FROM `class_subject` c LEFT JOIN `teacher` p ON p.id=c.teacher_id WHERE c.teacher_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_student_class' AS rule, COUNT(*) AS violations FROM `student` c LEFT JOIN `class` p ON p.id=c.class_id WHERE c.class_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_class_subject' AS rule, COUNT(*) AS violations FROM `exam` c LEFT JOIN `class_subject` p ON p.id=c.class_subject_id WHERE c.class_subject_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_retake_of' AS rule, COUNT(*) AS violations FROM `exam` c LEFT JOIN `exam` p ON p.id=c.retake_of_exam_id WHERE c.retake_of_exam_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_student_exam' AS rule, COUNT(*) AS violations FROM `exam_student` c LEFT JOIN `exam` p ON p.id=c.exam_id WHERE c.exam_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_student_student' AS rule, COUNT(*) AS violations FROM `exam_student` c LEFT JOIN `student` p ON p.id=c.student_id WHERE c.student_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_grade_student' AS rule, COUNT(*) AS violations FROM `grade` c LEFT JOIN `student` p ON p.id=c.student_id WHERE c.student_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_grade_class_subject' AS rule, COUNT(*) AS violations FROM `grade` c LEFT JOIN `class_subject` p ON p.id=c.class_subject_id WHERE c.class_subject_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_submission_exam' AS rule, COUNT(*) AS violations FROM `exam_submission` c LEFT JOIN `exam` p ON p.id=c.exam_id WHERE c.exam_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_submission_student' AS rule, COUNT(*) AS violations FROM `exam_submission` c LEFT JOIN `student` p ON p.id=c.student_id WHERE c.student_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_submission_grade' AS rule, COUNT(*) AS violations FROM `exam_submission` c LEFT JOIN `grade` p ON p.id=c.grade_id WHERE c.grade_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_question_exam' AS rule, COUNT(*) AS violations FROM `question` c LEFT JOIN `exam` p ON p.id=c.exam_id WHERE c.exam_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_question_option_question' AS rule, COUNT(*) AS violations FROM `question_option` c LEFT JOIN `question` p ON p.id=c.question_id WHERE c.question_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_submission_answer_submission' AS rule, COUNT(*) AS violations FROM `submission_answer` c LEFT JOIN `exam_submission` p ON p.id=c.submission_id WHERE c.submission_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_submission_answer_question' AS rule, COUNT(*) AS violations FROM `submission_answer` c LEFT JOIN `question` p ON p.id=c.question_id WHERE c.question_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_submission_answer_option_answer' AS rule, COUNT(*) AS violations FROM `submission_answer_option` c LEFT JOIN `submission_answer` p ON p.id=c.submission_answer_id WHERE c.submission_answer_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_submission_answer_option_option' AS rule, COUNT(*) AS violations FROM `submission_answer_option` c LEFT JOIN `question_option` p ON p.id=c.option_id WHERE c.option_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'fk_exam_violation_submission' AS rule, COUNT(*) AS violations FROM `exam_violation` c LEFT JOIN `exam_submission` p ON p.id=c.submission_id WHERE c.submission_id IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));
-- MT3A-BATCH
SELECT 'student_profile_owner' AS rule,COUNT(*) AS violations FROM institution_student_profile l JOIN `student` p ON p.id=l.student_id WHERE l.institution_id <> COALESCE(p.institution_id,@legacy_id);
-- MT3A-BATCH
SELECT 'teacher_profile_owner' AS rule,COUNT(*) AS violations FROM institution_teacher_profile l JOIN `teacher` p ON p.id=l.teacher_id WHERE l.institution_id <> COALESCE(p.institution_id,@legacy_id);
-- MT3A-BATCH
SELECT 'answer_exam' AS rule,COUNT(*) AS violations FROM submission_answer a JOIN exam_submission s ON s.id=a.submission_id JOIN question q ON q.id=a.question_id WHERE s.exam_id<>q.exam_id;
-- MT3A-BATCH
SELECT 'selected_option_question' AS rule,COUNT(*) AS violations FROM submission_answer_option b JOIN submission_answer a ON a.id=b.submission_answer_id JOIN question_option o ON o.id=b.option_id WHERE a.question_id<>o.question_id;
-- MT3A-BATCH
SELECT 'audit_unresolved' AS rule,COUNT(*) AS violations FROM audit_event a LEFT JOIN exam e ON a.entity_type='exam' AND e.id=a.entity_id LEFT JOIN exam_submission s ON a.entity_type='exam_submission' AND s.id=a.entity_id WHERE NOT (a.entity_type='platform' AND a.action LIKE 'platform.%' AND a.entity_id IS NULL AND a.institution_id IS NULL AND a.before_json IS NULL AND a.after_json IS NULL) AND ((a.entity_type NOT IN ('exam','exam_submission')) OR (a.institution_id IS NULL AND e.id IS NULL AND s.id IS NULL) OR (a.institution_id IS NOT NULL AND COALESCE(e.institution_id,s.institution_id) IS NOT NULL AND a.institution_id<>COALESCE(e.institution_id,s.institution_id)));
