-- er-grade-scale-1 read-only validation: every violation count must be zero.
SELECT 'grade_scale_marker_missing',1-COUNT(*) FROM er_schema_release WHERE version='er-grade-scale-1';
SELECT 'grade_value_outside_scale',COUNT(*) FROM grade WHERE value<0 OR value>30 OR value<>TRUNCATE(value,0);
SELECT 'suggested_grade_outside_scale',COUNT(*) FROM exam_submission WHERE suggested_grade IS NOT NULL AND (suggested_grade<0 OR suggested_grade>30 OR suggested_grade<>TRUNCATE(suggested_grade,0));
SELECT 'passed_incoherent_with_grade',COUNT(*) FROM exam_submission s JOIN grade g ON g.id=s.grade_id WHERE NOT(s.passed<=>(g.value>=18));
SELECT 'grade_description_invalid',COUNT(*) FROM grade WHERE description IS NOT NULL AND (CHAR_LENGTH(TRIM(description))=0 OR description<>TRIM(description) OR CHAR_LENGTH(description)>200);
SELECT 'grade_batch_duplicate_student',COUNT(*) FROM (SELECT 1 FROM grade WHERE batch_event_id IS NOT NULL GROUP BY institution_id,batch_event_id,student_id HAVING COUNT(*)>1) d;
SELECT 'grade_columns_missing',2-COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='grade' AND COLUMN_NAME IN ('description','batch_event_id');
