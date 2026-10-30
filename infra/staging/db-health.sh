#!/bin/sh
set -eu
value=$(mysql --defaults-extra-file=/run/secrets/root.cnf --protocol=SOCKET --connect-timeout=3 --batch --skip-column-names \
  -e "SELECT IF(VERSION()='8.4.13' AND @@hostname='er-stage-db' AND @@port=3306 AND @@datadir='/var/lib/mysql/' AND @@global.ssl_ca='/run/secrets/mysql-ca.pem' AND @@global.ssl_cert='/run/secrets/mysql-server.pem' AND @@global.ssl_key='/run/secrets/mysql-server-key.pem' AND @@global.tls_version='TLSv1.2' AND @@global.require_secure_transport=1 AND @@global.skip_networking=0 AND (SELECT id FROM er_stage_control.identity)='$ER_STAGE_ID',1,0)")
if [ "$value" != 1 ]; then
  echo 'MySQL readiness rejected: version, identity, TLS configuration, secure transport or final network startup mismatch' >&2
  exit 1
fi
