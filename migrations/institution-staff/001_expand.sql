-- 20261027.1: maintenance window required; MySQL DDL implicitly commits.
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
ALTER TABLE institution_membership_role MODIFY role ENUM('InstitutionAdmin','Teacher','Student','Staff') COLLATE utf8mb4_unicode_ci NOT NULL;

DROP TRIGGER `mt5c_invitation_insert`;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_invitation_insert BEFORE INSERT ON institution_invitation FOR EACH ROW BEGIN
 IF NEW.kind NOT IN ('FirstAdmin','Membership','Recovery') OR NEW.status<>'pending' OR NEW.attempts<>0 OR NEW.consumed_membership_id IS NOT NULL
  OR NEW.token_hash NOT REGEXP BINARY '^[A-F0-9]{64}$' OR NEW.channel_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'
  OR NEW.expires_at_utc<=NEW.created_at_utc OR JSON_TYPE(NEW.roles)<>'ARRAY' OR JSON_LENGTH(NEW.roles) NOT BETWEEN 1 AND 4
  OR JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[0]'))<>'STRING'
  OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[0]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff')
  OR (JSON_LENGTH(NEW.roles)>1 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[1]'))<>'STRING' OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[1]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff')))
  OR (JSON_LENGTH(NEW.roles)>2 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[2]'))<>'STRING' OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[2]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff')))
  OR (JSON_LENGTH(NEW.roles)>1 AND JSON_EXTRACT(NEW.roles,'$[0]')=JSON_EXTRACT(NEW.roles,'$[1]'))
  OR (JSON_LENGTH(NEW.roles)>2 AND (JSON_EXTRACT(NEW.roles,'$[0]')=JSON_EXTRACT(NEW.roles,'$[2]') OR JSON_EXTRACT(NEW.roles,'$[1]')=JSON_EXTRACT(NEW.roles,'$[2]')))
  OR (JSON_LENGTH(NEW.roles)>3 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[3]'))<>'STRING' OR BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[3]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff') OR JSON_EXTRACT(NEW.roles,'$[3]')=JSON_EXTRACT(NEW.roles,'$[0]') OR JSON_EXTRACT(NEW.roles,'$[3]')=JSON_EXTRACT(NEW.roles,'$[1]') OR JSON_EXTRACT(NEW.roles,'$[3]')=JSON_EXTRACT(NEW.roles,'$[2]')))
  OR (NEW.kind IN ('FirstAdmin','Recovery') AND CAST(NEW.roles AS CHAR)<>'["InstitutionAdmin"]') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C invalid invitation';
 END IF;
END$$
DELIMITER ;

DROP TRIGGER `mt5d_event_insert`;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER mt5d_event_insert BEFORE INSERT ON institution_membership_event
FOR EACH ROW BEGIN
 DECLARE n_before INT;
 DECLARE n_after INT;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.actor_membership_id=NEW.target_membership_id
  OR NEW.revision_before<0 OR NEW.revision_before=9223372036854775807 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid identity or revision';
 END IF;
 IF NEW.revision_after<>NEW.revision_before+1 OR CHAR_LENGTH(TRIM(NEW.reason))=0
  OR NEW.reason REGEXP '[[:cntrl:]]' OR NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$')
  OR NEW.occurred_at_utc IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid event shape';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id
   JOIN user u ON u.id=m.user_id JOIN institution_membership_role r ON r.membership_id=m.id
   JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id
   WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id
   AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin' AND s.security_generation>=0) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D actor is not an active institution admin';
 END IF;
 IF JSON_TYPE(NEW.previous_roles)<>'ARRAY' OR JSON_TYPE(NEW.new_roles)<>'ARRAY'
   OR NEW.previous_status NOT IN ('active','suspended') OR NEW.new_status NOT IN ('active','suspended') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid snapshot';
 END IF;
 SET n_before=JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('InstitutionAdmin'))+
   JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Student'))+JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Teacher'))+JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Staff'));
 SET n_after=JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('InstitutionAdmin'))+
   JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Student'))+JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Teacher'))+JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Staff'));
 IF JSON_LENGTH(NEW.previous_roles)<>n_before OR JSON_LENGTH(NEW.new_roles)<>n_after THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid or duplicate role';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_security_state s
   ON s.institution_id=m.institution_id AND s.membership_id=m.id WHERE m.institution_id=NEW.institution_id
   AND m.id=NEW.target_membership_id AND BINARY m.status=BINARY NEW.new_status AND s.security_generation=NEW.revision_after)
  OR n_after<>(SELECT COUNT(*) FROM institution_membership_role WHERE membership_id=NEW.target_membership_id)
  OR EXISTS(SELECT 1 FROM institution_membership_role WHERE membership_id=NEW.target_membership_id
   AND JSON_CONTAINS(NEW.new_roles,JSON_QUOTE(role))<>1) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D snapshot does not match persisted mutation';
 END IF;
 IF NEW.action IN ('AddRole','RemoveRole') THEN
  IF NEW.target_role IS NULL OR NEW.target_role NOT IN ('InstitutionAdmin','Teacher','Student','Staff')
   OR NEW.previous_status<>NEW.new_status THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid role mutation';
  END IF;
  IF NEW.action='AddRole' THEN
   IF n_after<>n_before+1 OR JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE(NEW.target_role))<>0
    OR JSON_CONTAINS(NEW.new_roles,JSON_QUOTE(NEW.target_role))<>1 OR JSON_CONTAINS(NEW.new_roles,NEW.previous_roles)<>1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid role addition';
   END IF;
  ELSE
   IF n_before<>n_after+1 OR JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE(NEW.target_role))<>1
    OR JSON_CONTAINS(NEW.new_roles,JSON_QUOTE(NEW.target_role))<>0 OR JSON_CONTAINS(NEW.previous_roles,NEW.new_roles)<>1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid role removal';
   END IF;
  END IF;
 ELSEIF NEW.action IN ('Suspend','Reactivate') THEN
  IF NEW.target_role IS NOT NULL OR JSON_CONTAINS(NEW.previous_roles,NEW.new_roles)<>1
    OR JSON_CONTAINS(NEW.new_roles,NEW.previous_roles)<>1
    OR (NEW.action='Suspend' AND NOT(NEW.previous_status='active' AND NEW.new_status='suspended'))
    OR (NEW.action='Reactivate' AND NOT(NEW.previous_status='suspended' AND NEW.new_status='active')) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid status mutation';
  END IF;
 ELSE
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D unknown action';
 END IF;
