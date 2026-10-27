"""Offline Phase 2 schema/artifact contract verification. Never connects to SQL."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def verify():
    manifest = json.loads((ROOT / 'migrations/bootstrap/manifest.json').read_text(encoding='utf-8'))
    for name, digest in manifest['files'].items():
        path = (ROOT / name).resolve(strict=True)
        assert ROOT in path.parents, name
        assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == digest, name
        assert subprocess.run(['git', 'check-ignore', '--no-index', '-q', name], cwd=ROOT).returncode == 1, name
    contract = json.loads((ROOT / 'migrations/bootstrap/schema-contract.json').read_text(encoding='utf-8'))
    assert contract['inventory'] == manifest['inventory'] == dict(tables=52, triggers=129, fks=158, indexes=218)
    assert len(contract['tables']) == 52
    assert len(contract['triggers']) == len(contract['trigger_metadata']) == 129
    migration = (ROOT / 'migrations/institution-email/001_expand.sql').read_text(encoding='utf-8')
    bootstrap = (ROOT / 'migrations/bootstrap/001_schema.sql').read_text(encoding='utf-8')
    assert migration in bootstrap, 'Bootstrap must preserve the exact email additive migration before subsequent phases'
    expected = {'institution_email_' + suffix for suffix in ('mutex', 'policy', 'assignment', 'reservation', 'event')}
    assert set(re.findall(r'CREATE TABLE (institution_email_\w+)', migration)) == expected
    assert len(re.findall(r'CREATE TRIGGER email_\w+', migration)) == 18
    assert "enum('InstitutionAdmin','Teacher','Student','Staff')" in contract['tables']['institution_membership_role']
    policy = contract['tables']['institution_email_policy']
    assignment = contract['tables']['institution_email_assignment']
    namespace = contract['tables']['institution_email_reservation']
    assert 'PRIMARY KEY (`institution_id`,`category`,`revision`)' in policy
    for person in ('student', 'teacher', 'member'):
        assert 'uq_email_assignment_' + person in assignment
    for name in ('student', 'teacher', 'member', 'policy', 'actor'):
        assert 'fk_email_assignment_' + name in assignment
    assert 'PRIMARY KEY (`address`)' in namespace and 'utf8mb4_unicode_ci' in namespace
    for trigger in ('email_event_update', 'email_event_delete', 'email_policy_update', 'email_policy_delete', 'email_assignment_delete'):
        assert "SIGNAL SQLSTATE '45000'" in contract['trigger_metadata'][trigger]['ACTION_STATEMENT']
    for trigger in ('email_user_insert', 'email_user_update', 'email_assignment_reserve', 'email_assignment_override_reserve'):
        assert 'institution_email_reservation' in contract['trigger_metadata'][trigger]['ACTION_STATEMENT']
    return len(manifest['files'])


if __name__ == '__main__':
    print(f'{verify()} SHA256 files and institutional email static contract verified; no SQL executed')
