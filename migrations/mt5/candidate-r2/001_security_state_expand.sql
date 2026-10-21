-- MT-5B-R1 reconstructed candidate; NOT the lost certified SQL.
-- Review only. Never run against the MT4C baseline on port 3306.
-- MySQL 5.7.24, quiescent writers, verified backup and reviewed driver required.
-- R1 installation gate: no routine/object is created to check the target.
-- PREPARE fails on an unresolved identifier on any non-authorized target.
SET @mt5b_r1_gate_sql=IF(COALESCE((DATABASE()='electronic_register_test'
 AND VERSION()='5.7.24' AND @@port=13306
 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND @@foreign_key_checks=1 AND @@unique_checks=1
 AND FIND_IN_SET('STRICT_TRANS_TABLES',@@SESSION.sql_mode)>0
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND NOT EXISTS(SELECT 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()
   AND TABLE_NAME='institution_membership_security_state')
 AND NOT EXISTS(SELECT 1 FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE()
   AND LEFT(TRIGGER_NAME,5)='mt5b_')),0),
 'DO 0','SELECT MT5B_R1_TARGET_MISMATCH');
PREPARE mt5b_r1_gate FROM @mt5b_r1_gate_sql;
EXECUTE mt5b_r1_gate;
DEALLOCATE PREPARE mt5b_r1_gate;
SET @mt5b_r1_gate_sql=NULL;

