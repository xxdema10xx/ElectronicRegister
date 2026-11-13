-- MT-5C additive onboarding. One shot on an attested isolated test instance.
SET @mt5c_gate=IF(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24'
 AND @@port=13306 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND @@foreign_key_checks=1 AND @@unique_checks=1
 AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_TYPE='BASE TABLE')=34
 AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE())=68
 AND NOT EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='institution_invitation')),0),'DO 0','SELECT MT5C_TARGET_OR_PREDECESSOR_MISMATCH');
PREPARE mt5c_check FROM @mt5c_gate; EXECUTE mt5c_check; DEALLOCATE PREPARE mt5c_check;

CREATE TABLE institution_invitation (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 recipient_user_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 kind VARCHAR(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 roles JSON NOT NULL,
 status VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 token_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 channel_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 attempts INT NOT NULL DEFAULT 0,
 created_at_utc DATETIME(6) NOT NULL,
 consumed_membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 PRIMARY KEY(institution_id,id),
 CONSTRAINT fk_invitation_owner FOREIGN KEY(institution_id) REFERENCES institution(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_invitation_recipient FOREIGN KEY(recipient_user_id) REFERENCES user(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_invitation_membership FOREIGN KEY(institution_id,consumed_membership_id) REFERENCES institution_membership(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 KEY idx_invitation_recipient(institution_id,recipient_user_id,created_at_utc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_onboarding_event (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 actor_kind VARCHAR(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 operator_id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
 key_id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
 actor_user_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 actor_membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 action VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 invitation_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 intent_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 reason VARCHAR(500) NOT NULL,
 evidence_reference VARCHAR(200) NULL,
 result_json JSON NOT NULL,
 occurred_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_onboarding_event_owner(institution_id,id),
 KEY idx_onboarding_event_time(institution_id,occurred_at_utc,id),
 CONSTRAINT fk_onboarding_event_owner FOREIGN KEY(institution_id) REFERENCES institution(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_onboarding_event_invite FOREIGN KEY(institution_id,invitation_id) REFERENCES institution_invitation(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_onboarding_event_user FOREIGN KEY(actor_user_id) REFERENCES user(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_onboarding_event_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE platform_operation_nonce (
 operator_id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 nonce CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 key_id VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 event_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(operator_id,nonce),
 CONSTRAINT fk_platform_nonce_event FOREIGN KEY(institution_id,event_id) REFERENCES institution_onboarding_event(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_provisioning (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 approval_event_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 first_invitation_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 legal_identity VARCHAR(200) NOT NULL,
 evidence_reference VARCHAR(200) NOT NULL,
 PRIMARY KEY(institution_id),
 CONSTRAINT fk_provisioning_owner FOREIGN KEY(institution_id) REFERENCES institution(id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_provisioning_approval FOREIGN KEY(institution_id,approval_event_id) REFERENCES institution_onboarding_event(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT fk_provisioning_invitation FOREIGN KEY(institution_id,first_invitation_id) REFERENCES institution_invitation(institution_id,id) ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER mt5c_invitation_insert BEFORE INSERT ON institution_invitation FOR EACH ROW BEGIN
 IF NEW.kind NOT IN ('FirstAdmin','Membership','Recovery') OR NEW.status<>'pending' OR NEW.attempts<>0 OR NEW.consumed_membership_id IS NOT NULL
  OR NEW.token_hash NOT REGEXP BINARY '^[A-F0-9]{64}$' OR NEW.channel_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'
  OR NEW.expires_at_utc<=NEW.created_at_utc OR JSON_TYPE(NEW.roles)<>'ARRAY' OR JSON_LENGTH(NEW.roles) NOT BETWEEN 1 AND 3
  OR JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[0]'))<>'STRING'
  OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[0]')) NOT IN ('InstitutionAdmin','Teacher','Student')
  OR (JSON_LENGTH(NEW.roles)>1 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[1]'))<>'STRING' OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[1]')) NOT IN ('InstitutionAdmin','Teacher','Student')))
  OR (JSON_LENGTH(NEW.roles)>2 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[2]'))<>'STRING' OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[2]')) NOT IN ('InstitutionAdmin','Teacher','Student')))
  OR (JSON_LENGTH(NEW.roles)>1 AND JSON_EXTRACT(NEW.roles,'$[0]')=JSON_EXTRACT(NEW.roles,'$[1]'))
  OR (JSON_LENGTH(NEW.roles)>2 AND (JSON_EXTRACT(NEW.roles,'$[0]')=JSON_EXTRACT(NEW.roles,'$[2]') OR JSON_EXTRACT(NEW.roles,'$[1]')=JSON_EXTRACT(NEW.roles,'$[2]')))
  OR (NEW.kind IN ('FirstAdmin','Recovery') AND CAST(NEW.roles AS CHAR)<>'["InstitutionAdmin"]') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C invalid invitation';
 END IF;
END$$
CREATE TRIGGER mt5c_invitation_update BEFORE UPDATE ON institution_invitation FOR EACH ROW BEGIN
 IF NEW.institution_id<>OLD.institution_id OR NEW.id<>OLD.id OR NEW.recipient_user_id<>OLD.recipient_user_id
  OR BINARY NEW.kind<>BINARY OLD.kind OR CAST(NEW.roles AS CHAR)<>CAST(OLD.roles AS CHAR)
  OR NEW.created_at_utc<>OLD.created_at_utc OR OLD.status<>'pending'
  OR NEW.status NOT IN ('pending','consumed','revoked') OR NEW.attempts NOT BETWEEN 0 AND 5
  OR (NEW.status='consumed' AND NEW.consumed_membership_id IS NULL)
  OR (NEW.status<>'consumed' AND NEW.consumed_membership_id IS NOT NULL)
  OR NEW.token_hash NOT REGEXP BINARY '^[A-F0-9]{64}$' OR NEW.channel_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'
  OR NEW.expires_at_utc<=NEW.created_at_utc THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C immutable or invalid invitation';
 END IF;
END$$
CREATE TRIGGER mt5c_event_insert BEFORE INSERT ON institution_onboarding_event FOR EACH ROW BEGIN
 IF NEW.actor_kind NOT IN ('Operator','Member','Recipient') OR NEW.intent_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'
  OR CHAR_LENGTH(TRIM(NEW.reason)) NOT BETWEEN 1 AND 500 OR JSON_TYPE(NEW.result_json)<>'OBJECT'
  OR (NEW.actor_kind='Operator' AND (NEW.operator_id IS NULL OR NEW.key_id IS NULL OR NEW.actor_user_id IS NOT NULL OR NEW.actor_membership_id IS NOT NULL))
  OR (NEW.actor_kind<>'Operator' AND (NEW.operator_id IS NOT NULL OR NEW.key_id IS NOT NULL OR NEW.actor_user_id IS NULL))
  OR (NEW.actor_kind='Member' AND NEW.actor_membership_id IS NULL)
  OR (NEW.actor_kind='Recipient' AND NEW.actor_membership_id IS NOT NULL) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C invalid attributed event';
 END IF;
END$$
CREATE TRIGGER mt5c_event_update BEFORE UPDATE ON institution_onboarding_event FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C immutable audit'; END$$
CREATE TRIGGER mt5c_nonce_update BEFORE UPDATE ON platform_operation_nonce FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C immutable nonce'; END$$
CREATE TRIGGER mt5c_provisioning_update BEFORE UPDATE ON institution_provisioning FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C immutable approval'; END$$
CREATE TRIGGER mt5c_provisioning_insert BEFORE INSERT ON institution_provisioning FOR EACH ROW BEGIN
 IF CHAR_LENGTH(TRIM(NEW.legal_identity))=0 OR CHAR_LENGTH(TRIM(NEW.evidence_reference))=0
 OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.approval_event_id AND e.actor_kind='Operator' AND e.action='Provision')
 OR NOT EXISTS(SELECT 1 FROM institution_invitation i WHERE i.institution_id=NEW.institution_id AND i.id=NEW.first_invitation_id AND i.kind='FirstAdmin' AND i.status='pending') THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C verified approval required'; END IF;
END$$
CREATE TRIGGER mt5c_nonce_insert BEFORE INSERT ON platform_operation_nonce FOR EACH ROW BEGIN
 IF NEW.nonce NOT REGEXP BINARY '^[A-F0-9]{64}$' OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id AND e.actor_kind='Operator' AND e.operator_id=NEW.operator_id) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C nonce actor mismatch'; END IF;
END$$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER mt5c_event_delete BEFORE DELETE ON institution_onboarding_event FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C retention required';
 END IF;
END$$
CREATE TRIGGER mt5c_nonce_delete BEFORE DELETE ON platform_operation_nonce FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C retention required';
 END IF;
END$$
CREATE TRIGGER mt5c_invitation_delete BEFORE DELETE ON institution_invitation FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C retention required';
 END IF;
END$$
CREATE TRIGGER mt5c_provisioning_delete BEFORE DELETE ON institution_provisioning FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C retention required';
 END IF;
END$$
DELIMITER ;