END$$
DELIMITER ;

DROP TRIGGER `mt5d_event_snapshot_shape`;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER mt5d_event_snapshot_shape BEFORE INSERT ON institution_membership_event
FOR EACH ROW BEGIN
 DECLARE position_index INT DEFAULT 0;
 IF JSON_LENGTH(NEW.previous_roles)>4 OR JSON_LENGTH(NEW.new_roles)>4 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D role snapshot exceeds catalog';
 END IF;
 WHILE position_index<4 DO
  IF position_index<JSON_LENGTH(NEW.previous_roles) AND NOT(COALESCE((
    JSON_TYPE(JSON_EXTRACT(NEW.previous_roles,CONCAT('$[',position_index,']')))='STRING'
    AND BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.previous_roles,CONCAT('$[',position_index,']'))) IN ('InstitutionAdmin','Teacher','Student','Staff')),0)) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D previous roles must be flat catalog strings';
  END IF;
  IF position_index<JSON_LENGTH(NEW.new_roles) AND NOT(COALESCE((
    JSON_TYPE(JSON_EXTRACT(NEW.new_roles,CONCAT('$[',position_index,']')))='STRING'
    AND BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.new_roles,CONCAT('$[',position_index,']'))) IN ('InstitutionAdmin','Teacher','Student','Staff')),0)) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D new roles must be flat catalog strings';
  END IF;
  SET position_index=position_index+1;
 END WHILE;
END$$
DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
DROP TRIGGER `mt3a_audit_insert`;
DELIMITER $$
CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_audit_insert BEFORE INSERT ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;

 IF NEW.entity_type='platform' THEN
 IF NEW.institution_id IS NOT NULL OR NEW.entity_id IS NOT NULL OR NEW.before_json IS NOT NULL OR NEW.after_json IS NOT NULL OR NEW.action NOT LIKE 'platform.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A platform shape required'; END IF;
 ELSE
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='grade' THEN SET resolved=(SELECT institution_id FROM grade WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'INSERT'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END IF;
 END$$
DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
DROP TRIGGER `mt3a_audit_update`;
DELIMITER $$
CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_audit_update BEFORE UPDATE ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;
 IF OLD.action LIKE 'staff.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Staff audit append only'; END IF;
 IF NOT(NEW.institution_id <=> OLD.institution_id) OR NOT(NEW.entity_type <=> OLD.entity_type) OR NOT(NEW.entity_id <=> OLD.entity_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit attribution immutable'; END IF;
 IF NEW.entity_type='platform' THEN
 IF NEW.institution_id IS NOT NULL OR NEW.entity_id IS NOT NULL OR NEW.before_json IS NOT NULL OR NEW.after_json IS NOT NULL OR NEW.action NOT LIKE 'platform.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A platform shape required'; END IF;
 ELSE
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='grade' THEN SET resolved=(SELECT institution_id FROM grade WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'UPDATE'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END IF;
 END$$
DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER staff_audit_delete BEFORE DELETE ON audit_event FOR EACH ROW BEGIN
 IF OLD.action LIKE 'staff.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Staff audit append only'; END IF;
END$$
DELIMITER ;
