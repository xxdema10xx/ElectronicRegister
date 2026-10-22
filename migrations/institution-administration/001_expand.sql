-- 20261022.3: shared institutional administration audit and durable optimistic revisions.
-- Apply once after isolated-target preflight and a verified full backup.
CREATE TABLE institution_administration_event (
  institution_id char(36) CHARACTER SET ascii NOT NULL,
  id char(36) CHARACTER SET ascii NOT NULL,
  actor_user_id char(36) CHARACTER SET ascii NOT NULL,
  actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
  entity_type varchar(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  entity_id char(36) CHARACTER SET ascii NOT NULL,
  revision bigint(20) NOT NULL,
  action varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  before_json longtext COLLATE utf8mb4_unicode_ci,
  after_json longtext COLLATE utf8mb4_unicode_ci,
  occurred_at_utc datetime(6) NOT NULL,
  PRIMARY KEY (institution_id,id),
  UNIQUE KEY uq_administration_event_revision (institution_id,entity_type,entity_id,revision),
  KEY idx_administration_event_actor (institution_id,actor_membership_id,actor_user_id),
  CONSTRAINT fk_administration_event_owner FOREIGN KEY (institution_id) REFERENCES institution(id),
  CONSTRAINT fk_administration_event_actor FOREIGN KEY (institution_id,actor_membership_id,actor_user_id)
    REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET SESSION character_set_client='utf8mb4';
SET SESSION collation_connection='utf8mb4_unicode_ci';
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER administration_event_insert BEFORE INSERT ON institution_administration_event FOR EACH ROW BEGIN
 DECLARE resource_owner char(36) CHARACTER SET ascii DEFAULT NULL;
 IF NEW.revision<1 OR NEW.action NOT IN ('created','updated','deleted') OR NEW.entity_type NOT IN ('study_area','study_path','biennium','biennium_study_area','biennium_study_path','class_subject','campus')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration event shape required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id
 JOIN institution_membership_role r ON r.membership_id=m.id
 WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id
 AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration actor required'; END IF;
 IF (NEW.before_json IS NOT NULL AND (NOT JSON_VALID(NEW.before_json) OR JSON_TYPE(NEW.before_json)<>'OBJECT')) OR
 (NEW.after_json IS NOT NULL AND (NOT JSON_VALID(NEW.after_json) OR JSON_TYPE(NEW.after_json)<>'OBJECT'))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration snapshot required'; END IF;
 CASE NEW.entity_type
 WHEN 'study_area' THEN SET resource_owner=(SELECT institution_id FROM study_area WHERE id=NEW.entity_id);
 WHEN 'study_path' THEN SET resource_owner=(SELECT institution_id FROM study_path WHERE id=NEW.entity_id);
 WHEN 'biennium' THEN SET resource_owner=(SELECT institution_id FROM biennium WHERE id=NEW.entity_id);
 WHEN 'biennium_study_area' THEN SET resource_owner=(SELECT institution_id FROM biennium_study_area WHERE id=NEW.entity_id);
 WHEN 'biennium_study_path' THEN SET resource_owner=(SELECT institution_id FROM biennium_study_path WHERE id=NEW.entity_id);
 WHEN 'class_subject' THEN SET resource_owner=(SELECT institution_id FROM class_subject WHERE id=NEW.entity_id);
 WHEN 'campus' THEN SET resource_owner=(SELECT institution_id FROM campus WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration resource required';
 END CASE;
 IF NEW.action='deleted' THEN
  IF resource_owner IS NOT NULL OR NEW.before_json IS NULL OR NEW.after_json IS NOT NULL
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration deletion snapshot required'; END IF;
 ELSE
  IF resource_owner IS NULL OR resource_owner<>NEW.institution_id OR NEW.after_json IS NULL
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration owner required'; END IF;
  IF NEW.action='created' AND (NEW.before_json IS NOT NULL OR NEW.revision<>1)
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration creation snapshot required'; END IF;
  IF NEW.action='updated' AND NEW.before_json IS NULL
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration update snapshot required'; END IF;
 END IF;
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER administration_event_update BEFORE UPDATE ON institution_administration_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration audit append only';
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER administration_event_delete BEFORE DELETE ON institution_administration_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Administration audit append only';
END$$
DELIMITER ;
