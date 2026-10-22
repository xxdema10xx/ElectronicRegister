-- 20261022.2: append-only class audit and durable optimistic revisions.
-- Apply once after isolated-target preflight and a verified full backup.
CREATE TABLE institution_class_event (
  institution_id char(36) CHARACTER SET ascii NOT NULL,
  id char(36) CHARACTER SET ascii NOT NULL,
  actor_user_id char(36) CHARACTER SET ascii NOT NULL,
  actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
  class_id char(36) CHARACTER SET ascii NOT NULL,
  revision bigint(20) NOT NULL,
  action varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  before_json longtext COLLATE utf8mb4_unicode_ci,
  after_json longtext COLLATE utf8mb4_unicode_ci,
  occurred_at_utc datetime(6) NOT NULL,
  PRIMARY KEY (institution_id,id),
  UNIQUE KEY uq_class_event_revision (institution_id,class_id,revision),
  KEY idx_class_event_actor (institution_id,actor_membership_id,actor_user_id),
  CONSTRAINT fk_class_event_owner FOREIGN KEY (institution_id) REFERENCES institution(id),
  CONSTRAINT fk_class_event_actor FOREIGN KEY (institution_id,actor_membership_id,actor_user_id)
    REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET SESSION character_set_client='utf8mb4';
SET SESSION collation_connection='utf8mb4_unicode_ci';
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER class_event_insert BEFORE INSERT ON institution_class_event FOR EACH ROW BEGIN
 IF NEW.revision<1 OR NEW.action NOT IN ('class.created','class.renamed','class.deleted')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class event shape required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id
   JOIN institution_membership_role r ON r.membership_id=m.id
   WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id
   AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class event actor required'; END IF;
 IF (NEW.before_json IS NOT NULL AND NOT JSON_VALID(NEW.before_json)) OR
    (NEW.after_json IS NOT NULL AND NOT JSON_VALID(NEW.after_json))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class event JSON required'; END IF;
 IF NEW.action='class.deleted' THEN
   IF NEW.before_json IS NULL OR NEW.after_json IS NOT NULL OR
      EXISTS(SELECT 1 FROM class c WHERE c.id=NEW.class_id)
   THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class deletion snapshot required'; END IF;
 ELSE
   IF NEW.after_json IS NULL OR NOT EXISTS(SELECT 1 FROM class c WHERE c.id=NEW.class_id AND c.institution_id=NEW.institution_id)
   THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class owner snapshot required'; END IF;
   IF NEW.action='class.created' AND (NEW.before_json IS NOT NULL OR NEW.revision<>1)
   THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class creation snapshot required'; END IF;
   IF NEW.action='class.renamed' AND NEW.before_json IS NULL
   THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class rename snapshot required'; END IF;
 END IF;
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER class_event_update BEFORE UPDATE ON institution_class_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class audit append only';
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER class_event_delete BEFORE DELETE ON institution_class_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Class audit append only';
END$$
DELIMITER ;
