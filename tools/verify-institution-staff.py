"""Read-only Staff artifact/schema verifier. No Python database packages required."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def verify_files():
    manifest = json.loads((ROOT / 'migrations/bootstrap/manifest.json').read_text(encoding='utf-8'))
    for name, expected in manifest['files'].items():
        path = (ROOT / name).resolve(strict=True)
        assert ROOT in path.parents, name
        assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == expected, name
    schema = (ROOT / 'migrations/bootstrap/001_schema.sql').read_text(encoding='utf-8')
    contract = json.loads((ROOT / 'migrations/bootstrap/schema-contract.json').read_text(encoding='utf-8'))
    assert "enum('InstitutionAdmin','Teacher','Student','Staff')" in contract['tables']['institution_membership_role']
    assert contract['inventory']['triggers'] == len(contract['triggers']) == len(contract['trigger_metadata']) == 129
    assert 'Staff audit append only' in schema
    for name in ('mt5c_invitation_insert', 'mt5d_event_insert', 'mt5d_event_snapshot_shape'):
        assert 'Staff' in contract['triggers'][name]['statement']
    return contract, len(manifest['files'])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--mysql-bin', type=Path)
    parser.add_argument('--port', type=int)
    parser.add_argument('--datadir', type=Path)
    args = parser.parse_args()
    contract, files = verify_files()
    if args.mysql_bin:
        assert args.port and 1024 <= args.port <= 65535 and args.port not in (3306, 13306)
        assert args.datadir and (ROOT / '.staff-private').resolve() in args.datadir.resolve().parents

        def query(sql):
            result = subprocess.run([str(args.mysql_bin / 'mysql.exe'), '--no-defaults', '--protocol=TCP',
                                     '--host=127.0.0.1', '--port=' + str(args.port), '--user=root',
                                     '--database=electronic_register_test', '--batch', '--raw', '--skip-column-names',
                                     '--default-character-set=utf8mb4'], input=sql, text=True,
                                    capture_output=True, check=True)
            return result.stdout.strip().replace('\r\n', '\n')

        identity = json.loads(query("SELECT JSON_OBJECT('version',VERSION(),'port',@@port,'datadir',@@datadir,'db',DATABASE());"))
        assert identity['version'] == '5.7.24' and identity['port'] == args.port and identity['db'] == contract['database']
        assert Path(identity['datadir']).resolve() == args.datadir.resolve()
        for name, expected in contract['tables'].items():
            assert re.fullmatch(r'[a-z0-9_]+', name)
            actual = query('SHOW CREATE TABLE `' + name + '`;').split('\t', 1)[1]
            assert re.sub(r' AUTO_INCREMENT=\d+', '', actual) == expected, name
        for name, expected in contract['trigger_metadata'].items():
            keys = list(expected)
            assert all(re.fullmatch(r'[A-Z_]+', key) for key in keys)
            sql = 'SELECT JSON_OBJECT(' + ','.join("'" + key + "'," + key for key in keys) + ") FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME='" + name + "';"
            assert json.loads(query(sql)) == expected, name
        print(f'{files} SHA256 files and complete MySQL schema contract verified read-only')
    else:
        print(f'{files} SHA256 files and static Staff contract verified; no SQL executed')


if __name__ == '__main__':
    main()
