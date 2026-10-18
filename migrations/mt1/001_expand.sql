-- MT-1 EXPAND. Canonical database-first source for the four new tables only.
-- Execute only through Mt1SchemaMigration on electronic_register_test in MT-1.
-- DDL may commit implicitly on MySQL 5.7; restore is NOT an atomic rollback.
CREATE TABLE IF NOT EXISTS institution (
    id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    name varchar(200) NOT NULL,
    code varchar(64) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    legal_tax_code varchar(32) NULL,
    region varchar(100) NULL,
    status enum('provisioning','active','suspended','archived') NOT NULL DEFAULT 'provisioning',
    created_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    UNIQUE KEY uq_institution_code (code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS campus (
    id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    institution_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    name varchar(200) NOT NULL,
    address varchar(300) NULL,
    city varchar(100) NULL,
    province varchar(100) NULL,
    is_active tinyint(1) NOT NULL DEFAULT 1,
    PRIMARY KEY (id),
    KEY idx_campus_institution (institution_id),
    CONSTRAINT fk_campus_institution FOREIGN KEY (institution_id) REFERENCES institution (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS institution_membership (
    id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    institution_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    user_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    status enum('pending','active','suspended','left') NOT NULL DEFAULT 'pending',
    joined_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    suspended_at datetime(6) NULL,
    PRIMARY KEY (id),
    UNIQUE KEY uq_membership_user_institution (user_id, institution_id),
    KEY idx_membership_institution (institution_id),
    CONSTRAINT fk_membership_institution FOREIGN KEY (institution_id) REFERENCES institution (id) ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT fk_membership_user FOREIGN KEY (user_id) REFERENCES user (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS institution_membership_role (
    membership_id char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    role enum('InstitutionAdmin','Teacher','Student') NOT NULL,
    PRIMARY KEY (membership_id, role),
    CONSTRAINT fk_membership_role_membership FOREIGN KEY (membership_id) REFERENCES institution_membership (id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
