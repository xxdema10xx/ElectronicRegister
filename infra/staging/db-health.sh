#!/bin/sh
set -eu
value=$(mysql --defaults-extra-file=/run/secrets/root.cnf --protocol=SOCKET --connect-timeout=3 --batch --skip-column-names \
  -e "SELECT IF(VERSION()='8.4.13' AND @@global.version_ssl_library LIKE 'OpenSSL %' AND @@global.tls_version='TLSv1.2' AND @@global.require_secure_transport=1 AND @@global.skip_networking=0 AND (SELECT id FROM er_stage_control.identity)='$ER_STAGE_ID',1,0)")
if [ "$value" != 1 ]; then
  echo 'MySQL readiness rejected: version, SSL availability, TLSv1.2, secure transport or final network startup mismatch' >&2
  exit 1
fi
