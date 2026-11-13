-- MT-4A EXPAND. Execute only after verified backup/preflight, never at startup.
ALTER TABLE campus ADD UNIQUE KEY uq_mt4_campus_owner (institution_id,id);
CREATE TABLE course_edition (
 id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 institution_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 study_path_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 code varchar(64) NOT NULL,
 name varchar(200) NOT NULL,
 start_date date NULL,
 end_date date NULL,
 status varchar(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 created_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 updated_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
 PRIMARY KEY(id), UNIQUE KEY uq_mt4_edition_owner(institution_id,id),
 UNIQUE KEY uq_mt4_edition_code(institution_id,code),
 KEY idx_mt4_edition_path(institution_id,study_path_id),
 CONSTRAINT fk_mt4_edition_owner FOREIGN KEY(institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT fk_mt4_edition_path FOREIGN KEY(institution_id,study_path_id) REFERENCES study_path(institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE course_edition_campus (
 institution_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 course_edition_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 campus_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 PRIMARY KEY(institution_id,course_edition_id,campus_id),
 KEY idx_mt4_edition_campus(institution_id,campus_id),
 CONSTRAINT fk_mt4_ec_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT fk_mt4_ec_campus FOREIGN KEY(institution_id,campus_id) REFERENCES campus(institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE student_enrollment (
 id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 institution_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 student_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 course_edition_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 enrollment_date date NOT NULL,
 completion_date date NULL,
 status varchar(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 created_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 updated_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
 PRIMARY KEY(id), UNIQUE KEY uq_mt4_enrollment_owner(institution_id,id),
 UNIQUE KEY uq_mt4_enrollment_student(institution_id,student_id,course_edition_id),
 KEY idx_mt4_enrollment_edition(institution_id,course_edition_id),
 CONSTRAINT fk_mt4_enrollment_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT fk_mt4_enrollment_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
ALTER TABLE class ADD COLUMN course_edition_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
 ADD KEY idx_mt4_class_edition(institution_id,course_edition_id),
 ADD CONSTRAINT fk_mt4_class_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;
DELIMITER $$
CREATE TRIGGER mt4_course_edition_insert BEFORE INSERT ON course_edition FOR EACH ROW BEGIN
 IF NEW.code NOT REGEXP '[^[:space:]]' OR NEW.name NOT REGEXP '[^[:space:]]' OR BINARY NEW.status NOT IN ('planned','active','completed','cancelled') OR (NEW.start_date IS NOT NULL AND YEAR(NEW.start_date)<1000) OR (NEW.end_date IS NOT NULL AND YEAR(NEW.end_date)<1000) OR (NEW.start_date IS NOT NULL AND NEW.end_date IS NOT NULL AND NEW.end_date<NEW.start_date) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$
CREATE TRIGGER mt4_course_edition_update BEFORE UPDATE ON course_edition FOR EACH ROW BEGIN
 IF NEW.code NOT REGEXP '[^[:space:]]' OR NEW.name NOT REGEXP '[^[:space:]]' OR BINARY NEW.status NOT IN ('planned','active','completed','cancelled') OR (NEW.start_date IS NOT NULL AND YEAR(NEW.start_date)<1000) OR (NEW.end_date IS NOT NULL AND YEAR(NEW.end_date)<1000) OR (NEW.start_date IS NOT NULL AND NEW.end_date IS NOT NULL AND NEW.end_date<NEW.start_date) OR NOT (NEW.id<=>OLD.id) OR NOT (NEW.institution_id<=>OLD.institution_id) OR NOT (NEW.study_path_id<=>OLD.study_path_id) OR NOT (NEW.created_at<=>OLD.created_at) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$
CREATE TRIGGER mt4_student_enrollment_insert BEFORE INSERT ON student_enrollment FOR EACH ROW BEGIN
 IF YEAR(NEW.enrollment_date)<1000 OR BINARY NEW.status NOT IN ('enrolled','completed','withdrawn','cancelled') OR (NEW.completion_date IS NOT NULL AND NEW.completion_date<NEW.enrollment_date) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$
CREATE TRIGGER mt4_student_enrollment_update BEFORE UPDATE ON student_enrollment FOR EACH ROW BEGIN
 IF YEAR(NEW.enrollment_date)<1000 OR BINARY NEW.status NOT IN ('enrolled','completed','withdrawn','cancelled') OR (NEW.completion_date IS NOT NULL AND NEW.completion_date<NEW.enrollment_date) OR NOT (NEW.id<=>OLD.id) OR NOT (NEW.institution_id<=>OLD.institution_id) OR NOT (NEW.student_id<=>OLD.student_id) OR NOT (NEW.course_edition_id<=>OLD.course_edition_id) OR NOT (NEW.enrollment_date<=>OLD.enrollment_date) OR NOT (NEW.created_at<=>OLD.created_at) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$
CREATE TRIGGER mt4_course_edition_campus_update BEFORE UPDATE ON course_edition_campus FOR EACH ROW BEGIN
 IF NOT (NEW.institution_id<=>OLD.institution_id) OR NOT (NEW.course_edition_id<=>OLD.course_edition_id) OR NOT (NEW.campus_id<=>OLD.campus_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$
CREATE TRIGGER mt4_class_insert BEFORE INSERT ON class FOR EACH ROW BEGIN
 IF NEW.course_edition_id IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM course_edition ce JOIN biennium_study_path bsp ON bsp.id=NEW.biennium_study_path_id AND bsp.institution_id=ce.institution_id AND bsp.study_path_id=ce.study_path_id
 WHERE ce.id=NEW.course_edition_id AND ce.institution_id=NEW.institution_id)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 class edition path mismatch'; END IF;
END$$
CREATE TRIGGER mt4_class_update BEFORE UPDATE ON class FOR EACH ROW BEGIN
 IF OLD.course_edition_id IS NOT NULL AND NOT (NEW.course_edition_id<=>OLD.course_edition_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 class attribution is immutable'; END IF;
 IF NEW.course_edition_id IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM course_edition ce JOIN biennium_study_path bsp ON bsp.id=NEW.biennium_study_path_id AND bsp.institution_id=ce.institution_id AND bsp.study_path_id=ce.study_path_id
 WHERE ce.id=NEW.course_edition_id AND ce.institution_id=NEW.institution_id)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 class edition path mismatch'; END IF;
END$$
CREATE TRIGGER mt4_bsp_update BEFORE UPDATE ON biennium_study_path FOR EACH ROW BEGIN
 IF NOT (NEW.study_path_id<=>OLD.study_path_id) AND EXISTS(SELECT 1 FROM class WHERE biennium_study_path_id=OLD.id AND course_edition_id IS NOT NULL)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 assigned class path is historical'; END IF;
END$$
DELIMITER ;
