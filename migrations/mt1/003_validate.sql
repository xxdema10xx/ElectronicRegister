-- Read-only diagnostics. All violation counts must be zero, except legacy_users is informational.
-- Supply @legacy_id on this connection. Compare immutable before/after legacy manifest separately.
SELECT COUNT(*) AS legacy_users FROM user;
SELECT COUNT(*) AS users_without_legacy_membership FROM user u
LEFT JOIN institution_membership m ON m.user_id = u.id AND m.institution_id = @legacy_id
WHERE m.id IS NULL;
SELECT COUNT(*) AS duplicate_memberships FROM (
    SELECT user_id, institution_id FROM institution_membership GROUP BY user_id, institution_id HAVING COUNT(*) > 1
) duplicates;
SELECT COUNT(*) AS orphan_campus FROM campus c LEFT JOIN institution i ON i.id = c.institution_id WHERE i.id IS NULL;
SELECT COUNT(*) AS orphan_memberships FROM institution_membership m
LEFT JOIN institution i ON i.id=m.institution_id LEFT JOIN user u ON u.id=m.user_id
WHERE i.id IS NULL OR u.id IS NULL;
SELECT COUNT(*) AS orphan_roles FROM institution_membership_role r
LEFT JOIN institution_membership m ON m.id=r.membership_id WHERE m.id IS NULL;
SELECT COUNT(*) AS wrong_legacy_roles FROM institution_membership m JOIN user u ON u.id=m.user_id
LEFT JOIN institution_membership_role r ON r.membership_id=m.id
WHERE m.institution_id=@legacy_id AND (r.role IS NULL OR r.role <> CASE u.role
WHEN 'admin' THEN 'InstitutionAdmin' WHEN 'teacher' THEN 'Teacher' WHEN 'student' THEN 'Student' END);
SELECT COUNT(*) AS enabled_legacy_institution FROM institution WHERE id=@legacy_id AND status<>'provisioning';
