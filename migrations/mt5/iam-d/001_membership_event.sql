-- MT-5B-D additive IAM ledger. One-shot; verified private backup required.
-- Never run on the protected baseline. No DOWN or automatic recovery.
SET @mt5d_gate=IF(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24'
 AND @@port=13306 AND @@hostname='CES01622' AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND @@foreign_key_checks=1 AND @@unique_checks=1
 AND FIND_IN_SET('STRICT_TRANS_TABLES',@@SESSION.sql_mode)>0
 AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE')=33
 AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE())=64
 AND NOT EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_membership_event')
 AND NOT EXISTS(SELECT 1 FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND LEFT(TRIGGER_NAME,6)='mt5d_')) ,0),
 'DO 0','SELECT MT5D_TARGET_OR_PREDECESSOR_MISMATCH');
PREPARE mt5d_check FROM @mt5d_gate;
EXECUTE mt5d_check;
DEALLOCATE PREPARE mt5d_check;
SET @mt5d_gate=NULL;

CREATE TABLE institution_membership_event (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 actor_user_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 actor_membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 target_membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 action VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 target_role VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NULL,
 previous_status VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 new_status VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 previous_roles JSON NOT NULL,
 new_roles JSON NOT NULL,
 reason VARCHAR(500) NOT NULL,
 revision_before BIGINT NOT NULL,
 revision_after BIGINT NOT NULL,
 request_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 occurred_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(institution_id,id),
 UNIQUE KEY uq_membership_event_revision(institution_id,target_membership_id,revision_after),
 KEY idx_membership_event_actor(institution_id,actor_membership_id,actor_user_id),
 CONSTRAINT fk_membership_event_target FOREIGN KEY(institution_id,target_membership_id)
  REFERENCES institution_membership(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_membership_event_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id)
  REFERENCES institution_membership(institution_id,id,user_id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER mt5d_event_insert BEFORE INSERT ON institution_membership_event
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
   JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Student'))+JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Teacher'));
 SET n_after=JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('InstitutionAdmin'))+
   JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Student'))+JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Teacher'));
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
  IF NEW.target_role IS NULL OR NEW.target_role NOT IN ('InstitutionAdmin','Teacher','Student')
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

CREATE TRIGGER mt5d_event_update BEFORE UPDATE ON institution_membership_event
FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D audit is immutable';
END$$

CREATE TRIGGER mt5d_event_delete BEFORE DELETE ON institution_membership_event
FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D audit retention required';
 END IF;
END$$
DELIMITER ;
