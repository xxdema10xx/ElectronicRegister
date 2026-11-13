-- MT-3 checkpoint A (partial). No legacy row or generated entity is altered.
-- Run only via guarded tooling against electronic_register_test, after verified backup.
-- MySQL 5.7 DDL commits implicitly. This is NOT full didactic ownership/cutover.
ALTER TABLE institution_membership ADD UNIQUE KEY uq_membership_institution_id (institution_id,id);

CREATE TABLE institution_student_profile (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 student_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 PRIMARY KEY (institution_id,membership_id),
 UNIQUE KEY uq_institution_student_profile_student (student_id),
 CONSTRAINT fk_student_profile_membership FOREIGN KEY (institution_id,membership_id)
   REFERENCES institution_membership (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT fk_student_profile_student FOREIGN KEY (student_id)
   REFERENCES student (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_teacher_profile (
 institution_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 membership_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 teacher_id CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 PRIMARY KEY (institution_id,membership_id),
 UNIQUE KEY uq_institution_teacher_profile_teacher (teacher_id),
 CONSTRAINT fk_teacher_profile_membership FOREIGN KEY (institution_id,membership_id)
   REFERENCES institution_membership (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT fk_teacher_profile_teacher FOREIGN KEY (teacher_id)
   REFERENCES teacher (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
