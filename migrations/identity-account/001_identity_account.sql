-- Additive identity state, no role/tenant inference or historical data rewrite.
-- Apply once through an authorized deployment; verified only on a new isolated instance.
CREATE TABLE identity_account_state (
  user_id char(36) CHARACTER SET ascii NOT NULL,
  generation bigint NOT NULL DEFAULT 0,
  display_name varchar(160) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY(user_id),
  CONSTRAINT fk_identity_state_user FOREIGN KEY(user_id) REFERENCES `user`(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE identity_account_event (
  id char(36) CHARACTER SET ascii NOT NULL,
  user_id char(36) CHARACTER SET ascii NOT NULL,
  action varchar(40) CHARACTER SET ascii NOT NULL,
  created_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY(id),
  KEY idx_identity_event_user(user_id,created_at),
  CONSTRAINT fk_identity_event_user FOREIGN KEY(user_id) REFERENCES `user`(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER identity_account_event_no_update BEFORE UPDATE ON identity_account_event FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Identity events are append-only'; END$$
CREATE TRIGGER identity_account_event_no_delete BEFORE DELETE ON identity_account_event FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Identity events are append-only'; END$$
CREATE TRIGGER identity_account_state_guard BEFORE UPDATE ON identity_account_state FOR EACH ROW
BEGIN
 IF NEW.user_id<>OLD.user_id OR NEW.generation<OLD.generation OR NEW.generation>OLD.generation+1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid identity security transition';
 END IF;
END$$
DELIMITER ;
