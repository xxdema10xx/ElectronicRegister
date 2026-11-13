-- er-platform-identity-1. MySQL 8.4.13 only. Apply ONCE after er-quality-2-mysql84,
-- through tools/mysql84-release.cjs successor (DEFINER binding) on an attested instance.
-- DDL implicitly commits: no rollback is promised, never replay after a partial failure.
-- The release marker is written last, so a partial installation is never "ready".
-- No historical event, actor or fiscal code is backfilled: existing users keep NULL.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';

ALTER TABLE `user`
 ADD COLUMN fiscal_code_hmac char(64) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
 ADD COLUMN fiscal_code_hmac_key_id varchar(32) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
 ADD COLUMN fiscal_code_enc varbinary(64) DEFAULT NULL,
 ADD COLUMN fiscal_code_enc_key_id varchar(32) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
 ADD COLUMN lifecycle_state enum('Active','Closed') CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'Active',
 ADD COLUMN closed_at_utc datetime(6) DEFAULT NULL,
 ADD COLUMN reactivated_at_utc datetime(6) DEFAULT NULL,
 ADD UNIQUE KEY uq_user_fiscal_code_hmac (fiscal_code_hmac),
 ADD KEY idx_user_lifecycle (lifecycle_state,id);

-- Append-only lifecycle ledger: identifiers, transition, rule and UTC only.
CREATE TABLE identity_lifecycle_event (
 id char(36) CHARACTER SET ascii NOT NULL,
 user_id char(36) CHARACTER SET ascii NOT NULL,
 transition varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 rule_code varchar(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 rule_version int NOT NULL,
 security_generation bigint NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 PRIMARY KEY (id),
 UNIQUE KEY uq_lifecycle_transition (user_id,transition,security_generation),
 KEY idx_lifecycle_user_time (user_id,occurred_at_utc),
 CONSTRAINT fk_lifecycle_user FOREIGN KEY (user_id) REFERENCES `user` (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER platform_user_insert BEFORE INSERT ON `user` FOR EACH ROW BEGIN
 IF NEW.lifecycle_state<>'Active' OR NEW.closed_at_utc IS NOT NULL OR NEW.reactivated_at_utc IS NOT NULL OR
  NOT((NEW.fiscal_code_hmac IS NULL AND NEW.fiscal_code_hmac_key_id IS NULL AND NEW.fiscal_code_enc IS NULL AND NEW.fiscal_code_enc_key_id IS NULL) OR
   (NEW.fiscal_code_hmac IS NOT NULL AND NEW.fiscal_code_hmac_key_id IS NOT NULL AND NEW.fiscal_code_enc IS NOT NULL AND NEW.fiscal_code_enc_key_id IS NOT NULL AND
    REGEXP_LIKE(NEW.fiscal_code_hmac,'^[0-9A-F]{64}$','c') AND REGEXP_LIKE(NEW.fiscal_code_hmac_key_id,'^[A-Za-z0-9._-]{1,32}$','c') AND
    REGEXP_LIKE(NEW.fiscal_code_enc_key_id,'^[A-Za-z0-9._-]{1,32}$','c') AND LENGTH(NEW.fiscal_code_enc) BETWEEN 29 AND 64)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Platform identity shape invalid';
 END IF;
END$$
-- The fixture exception reuses the attested-instance predicate of the mt5c_* purge
-- guards. Unbound it requires 5.7.24 and is never true on 8.4; only the owned test
-- bundle (tools/mysql84-fixture.cjs) binds it to the container created by the test.
CREATE TRIGGER platform_user_update BEFORE UPDATE ON `user` FOR EACH ROW BEGIN
 IF NOT(NEW.id <=> OLD.id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='User identity immutable';
 END IF;
 IF NOT(CAST(NEW.email AS BINARY) <=> CAST(OLD.email AS BINARY)) AND NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND LOWER(OLD.email) LIKE '%example.test' AND LOWER(NEW.email) LIKE '%example.test'),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='User email immutable';
 END IF;
 IF NOT(NEW.fiscal_code_hmac <=> OLD.fiscal_code_hmac) OR NOT(NEW.fiscal_code_hmac_key_id <=> OLD.fiscal_code_hmac_key_id) OR
  NOT(NEW.fiscal_code_enc <=> OLD.fiscal_code_enc) OR NOT(NEW.fiscal_code_enc_key_id <=> OLD.fiscal_code_enc_key_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='User fiscal code immutable';
 END IF;
 IF (NEW.lifecycle_state=OLD.lifecycle_state AND (NOT(NEW.closed_at_utc <=> OLD.closed_at_utc) OR NOT(NEW.reactivated_at_utc <=> OLD.reactivated_at_utc))) OR
  (OLD.lifecycle_state='Active' AND NEW.lifecycle_state='Closed' AND (NEW.closed_at_utc IS NULL OR NEW.closed_at_utc <=> OLD.closed_at_utc OR NOT(NEW.reactivated_at_utc <=> OLD.reactivated_at_utc))) OR
  (OLD.lifecycle_state='Closed' AND NEW.lifecycle_state='Active' AND (NEW.reactivated_at_utc IS NULL OR NEW.reactivated_at_utc <=> OLD.reactivated_at_utc OR NOT(NEW.closed_at_utc <=> OLD.closed_at_utc))) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='User lifecycle transition invalid';
 END IF;
END$$
CREATE TRIGGER platform_user_delete BEFORE DELETE ON `user` FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND LOWER(OLD.email) LIKE '%example.test'),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='User deletion forbidden';
 END IF;
END$$
CREATE TRIGGER lifecycle_event_insert BEFORE INSERT ON identity_lifecycle_event FOR EACH ROW BEGIN
 IF NOT REGEXP_LIKE(NEW.id,'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$','c') OR NEW.id='00000000-0000-0000-0000-000000000000' OR
  NEW.transition NOT IN ('Closed','Reactivated') OR NEW.rule_version<1 OR NEW.security_generation<1 OR NEW.occurred_at_utc IS NULL OR
  (NEW.transition='Closed' AND NEW.rule_code NOT IN ('NoActiveMembership','StudentPeriodElapsed')) OR
  (NEW.transition='Reactivated' AND NEW.rule_code NOT IN ('AccountRecreated','MembershipReactivated','InstitutionReactivated','ActiveMembershipFound')) OR
  NOT EXISTS(SELECT 1 FROM `user` u WHERE u.id=NEW.user_id AND u.lifecycle_state=IF(NEW.transition='Closed','Closed','Active')) OR
  NOT EXISTS(SELECT 1 FROM identity_account_state s WHERE s.user_id=NEW.user_id AND s.generation=NEW.security_generation) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Lifecycle event invalid';
 END IF;
END$$
CREATE TRIGGER lifecycle_event_update BEFORE UPDATE ON identity_lifecycle_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Lifecycle events are append-only';
END$$
CREATE TRIGGER lifecycle_event_delete BEFORE DELETE ON identity_lifecycle_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Lifecycle events are retained';
END$$
DELIMITER ;

INSERT INTO er_schema_release(version) VALUES('er-platform-identity-1');
