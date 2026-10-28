-- Institution calendar, additive MySQL 5.7.24. DDL is not transactional.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,NO_ENGINE_SUBSTITUTION';
CREATE TABLE institution_calendar_event (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 resource_kind varchar(16) CHARACTER SET ascii NOT NULL,
 resource_id char(36) CHARACTER SET ascii NOT NULL,
 operation varchar(48) CHARACTER SET ascii NOT NULL,
 revision_before bigint DEFAULT NULL,
 revision_after bigint NOT NULL,
 request_hash char(64) CHARACTER SET ascii NOT NULL,
 before_json json DEFAULT NULL,
 state_json json NOT NULL,
 receipt_json json NOT NULL,
 reason varchar(500) NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id), UNIQUE KEY uq_cal_event_owner(institution_id,id),
 UNIQUE KEY uq_cal_resource_revision(institution_id,resource_kind,resource_id,revision_after),
 KEY idx_cal_history(institution_id,resource_id,occurred_at_utc,id),
 CONSTRAINT fk_cal_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id)
 REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_calendar_settings (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 time_zone_id varchar(128) NOT NULL,
 approval_lead_days int NOT NULL,
 revision bigint NOT NULL,
 event_id char(36) CHARACTER SET ascii NOT NULL,
 PRIMARY KEY(institution_id),
 CONSTRAINT fk_cal_settings_owner FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_cal_settings_event FOREIGN KEY(institution_id,event_id) REFERENCES institution_calendar_event(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_calendar_period (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 class_id char(36) CHARACTER SET ascii NOT NULL,
 month_date date NOT NULL,
 status enum('Draft','Published') NOT NULL,
 revision bigint NOT NULL,
 published_by_user_id char(36) CHARACTER SET ascii DEFAULT NULL,
 published_by_membership_id char(36) CHARACTER SET ascii DEFAULT NULL,
 published_at_utc datetime(6) DEFAULT NULL,
 event_id char(36) CHARACTER SET ascii NOT NULL,
 PRIMARY KEY(id), UNIQUE KEY uq_cal_period_owner(institution_id,id),
 UNIQUE KEY uq_cal_period_class(institution_id,class_id,month_date),
 UNIQUE KEY uq_cal_period_lesson(institution_id,class_id,id),
 CONSTRAINT fk_cal_period_settings FOREIGN KEY(institution_id) REFERENCES institution_calendar_settings(institution_id),
 CONSTRAINT fk_cal_period_class FOREIGN KEY(institution_id,class_id) REFERENCES class(institution_id,id),
 CONSTRAINT fk_cal_period_event FOREIGN KEY(institution_id,event_id) REFERENCES institution_calendar_event(institution_id,id),
 CONSTRAINT fk_cal_publisher FOREIGN KEY(institution_id,published_by_membership_id,published_by_user_id)
 REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_calendar_import (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 revision bigint NOT NULL,
 status enum('Preview','Confirmed') NOT NULL,
 file_sha256 char(64) CHARACTER SET ascii NOT NULL,
 preview_digest char(64) CHARACTER SET ascii NOT NULL,
 raw_json json NOT NULL,
 settings_json json NOT NULL,
 preview_json json NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 expires_at_utc datetime(6) NOT NULL,
 event_id char(36) CHARACTER SET ascii NOT NULL,
 PRIMARY KEY(id), UNIQUE KEY uq_cal_import_owner(institution_id,id),
 KEY idx_cal_import_private(institution_id,actor_membership_id,status,expires_at_utc),
 CONSTRAINT fk_cal_import_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id)
 REFERENCES institution_membership(institution_id,id,user_id),
 CONSTRAINT fk_cal_import_event FOREIGN KEY(institution_id,event_id) REFERENCES institution_calendar_event(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_lesson (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 period_id char(36) CHARACTER SET ascii NOT NULL,
 class_id char(36) CHARACTER SET ascii NOT NULL,
 course_edition_id char(36) CHARACTER SET ascii NOT NULL,
 version_id char(36) CHARACTER SET ascii NOT NULL,
 module_id char(36) CHARACTER SET ascii NOT NULL,
 subject_id char(36) CHARACTER SET ascii NOT NULL,
 class_subject_id char(36) CHARACTER SET ascii NOT NULL,
 original_teacher_id char(36) CHARACTER SET ascii NOT NULL,
 teacher_id char(36) CHARACTER SET ascii NOT NULL,
 local_date date NOT NULL,
 starts_at time NOT NULL,
 ends_at time NOT NULL,
 time_zone_id varchar(128) NOT NULL,
 start_utc datetime NOT NULL,
 end_utc datetime NOT NULL,
 publication enum('Draft','Published') NOT NULL,
 status enum('Scheduled','Cancelled','Completed') NOT NULL,
 revision bigint NOT NULL,
 delivered_minutes int DEFAULT NULL,
 confirmed_by_user_id char(36) CHARACTER SET ascii DEFAULT NULL,
 confirmed_by_membership_id char(36) CHARACTER SET ascii DEFAULT NULL,
 confirmed_at_utc datetime(6) DEFAULT NULL,
 recovery_of_id char(36) CHARACTER SET ascii DEFAULT NULL,
 import_id char(36) CHARACTER SET ascii DEFAULT NULL,
 substitution_reason varchar(500) DEFAULT NULL,
 room varchar(200) DEFAULT NULL,
 description varchar(2000) DEFAULT NULL,
 external_reference varchar(100) DEFAULT NULL,
 event_id char(36) CHARACTER SET ascii NOT NULL,
 PRIMARY KEY(id), UNIQUE KEY uq_cal_lesson_owner(institution_id,id),
 KEY idx_cal_class_time(institution_id,class_id,start_utc,end_utc),
 KEY idx_cal_teacher_time(institution_id,teacher_id,start_utc,end_utc),
 KEY idx_cal_module_hours(institution_id,class_id,module_id,publication,status),
 CONSTRAINT fk_cal_lesson_period FOREIGN KEY(institution_id,class_id,period_id) REFERENCES institution_calendar_period(institution_id,class_id,id),
 CONSTRAINT fk_cal_lesson_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id),
 CONSTRAINT fk_cal_lesson_module FOREIGN KEY(institution_id,version_id,module_id) REFERENCES formative_module(institution_id,version_id,id),
 CONSTRAINT fk_cal_lesson_subject FOREIGN KEY(institution_id,subject_id) REFERENCES subject(institution_id,id),
 CONSTRAINT fk_cal_lesson_assignment FOREIGN KEY(institution_id,class_subject_id) REFERENCES class_subject(institution_id,id),
 CONSTRAINT fk_cal_lesson_original FOREIGN KEY(institution_id,original_teacher_id) REFERENCES teacher(institution_id,id),
 CONSTRAINT fk_cal_lesson_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id),
 CONSTRAINT fk_cal_lesson_recovery FOREIGN KEY(institution_id,recovery_of_id) REFERENCES institution_lesson(institution_id,id),
 CONSTRAINT fk_cal_lesson_import FOREIGN KEY(institution_id,import_id) REFERENCES institution_calendar_import(institution_id,id),
 CONSTRAINT fk_cal_lesson_event FOREIGN KEY(institution_id,event_id) REFERENCES institution_calendar_event(institution_id,id),
 CONSTRAINT fk_cal_confirmer FOREIGN KEY(institution_id,confirmed_by_membership_id,confirmed_by_user_id)
 REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_calendar_occupancy (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 lesson_id char(36) CHARACTER SET ascii NOT NULL,
 resource_kind enum('Class','Teacher') NOT NULL,
 resource_id char(36) CHARACTER SET ascii NOT NULL,
 start_utc datetime NOT NULL,
 end_utc datetime NOT NULL,
 event_id char(36) CHARACTER SET ascii NOT NULL,
 PRIMARY KEY(institution_id,lesson_id,resource_kind),
 KEY idx_cal_occupancy_resource(institution_id,resource_kind,resource_id,start_utc,end_utc),
 CONSTRAINT fk_cal_occupancy_lesson FOREIGN KEY(institution_id,lesson_id) REFERENCES institution_lesson(institution_id,id),
 CONSTRAINT fk_cal_occupancy_event FOREIGN KEY(institution_id,event_id) REFERENCES institution_calendar_event(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
DELIMITER $$
CREATE TRIGGER institution_calendar_event_bi BEFORE INSERT ON institution_calendar_event FOR EACH ROW
BEGIN

 DECLARE owner_lock char(36);
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NEW.revision_after<0 OR CHAR_LENGTH(TRIM(NEW.reason))=0 OR CHAR_LENGTH(NEW.request_hash)<>64 OR
 NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id
 WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id
 AND m.status='active' AND i.status='active') OR
 NOT (EXISTS(SELECT 1 FROM institution_membership_role r WHERE r.membership_id=NEW.actor_membership_id AND r.role IN('InstitutionAdmin','Staff'))
 OR (NEW.resource_kind='Lesson' AND NEW.operation='LessonCompleted' AND JSON_UNQUOTE(JSON_EXTRACT(NEW.state_json,'$.Status'))='Completed' AND EXISTS(SELECT 1 FROM institution_membership_role r WHERE r.membership_id=NEW.actor_membership_id AND r.role='Teacher')
 AND EXISTS(SELECT 1 FROM institution_teacher_profile p JOIN institution_lesson l ON l.institution_id=p.institution_id AND l.teacher_id=p.teacher_id
 WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.actor_membership_id AND l.id=NEW.resource_id AND l.publication='Published' AND l.status='Scheduled')))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
END$$
CREATE TRIGGER institution_calendar_event_bu BEFORE UPDATE ON institution_calendar_event FOR EACH ROW
BEGIN
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant';
END$$
CREATE TRIGGER institution_calendar_event_bd BEFORE DELETE ON institution_calendar_event FOR EACH ROW
BEGIN
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant';
END$$
CREATE TRIGGER institution_calendar_settings_bi BEFORE INSERT ON institution_calendar_settings FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Settings' AND e.resource_id=NEW.institution_id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.revision<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF CHAR_LENGTH(TRIM(NEW.time_zone_id))=0 OR NEW.approval_lead_days NOT BETWEEN 0 AND 366 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_calendar_settings_bu BEFORE UPDATE ON institution_calendar_settings FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Settings' AND e.resource_id=NEW.institution_id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT(NEW.institution_id<=>OLD.institution_id) OR NEW.institution_id<>OLD.institution_id OR NEW.revision<>OLD.revision+1 OR NEW.event_id=OLD.event_id OR NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND e.revision_before=OLD.revision) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF CHAR_LENGTH(TRIM(NEW.time_zone_id))=0 OR NEW.approval_lead_days NOT BETWEEN 0 AND 366 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.time_zone_id<>OLD.time_zone_id AND EXISTS(SELECT 1 FROM institution_lesson WHERE institution_id=NEW.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_calendar_settings_bd BEFORE DELETE ON institution_calendar_settings FOR EACH ROW
BEGIN
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant';
END$$
CREATE TRIGGER institution_calendar_period_bi BEFORE INSERT ON institution_calendar_period FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Period' AND e.resource_id=NEW.id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.revision<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF DAY(NEW.month_date)<>1 OR NEW.status NOT IN('Draft','Published') OR (NEW.status='Published' AND (NEW.published_at_utc IS NULL OR NEW.published_by_user_id IS NULL OR NEW.published_by_membership_id IS NULL)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_calendar_period_bu BEFORE UPDATE ON institution_calendar_period FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Period' AND e.resource_id=NEW.id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT(NEW.id<=>OLD.id) OR NEW.institution_id<>OLD.institution_id OR NEW.revision<>OLD.revision+1 OR NEW.event_id=OLD.event_id OR NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND e.revision_before=OLD.revision) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF DAY(NEW.month_date)<>1 OR NEW.status NOT IN('Draft','Published') OR (NEW.status='Published' AND (NEW.published_at_utc IS NULL OR NEW.published_by_user_id IS NULL OR NEW.published_by_membership_id IS NULL)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.class_id<>OLD.class_id OR NEW.month_date<>OLD.month_date OR (OLD.status='Published' AND NEW.status<>'Published') THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_calendar_period_bd BEFORE DELETE ON institution_calendar_period FOR EACH ROW
BEGIN
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant';
END$$
CREATE TRIGGER institution_calendar_import_bi BEFORE INSERT ON institution_calendar_import FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Import' AND e.resource_id=NEW.id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.revision<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.status NOT IN('Preview','Confirmed') OR NEW.expires_at_utc<=NEW.created_at_utc OR OCTET_LENGTH(NEW.raw_json)>2097152 OR OCTET_LENGTH(NEW.preview_json)>2097152 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_calendar_import_bu BEFORE UPDATE ON institution_calendar_import FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Import' AND e.resource_id=NEW.id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT(NEW.id<=>OLD.id) OR NEW.institution_id<>OLD.institution_id OR NEW.revision<>OLD.revision+1 OR NEW.event_id=OLD.event_id OR NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND e.revision_before=OLD.revision) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.status NOT IN('Preview','Confirmed') OR NEW.expires_at_utc<=NEW.created_at_utc OR OCTET_LENGTH(NEW.raw_json)>2097152 OR OCTET_LENGTH(NEW.preview_json)>2097152 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF OLD.status<>'Preview' OR NEW.actor_user_id<>OLD.actor_user_id OR NEW.actor_membership_id<>OLD.actor_membership_id OR NEW.created_at_utc<>OLD.created_at_utc OR NEW.expires_at_utc<>OLD.expires_at_utc THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_calendar_import_bd BEFORE DELETE ON institution_calendar_import FOR EACH ROW
BEGIN
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant';
END$$
CREATE TRIGGER institution_lesson_bi BEFORE INSERT ON institution_lesson FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Lesson' AND e.resource_id=NEW.id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.revision<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.publication NOT IN('Draft','Published') OR NEW.status NOT IN('Scheduled','Cancelled','Completed') OR
 NEW.ends_at<=NEW.starts_at OR NEW.starts_at<'00:00:00' OR NEW.ends_at>='24:00:00' OR SECOND(NEW.starts_at)<>0 OR SECOND(NEW.ends_at)<>0 OR
 NEW.end_utc<=NEW.start_utc OR SECOND(NEW.start_utc)<>0 OR SECOND(NEW.end_utc)<>0 OR
 (NEW.status='Completed' AND (NEW.publication<>'Published' OR NEW.delivered_minutes IS NULL OR NEW.delivered_minutes NOT BETWEEN 1 AND 1440 OR NEW.confirmed_at_utc IS NULL OR NEW.confirmed_by_user_id IS NULL OR NEW.confirmed_by_membership_id IS NULL)) OR
 (NEW.status<>'Completed' AND (NEW.delivered_minutes IS NOT NULL OR NEW.confirmed_at_utc IS NOT NULL OR NEW.confirmed_by_user_id IS NOT NULL OR NEW.confirmed_by_membership_id IS NOT NULL)) OR
 (NEW.teacher_id<>NEW.original_teacher_id AND (NEW.substitution_reason IS NULL OR CHAR_LENGTH(TRIM(NEW.substitution_reason))=0))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT EXISTS(SELECT 1 FROM class c JOIN course_edition_formative_plan b ON b.institution_id=c.institution_id AND b.course_edition_id=c.course_edition_id
 JOIN formative_plan_version v ON v.institution_id=b.institution_id AND v.id=b.version_id
 JOIN formative_module m ON m.institution_id=v.institution_id AND m.version_id=v.id
 JOIN class_subject cs ON cs.institution_id=c.institution_id AND cs.class_id=c.id AND cs.subject_id=m.subject_id
 JOIN institution_calendar_period p ON p.institution_id=c.institution_id AND p.class_id=c.id AND p.id=NEW.period_id
 WHERE c.institution_id=NEW.institution_id AND c.id=NEW.class_id AND c.course_edition_id=NEW.course_edition_id
 AND v.id=NEW.version_id AND v.status='Published' AND m.id=NEW.module_id AND m.subject_id=NEW.subject_id AND cs.id=NEW.class_subject_id
 AND p.month_date=DATE_SUB(NEW.local_date,INTERVAL DAY(NEW.local_date)-1 DAY)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.recovery_of_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_lesson l WHERE l.institution_id=NEW.institution_id AND l.id=NEW.recovery_of_id
 AND l.id<>NEW.id AND l.status='Cancelled' AND l.class_id=NEW.class_id AND l.module_id=NEW.module_id AND l.version_id=NEW.version_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND JSON_CONTAINS(e.state_json,JSON_OBJECT(
 'ClassId',NEW.class_id,'ModuleId',NEW.module_id,'TeacherId',NEW.teacher_id,'Publication',NEW.publication,'Status',NEW.status,
 'PeriodId',NEW.period_id,'CourseEditionId',NEW.course_edition_id,'VersionId',NEW.version_id,'SubjectId',NEW.subject_id,'ClassSubjectId',NEW.class_subject_id,'OriginalTeacherId',NEW.original_teacher_id,
 'TimeZoneId',NEW.time_zone_id,'StartUtc',DATE_FORMAT(NEW.start_utc,'%Y-%m-%dT%H:%i:%sZ'),'EndUtc',DATE_FORMAT(NEW.end_utc,'%Y-%m-%dT%H:%i:%sZ'),
 'DeliveredMinutes',NEW.delivered_minutes,'RecoveryOfLessonId',NEW.recovery_of_id,'Room',NEW.room,'Description',NEW.description,'ExternalReference',NEW.external_reference,
 'Date',DATE_FORMAT(NEW.local_date,'%Y-%m-%d'),'StartsAt',TIME_FORMAT(NEW.starts_at,'%H:%i:%s'),'EndsAt',TIME_FORMAT(NEW.ends_at,'%H:%i:%s')))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.publication='Published' AND NEW.status<>'Cancelled' THEN
 SELECT COUNT(*) INTO overlap_count FROM institution_calendar_occupancy o WHERE o.institution_id=NEW.institution_id AND o.lesson_id<>NEW.id
 AND ((o.resource_kind='Class' AND o.resource_id=NEW.class_id) OR (o.resource_kind='Teacher' AND o.resource_id=NEW.teacher_id))
 AND o.start_utc<NEW.end_utc AND NEW.start_utc<o.end_utc FOR UPDATE;
 IF overlap_count>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF; END IF;
 IF NEW.status<>'Scheduled' OR NEW.publication<>'Draft' OR NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND e.operation='LessonCreated' AND e.revision_before IS NULL) OR NOT EXISTS(SELECT 1 FROM class_subject cs WHERE cs.institution_id=NEW.institution_id AND cs.id=NEW.class_subject_id AND cs.teacher_id=NEW.original_teacher_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_lesson_bu BEFORE UPDATE ON institution_lesson FOR EACH ROW
BEGIN
 DECLARE owner_lock char(36);
 DECLARE overlap_count int DEFAULT 0;
 SELECT id INTO owner_lock FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id
 AND e.resource_kind='Lesson' AND e.resource_id=NEW.id AND e.revision_after=NEW.revision
 AND JSON_CONTAINS(e.state_json,JSON_OBJECT('Revision',NEW.revision))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT(NEW.id<=>OLD.id) OR NEW.institution_id<>OLD.institution_id OR NEW.revision<>OLD.revision+1 OR NEW.event_id=OLD.event_id OR NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND e.revision_before=OLD.revision) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.publication NOT IN('Draft','Published') OR NEW.status NOT IN('Scheduled','Cancelled','Completed') OR
 NEW.ends_at<=NEW.starts_at OR NEW.starts_at<'00:00:00' OR NEW.ends_at>='24:00:00' OR SECOND(NEW.starts_at)<>0 OR SECOND(NEW.ends_at)<>0 OR
 NEW.end_utc<=NEW.start_utc OR SECOND(NEW.start_utc)<>0 OR SECOND(NEW.end_utc)<>0 OR
 (NEW.status='Completed' AND (NEW.publication<>'Published' OR NEW.delivered_minutes IS NULL OR NEW.delivered_minutes NOT BETWEEN 1 AND 1440 OR NEW.confirmed_at_utc IS NULL OR NEW.confirmed_by_user_id IS NULL OR NEW.confirmed_by_membership_id IS NULL)) OR
 (NEW.status<>'Completed' AND (NEW.delivered_minutes IS NOT NULL OR NEW.confirmed_at_utc IS NOT NULL OR NEW.confirmed_by_user_id IS NOT NULL OR NEW.confirmed_by_membership_id IS NOT NULL)) OR
 (NEW.teacher_id<>NEW.original_teacher_id AND (NEW.substitution_reason IS NULL OR CHAR_LENGTH(TRIM(NEW.substitution_reason))=0))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT EXISTS(SELECT 1 FROM class c JOIN course_edition_formative_plan b ON b.institution_id=c.institution_id AND b.course_edition_id=c.course_edition_id
 JOIN formative_plan_version v ON v.institution_id=b.institution_id AND v.id=b.version_id
 JOIN formative_module m ON m.institution_id=v.institution_id AND m.version_id=v.id
 JOIN class_subject cs ON cs.institution_id=c.institution_id AND cs.class_id=c.id AND cs.subject_id=m.subject_id
 JOIN institution_calendar_period p ON p.institution_id=c.institution_id AND p.class_id=c.id AND p.id=NEW.period_id
 WHERE c.institution_id=NEW.institution_id AND c.id=NEW.class_id AND c.course_edition_id=NEW.course_edition_id
 AND v.id=NEW.version_id AND v.status='Published' AND m.id=NEW.module_id AND m.subject_id=NEW.subject_id AND cs.id=NEW.class_subject_id
 AND p.month_date=DATE_SUB(NEW.local_date,INTERVAL DAY(NEW.local_date)-1 DAY)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.recovery_of_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_lesson l WHERE l.institution_id=NEW.institution_id AND l.id=NEW.recovery_of_id
 AND l.id<>NEW.id AND l.status='Cancelled' AND l.class_id=NEW.class_id AND l.module_id=NEW.module_id AND l.version_id=NEW.version_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND JSON_CONTAINS(e.state_json,JSON_OBJECT(
 'ClassId',NEW.class_id,'ModuleId',NEW.module_id,'TeacherId',NEW.teacher_id,'Publication',NEW.publication,'Status',NEW.status,
 'PeriodId',NEW.period_id,'CourseEditionId',NEW.course_edition_id,'VersionId',NEW.version_id,'SubjectId',NEW.subject_id,'ClassSubjectId',NEW.class_subject_id,'OriginalTeacherId',NEW.original_teacher_id,
 'TimeZoneId',NEW.time_zone_id,'StartUtc',DATE_FORMAT(NEW.start_utc,'%Y-%m-%dT%H:%i:%sZ'),'EndUtc',DATE_FORMAT(NEW.end_utc,'%Y-%m-%dT%H:%i:%sZ'),
 'DeliveredMinutes',NEW.delivered_minutes,'RecoveryOfLessonId',NEW.recovery_of_id,'Room',NEW.room,'Description',NEW.description,'ExternalReference',NEW.external_reference,
 'Date',DATE_FORMAT(NEW.local_date,'%Y-%m-%d'),'StartsAt',TIME_FORMAT(NEW.starts_at,'%H:%i:%s'),'EndsAt',TIME_FORMAT(NEW.ends_at,'%H:%i:%s')))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NEW.publication='Published' AND NEW.status<>'Cancelled' THEN
 SELECT COUNT(*) INTO overlap_count FROM institution_calendar_occupancy o WHERE o.institution_id=NEW.institution_id AND o.lesson_id<>NEW.id
 AND ((o.resource_kind='Class' AND o.resource_id=NEW.class_id) OR (o.resource_kind='Teacher' AND o.resource_id=NEW.teacher_id))
 AND o.start_utc<NEW.end_utc AND NEW.start_utc<o.end_utc FOR UPDATE;
 IF overlap_count>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF; END IF;
 IF NEW.class_id<>OLD.class_id OR NEW.course_edition_id<>OLD.course_edition_id OR NEW.version_id<>OLD.version_id OR NEW.original_teacher_id<>OLD.original_teacher_id OR NOT(NEW.import_id<=>OLD.import_id) OR
 (OLD.publication='Published' AND NEW.publication<>'Published') OR
 (OLD.status='Cancelled' AND NEW.status<>'Cancelled') OR
 (OLD.status='Completed' AND (NEW.status<>'Completed' OR NEW.module_id<>OLD.module_id OR NEW.teacher_id<>OLD.teacher_id OR NEW.start_utc<>OLD.start_utc OR NEW.end_utc<>OLD.end_utc OR NEW.local_date<>OLD.local_date OR NEW.starts_at<>OLD.starts_at OR NEW.ends_at<>OLD.ends_at))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_calendar_event e WHERE e.id=NEW.event_id AND (
 (e.operation='LessonUpdated' AND OLD.status='Scheduled' AND NEW.status='Scheduled' AND NEW.publication=OLD.publication) OR
 (e.operation='LessonPublished' AND OLD.publication='Draft' AND NEW.publication='Published' AND OLD.status='Scheduled' AND NEW.status='Scheduled') OR
 (e.operation IN('LessonCancelled','DraftReplaced') AND OLD.status='Scheduled' AND NEW.status='Cancelled' AND NEW.teacher_id=OLD.teacher_id AND NEW.module_id=OLD.module_id AND NEW.start_utc=OLD.start_utc AND NEW.end_utc=OLD.end_utc) OR
 (e.operation='TeacherSubstituted' AND OLD.status='Scheduled' AND NEW.status='Scheduled' AND NEW.module_id=OLD.module_id AND NEW.start_utc=OLD.start_utc AND NEW.end_utc=OLD.end_utc) OR
 (e.operation='LessonRescheduled' AND OLD.status='Scheduled' AND NEW.status='Scheduled' AND NEW.module_id=OLD.module_id AND NEW.teacher_id=OLD.teacher_id) OR
 (e.operation='LessonCompleted' AND OLD.status='Scheduled' AND OLD.publication='Published' AND NEW.status='Completed' AND NEW.teacher_id=OLD.teacher_id AND NEW.module_id=OLD.module_id AND NEW.start_utc=OLD.start_utc AND NEW.end_utc=OLD.end_utc AND NEW.confirmed_by_user_id=e.actor_user_id AND NEW.confirmed_by_membership_id=e.actor_membership_id) OR
 (e.operation='DeliveredMinutesCorrected' AND OLD.status='Completed' AND NEW.status='Completed' AND NEW.confirmed_by_user_id=e.actor_user_id AND NEW.confirmed_by_membership_id=e.actor_membership_id)))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;

END$$
CREATE TRIGGER institution_lesson_bd BEFORE DELETE ON institution_lesson FOR EACH ROW
BEGIN
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant';
END$$
CREATE TRIGGER institution_calendar_occupancy_bi BEFORE INSERT ON institution_calendar_occupancy FOR EACH ROW
BEGIN
DECLARE latest_event char(36);
 SELECT id INTO latest_event FROM institution_calendar_event WHERE institution_id=NEW.institution_id AND resource_kind='Lesson' AND resource_id=NEW.lesson_id ORDER BY revision_after DESC LIMIT 1 FOR UPDATE;
 IF latest_event<>NEW.event_id OR NEW.resource_kind NOT IN('Class','Teacher') OR NOT EXISTS(SELECT 1 FROM institution_lesson l WHERE l.institution_id=NEW.institution_id AND l.id=NEW.lesson_id AND l.event_id=NEW.event_id AND l.publication='Published' AND l.status<>'Cancelled'
 AND l.start_utc=NEW.start_utc AND l.end_utc=NEW.end_utc AND ((NEW.resource_kind='Class' AND NEW.resource_id=l.class_id) OR (NEW.resource_kind='Teacher' AND NEW.resource_id=l.teacher_id))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
END$$
CREATE TRIGGER institution_calendar_occupancy_bu BEFORE UPDATE ON institution_calendar_occupancy FOR EACH ROW
BEGIN
SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant';
END$$
CREATE TRIGGER institution_calendar_occupancy_bd BEFORE DELETE ON institution_calendar_occupancy FOR EACH ROW
BEGIN
DECLARE latest_event char(36);
 SELECT id INTO latest_event FROM institution_calendar_event WHERE institution_id=OLD.institution_id AND resource_kind='Lesson' AND resource_id=OLD.lesson_id ORDER BY revision_after DESC LIMIT 1 FOR UPDATE;
 IF latest_event=OLD.event_id OR EXISTS(SELECT 1 FROM institution_lesson l WHERE l.institution_id=OLD.institution_id AND l.id=OLD.lesson_id AND l.event_id=OLD.event_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
END$$
CREATE TRIGGER calendar_lesson_occupancy_au AFTER UPDATE ON institution_lesson FOR EACH ROW
BEGIN
 DELETE FROM institution_calendar_occupancy WHERE institution_id=NEW.institution_id AND lesson_id=NEW.id;
 IF NEW.publication='Published' AND NEW.status<>'Cancelled' THEN
 INSERT INTO institution_calendar_occupancy(institution_id,lesson_id,resource_kind,resource_id,start_utc,end_utc,event_id)
 VALUES(NEW.institution_id,NEW.id,'Class',NEW.class_id,NEW.start_utc,NEW.end_utc,NEW.event_id),(NEW.institution_id,NEW.id,'Teacher',NEW.teacher_id,NEW.start_utc,NEW.end_utc,NEW.event_id);
 END IF;
END$$
CREATE TRIGGER calendar_class_binding_bu BEFORE UPDATE ON class FOR EACH ROW
BEGIN
 IF NOT(NEW.course_edition_id<=>OLD.course_edition_id) AND EXISTS(SELECT 1 FROM institution_lesson l WHERE l.institution_id=OLD.institution_id AND l.class_id=OLD.id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
END$$
CREATE TRIGGER calendar_assignment_binding_bu BEFORE UPDATE ON class_subject FOR EACH ROW
BEGIN
 IF (NEW.class_id<>OLD.class_id OR NEW.subject_id<>OLD.subject_id) AND EXISTS(SELECT 1 FROM institution_lesson l WHERE l.institution_id=OLD.institution_id AND l.class_subject_id=OLD.id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Calendar invariant'; END IF;
END$$
DELIMITER ;
