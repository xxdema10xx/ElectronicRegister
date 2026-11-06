#!/usr/bin/env bash
# EC2 staging release, one phase per invocation:
#   sudo bash infra/staging/release-ec2.sh check|backup|deploy|switch|seed-demo|verify|rollback
# Every phase is idempotent, writes its whole output also to
#   /srv/electronicregister-staging/release-<sha>/logs/<phase>-<UTC>.log  (link: latest-<phase>.log)
# and ends with a single line "ESITO <phase>: OK" or "ESITO <phase>: FAIL".
# Procedure and expected output: docs/operations/EC2_RELEASE.md. Logic: infra/staging/release_ec2.py.
set -Eeuo pipefail
umask 077
export PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 LC_ALL=C.UTF-8
# git status (here and in host.py) must not rewrite the index of the public checkout under umask 077:
# the non-root ops container reads it.
export GIT_OPTIONAL_LOCKS=0

phase=${1:-}
case "$phase" in
  check|backup|deploy|switch|seed-demo|verify|rollback) ;;
  *) echo "Uso: sudo bash $0 check|backup|deploy|switch|seed-demo|verify|rollback" >&2; exit 2 ;;
esac
if [ "$(id -u)" != 0 ]; then
  echo "ESITO $phase: FAIL (eseguire con sudo)"; exit 1
fi

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)
repo=$(cd "$here/../.." && pwd -P)
python=${ER_PYTHON:-python3}
rev=$(git -c safe.directory='*' -C "$repo" rev-parse HEAD)
[[ "$rev" =~ ^[a-f0-9]{40}$ ]] || { echo "ESITO $phase: FAIL (revisione non valida)"; exit 1; }
base=${ER_RELEASE_BASE:-/srv/electronicregister-staging}
release="$base/release-$rev"
install -d -m 0700 "$release" "$release/logs"
log="$release/logs/$phase-$(date -u +%Y%m%dT%H%M%SZ).log"
: > "$log"
chmod 0600 "$log"
ln -sfn "$(basename "$log")" "$release/logs/latest-$phase.log"
ln -sfn "$(basename "$log")" "$release/logs/latest.log"

# One phase at a time, also across dropped console sessions.
exec 9>"$release/release.lock"
if ! flock -n 9; then
  echo "ESITO $phase: FAIL (un'altra fase e ancora in corso: seguire $release/logs/latest.log)" | tee -a "$log"
  exit 1
fi

echo "Log: $log" | tee -a "$log"
set +e
"$python" -B -u "$here/release_ec2.py" "$phase" 2>&1 | tee -a "$log"
status=${PIPESTATUS[0]}
set -e
if [ "$status" = 0 ]; then
  echo "ESITO $phase: OK" | tee -a "$log"
else
  echo "ESITO $phase: FAIL (codice $status, log $log)" | tee -a "$log"
fi
exit "$status"
