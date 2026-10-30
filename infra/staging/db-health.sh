#!/bin/sh
set -eu
value=$(mysql --defaults-extra-file=/run/secrets/root.cnf --protocol=SOCKET --connect-timeout=3 --batch --skip-column-names \
  -e "SELECT IF(VERSION()='5.7.44' AND @@global.have_ssl='YES' AND @@global.have_openssl='YES' AND @@global.tls_version='TLSv1.2' AND @@global.require_secure_transport=1 AND @@global.skip_networking=0,1,0)")
if [ "$value" != 1 ]; then
  echo 'MySQL readiness rejected: version, SSL availability, TLSv1.2, secure transport or final network startup mismatch' >&2
  exit 1
fi
