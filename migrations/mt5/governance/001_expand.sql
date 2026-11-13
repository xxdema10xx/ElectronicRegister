-- MT-5D additive governance/recovery. Never replay on an installed schema.
SET @mt5d_gov_gate=IF(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24'
 AND @@port=13306 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND @@foreign_key_checks=1 AND @@unique_checks=1
 AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE())=38
 AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE())=80
 AND NOT EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN ('institution_recovery','institution_recovery_approval'))),0),'DO 0','SELECT MT5D_GOV_TARGET_OR_PREDECESSOR_MISMATCH');
PREPARE mt5d_gov_check FROM @mt5d_gov_gate; EXECUTE mt5d_gov_check; DEALLOCATE PREPARE mt5d_gov_check;
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

CREATE TABLE institution_recovery (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 recipient_user_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 request_event_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 signed_request JSON NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(institution_id,id),
 CONSTRAINT fk_recovery_owner FOREIGN KEY(institution_id) REFERENCES institution(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_recovery_invitation FOREIGN KEY(institution_id,id) REFERENCES institution_invitation(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_recovery_recipient FOREIGN KEY(recipient_user_id) REFERENCES user(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_recovery_request FOREIGN KEY(institution_id,request_event_id) REFERENCES institution_onboarding_event(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_recovery_approval (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 recovery_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 operator_id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 event_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 signed_operation JSON NOT NULL,
 PRIMARY KEY(institution_id,recovery_id,operator_id),
 CONSTRAINT fk_recovery_approval_case FOREIGN KEY(institution_id,recovery_id) REFERENCES institution_recovery(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_recovery_approval_event FOREIGN KEY(institution_id,event_id) REFERENCES institution_onboarding_event(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER mt5gov_institution_transition BEFORE UPDATE ON institution FOR EACH ROW BEGIN
 IF NOT(NEW.status <=> OLD.status) THEN
  IF OLD.status='archived' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D archived institution is terminal'; END IF;
  IF OLD.status IN ('active','suspended') THEN
   IF EXISTS(SELECT 1 FROM institution_membership m LEFT JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id WHERE m.institution_id=OLD.id AND s.membership_id IS NULL)
    OR EXISTS(SELECT 1 FROM institution_membership_security_state s WHERE s.institution_id=OLD.id AND s.security_generation>=9223372036854775807) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D missing state or generation overflow'; END IF;
  END IF;
  IF OLD.status='suspended' AND NEW.status='active' AND NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id AND r.role='InstitutionAdmin' JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id JOIN user u ON u.id=m.user_id WHERE m.institution_id=OLD.id AND m.status='active' AND s.security_generation>=0) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D valid admin required for reactivation'; END IF;
 END IF;
END$$
CREATE TRIGGER mt5gov_institution_generation AFTER UPDATE ON institution FOR EACH ROW BEGIN
 IF OLD.status IN ('active','suspended') AND NOT(NEW.status <=> OLD.status) THEN
  UPDATE institution_membership_security_state SET security_generation=security_generation+1 WHERE institution_id=NEW.id;
 END IF;
END$$
CREATE TRIGGER mt5gov_recovery_insert BEFORE INSERT ON institution_recovery FOR EACH ROW BEGIN
 IF JSON_TYPE(NEW.signed_request)<>'OBJECT' OR NEW.expires_at_utc<=UTC_TIMESTAMP(6) OR NEW.expires_at_utc>UTC_TIMESTAMP(6)+INTERVAL 1 DAY
 OR NOT EXISTS(SELECT 1 FROM institution i WHERE i.id=NEW.institution_id AND i.status='suspended')
 OR NOT EXISTS(SELECT 1 FROM institution_invitation i WHERE i.institution_id=NEW.institution_id AND i.id=NEW.id AND i.recipient_user_id=NEW.recipient_user_id AND i.kind='Recovery' AND i.status='pending')
 OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.request_event_id AND e.actor_kind='Operator' AND e.action='RecoveryRequested' AND e.operator_id=JSON_UNQUOTE(JSON_EXTRACT(NEW.signed_request,'$.OperatorId'))) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid recovery request'; END IF;
END$$
CREATE TRIGGER mt5gov_approval_insert BEFORE INSERT ON institution_recovery_approval FOR EACH ROW BEGIN
 IF JSON_TYPE(NEW.signed_operation)<>'OBJECT'
 OR (SELECT COUNT(*) FROM institution_recovery_approval a WHERE a.institution_id=NEW.institution_id AND a.recovery_id=NEW.recovery_id)>=2
 OR NOT EXISTS(SELECT 1 FROM institution_recovery r WHERE r.institution_id=NEW.institution_id AND r.id=NEW.recovery_id AND r.expires_at_utc>UTC_TIMESTAMP(6))
 OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id AND e.actor_kind='Operator' AND e.action='RecoveryApproved' AND e.operator_id=NEW.operator_id AND e.operator_id=JSON_UNQUOTE(JSON_EXTRACT(NEW.signed_operation,'$.OperatorId'))) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid independent approval'; END IF;
END$$
CREATE TRIGGER mt5gov_recovery_update BEFORE UPDATE ON institution_recovery FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D immutable recovery request'; END$$
CREATE TRIGGER mt5gov_approval_update BEFORE UPDATE ON institution_recovery_approval FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D immutable recovery approval'; END$$
CREATE TRIGGER mt5gov_recovery_delete BEFORE DELETE ON institution_recovery FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D recovery retention required'; END IF;
END$$
CREATE TRIGGER mt5gov_approval_delete BEFORE DELETE ON institution_recovery_approval FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D recovery retention required'; END IF;
END$$
DELIMITER ;
