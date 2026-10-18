-- DML only; caller supplies @legacy_id, @legacy_code, @legacy_name.
-- Caller owns transaction, exclusive migration window, and parameter/conflict validation.
-- No UPDATE/DELETE on legacy tables. The migration record is not an enabled tenant.
INSERT INTO institution (id, name, code, status)
SELECT @legacy_id, @legacy_name, @legacy_code, 'provisioning'
WHERE NOT EXISTS (SELECT 1 FROM institution WHERE id = @legacy_id);

INSERT INTO institution_membership (id, institution_id, user_id, status)
SELECT UUID(), @legacy_id, u.id, 'active'
FROM user u
WHERE NOT EXISTS (
    SELECT 1 FROM institution_membership m WHERE m.user_id = u.id AND m.institution_id = @legacy_id
);

INSERT INTO institution_membership_role (membership_id, role)
SELECT m.id, CASE u.role WHEN 'admin' THEN 'InstitutionAdmin' WHEN 'teacher' THEN 'Teacher' WHEN 'student' THEN 'Student' END
FROM user u JOIN institution_membership m ON m.user_id = u.id AND m.institution_id = @legacy_id
WHERE NOT EXISTS (SELECT 1 FROM institution_membership_role r WHERE r.membership_id = m.id);
