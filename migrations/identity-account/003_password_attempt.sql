-- Global account quota, shared by all API replicas; no tenant authority.
-- Apply once with the release preflight, never from an HTTP readiness probe.
CREATE TABLE identity_password_attempt (
 user_id char(36) CHARACTER SET ascii NOT NULL,
 window_start datetime(6) NOT NULL,
 attempts int NOT NULL,
 lease_id char(36) CHARACTER SET ascii DEFAULT NULL,
 lease_until datetime(6) DEFAULT NULL,
 PRIMARY KEY(user_id),
 CONSTRAINT fk_password_attempt_user FOREIGN KEY(user_id) REFERENCES `user`(id) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
