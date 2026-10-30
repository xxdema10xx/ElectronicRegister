#!/bin/bash
# Executed ONLY by the database entrypoint on a genuinely empty owned volume.
# Application schema DDL is a separate operator command, never API startup.
(
set -euo pipefail
echo 'ER staging bootstrap: started'
trap 'echo "ER staging bootstrap: failed at line $LINENO" >&2' ERR
[[ "$ER_STAGE_ID" =~ ^[a-f0-9]{32}$ ]]
[[ "$ER_STAGE_SQL_HOST" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.%$ ]]
password=$(cat /run/secrets/migrator-password)
[[ "$password" =~ ^[a-f0-9]{64}$ ]]
mysql --defaults-extra-file=/run/secrets/root.cnf --protocol=SOCKET <<SQL
CREATE DATABASE er_stage_control CHARACTER SET ascii;
CREATE TABLE er_stage_control.identity(id char(32) NOT NULL PRIMARY KEY);
INSERT INTO er_stage_control.identity VALUES('$ER_STAGE_ID');
CREATE DATABASE electronic_register_staging CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'er_migrator'@'$ER_STAGE_SQL_HOST' IDENTIFIED WITH caching_sha2_password BY '$password' REQUIRE SSL;
GRANT SELECT,INSERT,UPDATE,DELETE,CREATE,DROP,ALTER,INDEX,REFERENCES,TRIGGER,CREATE ROUTINE,ALTER ROUTINE,EXECUTE ON electronic_register_staging.* TO 'er_migrator'@'$ER_STAGE_SQL_HOST' WITH GRANT OPTION;
GRANT CREATE USER,SET_ANY_DEFINER ON *.* TO 'er_migrator'@'$ER_STAGE_SQL_HOST';
GRANT SELECT(User,Host,plugin,ssl_type,account_locked) ON mysql.user TO 'er_migrator'@'$ER_STAGE_SQL_HOST' WITH GRANT OPTION;
GRANT SHOW_ROUTINE ON *.* TO 'er_migrator'@'$ER_STAGE_SQL_HOST' WITH GRANT OPTION;
GRANT SELECT ON er_stage_control.identity TO 'er_migrator'@'$ER_STAGE_SQL_HOST' WITH GRANT OPTION;
SQL
unset password
printf '8.4.13:%s' "$ER_STAGE_ID" > /var/lib/mysql/.er-engine
echo 'ER staging bootstrap: completed'
)
