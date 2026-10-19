-- DML only. Caller must validate explicit LEGACY_MT1 holding, compatible roles and collisions first.
-- No change to any legacy column; do not create users/profiles or assign unowned profiles by guesswork.
INSERT INTO institution_student_profile (institution_id,membership_id,student_id)
SELECT @legacy_id,m.id,u.student_id FROM user u JOIN institution_membership m
ON m.user_id=u.id AND m.institution_id=@legacy_id
WHERE u.student_id IS NOT NULL AND NOT EXISTS (
 SELECT 1 FROM institution_student_profile p WHERE p.institution_id=@legacy_id AND p.membership_id=m.id AND p.student_id=u.student_id);

INSERT INTO institution_teacher_profile (institution_id,membership_id,teacher_id)
SELECT @legacy_id,m.id,u.teacher_id FROM user u JOIN institution_membership m
ON m.user_id=u.id AND m.institution_id=@legacy_id
WHERE u.teacher_id IS NOT NULL AND NOT EXISTS (
 SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=@legacy_id AND p.membership_id=m.id AND p.teacher_id=u.teacher_id);
