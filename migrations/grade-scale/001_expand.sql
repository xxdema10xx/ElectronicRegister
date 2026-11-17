-- er-grade-scale-1. Grades move from tenths (1-10, pass 6, two decimals) to thirtieths (0-30, pass 18, integers).
-- Apply ONCE after er-platform-identity-1, through tools/mysql84-release.cjs successor (DEFINER binding)
-- on an attested instance. The ALTER implicitly commits and fails on a replay (duplicate column), so a
-- second run stops before touching data. The conversion and the marker commit together, in one
-- transaction, and every UPDATE is guarded by the marker: values are converted at most once.
-- Grade values are never written to logs or audit by this script; historical audit JSON is untouched.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

-- Optional description of an off-platform test, and the client event of the batch that created a
-- manual grade (idempotency of POST /api/institution/grades/batch: one grade per student per event).
ALTER TABLE grade
 ADD COLUMN description varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
 ADD COLUMN batch_event_id char(36) CHARACTER SET ascii DEFAULT NULL,
 ADD UNIQUE KEY uq_grade_batch_student (institution_id,batch_event_id,student_id);

START TRANSACTION;
-- new = ROUND(old x 3); an insufficient grade (old < 6) stays insufficient: at most 17 (5.99 x 3 = 17.97 -> 17).
-- Only value changes: the MT3A owner checked by mt3a_grade_update is untouched.
UPDATE grade
 SET value=IF(value<6,LEAST(ROUND(value*3),17),ROUND(value*3))
 WHERE NOT EXISTS(SELECT 1 FROM er_schema_release WHERE version='er-grade-scale-1');
UPDATE exam_submission
 SET suggested_grade=IF(suggested_grade<6,LEAST(ROUND(suggested_grade*3),17),ROUND(suggested_grade*3))
 WHERE suggested_grade IS NOT NULL AND NOT EXISTS(SELECT 1 FROM er_schema_release WHERE version='er-grade-scale-1');
-- Passed follows the official grade only (>= 18); submissions without a grade keep their provisional value.
UPDATE exam_submission s JOIN grade g ON g.id=s.grade_id
 SET s.passed=(g.value>=18)
 WHERE NOT EXISTS(SELECT 1 FROM er_schema_release WHERE version='er-grade-scale-1');
INSERT INTO er_schema_release(version) VALUES('er-grade-scale-1');
COMMIT;
