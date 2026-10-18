-- MT3A follow-up to immutable 002. No identity/membership/profile creation.
INSERT INTO institution_student_profile(institution_id,membership_id,student_id)
SELECT m.institution_id,m.id,u.student_id
FROM user u JOIN institution_membership m ON m.user_id=u.id AND m.institution_id=@legacy_id
JOIN student s ON s.id=u.student_id AND s.institution_id=m.institution_id
WHERE NOT EXISTS (SELECT 1 FROM institution_student_profile p WHERE p.institution_id=m.institution_id AND p.membership_id=m.id AND p.student_id=u.student_id);
INSERT INTO institution_teacher_profile(institution_id,membership_id,teacher_id)
SELECT m.institution_id,m.id,u.teacher_id
FROM user u JOIN institution_membership m ON m.user_id=u.id AND m.institution_id=@legacy_id
JOIN teacher t ON t.id=u.teacher_id AND t.institution_id=m.institution_id
WHERE NOT EXISTS (SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=m.institution_id AND p.membership_id=m.id AND p.teacher_id=u.teacher_id);