-- Plain CREATE intentionally rejects a second EXPAND. No destructive DOWN.
CREATE TABLE institution_membership_security_state (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 security_generation BIGINT NOT NULL DEFAULT 0,
 PRIMARY KEY (institution_id,membership_id),
 UNIQUE KEY uq_membership_security_identity (membership_id),
 CONSTRAINT fk_membership_security_owner FOREIGN KEY (institution_id,membership_id)
  REFERENCES institution_membership (institution_id,id)
  ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER mt5b_security_insert BEFORE INSERT ON institution_membership_security_state
FOR EACH ROW BEGIN
 -- Ownership is checked by the FK, preserving 1452 for wrong owner/orphan.
 IF NEW.security_generation IS NULL OR NEW.security_generation<>0 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B initial generation must be zero';
 END IF;
END$$

CREATE TRIGGER mt5b_security_update BEFORE UPDATE ON institution_membership_security_state
FOR EACH ROW BEGIN
 IF NOT(BINARY NEW.institution_id <=> BINARY OLD.institution_id)
    OR NOT(BINARY NEW.membership_id <=> BINARY OLD.membership_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B security identity is immutable';
 END IF;
 IF NEW.security_generation IS NULL OR NEW.security_generation<0
    OR NEW.security_generation<OLD.security_generation THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B generation cannot decrease';
 END IF;
 -- Advancing directly only revokes additional access. No application setter.
END$$

CREATE TRIGGER mt5b_security_delete BEFORE DELETE ON institution_membership_security_state
FOR EACH ROW BEGIN
 -- No membership read here: parent BEFORE DELETE may delete this sidecar.
 IF NOT(COALESCE((
 DATABASE()='electronic_register_test'
 AND VERSION()='5.7.24' AND @@port=13306
 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
 AND EXISTS(
   SELECT 1 FROM institution i WHERE i.id=OLD.institution_id
    AND BINARY i.code<>'MT3A_LEGACY_TEST'
    AND BINARY i.code REGEXP '^MT(1|1_REHEARSAL|2|3|3_REHEARSAL|3A|3B|3C|3D|4|4B|5A|5BP)_[0-9a-fA-F]{32}$'
 )
),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B security state retention required';
 END IF;
END$$

CREATE TRIGGER mt5b_membership_security_insert AFTER INSERT ON institution_membership
FOR EACH ROW BEGIN
 INSERT INTO institution_membership_security_state
  (institution_id,membership_id,security_generation) VALUES(NEW.institution_id,NEW.id,0);
END$$

CREATE TRIGGER mt5b_membership_identity_update BEFORE UPDATE ON institution_membership
FOR EACH ROW BEGIN
 IF NOT(BINARY NEW.id <=> BINARY OLD.id)
    OR NOT(BINARY NEW.institution_id <=> BINARY OLD.institution_id)
    OR NOT(BINARY NEW.user_id <=> BINARY OLD.user_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B membership identity is immutable';
 END IF;
END$$

CREATE TRIGGER mt5b_membership_security_update AFTER UPDATE ON institution_membership
FOR EACH ROW BEGIN
 IF NOT EXISTS(SELECT 1 FROM institution_membership_security_state
   WHERE institution_id=NEW.institution_id AND membership_id=NEW.id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B missing security state';
 END IF;
 IF NOT(NEW.status <=> OLD.status) THEN
 UPDATE institution_membership_security_state
 SET security_generation=security_generation+1
 WHERE institution_id=NEW.institution_id AND membership_id=NEW.id
   AND security_generation>=0 AND security_generation<9223372036854775807;
 IF ROW_COUNT()<>1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B missing security state or generation exhausted';
 END IF;
 END IF;
 -- suspended_at-only and unchanged status do not represent new revocation.
END$$

CREATE TRIGGER mt5b_membership_security_delete BEFORE DELETE ON institution_membership
FOR EACH ROW BEGIN
 IF NOT(COALESCE((
 DATABASE()='electronic_register_test'
 AND VERSION()='5.7.24' AND @@port=13306
 AND SUBSTRING_INDEX(USER(),'@',1)='root'
 AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
 AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
 AND EXISTS(
   SELECT 1 FROM institution i WHERE i.id=OLD.institution_id
    AND BINARY i.code<>'MT3A_LEGACY_TEST'
    AND BINARY i.code REGEXP '^MT(1|1_REHEARSAL|2|3|3_REHEARSAL|3A|3B|3C|3D|4|4B|5A|5BP)_[0-9a-fA-F]{32}$'
 )
),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B membership retention required';
 END IF;
 -- Only synthetic root teardown; normal FK RESTRICT is never disabled.
 -- Delete sidecar before its parent, with the sidecar DELETE guard still active.
 DELETE FROM institution_membership_security_state
 WHERE institution_id=OLD.institution_id AND membership_id=OLD.id;
END$$

CREATE TRIGGER mt5b_role_identity_update BEFORE UPDATE ON institution_membership_role
FOR EACH ROW BEGIN
 IF NOT(BINARY NEW.membership_id <=> BINARY OLD.membership_id)
    OR NOT(BINARY NEW.role <=> BINARY OLD.role) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B role rekey requires delete and insert';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership_security_state
   WHERE membership_id=OLD.membership_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B missing security state';
 END IF;
END$$

CREATE TRIGGER mt5b_role_security_insert AFTER INSERT ON institution_membership_role
FOR EACH ROW BEGIN
 DECLARE v_owner CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL;
 SELECT institution_id INTO v_owner FROM institution_membership WHERE id=NEW.membership_id;
 -- Read immutable parent only; never UPDATE it (INSERT SELECT / MySQL 1442).
 UPDATE institution_membership_security_state
 SET security_generation=security_generation+1
 WHERE institution_id=v_owner AND membership_id=NEW.membership_id
   AND security_generation>=0 AND security_generation<9223372036854775807;
 IF ROW_COUNT()<>1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B missing security state or generation exhausted';
 END IF;
END$$

CREATE TRIGGER mt5b_role_security_delete AFTER DELETE ON institution_membership_role
FOR EACH ROW BEGIN
 DECLARE v_owner CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL;
 SELECT institution_id INTO v_owner FROM institution_membership WHERE id=OLD.membership_id;
 -- Role DELETE never bypasses generation, including privileged fixture teardown.
 UPDATE institution_membership_security_state
 SET security_generation=security_generation+1
 WHERE institution_id=v_owner AND membership_id=OLD.membership_id
   AND security_generation>=0 AND security_generation<9223372036854775807;
 IF ROW_COUNT()<>1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B missing security state or generation exhausted';
 END IF;
END$$
DELIMITER ;
