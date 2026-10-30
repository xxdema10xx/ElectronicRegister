#!/bin/sh
set -eu
value=$(mysql --defaults-extra-file=/run/secrets/root.cnf --protocol=SOCKET --batch --skip-column-names \
  -e "SELECT IF(VERSION()='5.7.24' AND @@global.have_ssl='YES' AND FIND_IN_SET('TLSv1.2',@@global.tls_version)>0 AND @@global.skip_networking=0,1,0)")
test "$value" = 1
