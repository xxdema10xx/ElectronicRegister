-- Consolidated EMPTY test schema MT-5D. Metadata only; no dump or row data.

-- Fresh authorized test database ONLY. Do not apply historical migrations afterward.

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

CREATE TABLE `audit_event` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `actor_user_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `action` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `entity_type` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `entity_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `before_json` longtext COLLATE utf8mb4_unicode_ci,
  `after_json` longtext COLLATE utf8mb4_unicode_ci,
  `request_id` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `institution_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_audit_actor` (`actor_user_id`,`created_at`),
  KEY `idx_audit_entity` (`entity_type`,`entity_id`,`created_at`),
  KEY `idx_mt3a_audit_owner_created` (`institution_id`,`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `biennium` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `start_year` date NOT NULL,
  `end_year` date NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_biennium` (`institution_id`,`start_year`,`end_year`),
  UNIQUE KEY `uq_mt3a_biennium_owner` (`institution_id`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `biennium_study_area` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `biennium_id` char(36) CHARACTER SET ascii NOT NULL,
  `study_area_id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_biennium_area` (`biennium_id`,`study_area_id`),
  UNIQUE KEY `uq_mt3a_biennium_study_area_owner` (`institution_id`,`id`),
  KEY `idx_bsa_area` (`study_area_id`),
  KEY `idx_mt3a_bsa_biennium` (`institution_id`,`biennium_id`),
  KEY `idx_mt3a_bsa_area` (`institution_id`,`study_area_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `biennium_study_path` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `biennium_study_area_id` char(36) CHARACTER SET ascii NOT NULL,
  `study_path_id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_bsa_path` (`biennium_study_area_id`,`study_path_id`),
  UNIQUE KEY `uq_mt3a_biennium_study_path_owner` (`institution_id`,`id`),
  KEY `idx_bsp_path` (`study_path_id`),
  KEY `idx_mt3a_bsp_bsa` (`institution_id`,`biennium_study_area_id`),
  KEY `idx_mt3a_bsp_path` (`institution_id`,`study_path_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `campus` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `address` varchar(300) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `province` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `is_active` tinyint(1) NOT NULL DEFAULT '1',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_mt4_campus_owner` (`institution_id`,`id`),
  KEY `idx_campus_institution` (`institution_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `class` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `biennium_study_path_id` char(36) CHARACTER SET ascii NOT NULL,
  `name` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  `course_edition_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_class` (`biennium_study_path_id`,`name`),
  UNIQUE KEY `uq_mt3a_class_owner` (`institution_id`,`id`),
  KEY `idx_class_bsp` (`biennium_study_path_id`),
  KEY `idx_mt3a_class_bsp` (`institution_id`,`biennium_study_path_id`),
  KEY `idx_mt4_class_edition` (`institution_id`,`course_edition_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `class_subject` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `class_id` char(36) CHARACTER SET ascii NOT NULL,
  `subject_id` char(36) CHARACTER SET ascii NOT NULL,
  `teacher_id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_class_subject` (`class_id`,`subject_id`),
  UNIQUE KEY `uq_mt3a_class_subject_owner` (`institution_id`,`id`),
  KEY `idx_cs_subject` (`subject_id`),
  KEY `idx_cs_teacher` (`teacher_id`),
  KEY `idx_mt3a_cs_class` (`institution_id`,`class_id`),
  KEY `idx_mt3a_cs_subject` (`institution_id`,`subject_id`),
  KEY `idx_mt3a_cs_teacher` (`institution_id`,`teacher_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `course_administration_event` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `actor_user_id` char(36) CHARACTER SET ascii NOT NULL,
  `actor_membership_id` char(36) CHARACTER SET ascii NOT NULL,
  `course_edition_id` char(36) CHARACTER SET ascii NOT NULL,
  `enrollment_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `campus_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `class_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `action` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `previous_status` varchar(20) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
  `new_status` varchar(20) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
  `previous_completion_date` date DEFAULT NULL,
  `new_completion_date` date DEFAULT NULL,
  `revision_before` bigint(20) DEFAULT NULL,
  `revision_after` bigint(20) DEFAULT NULL,
  `reason` varchar(1000) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `request_hash` char(64) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
  `before_json` longtext COLLATE utf8mb4_unicode_ci,
  `after_json` longtext COLLATE utf8mb4_unicode_ci NOT NULL,
  `occurred_at_utc` datetime(6) NOT NULL,
  PRIMARY KEY (`institution_id`,`id`),
  KEY `idx_mt4c_enrollment_history` (`institution_id`,`enrollment_id`,`revision_after`,`occurred_at_utc`,`id`),
  KEY `idx_mt4c_edition_history` (`institution_id`,`course_edition_id`,`occurred_at_utc`,`id`),
  KEY `fk_mt4c_actor` (`actor_user_id`),
  KEY `fk_mt4c_membership` (`institution_id`,`actor_membership_id`,`actor_user_id`),
  KEY `fk_mt4c_campus` (`institution_id`,`campus_id`),
  KEY `fk_mt4c_class` (`institution_id`,`class_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `course_edition` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `study_path_id` char(36) CHARACTER SET ascii NOT NULL,
  `code` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `start_date` date DEFAULT NULL,
  `end_date` date DEFAULT NULL,
  `status` varchar(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_mt4_edition_owner` (`institution_id`,`id`),
  UNIQUE KEY `uq_mt4_edition_code` (`institution_id`,`code`),
  KEY `idx_mt4_edition_path` (`institution_id`,`study_path_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `course_edition_campus` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `course_edition_id` char(36) CHARACTER SET ascii NOT NULL,
  `campus_id` char(36) CHARACTER SET ascii NOT NULL,
  PRIMARY KEY (`institution_id`,`course_edition_id`,`campus_id`),
  KEY `idx_mt4_edition_campus` (`institution_id`,`campus_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `exam` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `class_subject_id` char(36) CHARACTER SET ascii NOT NULL,
  `retake_of_exam_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `available_from` datetime(6) NOT NULL,
  `available_until` datetime(6) NOT NULL,
  `duration_minutes` int(11) NOT NULL,
  `passing_percent` decimal(5,2) NOT NULL DEFAULT '60.00',
  `total_points` decimal(7,2) NOT NULL,
  `status` enum('draft','published','closed','cancelled') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'draft',
  `published_at` datetime(6) DEFAULT NULL,
  `closed_at` datetime(6) DEFAULT NULL,
  `cancelled_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  `retake_institution_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_mt3a_exam_owner` (`institution_id`,`id`),
  KEY `idx_exam_class_subject` (`class_subject_id`),
  KEY `idx_exam_retake_of` (`retake_of_exam_id`),
  KEY `idx_exam_status_window` (`status`,`available_from`,`available_until`),
  KEY `idx_mt3a_exam_class_subject` (`institution_id`,`class_subject_id`),
  KEY `idx_mt3a_exam_retake_of` (`retake_institution_id`,`retake_of_exam_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `exam_student` (
  `exam_id` char(36) CHARACTER SET ascii NOT NULL,
  `student_id` char(36) CHARACTER SET ascii NOT NULL,
  `extra_time_minutes` int(11) NOT NULL,
  `assigned_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`exam_id`,`student_id`),
  KEY `idx_exam_student_student` (`student_id`,`exam_id`),
  KEY `idx_mt3a_exam_student_exam` (`institution_id`,`exam_id`),
  KEY `idx_mt3a_exam_student_student` (`institution_id`,`student_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `exam_submission` (
  `display_order_json` longtext COLLATE utf8mb4_unicode_ci,
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `exam_id` char(36) CHARACTER SET ascii NOT NULL,
  `student_id` char(36) CHARACTER SET ascii NOT NULL,
  `status` enum('in_progress','graded','pending_review','failed_auto','finalized') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'in_progress',
  `started_at` datetime(6) NOT NULL,
  `deadline_at` datetime(6) NOT NULL,
  `submitted_at` datetime(6) DEFAULT NULL,
  `submit_reason` enum('manual','time_expired','violations') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `violation_count` int(11) NOT NULL,
  `last_client_revision` bigint(20) unsigned NOT NULL,
  `max_score` decimal(7,2) DEFAULT NULL,
  `passing_score` decimal(7,2) DEFAULT NULL,
  `closed_score` decimal(7,2) DEFAULT NULL,
  `open_max_points` decimal(7,2) DEFAULT NULL,
  `open_score` decimal(7,2) DEFAULT NULL,
  `total_score` decimal(7,2) DEFAULT NULL,
  `passed` tinyint(1) DEFAULT NULL,
  `suggested_grade` decimal(4,2) DEFAULT NULL,
  `override_reason` varchar(1000) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `grade_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `reviewed_by_user_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `finalized_at` datetime(6) DEFAULT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  `grade_institution_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_exam_submission_student` (`exam_id`,`student_id`),
  UNIQUE KEY `uq_mt3a_exam_submission_owner` (`institution_id`,`id`),
  UNIQUE KEY `uq_mt3a_submission_exam` (`institution_id`,`exam_id`,`id`),
  UNIQUE KEY `uq_exam_submission_grade` (`grade_id`),
  KEY `fk_exam_submission_reviewer` (`reviewed_by_user_id`),
  KEY `idx_exam_submission_exam_status` (`exam_id`,`status`),
  KEY `idx_exam_submission_expiration` (`status`,`deadline_at`),
  KEY `idx_exam_submission_student_status` (`student_id`,`status`),
  KEY `idx_mt3a_exam_submission_student` (`institution_id`,`student_id`),
  KEY `idx_mt3a_exam_submission_grade` (`grade_institution_id`,`grade_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `exam_violation` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `submission_id` char(36) CHARACTER SET ascii NOT NULL,
  `type` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `metadata_json` longtext COLLATE utf8mb4_unicode_ci,
  `occurred_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  KEY `idx_exam_violation_submission` (`submission_id`,`occurred_at`),
  KEY `idx_mt3a_exam_violation_submission` (`institution_id`,`submission_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `grade` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `student_id` char(36) CHARACTER SET ascii NOT NULL,
  `class_subject_id` char(36) CHARACTER SET ascii NOT NULL,
  `value` decimal(4,2) NOT NULL,
  `date` date NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_mt3a_grade_owner` (`institution_id`,`id`),
  KEY `idx_grade_class_subject` (`class_subject_id`),
  KEY `idx_grade_student` (`student_id`),
  KEY `idx_mt3a_grade_student` (`institution_id`,`student_id`),
  KEY `idx_mt3a_grade_class_subject` (`institution_id`,`class_subject_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `code` varchar(64) CHARACTER SET ascii NOT NULL,
  `legal_tax_code` varchar(32) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `region` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('provisioning','active','suspended','archived') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'provisioning',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_institution_code` (`code`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_invitation` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `recipient_user_id` char(36) CHARACTER SET ascii NOT NULL,
  `kind` varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `roles` json NOT NULL,
  `status` varchar(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `token_hash` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `channel_hash` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `expires_at_utc` datetime(6) NOT NULL,
  `attempts` int(11) NOT NULL DEFAULT '0',
  `created_at_utc` datetime(6) NOT NULL,
  `consumed_membership_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  PRIMARY KEY (`institution_id`,`id`),
  KEY `fk_invitation_recipient` (`recipient_user_id`),
  KEY `fk_invitation_membership` (`institution_id`,`consumed_membership_id`),
  KEY `idx_invitation_recipient` (`institution_id`,`recipient_user_id`,`created_at_utc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_membership` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `user_id` char(36) CHARACTER SET ascii NOT NULL,
  `status` enum('pending','active','suspended','left') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'pending',
  `joined_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `suspended_at` datetime(6) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `AK_institution_membership_institution_id_id` (`institution_id`,`id`),
  UNIQUE KEY `uq_membership_user_institution` (`user_id`,`institution_id`),
  UNIQUE KEY `uq_mt4c_membership_actor` (`institution_id`,`id`,`user_id`),
  KEY `idx_membership_institution` (`institution_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_membership_event` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `actor_user_id` char(36) CHARACTER SET ascii NOT NULL,
  `actor_membership_id` char(36) CHARACTER SET ascii NOT NULL,
  `target_membership_id` char(36) CHARACTER SET ascii NOT NULL,
  `action` varchar(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `target_role` varchar(20) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
  `previous_status` varchar(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `new_status` varchar(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `previous_roles` json NOT NULL,
  `new_roles` json NOT NULL,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `revision_before` bigint(20) NOT NULL,
  `revision_after` bigint(20) NOT NULL,
  `request_hash` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `occurred_at_utc` datetime(6) NOT NULL,
  PRIMARY KEY (`institution_id`,`id`),
  UNIQUE KEY `uq_membership_event_revision` (`institution_id`,`target_membership_id`,`revision_after`),
  KEY `idx_membership_event_actor` (`institution_id`,`actor_membership_id`,`actor_user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_membership_role` (
  `membership_id` char(36) CHARACTER SET ascii NOT NULL,
  `role` enum('InstitutionAdmin','Teacher','Student','Staff') COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`membership_id`,`role`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_membership_security_state` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `membership_id` char(36) CHARACTER SET ascii NOT NULL,
  `security_generation` bigint(20) NOT NULL DEFAULT '0',
  PRIMARY KEY (`institution_id`,`membership_id`),
  UNIQUE KEY `uq_membership_security_identity` (`membership_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_onboarding_event` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `actor_kind` varchar(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `operator_id` varchar(64) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
  `key_id` varchar(64) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL,
  `actor_user_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `actor_membership_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `action` varchar(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `invitation_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `intent_hash` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `evidence_reference` varchar(200) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `result_json` json NOT NULL,
  `occurred_at_utc` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_onboarding_event_owner` (`institution_id`,`id`),
  KEY `idx_onboarding_event_time` (`institution_id`,`occurred_at_utc`,`id`),
  KEY `fk_onboarding_event_invite` (`institution_id`,`invitation_id`),
  KEY `fk_onboarding_event_user` (`actor_user_id`),
  KEY `fk_onboarding_event_actor` (`institution_id`,`actor_membership_id`,`actor_user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_provisioning` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `approval_event_id` char(36) CHARACTER SET ascii NOT NULL,
  `first_invitation_id` char(36) CHARACTER SET ascii NOT NULL,
  `legal_identity` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `evidence_reference` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  PRIMARY KEY (`institution_id`),
  KEY `fk_provisioning_approval` (`institution_id`,`approval_event_id`),
  KEY `fk_provisioning_invitation` (`institution_id`,`first_invitation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_recovery` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `recipient_user_id` char(36) CHARACTER SET ascii NOT NULL,
  `request_event_id` char(36) CHARACTER SET ascii NOT NULL,
  `signed_request` json NOT NULL,
  `expires_at_utc` datetime(6) NOT NULL,
  PRIMARY KEY (`institution_id`,`id`),
  KEY `fk_recovery_recipient` (`recipient_user_id`),
  KEY `fk_recovery_request` (`institution_id`,`request_event_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_recovery_approval` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `recovery_id` char(36) CHARACTER SET ascii NOT NULL,
  `operator_id` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `event_id` char(36) CHARACTER SET ascii NOT NULL,
  `signed_operation` json NOT NULL,
  PRIMARY KEY (`institution_id`,`recovery_id`,`operator_id`),
  KEY `fk_recovery_approval_event` (`institution_id`,`event_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_student_profile` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `membership_id` char(36) CHARACTER SET ascii NOT NULL,
  `student_id` char(36) CHARACTER SET ascii NOT NULL,
  PRIMARY KEY (`institution_id`,`membership_id`),
  UNIQUE KEY `uq_institution_student_profile_student` (`student_id`),
  KEY `idx_mt3a_student_profile` (`institution_id`,`student_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `institution_teacher_profile` (
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `membership_id` char(36) CHARACTER SET ascii NOT NULL,
  `teacher_id` char(36) CHARACTER SET ascii NOT NULL,
  PRIMARY KEY (`institution_id`,`membership_id`),
  UNIQUE KEY `uq_institution_teacher_profile_teacher` (`teacher_id`),
  KEY `idx_mt3a_teacher_profile` (`institution_id`,`teacher_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `mt3a_legacy_scope` (
  `scope` enum('legacy') NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  PRIMARY KEY (`scope`),
  UNIQUE KEY `institution_id` (`institution_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `platform_operation_nonce` (
  `operator_id` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `nonce` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `key_id` varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `event_id` char(36) CHARACTER SET ascii NOT NULL,
  `expires_at_utc` datetime(6) NOT NULL,
  PRIMARY KEY (`operator_id`,`nonce`),
  KEY `fk_platform_nonce_event` (`institution_id`,`event_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `question` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `exam_id` char(36) CHARACTER SET ascii NOT NULL,
  `position` int(11) NOT NULL,
  `text` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `type` enum('single','multiple','open') COLLATE utf8mb4_unicode_ci NOT NULL,
  `points` decimal(7,2) NOT NULL,
  `required` tinyint(1) NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_question_position` (`exam_id`,`position`),
  UNIQUE KEY `uq_mt3a_question_owner` (`institution_id`,`id`),
  UNIQUE KEY `uq_mt3a_question_exam` (`institution_id`,`exam_id`,`id`),
  KEY `idx_question_exam` (`exam_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `question_option` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `question_id` char(36) CHARACTER SET ascii NOT NULL,
  `position` int(11) NOT NULL,
  `text` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_correct` tinyint(1) NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_question_option_position` (`question_id`,`position`),
  UNIQUE KEY `uq_mt3a_option_question` (`institution_id`,`question_id`,`id`),
  KEY `idx_question_option_question` (`question_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `student` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `first_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `last_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `class_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  `class_institution_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_mt3a_student_owner` (`institution_id`,`id`),
  KEY `idx_student_class` (`class_id`),
  KEY `idx_mt3a_student_class` (`class_institution_id`,`class_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `student_enrollment` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL,
  `student_id` char(36) CHARACTER SET ascii NOT NULL,
  `course_edition_id` char(36) CHARACTER SET ascii NOT NULL,
  `enrollment_date` date NOT NULL,
  `completion_date` date DEFAULT NULL,
  `status` varchar(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
  `revision` bigint(20) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_mt4_enrollment_owner` (`institution_id`,`id`),
  UNIQUE KEY `uq_mt4_enrollment_student` (`institution_id`,`student_id`,`course_edition_id`),
  KEY `idx_mt4_enrollment_edition` (`institution_id`,`course_edition_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `study_area` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_study_area_name` (`institution_id`,`name`),
  UNIQUE KEY `uq_mt3a_study_area_owner` (`institution_id`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `study_path` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_study_path_name` (`institution_id`,`name`),
  UNIQUE KEY `uq_mt3a_study_path_owner` (`institution_id`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `subject` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_subject_name` (`institution_id`,`name`),
  UNIQUE KEY `uq_mt3a_subject_owner` (`institution_id`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `submission_answer` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `submission_id` char(36) CHARACTER SET ascii NOT NULL,
  `question_id` char(36) CHARACTER SET ascii NOT NULL,
  `open_text` text COLLATE utf8mb4_unicode_ci,
  `is_correct` tinyint(1) DEFAULT NULL,
  `awarded_points` decimal(7,2) DEFAULT NULL,
  `teacher_comment` text COLLATE utf8mb4_unicode_ci,
  `last_saved_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  `exam_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_submission_answer` (`submission_id`,`question_id`),
  UNIQUE KEY `uq_mt3a_answer_question` (`institution_id`,`question_id`,`id`),
  KEY `idx_submission_answer_question` (`question_id`),
  KEY `idx_submission_answer_submission` (`submission_id`),
  KEY `idx_mt3a_submission_answer_submission` (`institution_id`,`exam_id`,`submission_id`),
  KEY `idx_mt3a_submission_answer_question` (`institution_id`,`exam_id`,`question_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `submission_answer_option` (
  `submission_answer_id` char(36) CHARACTER SET ascii NOT NULL,
  `option_id` char(36) CHARACTER SET ascii NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  `question_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`submission_answer_id`,`option_id`),
  KEY `idx_submission_answer_option_option` (`option_id`),
  KEY `idx_mt3a_submission_answer_option_answer` (`institution_id`,`question_id`,`submission_answer_id`),
  KEY `idx_mt3a_submission_answer_option_option` (`institution_id`,`question_id`,`option_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `teacher` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `first_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `last_name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `institution_id` char(36) CHARACTER SET ascii NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_mt3a_teacher_owner` (`institution_id`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `user` (
  `id` char(36) CHARACTER SET ascii NOT NULL,
  `email` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `password_hash` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `role` enum('admin','teacher','student') COLLATE utf8mb4_unicode_ci NOT NULL,
  `student_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  `teacher_id` char(36) CHARACTER SET ascii DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_user_email` (`email`),
  UNIQUE KEY `idx_user_student` (`student_id`),
  UNIQUE KEY `idx_user_teacher` (`teacher_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE `audit_event` ADD CONSTRAINT `fk_audit_actor` FOREIGN KEY (`actor_user_id`) REFERENCES `user` (`id`) ON DELETE SET NULL;

ALTER TABLE `audit_event` ADD CONSTRAINT `fk_mt3a_audit_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `biennium` ADD CONSTRAINT `fk_mt3a_biennium_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `biennium_study_area` ADD CONSTRAINT `fk_bsa_area` FOREIGN KEY (`study_area_id`) REFERENCES `study_area` (`id`);

ALTER TABLE `biennium_study_area` ADD CONSTRAINT `fk_bsa_biennium` FOREIGN KEY (`biennium_id`) REFERENCES `biennium` (`id`) ON DELETE CASCADE;

ALTER TABLE `biennium_study_area` ADD CONSTRAINT `fk_mt3a_biennium_study_area_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `biennium_study_area` ADD CONSTRAINT `fk_mt3a_bsa_area` FOREIGN KEY (`institution_id`, `study_area_id`) REFERENCES `study_area` (`institution_id`, `id`);

ALTER TABLE `biennium_study_area` ADD CONSTRAINT `fk_mt3a_bsa_biennium` FOREIGN KEY (`institution_id`, `biennium_id`) REFERENCES `biennium` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `biennium_study_path` ADD CONSTRAINT `fk_bsp_bsa` FOREIGN KEY (`biennium_study_area_id`) REFERENCES `biennium_study_area` (`id`) ON DELETE CASCADE;

ALTER TABLE `biennium_study_path` ADD CONSTRAINT `fk_bsp_path` FOREIGN KEY (`study_path_id`) REFERENCES `study_path` (`id`);

ALTER TABLE `biennium_study_path` ADD CONSTRAINT `fk_mt3a_biennium_study_path_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `biennium_study_path` ADD CONSTRAINT `fk_mt3a_bsp_bsa` FOREIGN KEY (`institution_id`, `biennium_study_area_id`) REFERENCES `biennium_study_area` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `biennium_study_path` ADD CONSTRAINT `fk_mt3a_bsp_path` FOREIGN KEY (`institution_id`, `study_path_id`) REFERENCES `study_path` (`institution_id`, `id`);

ALTER TABLE `campus` ADD CONSTRAINT `fk_campus_institution` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `class` ADD CONSTRAINT `fk_class_bsp` FOREIGN KEY (`biennium_study_path_id`) REFERENCES `biennium_study_path` (`id`);

ALTER TABLE `class` ADD CONSTRAINT `fk_mt3a_class_bsp` FOREIGN KEY (`institution_id`, `biennium_study_path_id`) REFERENCES `biennium_study_path` (`institution_id`, `id`);

ALTER TABLE `class` ADD CONSTRAINT `fk_mt3a_class_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `class` ADD CONSTRAINT `fk_mt4_class_edition` FOREIGN KEY (`institution_id`, `course_edition_id`) REFERENCES `course_edition` (`institution_id`, `id`);

ALTER TABLE `class_subject` ADD CONSTRAINT `fk_cs_class` FOREIGN KEY (`class_id`) REFERENCES `class` (`id`) ON DELETE CASCADE;

ALTER TABLE `class_subject` ADD CONSTRAINT `fk_cs_subject` FOREIGN KEY (`subject_id`) REFERENCES `subject` (`id`);

ALTER TABLE `class_subject` ADD CONSTRAINT `fk_cs_teacher` FOREIGN KEY (`teacher_id`) REFERENCES `teacher` (`id`);

ALTER TABLE `class_subject` ADD CONSTRAINT `fk_mt3a_class_subject_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `class_subject` ADD CONSTRAINT `fk_mt3a_cs_class` FOREIGN KEY (`institution_id`, `class_id`) REFERENCES `class` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `class_subject` ADD CONSTRAINT `fk_mt3a_cs_subject` FOREIGN KEY (`institution_id`, `subject_id`) REFERENCES `subject` (`institution_id`, `id`);

ALTER TABLE `class_subject` ADD CONSTRAINT `fk_mt3a_cs_teacher` FOREIGN KEY (`institution_id`, `teacher_id`) REFERENCES `teacher` (`institution_id`, `id`);

ALTER TABLE `course_administration_event` ADD CONSTRAINT `fk_mt4c_actor` FOREIGN KEY (`actor_user_id`) REFERENCES `user` (`id`);

ALTER TABLE `course_administration_event` ADD CONSTRAINT `fk_mt4c_campus` FOREIGN KEY (`institution_id`, `campus_id`) REFERENCES `campus` (`institution_id`, `id`);

ALTER TABLE `course_administration_event` ADD CONSTRAINT `fk_mt4c_class` FOREIGN KEY (`institution_id`, `class_id`) REFERENCES `class` (`institution_id`, `id`);

ALTER TABLE `course_administration_event` ADD CONSTRAINT `fk_mt4c_edition` FOREIGN KEY (`institution_id`, `course_edition_id`) REFERENCES `course_edition` (`institution_id`, `id`);

ALTER TABLE `course_administration_event` ADD CONSTRAINT `fk_mt4c_enrollment` FOREIGN KEY (`institution_id`, `enrollment_id`) REFERENCES `student_enrollment` (`institution_id`, `id`);

ALTER TABLE `course_administration_event` ADD CONSTRAINT `fk_mt4c_membership` FOREIGN KEY (`institution_id`, `actor_membership_id`, `actor_user_id`) REFERENCES `institution_membership` (`institution_id`, `id`, `user_id`);

ALTER TABLE `course_edition` ADD CONSTRAINT `fk_mt4_edition_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `course_edition` ADD CONSTRAINT `fk_mt4_edition_path` FOREIGN KEY (`institution_id`, `study_path_id`) REFERENCES `study_path` (`institution_id`, `id`);

ALTER TABLE `course_edition_campus` ADD CONSTRAINT `fk_mt4_ec_campus` FOREIGN KEY (`institution_id`, `campus_id`) REFERENCES `campus` (`institution_id`, `id`);

ALTER TABLE `course_edition_campus` ADD CONSTRAINT `fk_mt4_ec_edition` FOREIGN KEY (`institution_id`, `course_edition_id`) REFERENCES `course_edition` (`institution_id`, `id`);

ALTER TABLE `exam` ADD CONSTRAINT `fk_exam_class_subject` FOREIGN KEY (`class_subject_id`) REFERENCES `class_subject` (`id`);

ALTER TABLE `exam` ADD CONSTRAINT `fk_exam_retake_of` FOREIGN KEY (`retake_of_exam_id`) REFERENCES `exam` (`id`) ON DELETE SET NULL;

ALTER TABLE `exam` ADD CONSTRAINT `fk_mt3a_exam_class_subject` FOREIGN KEY (`institution_id`, `class_subject_id`) REFERENCES `class_subject` (`institution_id`, `id`);

ALTER TABLE `exam` ADD CONSTRAINT `fk_mt3a_exam_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `exam` ADD CONSTRAINT `fk_mt3a_exam_retake_of` FOREIGN KEY (`retake_institution_id`, `retake_of_exam_id`) REFERENCES `exam` (`institution_id`, `id`) ON DELETE SET NULL;

ALTER TABLE `exam_student` ADD CONSTRAINT `fk_exam_student_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam` (`id`) ON DELETE CASCADE;

ALTER TABLE `exam_student` ADD CONSTRAINT `fk_exam_student_student` FOREIGN KEY (`student_id`) REFERENCES `student` (`id`);

ALTER TABLE `exam_student` ADD CONSTRAINT `fk_mt3a_exam_student_exam` FOREIGN KEY (`institution_id`, `exam_id`) REFERENCES `exam` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `exam_student` ADD CONSTRAINT `fk_mt3a_exam_student_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `exam_student` ADD CONSTRAINT `fk_mt3a_exam_student_student` FOREIGN KEY (`institution_id`, `student_id`) REFERENCES `student` (`institution_id`, `id`);

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_exam_submission_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam` (`id`);

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_exam_submission_grade` FOREIGN KEY (`grade_id`) REFERENCES `grade` (`id`) ON DELETE SET NULL;

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_exam_submission_reviewer` FOREIGN KEY (`reviewed_by_user_id`) REFERENCES `user` (`id`) ON DELETE SET NULL;

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_exam_submission_student` FOREIGN KEY (`student_id`) REFERENCES `student` (`id`);

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_mt3a_exam_submission_exam` FOREIGN KEY (`institution_id`, `exam_id`) REFERENCES `exam` (`institution_id`, `id`);

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_mt3a_exam_submission_grade` FOREIGN KEY (`grade_institution_id`, `grade_id`) REFERENCES `grade` (`institution_id`, `id`) ON DELETE SET NULL;

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_mt3a_exam_submission_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `exam_submission` ADD CONSTRAINT `fk_mt3a_exam_submission_student` FOREIGN KEY (`institution_id`, `student_id`) REFERENCES `student` (`institution_id`, `id`);

ALTER TABLE `exam_violation` ADD CONSTRAINT `fk_exam_violation_submission` FOREIGN KEY (`submission_id`) REFERENCES `exam_submission` (`id`) ON DELETE CASCADE;

ALTER TABLE `exam_violation` ADD CONSTRAINT `fk_mt3a_exam_violation_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `exam_violation` ADD CONSTRAINT `fk_mt3a_exam_violation_submission` FOREIGN KEY (`institution_id`, `submission_id`) REFERENCES `exam_submission` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `grade` ADD CONSTRAINT `fk_grade_class_subject` FOREIGN KEY (`class_subject_id`) REFERENCES `class_subject` (`id`);

ALTER TABLE `grade` ADD CONSTRAINT `fk_grade_student` FOREIGN KEY (`student_id`) REFERENCES `student` (`id`) ON DELETE CASCADE;

ALTER TABLE `grade` ADD CONSTRAINT `fk_mt3a_grade_class_subject` FOREIGN KEY (`institution_id`, `class_subject_id`) REFERENCES `class_subject` (`institution_id`, `id`);

ALTER TABLE `grade` ADD CONSTRAINT `fk_mt3a_grade_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `grade` ADD CONSTRAINT `fk_mt3a_grade_student` FOREIGN KEY (`institution_id`, `student_id`) REFERENCES `student` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `institution_invitation` ADD CONSTRAINT `fk_invitation_membership` FOREIGN KEY (`institution_id`, `consumed_membership_id`) REFERENCES `institution_membership` (`institution_id`, `id`);

ALTER TABLE `institution_invitation` ADD CONSTRAINT `fk_invitation_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `institution_invitation` ADD CONSTRAINT `fk_invitation_recipient` FOREIGN KEY (`recipient_user_id`) REFERENCES `user` (`id`);

ALTER TABLE `institution_membership` ADD CONSTRAINT `fk_membership_institution` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `institution_membership` ADD CONSTRAINT `fk_membership_user` FOREIGN KEY (`user_id`) REFERENCES `user` (`id`);

ALTER TABLE `institution_membership_event` ADD CONSTRAINT `fk_membership_event_actor` FOREIGN KEY (`institution_id`, `actor_membership_id`, `actor_user_id`) REFERENCES `institution_membership` (`institution_id`, `id`, `user_id`);

ALTER TABLE `institution_membership_event` ADD CONSTRAINT `fk_membership_event_target` FOREIGN KEY (`institution_id`, `target_membership_id`) REFERENCES `institution_membership` (`institution_id`, `id`);

ALTER TABLE `institution_membership_role` ADD CONSTRAINT `fk_membership_role_membership` FOREIGN KEY (`membership_id`) REFERENCES `institution_membership` (`id`);

ALTER TABLE `institution_membership_security_state` ADD CONSTRAINT `fk_membership_security_owner` FOREIGN KEY (`institution_id`, `membership_id`) REFERENCES `institution_membership` (`institution_id`, `id`);

ALTER TABLE `institution_onboarding_event` ADD CONSTRAINT `fk_onboarding_event_actor` FOREIGN KEY (`institution_id`, `actor_membership_id`, `actor_user_id`) REFERENCES `institution_membership` (`institution_id`, `id`, `user_id`);

ALTER TABLE `institution_onboarding_event` ADD CONSTRAINT `fk_onboarding_event_invite` FOREIGN KEY (`institution_id`, `invitation_id`) REFERENCES `institution_invitation` (`institution_id`, `id`);

ALTER TABLE `institution_onboarding_event` ADD CONSTRAINT `fk_onboarding_event_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `institution_onboarding_event` ADD CONSTRAINT `fk_onboarding_event_user` FOREIGN KEY (`actor_user_id`) REFERENCES `user` (`id`);

ALTER TABLE `institution_provisioning` ADD CONSTRAINT `fk_provisioning_approval` FOREIGN KEY (`institution_id`, `approval_event_id`) REFERENCES `institution_onboarding_event` (`institution_id`, `id`);

ALTER TABLE `institution_provisioning` ADD CONSTRAINT `fk_provisioning_invitation` FOREIGN KEY (`institution_id`, `first_invitation_id`) REFERENCES `institution_invitation` (`institution_id`, `id`);

ALTER TABLE `institution_provisioning` ADD CONSTRAINT `fk_provisioning_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `institution_recovery` ADD CONSTRAINT `fk_recovery_invitation` FOREIGN KEY (`institution_id`, `id`) REFERENCES `institution_invitation` (`institution_id`, `id`);

ALTER TABLE `institution_recovery` ADD CONSTRAINT `fk_recovery_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `institution_recovery` ADD CONSTRAINT `fk_recovery_recipient` FOREIGN KEY (`recipient_user_id`) REFERENCES `user` (`id`);

ALTER TABLE `institution_recovery` ADD CONSTRAINT `fk_recovery_request` FOREIGN KEY (`institution_id`, `request_event_id`) REFERENCES `institution_onboarding_event` (`institution_id`, `id`);

ALTER TABLE `institution_recovery_approval` ADD CONSTRAINT `fk_recovery_approval_case` FOREIGN KEY (`institution_id`, `recovery_id`) REFERENCES `institution_recovery` (`institution_id`, `id`);

ALTER TABLE `institution_recovery_approval` ADD CONSTRAINT `fk_recovery_approval_event` FOREIGN KEY (`institution_id`, `event_id`) REFERENCES `institution_onboarding_event` (`institution_id`, `id`);

ALTER TABLE `institution_student_profile` ADD CONSTRAINT `fk_mt3a_student_profile_owner` FOREIGN KEY (`institution_id`, `student_id`) REFERENCES `student` (`institution_id`, `id`);

ALTER TABLE `institution_student_profile` ADD CONSTRAINT `fk_student_profile_membership` FOREIGN KEY (`institution_id`, `membership_id`) REFERENCES `institution_membership` (`institution_id`, `id`);

ALTER TABLE `institution_student_profile` ADD CONSTRAINT `fk_student_profile_student` FOREIGN KEY (`student_id`) REFERENCES `student` (`id`);

ALTER TABLE `institution_teacher_profile` ADD CONSTRAINT `fk_mt3a_teacher_profile_owner` FOREIGN KEY (`institution_id`, `teacher_id`) REFERENCES `teacher` (`institution_id`, `id`);

ALTER TABLE `institution_teacher_profile` ADD CONSTRAINT `fk_teacher_profile_membership` FOREIGN KEY (`institution_id`, `membership_id`) REFERENCES `institution_membership` (`institution_id`, `id`);

ALTER TABLE `institution_teacher_profile` ADD CONSTRAINT `fk_teacher_profile_teacher` FOREIGN KEY (`teacher_id`) REFERENCES `teacher` (`id`);

ALTER TABLE `mt3a_legacy_scope` ADD CONSTRAINT `fk_mt3a_legacy_scope` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `platform_operation_nonce` ADD CONSTRAINT `fk_platform_nonce_event` FOREIGN KEY (`institution_id`, `event_id`) REFERENCES `institution_onboarding_event` (`institution_id`, `id`);

ALTER TABLE `question` ADD CONSTRAINT `fk_mt3a_question_exam` FOREIGN KEY (`institution_id`, `exam_id`) REFERENCES `exam` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `question` ADD CONSTRAINT `fk_mt3a_question_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `question` ADD CONSTRAINT `fk_question_exam` FOREIGN KEY (`exam_id`) REFERENCES `exam` (`id`) ON DELETE CASCADE;

ALTER TABLE `question_option` ADD CONSTRAINT `fk_mt3a_question_option_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `question_option` ADD CONSTRAINT `fk_mt3a_question_option_question` FOREIGN KEY (`institution_id`, `question_id`) REFERENCES `question` (`institution_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `question_option` ADD CONSTRAINT `fk_question_option_question` FOREIGN KEY (`question_id`) REFERENCES `question` (`id`) ON DELETE CASCADE;

ALTER TABLE `student` ADD CONSTRAINT `fk_mt3a_student_class` FOREIGN KEY (`class_institution_id`, `class_id`) REFERENCES `class` (`institution_id`, `id`) ON DELETE SET NULL;

ALTER TABLE `student` ADD CONSTRAINT `fk_mt3a_student_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `student` ADD CONSTRAINT `fk_student_class` FOREIGN KEY (`class_id`) REFERENCES `class` (`id`) ON DELETE SET NULL;

ALTER TABLE `student_enrollment` ADD CONSTRAINT `fk_mt4_enrollment_edition` FOREIGN KEY (`institution_id`, `course_edition_id`) REFERENCES `course_edition` (`institution_id`, `id`);

ALTER TABLE `student_enrollment` ADD CONSTRAINT `fk_mt4_enrollment_student` FOREIGN KEY (`institution_id`, `student_id`) REFERENCES `student` (`institution_id`, `id`);

ALTER TABLE `study_area` ADD CONSTRAINT `fk_mt3a_study_area_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `study_path` ADD CONSTRAINT `fk_mt3a_study_path_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `subject` ADD CONSTRAINT `fk_mt3a_subject_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `submission_answer` ADD CONSTRAINT `fk_mt3a_submission_answer_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `submission_answer` ADD CONSTRAINT `fk_mt3a_submission_answer_question` FOREIGN KEY (`institution_id`, `exam_id`, `question_id`) REFERENCES `question` (`institution_id`, `exam_id`, `id`);

ALTER TABLE `submission_answer` ADD CONSTRAINT `fk_mt3a_submission_answer_submission` FOREIGN KEY (`institution_id`, `exam_id`, `submission_id`) REFERENCES `exam_submission` (`institution_id`, `exam_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `submission_answer` ADD CONSTRAINT `fk_submission_answer_question` FOREIGN KEY (`question_id`) REFERENCES `question` (`id`);

ALTER TABLE `submission_answer` ADD CONSTRAINT `fk_submission_answer_submission` FOREIGN KEY (`submission_id`) REFERENCES `exam_submission` (`id`) ON DELETE CASCADE;

ALTER TABLE `submission_answer_option` ADD CONSTRAINT `fk_mt3a_submission_answer_option_answer` FOREIGN KEY (`institution_id`, `question_id`, `submission_answer_id`) REFERENCES `submission_answer` (`institution_id`, `question_id`, `id`) ON DELETE CASCADE;

ALTER TABLE `submission_answer_option` ADD CONSTRAINT `fk_mt3a_submission_answer_option_option` FOREIGN KEY (`institution_id`, `question_id`, `option_id`) REFERENCES `question_option` (`institution_id`, `question_id`, `id`);

ALTER TABLE `submission_answer_option` ADD CONSTRAINT `fk_mt3a_submission_answer_option_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `submission_answer_option` ADD CONSTRAINT `fk_submission_answer_option_answer` FOREIGN KEY (`submission_answer_id`) REFERENCES `submission_answer` (`id`) ON DELETE CASCADE;

ALTER TABLE `submission_answer_option` ADD CONSTRAINT `fk_submission_answer_option_option` FOREIGN KEY (`option_id`) REFERENCES `question_option` (`id`);

ALTER TABLE `teacher` ADD CONSTRAINT `fk_mt3a_teacher_owner` FOREIGN KEY (`institution_id`) REFERENCES `institution` (`id`);

ALTER TABLE `user` ADD CONSTRAINT `fk_user_student` FOREIGN KEY (`student_id`) REFERENCES `student` (`id`) ON DELETE SET NULL;

ALTER TABLE `user` ADD CONSTRAINT `fk_user_teacher` FOREIGN KEY (`teacher_id`) REFERENCES `teacher` (`id`) ON DELETE SET NULL;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_audit_insert BEFORE INSERT ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;

 IF NEW.entity_type='platform' THEN
 IF NEW.institution_id IS NOT NULL OR NEW.entity_id IS NOT NULL OR NEW.before_json IS NOT NULL OR NEW.after_json IS NOT NULL OR NEW.action NOT LIKE 'platform.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A platform shape required'; END IF;
 ELSE
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='grade' THEN SET resolved=(SELECT institution_id FROM grade WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'INSERT'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END IF;
 END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_audit_update BEFORE UPDATE ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;
 IF OLD.action LIKE 'staff.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Staff audit append only'; END IF;
 IF NOT(NEW.institution_id <=> OLD.institution_id) OR NOT(NEW.entity_type <=> OLD.entity_type) OR NOT(NEW.entity_id <=> OLD.entity_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit attribution immutable'; END IF;
 IF NEW.entity_type='platform' THEN
 IF NEW.institution_id IS NOT NULL OR NEW.entity_id IS NOT NULL OR NEW.before_json IS NOT NULL OR NEW.after_json IS NOT NULL OR NEW.action NOT LIKE 'platform.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A platform shape required'; END IF;
 ELSE
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='grade' THEN SET resolved=(SELECT institution_id FROM grade WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF 'UPDATE'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END IF;
 END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_biennium_insert BEFORE INSERT ON `biennium` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_biennium_update BEFORE UPDATE ON `biennium` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_biennium_study_area_insert BEFORE INSERT ON `biennium_study_area` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `biennium` WHERE id=NEW.biennium_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_biennium_study_area_update BEFORE UPDATE ON `biennium_study_area` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_biennium_study_path_insert BEFORE INSERT ON `biennium_study_path` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `biennium_study_area` WHERE id=NEW.biennium_study_area_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_biennium_study_path_update BEFORE UPDATE ON `biennium_study_path` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_bsp_update BEFORE UPDATE ON biennium_study_path FOR EACH ROW BEGIN
 IF NOT (NEW.study_path_id<=>OLD.study_path_id) AND EXISTS(SELECT 1 FROM class WHERE biennium_study_path_id=OLD.id AND course_edition_id IS NOT NULL)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 assigned class path is historical'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_class_insert BEFORE INSERT ON `class` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `biennium_study_path` WHERE id=NEW.biennium_study_path_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_class_insert BEFORE INSERT ON class FOR EACH ROW BEGIN
 IF NEW.course_edition_id IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM course_edition ce JOIN biennium_study_path bsp ON bsp.id=NEW.biennium_study_path_id AND bsp.institution_id=ce.institution_id AND bsp.study_path_id=ce.study_path_id
 WHERE ce.id=NEW.course_edition_id AND ce.institution_id=NEW.institution_id)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 class edition path mismatch'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_class_update BEFORE UPDATE ON `class` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_class_update BEFORE UPDATE ON class FOR EACH ROW BEGIN
 IF OLD.course_edition_id IS NOT NULL AND NOT (NEW.course_edition_id<=>OLD.course_edition_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 class attribution is immutable'; END IF;
 IF NEW.course_edition_id IS NOT NULL AND NOT EXISTS(
 SELECT 1 FROM course_edition ce JOIN biennium_study_path bsp ON bsp.id=NEW.biennium_study_path_id AND bsp.institution_id=ce.institution_id AND bsp.study_path_id=ce.study_path_id
 WHERE ce.id=NEW.course_edition_id AND ce.institution_id=NEW.institution_id)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 class edition path mismatch'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_class_subject_insert BEFORE INSERT ON `class_subject` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `class` WHERE id=NEW.class_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_class_subject_update BEFORE UPDATE ON `class_subject` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4c_course_event_delete BEFORE DELETE ON course_administration_event FOR EACH ROW BEGIN
 
 
 IF NOT(DATABASE()='electronic_register_test' AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND COALESCE(@er_mt4c_purge_owner,'')=OLD.institution_id AND EXISTS(
   SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND i.code REGEXP '^MT4B_[0-9a-f]{32}$'))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C append only'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4c_course_event_insert BEFORE INSERT ON course_administration_event FOR EACH ROW BEGIN
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
 WHERE m.id=NEW.actor_membership_id AND m.institution_id=NEW.institution_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C verified actor required'; END IF;
 IF NOT JSON_VALID(NEW.after_json) OR (NEW.before_json IS NOT NULL AND NOT JSON_VALID(NEW.before_json)) OR
 NEW.action NOT IN ('course-edition.create','course-edition.update','campus.link','class.assign','enrollment.create','enrollment.transition','enrollment.completion-date.correct')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C event shape invalid'; END IF;
 IF NEW.action LIKE 'enrollment.%' THEN
  IF NEW.enrollment_id IS NULL OR NEW.campus_id IS NOT NULL OR NEW.class_id IS NOT NULL OR NOT EXISTS(
   SELECT 1 FROM student_enrollment e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.enrollment_id AND e.course_edition_id=NEW.course_edition_id
   AND e.status=NEW.new_status AND e.completion_date <=> NEW.new_completion_date AND e.revision=NEW.revision_after)
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C enrollment snapshot invalid'; END IF;
 ELSEIF NEW.enrollment_id IS NOT NULL OR NEW.previous_status IS NOT NULL OR NEW.new_status IS NOT NULL OR NEW.revision_before IS NOT NULL OR NEW.revision_after IS NOT NULL OR NEW.reason IS NOT NULL OR NEW.request_hash IS NOT NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C catalog shape invalid';
 END IF;
 IF NEW.action IN ('enrollment.transition','enrollment.completion-date.correct') THEN
  IF NEW.reason IS NULL OR NEW.reason NOT REGEXP '[^[:space:]]' OR NEW.request_hash IS NULL OR NEW.request_hash NOT REGEXP '^[0-9A-F]{64}$'
   OR NEW.revision_before IS NULL OR NEW.revision_before<0 OR NEW.revision_after<>NEW.revision_before+1 OR NEW.new_completion_date IS NULL
  THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C lifecycle shape invalid'; END IF;
  IF NEW.action='enrollment.transition' AND (NOT(NEW.previous_status <=> 'enrolled') OR NEW.new_status NOT IN ('completed','withdrawn','cancelled')) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C transition invalid'; END IF;
  IF NEW.action='enrollment.completion-date.correct' AND (NEW.new_status NOT IN ('completed','withdrawn','cancelled') OR NOT(NEW.previous_status <=> NEW.new_status)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C correction invalid'; END IF;
 END IF;
 IF NEW.action='enrollment.create' AND (NEW.previous_status IS NOT NULL OR NEW.revision_before IS NOT NULL OR NEW.revision_after<>0) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C creation shape invalid'; END IF;
 IF (NEW.action='campus.link') <> (NEW.campus_id IS NOT NULL) OR (NEW.action='class.assign') <> (NEW.class_id IS NOT NULL) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C link shape invalid'; END IF;
 IF NEW.action='campus.link' AND NOT EXISTS(SELECT 1 FROM course_edition_campus c WHERE c.institution_id=NEW.institution_id AND c.course_edition_id=NEW.course_edition_id AND c.campus_id=NEW.campus_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C campus snapshot invalid'; END IF;
 IF NEW.action='class.assign' AND NOT EXISTS(SELECT 1 FROM class c WHERE c.institution_id=NEW.institution_id AND c.course_edition_id=NEW.course_edition_id AND c.id=NEW.class_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C class snapshot invalid'; END IF;
 SET NEW.occurred_at_utc=UTC_TIMESTAMP(6);
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4c_course_event_update BEFORE UPDATE ON course_administration_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C append only';
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_course_edition_insert BEFORE INSERT ON course_edition FOR EACH ROW BEGIN
 IF NEW.code NOT REGEXP '[^[:space:]]' OR NEW.name NOT REGEXP '[^[:space:]]' OR BINARY NEW.status NOT IN ('planned','active','completed','cancelled') OR (NEW.start_date IS NOT NULL AND YEAR(NEW.start_date)<1000) OR (NEW.end_date IS NOT NULL AND YEAR(NEW.end_date)<1000) OR (NEW.start_date IS NOT NULL AND NEW.end_date IS NOT NULL AND NEW.end_date<NEW.start_date) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_course_edition_update BEFORE UPDATE ON course_edition FOR EACH ROW BEGIN
 IF NEW.code NOT REGEXP '[^[:space:]]' OR NEW.name NOT REGEXP '[^[:space:]]' OR BINARY NEW.status NOT IN ('planned','active','completed','cancelled') OR (NEW.start_date IS NOT NULL AND YEAR(NEW.start_date)<1000) OR (NEW.end_date IS NOT NULL AND YEAR(NEW.end_date)<1000) OR (NEW.start_date IS NOT NULL AND NEW.end_date IS NOT NULL AND NEW.end_date<NEW.start_date) OR NOT (NEW.id<=>OLD.id) OR NOT (NEW.institution_id<=>OLD.institution_id) OR NOT (NEW.study_path_id<=>OLD.study_path_id) OR NOT (NEW.created_at<=>OLD.created_at) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_course_edition_campus_update BEFORE UPDATE ON course_edition_campus FOR EACH ROW BEGIN
 IF NOT (NEW.institution_id<=>OLD.institution_id) OR NOT (NEW.course_edition_id<=>OLD.course_edition_id) OR NOT (NEW.campus_id<=>OLD.campus_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_insert BEFORE INSERT ON `exam` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `class_subject` WHERE id=NEW.class_subject_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.retake_institution_id=IF(NEW.retake_of_exam_id IS NULL,NULL,NEW.institution_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_update BEFORE UPDATE ON `exam` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.retake_institution_id=IF(NEW.retake_of_exam_id IS NULL,NULL,NEW.institution_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_student_insert BEFORE INSERT ON `exam_student` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam` WHERE id=NEW.exam_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_student_update BEFORE UPDATE ON `exam_student` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_submission_insert BEFORE INSERT ON `exam_submission` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam` WHERE id=NEW.exam_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.grade_institution_id=IF(NEW.grade_id IS NULL,NULL,NEW.institution_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_submission_update BEFORE UPDATE ON `exam_submission` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.grade_institution_id=IF(NEW.grade_id IS NULL,NULL,NEW.institution_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_violation_insert BEFORE INSERT ON `exam_violation` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam_submission` WHERE id=NEW.submission_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_exam_violation_update BEFORE UPDATE ON `exam_violation` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_grade_insert BEFORE INSERT ON `grade` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `student` WHERE id=NEW.student_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_grade_update BEFORE UPDATE ON `grade` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_institution_generation AFTER UPDATE ON institution FOR EACH ROW BEGIN
 IF OLD.status IN ('active','suspended') AND NOT(NEW.status <=> OLD.status) THEN
  UPDATE institution_membership_security_state SET security_generation=security_generation+1 WHERE institution_id=NEW.id;
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_institution_transition BEFORE UPDATE ON institution FOR EACH ROW BEGIN
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

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_invitation_delete BEFORE DELETE ON institution_invitation FOR EACH ROW BEGIN
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

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_invitation_insert BEFORE INSERT ON institution_invitation FOR EACH ROW BEGIN
 IF NEW.kind NOT IN ('FirstAdmin','Membership','Recovery') OR NEW.status<>'pending' OR NEW.attempts<>0 OR NEW.consumed_membership_id IS NOT NULL
  OR NEW.token_hash NOT REGEXP BINARY '^[A-F0-9]{64}$' OR NEW.channel_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'
  OR NEW.expires_at_utc<=NEW.created_at_utc OR JSON_TYPE(NEW.roles)<>'ARRAY' OR JSON_LENGTH(NEW.roles) NOT BETWEEN 1 AND 4
  OR JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[0]'))<>'STRING'
  OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[0]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff')
  OR (JSON_LENGTH(NEW.roles)>1 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[1]'))<>'STRING' OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[1]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff')))
  OR (JSON_LENGTH(NEW.roles)>2 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[2]'))<>'STRING' OR JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[2]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff')))
  OR (JSON_LENGTH(NEW.roles)>1 AND JSON_EXTRACT(NEW.roles,'$[0]')=JSON_EXTRACT(NEW.roles,'$[1]'))
  OR (JSON_LENGTH(NEW.roles)>2 AND (JSON_EXTRACT(NEW.roles,'$[0]')=JSON_EXTRACT(NEW.roles,'$[2]') OR JSON_EXTRACT(NEW.roles,'$[1]')=JSON_EXTRACT(NEW.roles,'$[2]')))
  OR (JSON_LENGTH(NEW.roles)>3 AND (JSON_TYPE(JSON_EXTRACT(NEW.roles,'$[3]'))<>'STRING' OR BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.roles,'$[3]')) NOT IN ('InstitutionAdmin','Teacher','Student','Staff') OR JSON_EXTRACT(NEW.roles,'$[3]')=JSON_EXTRACT(NEW.roles,'$[0]') OR JSON_EXTRACT(NEW.roles,'$[3]')=JSON_EXTRACT(NEW.roles,'$[1]') OR JSON_EXTRACT(NEW.roles,'$[3]')=JSON_EXTRACT(NEW.roles,'$[2]')))
  OR (NEW.kind IN ('FirstAdmin','Recovery') AND CAST(NEW.roles AS CHAR)<>'["InstitutionAdmin"]') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C invalid invitation';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_invitation_update BEFORE UPDATE ON institution_invitation FOR EACH ROW BEGIN
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

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_membership_security_insert AFTER INSERT ON institution_membership
FOR EACH ROW BEGIN
 INSERT INTO institution_membership_security_state
  (institution_id,membership_id,security_generation) VALUES(NEW.institution_id,NEW.id,0);
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_membership_security_update AFTER UPDATE ON institution_membership
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
 
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_membership_delete_contract BEFORE DELETE ON institution_membership
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
  SIGNAL SQLSTATE '23000' SET MYSQL_ERRNO=1451,
   MESSAGE_TEXT='MT5B membership is retained by its security state';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_membership_security_delete BEFORE DELETE ON institution_membership
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
 
 
 DELETE FROM institution_membership_security_state
 WHERE institution_id=OLD.institution_id AND membership_id=OLD.id;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_membership_identity_update BEFORE UPDATE ON institution_membership
FOR EACH ROW BEGIN
 IF NOT(BINARY NEW.id <=> BINARY OLD.id)
    OR NOT(BINARY NEW.institution_id <=> BINARY OLD.institution_id)
    OR NOT(BINARY NEW.user_id <=> BINARY OLD.user_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B membership identity is immutable';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5d_event_delete BEFORE DELETE ON institution_membership_event
FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D audit retention required';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5d_event_insert BEFORE INSERT ON institution_membership_event
FOR EACH ROW BEGIN
 DECLARE n_before INT;
 DECLARE n_after INT;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.actor_membership_id=NEW.target_membership_id
  OR NEW.revision_before<0 OR NEW.revision_before=9223372036854775807 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid identity or revision';
 END IF;
 IF NEW.revision_after<>NEW.revision_before+1 OR CHAR_LENGTH(TRIM(NEW.reason))=0
  OR NEW.reason REGEXP '[[:cntrl:]]' OR NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$')
  OR NEW.occurred_at_utc IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid event shape';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id
   JOIN user u ON u.id=m.user_id JOIN institution_membership_role r ON r.membership_id=m.id
   JOIN institution_membership_security_state s ON s.institution_id=m.institution_id AND s.membership_id=m.id
   WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id
   AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin' AND s.security_generation>=0) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D actor is not an active institution admin';
 END IF;
 IF JSON_TYPE(NEW.previous_roles)<>'ARRAY' OR JSON_TYPE(NEW.new_roles)<>'ARRAY'
   OR NEW.previous_status NOT IN ('active','suspended') OR NEW.new_status NOT IN ('active','suspended') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid snapshot';
 END IF;
 SET n_before=JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('InstitutionAdmin'))+
   JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Student'))+JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Teacher'))+JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE('Staff'));
 SET n_after=JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('InstitutionAdmin'))+
   JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Student'))+JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Teacher'))+JSON_CONTAINS(NEW.new_roles,JSON_QUOTE('Staff'));
 IF JSON_LENGTH(NEW.previous_roles)<>n_before OR JSON_LENGTH(NEW.new_roles)<>n_after THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid or duplicate role';
 END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_security_state s
   ON s.institution_id=m.institution_id AND s.membership_id=m.id WHERE m.institution_id=NEW.institution_id
   AND m.id=NEW.target_membership_id AND BINARY m.status=BINARY NEW.new_status AND s.security_generation=NEW.revision_after)
  OR n_after<>(SELECT COUNT(*) FROM institution_membership_role WHERE membership_id=NEW.target_membership_id)
  OR EXISTS(SELECT 1 FROM institution_membership_role WHERE membership_id=NEW.target_membership_id
   AND JSON_CONTAINS(NEW.new_roles,JSON_QUOTE(role))<>1) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D snapshot does not match persisted mutation';
 END IF;
 IF NEW.action IN ('AddRole','RemoveRole') THEN
  IF NEW.target_role IS NULL OR NEW.target_role NOT IN ('InstitutionAdmin','Teacher','Student','Staff')
   OR NEW.previous_status<>NEW.new_status THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid role mutation';
  END IF;
  IF NEW.action='AddRole' THEN
   IF n_after<>n_before+1 OR JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE(NEW.target_role))<>0
    OR JSON_CONTAINS(NEW.new_roles,JSON_QUOTE(NEW.target_role))<>1 OR JSON_CONTAINS(NEW.new_roles,NEW.previous_roles)<>1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid role addition';
   END IF;
  ELSE
   IF n_before<>n_after+1 OR JSON_CONTAINS(NEW.previous_roles,JSON_QUOTE(NEW.target_role))<>1
    OR JSON_CONTAINS(NEW.new_roles,JSON_QUOTE(NEW.target_role))<>0 OR JSON_CONTAINS(NEW.previous_roles,NEW.new_roles)<>1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid role removal';
   END IF;
  END IF;
 ELSEIF NEW.action IN ('Suspend','Reactivate') THEN
  IF NEW.target_role IS NOT NULL OR JSON_CONTAINS(NEW.previous_roles,NEW.new_roles)<>1
    OR JSON_CONTAINS(NEW.new_roles,NEW.previous_roles)<>1
    OR (NEW.action='Suspend' AND NOT(NEW.previous_status='active' AND NEW.new_status='suspended'))
    OR (NEW.action='Reactivate' AND NOT(NEW.previous_status='suspended' AND NEW.new_status='active')) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid status mutation';
  END IF;
 ELSE
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D unknown action';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5d_event_snapshot_shape BEFORE INSERT ON institution_membership_event
FOR EACH ROW BEGIN
 DECLARE position_index INT DEFAULT 0;
 IF JSON_LENGTH(NEW.previous_roles)>4 OR JSON_LENGTH(NEW.new_roles)>4 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D role snapshot exceeds catalog';
 END IF;
 WHILE position_index<4 DO
  IF position_index<JSON_LENGTH(NEW.previous_roles) AND NOT(COALESCE((
    JSON_TYPE(JSON_EXTRACT(NEW.previous_roles,CONCAT('$[',position_index,']')))='STRING'
    AND BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.previous_roles,CONCAT('$[',position_index,']'))) IN ('InstitutionAdmin','Teacher','Student','Staff')),0)) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D previous roles must be flat catalog strings';
  END IF;
  IF position_index<JSON_LENGTH(NEW.new_roles) AND NOT(COALESCE((
    JSON_TYPE(JSON_EXTRACT(NEW.new_roles,CONCAT('$[',position_index,']')))='STRING'
    AND BINARY JSON_UNQUOTE(JSON_EXTRACT(NEW.new_roles,CONCAT('$[',position_index,']'))) IN ('InstitutionAdmin','Teacher','Student','Staff')),0)) THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D new roles must be flat catalog strings';
  END IF;
  SET position_index=position_index+1;
 END WHILE;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5d_event_update BEFORE UPDATE ON institution_membership_event
FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D audit is immutable';
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_role_security_delete AFTER DELETE ON institution_membership_role
FOR EACH ROW BEGIN
 DECLARE v_owner CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL;
 SELECT institution_id INTO v_owner FROM institution_membership WHERE id=OLD.membership_id;
 
 UPDATE institution_membership_security_state
 SET security_generation=security_generation+1
 WHERE institution_id=v_owner AND membership_id=OLD.membership_id
   AND security_generation>=0 AND security_generation<9223372036854775807;
 IF ROW_COUNT()<>1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B missing security state or generation exhausted';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_role_security_insert AFTER INSERT ON institution_membership_role
FOR EACH ROW BEGIN
 DECLARE v_owner CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci DEFAULT NULL;
 SELECT institution_id INTO v_owner FROM institution_membership WHERE id=NEW.membership_id;
 
 UPDATE institution_membership_security_state
 SET security_generation=security_generation+1
 WHERE institution_id=v_owner AND membership_id=NEW.membership_id
   AND security_generation>=0 AND security_generation<9223372036854775807;
 IF ROW_COUNT()<>1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B missing security state or generation exhausted';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_role_identity_update BEFORE UPDATE ON institution_membership_role
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

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_security_delete BEFORE DELETE ON institution_membership_security_state
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
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B security state retention required';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_security_insert BEFORE INSERT ON institution_membership_security_state
FOR EACH ROW BEGIN
 
 IF NEW.security_generation IS NULL OR NEW.security_generation<>0 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B initial generation must be zero';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5b_security_update BEFORE UPDATE ON institution_membership_security_state
FOR EACH ROW BEGIN
 IF NOT(BINARY NEW.institution_id <=> BINARY OLD.institution_id)
    OR NOT(BINARY NEW.membership_id <=> BINARY OLD.membership_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B security identity is immutable';
 END IF;
 IF NEW.security_generation IS NULL OR NEW.security_generation<0
    OR NEW.security_generation<OLD.security_generation THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5B generation cannot decrease';
 END IF;
 
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_event_delete BEFORE DELETE ON institution_onboarding_event FOR EACH ROW BEGIN
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

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_event_insert BEFORE INSERT ON institution_onboarding_event FOR EACH ROW BEGIN
 IF NEW.actor_kind NOT IN ('Operator','Member','Recipient') OR NEW.intent_hash NOT REGEXP BINARY '^[A-F0-9]{64}$'
  OR CHAR_LENGTH(TRIM(NEW.reason)) NOT BETWEEN 1 AND 500 OR JSON_TYPE(NEW.result_json)<>'OBJECT'
  OR (NEW.actor_kind='Operator' AND (NEW.operator_id IS NULL OR NEW.key_id IS NULL OR NEW.actor_user_id IS NOT NULL OR NEW.actor_membership_id IS NOT NULL))
  OR (NEW.actor_kind<>'Operator' AND (NEW.operator_id IS NOT NULL OR NEW.key_id IS NOT NULL OR NEW.actor_user_id IS NULL))
  OR (NEW.actor_kind='Member' AND NEW.actor_membership_id IS NULL)
  OR (NEW.actor_kind='Recipient' AND NEW.actor_membership_id IS NOT NULL) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C invalid attributed event';
 END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_event_update BEFORE UPDATE ON institution_onboarding_event FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C immutable audit'; END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_provisioning_delete BEFORE DELETE ON institution_provisioning FOR EACH ROW BEGIN
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

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_provisioning_insert BEFORE INSERT ON institution_provisioning FOR EACH ROW BEGIN
 IF CHAR_LENGTH(TRIM(NEW.legal_identity))=0 OR CHAR_LENGTH(TRIM(NEW.evidence_reference))=0
 OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.approval_event_id AND e.actor_kind='Operator' AND e.action='Provision')
 OR NOT EXISTS(SELECT 1 FROM institution_invitation i WHERE i.institution_id=NEW.institution_id AND i.id=NEW.first_invitation_id AND i.kind='FirstAdmin' AND i.status='pending') THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C verified approval required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_provisioning_update BEFORE UPDATE ON institution_provisioning FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C immutable approval'; END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_recovery_delete BEFORE DELETE ON institution_recovery FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D recovery retention required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_recovery_insert BEFORE INSERT ON institution_recovery FOR EACH ROW BEGIN
 IF JSON_TYPE(NEW.signed_request)<>'OBJECT' OR NEW.expires_at_utc<=UTC_TIMESTAMP(6) OR NEW.expires_at_utc>UTC_TIMESTAMP(6)+INTERVAL 1 DAY
 OR NOT EXISTS(SELECT 1 FROM institution i WHERE i.id=NEW.institution_id AND i.status='suspended')
 OR NOT EXISTS(SELECT 1 FROM institution_invitation i WHERE i.institution_id=NEW.institution_id AND i.id=NEW.id AND i.recipient_user_id=NEW.recipient_user_id AND i.kind='Recovery' AND i.status='pending')
 OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.request_event_id AND e.actor_kind='Operator' AND e.action='RecoveryRequested' AND e.operator_id=JSON_UNQUOTE(JSON_EXTRACT(NEW.signed_request,'$.OperatorId'))) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid recovery request'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_recovery_update BEFORE UPDATE ON institution_recovery FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D immutable recovery request'; END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_approval_delete BEFORE DELETE ON institution_recovery_approval FOR EACH ROW BEGIN
 IF NOT(COALESCE((DATABASE()='electronic_register_test' AND VERSION()='5.7.24' AND @@port=13306
  AND SUBSTRING_INDEX(USER(),'@',1)='root'
  AND RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'
  AND FIND_IN_SET(OLD.institution_id,COALESCE(@er_mt5b_fixture_owners,''))>0
  AND EXISTS(SELECT 1 FROM institution i WHERE i.id=OLD.institution_id AND BINARY i.code<>'MT3A_LEGACY_TEST'
   AND BINARY i.code REGEXP '^MT5BP_[0-9a-fA-F]{32}$')),0)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D recovery retention required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_approval_insert BEFORE INSERT ON institution_recovery_approval FOR EACH ROW BEGIN
 IF JSON_TYPE(NEW.signed_operation)<>'OBJECT'
 OR (SELECT COUNT(*) FROM institution_recovery_approval a WHERE a.institution_id=NEW.institution_id AND a.recovery_id=NEW.recovery_id)>=2
 OR NOT EXISTS(SELECT 1 FROM institution_recovery r WHERE r.institution_id=NEW.institution_id AND r.id=NEW.recovery_id AND r.expires_at_utc>UTC_TIMESTAMP(6))
 OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id AND e.actor_kind='Operator' AND e.action='RecoveryApproved' AND e.operator_id=NEW.operator_id AND e.operator_id=JSON_UNQUOTE(JSON_EXTRACT(NEW.signed_operation,'$.OperatorId'))) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D invalid independent approval'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5gov_approval_update BEFORE UPDATE ON institution_recovery_approval FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5D immutable recovery approval'; END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_nonce_delete BEFORE DELETE ON platform_operation_nonce FOR EACH ROW BEGIN
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

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_nonce_insert BEFORE INSERT ON platform_operation_nonce FOR EACH ROW BEGIN
 IF NEW.nonce NOT REGEXP BINARY '^[A-F0-9]{64}$' OR NOT EXISTS(SELECT 1 FROM institution_onboarding_event e WHERE e.institution_id=NEW.institution_id AND e.id=NEW.event_id AND e.actor_kind='Operator' AND e.operator_id=NEW.operator_id) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C nonce actor mismatch'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt5c_nonce_update BEFORE UPDATE ON platform_operation_nonce FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT5C immutable nonce'; END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_question_insert BEFORE INSERT ON `question` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam` WHERE id=NEW.exam_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_question_update BEFORE UPDATE ON `question` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_question_option_insert BEFORE INSERT ON `question_option` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `question` WHERE id=NEW.question_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_question_option_update BEFORE UPDATE ON `question_option` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_student_insert BEFORE INSERT ON `student` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=COALESCE((SELECT institution_id FROM class WHERE id=NEW.class_id),(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy')); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.class_institution_id=IF(NEW.class_id IS NULL,NULL,NEW.institution_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_student_update BEFORE UPDATE ON `student` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.class_institution_id=IF(NEW.class_id IS NULL,NULL,NEW.institution_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_student_enrollment_insert BEFORE INSERT ON student_enrollment FOR EACH ROW BEGIN
 IF YEAR(NEW.enrollment_date)<1000 OR BINARY NEW.status NOT IN ('enrolled','completed','withdrawn','cancelled') OR (NEW.completion_date IS NOT NULL AND NEW.completion_date<NEW.enrollment_date) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4c_enrollment_revision_insert BEFORE INSERT ON student_enrollment FOR EACH ROW BEGIN
 IF NEW.revision<>0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C initial revision required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='cp850';

SET SESSION collation_connection='cp850_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4_student_enrollment_update BEFORE UPDATE ON student_enrollment FOR EACH ROW BEGIN
 IF YEAR(NEW.enrollment_date)<1000 OR BINARY NEW.status NOT IN ('enrolled','completed','withdrawn','cancelled') OR (NEW.completion_date IS NOT NULL AND NEW.completion_date<NEW.enrollment_date) OR NOT (NEW.id<=>OLD.id) OR NOT (NEW.institution_id<=>OLD.institution_id) OR NOT (NEW.student_id<=>OLD.student_id) OR NOT (NEW.course_edition_id<=>OLD.course_edition_id) OR NOT (NEW.enrollment_date<=>OLD.enrollment_date) OR NOT (NEW.created_at<=>OLD.created_at) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4 invalid structure or immutable identity'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`root`@`localhost` TRIGGER mt4c_enrollment_revision_update BEFORE UPDATE ON student_enrollment FOR EACH ROW BEGIN
 IF NEW.revision<OLD.revision OR NEW.revision>OLD.revision+1 OR
 ((NOT(NEW.status <=> OLD.status) OR NOT(NEW.completion_date <=> OLD.completion_date)) AND NEW.revision<>OLD.revision+1)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT4C revision mismatch'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_study_area_insert BEFORE INSERT ON `study_area` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_study_area_update BEFORE UPDATE ON `study_area` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_study_path_insert BEFORE INSERT ON `study_path` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_study_path_update BEFORE UPDATE ON `study_path` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_subject_insert BEFORE INSERT ON `subject` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_subject_update BEFORE UPDATE ON `subject` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_submission_answer_insert BEFORE INSERT ON `submission_answer` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `exam_submission` WHERE id=NEW.submission_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.exam_id=(SELECT exam_id FROM exam_submission WHERE id=NEW.submission_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_submission_answer_update BEFORE UPDATE ON `submission_answer` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.exam_id=(SELECT exam_id FROM exam_submission WHERE id=NEW.submission_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_submission_answer_option_insert BEFORE INSERT ON `submission_answer_option` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM `submission_answer` WHERE id=NEW.submission_answer_id); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.question_id=(SELECT question_id FROM submission_answer WHERE id=NEW.submission_answer_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_submission_answer_option_update BEFORE UPDATE ON `submission_answer_option` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
  SET NEW.question_id=(SELECT question_id FROM submission_answer WHERE id=NEW.submission_answer_id);
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_teacher_insert BEFORE INSERT ON `teacher` FOR EACH ROW BEGIN
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id=(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'); END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';

SET SESSION character_set_client='utf8mb4';

SET SESSION collation_connection='utf8mb4_general_ci';

DELIMITER $$

CREATE DEFINER=`er_test`@`localhost` TRIGGER mt3a_teacher_update BEFORE UPDATE ON `teacher` FOR EACH ROW BEGIN
  IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;
  IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;
END$$

DELIMITER ;

-- Institution Class CRUD 20261022.2 (public additive source).
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

-- Institution administration 20261022.3 (public additive source).
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

-- 20261022.4: additive current-membership revision and immutable transfer decision ledger.
-- Apply ONCE, only after isolated-target attestation, preflight and verified backup.
ALTER TABLE student ADD COLUMN transfer_revision bigint(20) NOT NULL DEFAULT 0;
CREATE TABLE student_class_transfer_event (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 id char(36) CHARACTER SET ascii NOT NULL,
 student_id char(36) CHARACTER SET ascii NOT NULL,
 from_class_id char(36) CHARACTER SET ascii NOT NULL,
 to_class_id char(36) CHARACTER SET ascii NOT NULL,
 from_course_edition_id char(36) CHARACTER SET ascii NOT NULL,
 to_course_edition_id char(36) CHARACTER SET ascii NOT NULL,
 source_enrollment_id char(36) CHARACTER SET ascii NOT NULL,
 destination_enrollment_id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 effective_date date NOT NULL,
 source_course_start_date date NOT NULL,
 destination_course_start_date date NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 applied_limit_months int NOT NULL,
 revision_before bigint(20) NOT NULL,
 revision_after bigint(20) NOT NULL,
 reason varchar(1000) NOT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 from_class_name varchar(200) NOT NULL,
 to_class_name varchar(200) NOT NULL,
 from_course_name varchar(200) NOT NULL,
 to_course_name varchar(200) NOT NULL,
 PRIMARY KEY(institution_id,id),
 UNIQUE KEY uq_transfer_revision(institution_id,student_id,revision_after),
 KEY idx_transfer_history(institution_id,student_id,occurred_at_utc,id),
 CONSTRAINT fk_transfer_owner FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_transfer_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id),
 CONSTRAINT fk_transfer_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_transfer_from_class FOREIGN KEY(institution_id,from_class_id) REFERENCES class(institution_id,id),
 CONSTRAINT fk_transfer_to_class FOREIGN KEY(institution_id,to_class_id) REFERENCES class(institution_id,id),
 CONSTRAINT fk_transfer_from_course FOREIGN KEY(institution_id,from_course_edition_id) REFERENCES course_edition(institution_id,id),
 CONSTRAINT fk_transfer_to_course FOREIGN KEY(institution_id,to_course_edition_id) REFERENCES course_edition(institution_id,id),
 CONSTRAINT fk_transfer_source_enrollment FOREIGN KEY(institution_id,source_enrollment_id) REFERENCES student_enrollment(institution_id,id),
 CONSTRAINT fk_transfer_destination_enrollment FOREIGN KEY(institution_id,destination_enrollment_id) REFERENCES student_enrollment(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET SESSION character_set_client='utf8mb4';
SET SESSION collation_connection='utf8mb4_unicode_ci';
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER transfer_event_insert BEFORE INSERT ON student_class_transfer_event FOR EACH ROW BEGIN
 IF NEW.from_class_id=NEW.to_class_id OR NEW.revision_before<0 OR NEW.revision_after<>NEW.revision_before+1
 OR NEW.applied_limit_months<1 OR NEW.applied_limit_months>120 OR NEW.reason NOT REGEXP '[^[:space:]]'
 OR NEW.request_hash NOT REGEXP '^[0-9A-F]{64}$' OR YEAR(NEW.effective_date)<1000
 OR NEW.effective_date>DATE_ADD(NEW.source_course_start_date,INTERVAL NEW.applied_limit_months MONTH)
 OR NEW.effective_date>DATE_ADD(NEW.destination_course_start_date,INTERVAL NEW.applied_limit_months MONTH)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer decision shape required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
 WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin')
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer actor required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM student s WHERE s.institution_id=NEW.institution_id AND s.id=NEW.student_id AND s.class_id=NEW.to_class_id AND s.transfer_revision=NEW.revision_after)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer current membership required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM class a JOIN class b ON b.institution_id=a.institution_id WHERE a.institution_id=NEW.institution_id AND a.id=NEW.from_class_id AND b.id=NEW.to_class_id AND a.course_edition_id=NEW.from_course_edition_id AND b.course_edition_id=NEW.to_course_edition_id)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer class course required'; END IF;
 IF NOT EXISTS(SELECT 1 FROM student_enrollment a JOIN student_enrollment b ON b.institution_id=a.institution_id AND b.student_id=a.student_id
 WHERE a.institution_id=NEW.institution_id AND a.student_id=NEW.student_id AND a.id=NEW.source_enrollment_id AND a.course_edition_id=NEW.from_course_edition_id
 AND b.id=NEW.destination_enrollment_id AND b.course_edition_id=NEW.to_course_edition_id AND b.status='enrolled'
 AND ((NEW.from_course_edition_id=NEW.to_course_edition_id AND a.id=b.id AND a.status='enrolled') OR
 (NEW.from_course_edition_id<>NEW.to_course_edition_id AND a.id<>b.id AND a.status='withdrawn' AND a.completion_date=NEW.effective_date AND b.enrollment_date=NEW.effective_date)))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer enrollment decision required'; END IF;
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER transfer_event_update BEFORE UPDATE ON student_class_transfer_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer audit append only';
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER transfer_event_delete BEFORE DELETE ON student_class_transfer_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer audit append only';
END$$
CREATE DEFINER=`root`@`localhost` TRIGGER student_transfer_revision_update BEFORE UPDATE ON student FOR EACH ROW BEGIN
 IF NEW.transfer_revision<0 OR (NEW.transfer_revision<>OLD.transfer_revision AND
 (NEW.transfer_revision<>OLD.transfer_revision+1 OR NEW.class_id<=>OLD.class_id))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Transfer revision invalid'; END IF;
END$$
DELIMITER ;

SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION';
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
DELIMITER $$
CREATE DEFINER=`root`@`localhost` TRIGGER staff_audit_delete BEFORE DELETE ON audit_event FOR EACH ROW BEGIN
 IF OLD.action LIKE 'staff.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Staff audit append only'; END IF;
END$$
DELIMITER ;

-- Institutional email additive schema 20261027.2, technical singleton only.
-- 20261027.2. MySQL 5.7.24. Maintenance window + attested instance + backup REQUIRED.
-- DDL implicitly commits. Never replay. No login/microsoft/invitation alias is created.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';

CREATE TABLE institution_email_mutex (
 id int NOT NULL, PRIMARY KEY(id)
) ENGINE=InnoDB;
INSERT INTO institution_email_mutex(id) VALUES(1);

CREATE TABLE institution_email_policy (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 policy_id char(36) CHARACTER SET ascii NOT NULL,
 category varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 revision bigint NOT NULL,
 state varchar(10) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 configuration json NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(institution_id,category,revision),
 UNIQUE KEY uq_email_policy_version(institution_id,policy_id,revision),
 CONSTRAINT fk_email_policy_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_email_policy_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id)
 REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_email_assignment (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 student_id char(36) CHARACTER SET ascii DEFAULT NULL,
 teacher_id char(36) CHARACTER SET ascii DEFAULT NULL,
 membership_id char(36) CHARACTER SET ascii DEFAULT NULL,
 category varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 policy_revision bigint NOT NULL,
 email varchar(254) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
 state enum('reserved','provisioned','verified','retired') NOT NULL,
 revision bigint NOT NULL,
 first_name_snapshot varchar(256) NOT NULL,
 last_name_snapshot varchar(256) NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 created_at_utc datetime(6) NOT NULL,
 updated_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_email_assignment_owner(institution_id,id),
 UNIQUE KEY uq_email_assignment_student(institution_id,student_id),
 UNIQUE KEY uq_email_assignment_teacher(institution_id,teacher_id),
 UNIQUE KEY uq_email_assignment_member(institution_id,membership_id),
 KEY idx_email_assignment_page(institution_id,created_at_utc,id),
 CONSTRAINT fk_email_assignment_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_email_assignment_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_email_assignment_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id),
 CONSTRAINT fk_email_assignment_member FOREIGN KEY(institution_id,membership_id) REFERENCES institution_membership(institution_id,id),
 CONSTRAINT fk_email_assignment_policy FOREIGN KEY(institution_id,category,policy_revision) REFERENCES institution_email_policy(institution_id,category,revision),
 CONSTRAINT fk_email_assignment_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Global coordination namespace, intentionally not a tenant directory. Collation
-- matches user.email, including case/accent equivalence. All writers register here.
CREATE TABLE institution_email_reservation (
 address varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
 user_id char(36) CHARACTER SET ascii DEFAULT NULL,
 institution_id char(36) CHARACTER SET ascii DEFAULT NULL,
 assignment_id char(36) CHARACTER SET ascii DEFAULT NULL,
 PRIMARY KEY(address),
 KEY idx_email_reservation_user(user_id),
 CONSTRAINT fk_email_reservation_user FOREIGN KEY(user_id) REFERENCES user(id) ON DELETE CASCADE,
 CONSTRAINT fk_email_reservation_assignment FOREIGN KEY(institution_id,assignment_id) REFERENCES institution_email_assignment(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE institution_email_event (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 operation varchar(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 category varchar(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 policy_revision bigint NOT NULL,
 assignment_id char(36) CHARACTER SET ascii DEFAULT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 before_json json DEFAULT NULL,
 after_json json NOT NULL,
 reason varchar(500) NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(institution_id,id),
 KEY idx_email_event_assignment(institution_id,assignment_id,occurred_at_utc,id),
 KEY idx_email_event_policy(institution_id,category,policy_revision),
 CONSTRAINT fk_email_event_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_email_event_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id),
 CONSTRAINT fk_email_event_policy FOREIGN KEY(institution_id,category,policy_revision) REFERENCES institution_email_policy(institution_id,category,revision),
 CONSTRAINT fk_email_event_assignment FOREIGN KEY(institution_id,assignment_id) REFERENCES institution_email_assignment(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DELIMITER $$
CREATE TRIGGER email_reservation_insert BEFORE INSERT ON institution_email_reservation FOR EACH ROW BEGIN
 IF BINARY NEW.address<>BINARY LOWER(NEW.address) OR
  NOT((NEW.user_id IS NOT NULL AND NEW.institution_id IS NULL AND NEW.assignment_id IS NULL) OR
      (NEW.user_id IS NULL AND NEW.institution_id IS NOT NULL AND NEW.assignment_id IS NOT NULL)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email reservation shape invalid';
 END IF;
 IF NEW.user_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM user WHERE id=NEW.user_id AND email=NEW.address) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email identity reservation mismatch'; END IF;
 IF NEW.assignment_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_email_assignment WHERE institution_id=NEW.institution_id AND id=NEW.assignment_id AND email=NEW.address) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email assignment reservation mismatch'; END IF;
END$$
CREATE TRIGGER email_reservation_update BEFORE UPDATE ON institution_email_reservation FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email reservation immutable';
END$$
CREATE TRIGGER email_reservation_delete BEFORE DELETE ON institution_email_reservation FOR EACH ROW BEGIN
 IF OLD.assignment_id IS NOT NULL OR EXISTS(SELECT 1 FROM user WHERE id=OLD.user_id AND email=OLD.address) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email reservation retained'; END IF;
END$$
CREATE TRIGGER email_user_insert AFTER INSERT ON user FOR EACH ROW BEGIN
 INSERT INTO institution_email_reservation(address,user_id) VALUES(LOWER(NEW.email),NEW.id);
END$$
CREATE TRIGGER email_user_update AFTER UPDATE ON user FOR EACH ROW BEGIN
 IF NOT(NEW.email <=> OLD.email) THEN
  DELETE FROM institution_email_reservation WHERE user_id=OLD.id;
  INSERT INTO institution_email_reservation(address,user_id) VALUES(LOWER(NEW.email),NEW.id);
 END IF;
END$$
CREATE TRIGGER email_policy_insert BEFORE INSERT ON institution_email_policy FOR EACH ROW BEGIN
 DECLARE latest BIGINT;
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT COALESCE(MAX(revision),0) INTO latest FROM institution_email_policy WHERE institution_id=NEW.institution_id AND category=NEW.category;
 IF NEW.category NOT IN ('Student','Teacher','Staff','InstitutionAdmin') OR NEW.state NOT IN ('Draft','Published','Disabled') OR
  NEW.revision<>latest+1 OR NEW.created_at_utc IS NULL OR JSON_TYPE(NEW.configuration)<>'OBJECT' OR
  (latest>0 AND NOT EXISTS(SELECT 1 FROM institution_email_policy WHERE institution_id=NEW.institution_id AND category=NEW.category AND revision=latest AND policy_id=NEW.policy_id)) OR
  NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
    WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy transition or actor invalid'; END IF;
END$$
CREATE TRIGGER email_policy_update BEFORE UPDATE ON institution_email_policy FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy versions append only';
END$$
CREATE TRIGGER email_policy_delete BEFORE DELETE ON institution_email_policy FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy versions retained';
END$$
CREATE TRIGGER email_assignment_insert BEFORE INSERT ON institution_email_assignment FOR EACH ROW BEGIN
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 DECLARE mutex INT;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT id INTO mutex FROM institution_email_mutex WHERE id=1 FOR UPDATE;
 IF NEW.revision<>1 OR NEW.state<>'reserved' OR NEW.email<>LOWER(TRIM(NEW.email)) OR CHAR_LENGTH(NEW.email)>254 OR
  (NEW.student_id IS NOT NULL AND NEW.teacher_id IS NOT NULL) OR
  (NEW.student_id IS NULL AND NEW.teacher_id IS NULL AND NEW.membership_id IS NULL) OR
  (NEW.category='Student' AND NEW.student_id IS NULL) OR (NEW.category='Teacher' AND NEW.teacher_id IS NULL) OR
  (NEW.category IN ('Staff','InstitutionAdmin') AND (NEW.membership_id IS NULL OR NEW.student_id IS NOT NULL OR NEW.teacher_id IS NOT NULL)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email assignment shape invalid'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_email_policy WHERE institution_id=NEW.institution_id AND category=NEW.category AND revision=NEW.policy_revision AND state='Published') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email policy not published'; END IF;
 IF NEW.membership_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_membership WHERE institution_id=NEW.institution_id AND id=NEW.membership_id AND status='active') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email recipient membership unavailable'; END IF;
 IF NEW.student_id IS NOT NULL AND NEW.membership_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_student_profile WHERE institution_id=NEW.institution_id AND student_id=NEW.student_id AND membership_id=NEW.membership_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email student membership mismatch'; END IF;
 IF NEW.teacher_id IS NOT NULL AND NEW.membership_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_teacher_profile WHERE institution_id=NEW.institution_id AND teacher_id=NEW.teacher_id AND membership_id=NEW.membership_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email teacher membership mismatch'; END IF;
 IF NEW.membership_id IS NULL AND (EXISTS(SELECT 1 FROM institution_student_profile WHERE institution_id=NEW.institution_id AND student_id=NEW.student_id) OR EXISTS(SELECT 1 FROM institution_teacher_profile WHERE institution_id=NEW.institution_id AND teacher_id=NEW.teacher_id)) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email linked recipient requires canonical membership'; END IF;
 IF NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
   WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active'
   AND (r.role='InstitutionAdmin' OR r.role='Staff' AND NEW.category IN ('Student','Teacher'))) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email actor not authorized'; END IF;
 IF EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND
  (a.student_id=NEW.student_id OR a.teacher_id=NEW.teacher_id OR a.membership_id=NEW.membership_id OR
   EXISTS(SELECT 1 FROM institution_student_profile p WHERE p.institution_id=NEW.institution_id AND
    (p.membership_id=NEW.membership_id AND p.student_id=a.student_id OR p.student_id=NEW.student_id AND p.membership_id=a.membership_id)) OR
   EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=NEW.institution_id AND
    (p.membership_id=NEW.membership_id AND p.teacher_id=a.teacher_id OR p.teacher_id=NEW.teacher_id AND p.membership_id=a.membership_id)))) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email recipient already assigned'; END IF;
END$$
CREATE TRIGGER email_assignment_reserve AFTER INSERT ON institution_email_assignment FOR EACH ROW BEGIN
 INSERT INTO institution_email_reservation(address,institution_id,assignment_id) VALUES(LOWER(NEW.email),NEW.institution_id,NEW.id);
END$$
CREATE TRIGGER email_assignment_update BEFORE UPDATE ON institution_email_assignment FOR EACH ROW BEGIN
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NOT(NEW.student_id<=>OLD.student_id) OR NOT(NEW.teacher_id<=>OLD.teacher_id) OR
  NOT(NEW.membership_id<=>OLD.membership_id) OR NEW.category<>OLD.category OR NEW.created_at_utc<>OLD.created_at_utc OR
  NEW.first_name_snapshot<>OLD.first_name_snapshot OR NEW.last_name_snapshot<>OLD.last_name_snapshot OR NEW.revision<>OLD.revision+1 OR
  NEW.state NOT IN ('reserved','retired') OR NEW.email<>LOWER(TRIM(NEW.email)) OR
  (NEW.email=OLD.email AND NOT(OLD.state='reserved' AND NEW.state='retired')) OR
  NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution i ON i.id=m.institution_id JOIN institution_membership_role r ON r.membership_id=m.id
   WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email override or retirement invalid'; END IF;
END$$
CREATE TRIGGER email_assignment_override_reserve AFTER UPDATE ON institution_email_assignment FOR EACH ROW BEGIN
 IF NOT(BINARY NEW.email <=> BINARY OLD.email) THEN
  INSERT INTO institution_email_reservation(address,institution_id,assignment_id) VALUES(LOWER(NEW.email),NEW.institution_id,NEW.id);
 END IF;
END$$
CREATE TRIGGER email_assignment_delete BEFORE DELETE ON institution_email_assignment FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email assignment history retained';
END$$
CREATE TRIGGER email_event_insert BEFORE INSERT ON institution_email_event FOR EACH ROW BEGIN
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.operation NOT IN ('PolicyWritten','Assigned','PersonCreatedAndAssigned','Overridden','Retired') OR
  NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$') OR NEW.occurred_at_utc IS NULL OR JSON_TYPE(NEW.after_json)<>'OBJECT' OR
  CHAR_LENGTH(TRIM(NEW.reason))=0 OR NEW.reason REGEXP '[[:cntrl:]]' OR
  (NEW.operation='PolicyWritten' AND NEW.assignment_id IS NOT NULL) OR (NEW.operation<>'PolicyWritten' AND NEW.assignment_id IS NULL) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email event invalid'; END IF;
END$$
CREATE TRIGGER email_event_update BEFORE UPDATE ON institution_email_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email audit append only';
END$$
CREATE TRIGGER email_event_delete BEFORE DELETE ON institution_email_event FOR EACH ROW BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email audit retained';
END$$
DELIMITER ;

-- Current-state namespace registration, not invented historical events. The
-- maintenance runner must stop all writers before DDL/backfill and verify afterward.
INSERT INTO institution_email_reservation(address,user_id) SELECT LOWER(email),id FROM user;

DELIMITER $$
CREATE TRIGGER email_student_profile_link BEFORE INSERT ON institution_student_profile FOR EACH ROW BEGIN
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 DECLARE mutex INT;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT id INTO mutex FROM institution_email_mutex WHERE id=1 FOR UPDATE;
 IF EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND a.student_id=NEW.student_id AND a.membership_id IS NOT NULL AND a.membership_id<>NEW.membership_id) OR
 EXISTS(SELECT 1 FROM institution_email_assignment a JOIN institution_email_assignment b ON b.institution_id=a.institution_id AND b.id<>a.id
  AND (b.membership_id=NEW.membership_id OR EXISTS(SELECT 1 FROM institution_student_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.student_id=b.student_id) OR EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.teacher_id=b.teacher_id))
  WHERE a.institution_id=NEW.institution_id AND a.student_id=NEW.student_id) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email identity reconciliation required before profile linkage'; END IF;
END$$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER email_teacher_profile_link BEFORE INSERT ON institution_teacher_profile FOR EACH ROW BEGIN
 DECLARE parent CHAR(36) CHARACTER SET ascii;
 DECLARE mutex INT;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 SELECT id INTO mutex FROM institution_email_mutex WHERE id=1 FOR UPDATE;
 IF EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND a.teacher_id=NEW.teacher_id AND a.membership_id IS NOT NULL AND a.membership_id<>NEW.membership_id) OR
 EXISTS(SELECT 1 FROM institution_email_assignment a JOIN institution_email_assignment b ON b.institution_id=a.institution_id AND b.id<>a.id
  AND (b.membership_id=NEW.membership_id OR EXISTS(SELECT 1 FROM institution_student_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.student_id=b.student_id) OR EXISTS(SELECT 1 FROM institution_teacher_profile p WHERE p.institution_id=NEW.institution_id AND p.membership_id=NEW.membership_id AND p.teacher_id=b.teacher_id))
  WHERE a.institution_id=NEW.institution_id AND a.teacher_id=NEW.teacher_id) THEN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Email identity reconciliation required before profile linkage'; END IF;
END$$
DELIMITER ;

-- Institutional bulk import 20261027.3.
-- 20261027.3. Additive MySQL5.7.24; attested isolated target, stopped writers and backup required.
-- DDL implicitly commits. Never automatically replay or promise transactional DDL rollback.
SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;
SET SESSION sql_mode='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_AUTO_CREATE_USER,NO_ENGINE_SUBSTITUTION';
CREATE TABLE institution_import_batch (
 id char(36) CHARACTER SET ascii NOT NULL,
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii NOT NULL,
 actor_membership_id char(36) CHARACTER SET ascii NOT NULL,
 category varchar(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 state enum('Preview','Completed','Expired') NOT NULL,
 revision bigint NOT NULL,
 file_sha256 char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 preview_digest char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 total_rows int NOT NULL,valid_rows int NOT NULL,invalid_rows int NOT NULL,
 payload json DEFAULT NULL,
 result_json json DEFAULT NULL,
 warnings_json json NOT NULL,
 policy_revision bigint DEFAULT NULL,
 created_at_utc datetime(6) NOT NULL,
 expires_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(id),
 UNIQUE KEY uq_import_batch_owner(institution_id,id),
 UNIQUE KEY uq_import_batch_category(institution_id,id,category),
 KEY idx_import_batch_actor(institution_id,actor_membership_id,actor_user_id,created_at_utc,id),
 KEY idx_import_batch_digest(institution_id,category,file_sha256,state),
 KEY idx_import_batch_expiry(state,expires_at_utc,id),
 CONSTRAINT fk_import_batch_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_import_batch_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id),
 CONSTRAINT fk_import_batch_policy FOREIGN KEY(institution_id,category,policy_revision) REFERENCES institution_email_policy(institution_id,category,revision)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_import_external_reference (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 category varchar(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 reference varchar(100) COLLATE utf8mb4_bin NOT NULL,
 student_id char(36) CHARACTER SET ascii DEFAULT NULL,
 teacher_id char(36) CHARACTER SET ascii DEFAULT NULL,
 PRIMARY KEY(institution_id,category,reference),
 CONSTRAINT fk_import_external_institution FOREIGN KEY(institution_id) REFERENCES institution(id),
 CONSTRAINT fk_import_external_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_import_external_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_import_item (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 batch_id char(36) CHARACTER SET ascii NOT NULL,
 row_number int NOT NULL,
 category varchar(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 student_id char(36) CHARACTER SET ascii DEFAULT NULL,
 teacher_id char(36) CHARACTER SET ascii DEFAULT NULL,
 email_assignment_id char(36) CHARACTER SET ascii DEFAULT NULL,
 external_reference varchar(100) COLLATE utf8mb4_bin DEFAULT NULL,
 PRIMARY KEY(institution_id,batch_id,row_number),
 CONSTRAINT fk_import_item_batch FOREIGN KEY(institution_id,batch_id,category) REFERENCES institution_import_batch(institution_id,id,category),
 CONSTRAINT fk_import_item_student FOREIGN KEY(institution_id,student_id) REFERENCES student(institution_id,id),
 CONSTRAINT fk_import_item_teacher FOREIGN KEY(institution_id,teacher_id) REFERENCES teacher(institution_id,id),
 CONSTRAINT fk_import_item_assignment FOREIGN KEY(institution_id,email_assignment_id) REFERENCES institution_email_assignment(institution_id,id),
 CONSTRAINT fk_import_item_external FOREIGN KEY(institution_id,category,external_reference) REFERENCES institution_import_external_reference(institution_id,category,reference)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
CREATE TABLE institution_import_event (
 institution_id char(36) CHARACTER SET ascii NOT NULL,
 id char(36) CHARACTER SET ascii NOT NULL,
 batch_id char(36) CHARACTER SET ascii NOT NULL,
 revision bigint NOT NULL,
 action varchar(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_kind enum('User','Retention') NOT NULL,
 actor_user_id char(36) CHARACTER SET ascii DEFAULT NULL,
 actor_membership_id char(36) CHARACTER SET ascii DEFAULT NULL,
 request_hash char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 result_json json NOT NULL,
 reason varchar(500) NOT NULL,
 occurred_at_utc datetime(6) NOT NULL,
 PRIMARY KEY(institution_id,id),
 UNIQUE KEY uq_import_event_revision(institution_id,batch_id,revision),
 CONSTRAINT fk_import_event_batch FOREIGN KEY(institution_id,batch_id) REFERENCES institution_import_batch(institution_id,id),
 CONSTRAINT fk_import_event_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
DELIMITER $$
CREATE TRIGGER import_batch_insert BEFORE INSERT ON institution_import_batch FOR EACH ROW BEGIN
 DECLARE parent char(36) CHARACTER SET ascii;
 SELECT id INTO parent FROM institution WHERE id=NEW.institution_id FOR UPDATE;
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NEW.category NOT IN ('Student','Teacher') OR NEW.state<>'Preview' OR NEW.revision<>1 OR NEW.payload IS NULL OR JSON_TYPE(NEW.payload)<>'OBJECT' OR NEW.result_json IS NOT NULL OR JSON_TYPE(NEW.warnings_json)<>'ARRAY' OR NEW.total_rows<1 OR NEW.total_rows>10000 OR NEW.valid_rows+NEW.invalid_rows<>NEW.total_rows OR NEW.expires_at_utc<=NEW.created_at_utc OR NOT(BINARY NEW.file_sha256 REGEXP '^[0-9A-F]{64}$') OR NOT(BINARY NEW.preview_digest REGEXP '^[0-9A-F]{64}$') OR
 NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN institution i ON i.id=m.institution_id WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role IN ('InstitutionAdmin','Staff')) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import preview ownership or state invalid'; END IF;
END$$
CREATE TRIGGER import_batch_update BEFORE UPDATE ON institution_import_batch FOR EACH ROW BEGIN
 IF NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.actor_user_id<>OLD.actor_user_id OR NEW.actor_membership_id<>OLD.actor_membership_id OR NEW.category<>OLD.category OR NEW.file_sha256<>OLD.file_sha256 OR NEW.created_at_utc<>OLD.created_at_utc OR NEW.expires_at_utc<>OLD.expires_at_utc OR NEW.revision<>OLD.revision+1 OR OLD.state<>'Preview' OR NEW.total_rows<>OLD.total_rows OR NEW.valid_rows+NEW.invalid_rows<>NEW.total_rows OR JSON_TYPE(NEW.warnings_json)<>'ARRAY' OR
 (NEW.state='Preview' AND (NEW.payload IS NULL OR NEW.result_json IS NOT NULL)) OR
 (NEW.state='Completed' AND (NEW.payload IS NOT NULL OR NEW.result_json IS NULL OR OLD.expires_at_utc<=UTC_TIMESTAMP(6) OR OLD.invalid_rows<>0 OR (SELECT COUNT(*) FROM institution_import_item WHERE institution_id=NEW.institution_id AND batch_id=NEW.id)<>OLD.total_rows)) OR
 (NEW.state='Expired' AND (NEW.payload IS NOT NULL OR NEW.result_json IS NOT NULL OR OLD.expires_at_utc>UTC_TIMESTAMP(6))) OR
 (NEW.state<>'Expired' AND NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN institution i ON i.id=m.institution_id WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role IN ('InstitutionAdmin','Staff'))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import transition or immutable fields invalid'; END IF;
END$$
CREATE TRIGGER import_batch_delete BEFORE DELETE ON institution_import_batch FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import history retained'; END$$
CREATE TRIGGER import_external_insert BEFORE INSERT ON institution_import_external_reference FOR EACH ROW BEGIN
 IF CHAR_LENGTH(TRIM(NEW.reference))=0 OR NEW.reference<>TRIM(NEW.reference) OR NEW.reference REGEXP '[[:cntrl:]]' OR NOT((NEW.category='Student' AND NEW.student_id IS NOT NULL AND NEW.teacher_id IS NULL) OR (NEW.category='Teacher' AND NEW.student_id IS NULL AND NEW.teacher_id IS NOT NULL)) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import external reference invalid'; END IF;
END$$
CREATE TRIGGER import_external_update BEFORE UPDATE ON institution_import_external_reference FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import reference immutable'; END$$
CREATE TRIGGER import_external_delete BEFORE DELETE ON institution_import_external_reference FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import reference retained'; END$$
CREATE TRIGGER import_item_insert BEFORE INSERT ON institution_import_item FOR EACH ROW BEGIN
 IF NEW.row_number<1 OR NOT((NEW.category='Student' AND NEW.student_id IS NOT NULL AND NEW.teacher_id IS NULL) OR (NEW.category='Teacher' AND NEW.student_id IS NULL AND NEW.teacher_id IS NOT NULL)) OR
 NOT EXISTS(SELECT 1 FROM institution_import_batch b WHERE b.institution_id=NEW.institution_id AND b.id=NEW.batch_id AND b.state='Preview' AND b.expires_at_utc>UTC_TIMESTAMP(6)) OR
 (NEW.email_assignment_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_email_assignment a WHERE a.institution_id=NEW.institution_id AND a.id=NEW.email_assignment_id AND a.category=NEW.category AND (a.student_id<=>NEW.student_id) AND (a.teacher_id<=>NEW.teacher_id))) OR
 (NEW.external_reference IS NOT NULL AND NOT EXISTS(SELECT 1 FROM institution_import_external_reference x WHERE x.institution_id=NEW.institution_id AND x.category=NEW.category AND x.reference=NEW.external_reference AND (x.student_id<=>NEW.student_id) AND (x.teacher_id<=>NEW.teacher_id))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import item ownership or target invalid'; END IF;
END$$
CREATE TRIGGER import_item_update BEFORE UPDATE ON institution_import_item FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import item immutable'; END$$
CREATE TRIGGER import_item_delete BEFORE DELETE ON institution_import_item FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import item retained'; END$$
CREATE TRIGGER import_event_insert BEFORE INSERT ON institution_import_event FOR EACH ROW BEGIN
 IF NEW.id='00000000-0000-0000-0000-000000000000' OR NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$') OR JSON_TYPE(NEW.result_json)<>'OBJECT' OR NEW.reason REGEXP '[[:cntrl:]]' OR CHAR_LENGTH(TRIM(NEW.reason))=0 OR
 NOT EXISTS(SELECT 1 FROM institution_import_batch b WHERE b.institution_id=NEW.institution_id AND b.id=NEW.batch_id AND b.revision=NEW.revision AND
 ((NEW.actor_kind='User' AND NEW.actor_user_id=b.actor_user_id AND NEW.actor_membership_id=b.actor_membership_id AND ((NEW.action='Previewed' AND b.state='Preview' AND b.revision=1) OR (NEW.action='Remapped' AND b.state='Preview' AND b.revision>1) OR (NEW.action='Confirmed' AND b.state='Completed'))) OR (NEW.actor_kind='Retention' AND NEW.actor_user_id IS NULL AND NEW.actor_membership_id IS NULL AND NEW.action='Expired' AND b.state='Expired' AND b.expires_at_utc<=UTC_TIMESTAMP(6)))) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import audit invalid'; END IF;
END$$
CREATE TRIGGER import_event_update BEFORE UPDATE ON institution_import_event FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import audit append only'; END$$
CREATE TRIGGER import_event_delete BEFORE DELETE ON institution_import_event FOR EACH ROW BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Import audit retained'; END$$
DELIMITER ;
