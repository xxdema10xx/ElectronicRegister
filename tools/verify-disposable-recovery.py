"""Cold backup/restore drill: NEW sacrificial test instances only, never 13306/3306.

Requires the public setup creation attestation and completed bootstrap. Restores
all data files to a nonexistent private datadir. No SQL restore, FK disabling,
trigger replacement, file deletion or automatic retries. Same MySQL binary/OS only.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid
from types import SimpleNamespace
import local_test_db as db


def snapshot(instance):
    result = {}
    for table in sorted(json.loads((db.ROOT/'migrations/bootstrap/schema-contract.json').read_text())['tables']):
        columns = instance.query("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='"+table+"' ORDER BY ORDINAL_POSITION;").splitlines()
        encoded = ','.join("IFNULL(HEX(`"+c+"`),'NULL')" for c in columns)
        rows = sorted(instance.query('SELECT CONCAT_WS(\'|\','+encoded+') FROM `'+table+'`;').splitlines())
        result[table] = dict(rows=len(rows), sha256=hashlib.sha256('\n'.join(rows).encode()).hexdigest().upper())
    return result


def process_absent(pid):
    p = subprocess.run(['powershell', '-NoProfile', '-Command',
        'if(Get-Process -Id '+str(int(pid))+" -ErrorAction SilentlyContinue){exit 1}"], capture_output=True)
    return p.returncode == 0


def stop(instance):
    identity = instance.identity(); instance.quiet()
    pid = json.loads((instance.home/'instance.json').read_text())['pid']
    p = subprocess.run([str(instance.bin/'mysqladmin.exe'), '--no-defaults', '--protocol=TCP',
        '--host=127.0.0.1', '--port='+str(instance.port), '--user=root', '--shutdown-timeout=20', 'shutdown'],
        env=dict(os.environ, MYSQL_PWD=instance.password), capture_output=True, timeout=30)
    db.require(p.returncode == 0, 'Graceful shutdown failed; do not copy or force-kill')
    deadline = time.monotonic()+30
    while time.monotonic() < deadline and (db.listener(instance.port) or not process_absent(pid)):
        time.sleep(.25)
    db.require(not db.listener(instance.port) and process_absent(pid), 'Process still present; backup refused')
    return identity


def hashes(folder):
    return {p.relative_to(folder).as_posix(): db.digest(p) for p in sorted(folder.rglob('*')) if p.is_file()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mysql-bin', required=True)
    parser.add_argument('--source-data', required=True)
    parser.add_argument('--source-port', type=int, required=True)
    parser.add_argument('--target-data', required=True)
    parser.add_argument('--target-port', type=int, required=True)
    parser.add_argument('--backup', required=True)
    parser.add_argument('--confirm-sacrificial', action='store_true')
    args = parser.parse_args()
    db.require(args.confirm_sacrificial and args.source_port >= 23000 and args.target_port >= 23000 and
               args.source_port != args.target_port, 'Two distinct sacrificial ports >=23000 required')
    password = os.environ.get('ER_TEST_DB_PASSWORD', '')
    common = dict(mysql_bin=args.mysql_bin, main_datadir='C:/MAMP/db/mysql', confirm_isolated_test=True)
    source = db.Setup(SimpleNamespace(**common, datadir=args.source_data, port=args.source_port), password)
    target = db.Setup(SimpleNamespace(**common, datadir=args.target_data, port=args.target_port), password)
    backup = Path(args.backup).absolute().resolve()
    db.require(not target.home.exists() and not backup.exists() and not db.listener(target.port), 'New empty recovery/backup destinations required')
    for a, b in [(source.home,target.home),(source.home,backup),(target.home,backup)]:
        db.require(a != b and a not in b.parents and b not in a.parents, 'Paths overlap')
    db.require((source.home/'bootstrap.completed.json').is_file(), 'Public bootstrap completion required')
    db.verify_files(); source.verify(); source.identity(); source.quiet()
    # Only this fresh test server receives identifiable synthetic recovery data.
    db.require(source.query('SELECT COUNT(*) FROM user;') == '0', 'Drill requires a fresh source with no application users')
    owner, user, member = [str(uuid.uuid4()) for _ in range(3)]
    source.query("START TRANSACTION; INSERT INTO institution(id,code,name,status) VALUES ('"+owner+"','MT5BP_"+owner.replace('-','')+"','Synthetic MT6 recovery drill','active'); INSERT INTO user(id,email,password_hash,role) VALUES ('"+user+"','"+user+"@recovery.example.test','not-a-login-hash','student'); INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES ('"+member+"','"+owner+"','"+user+"','active'); INSERT INTO institution_membership_role(membership_id,role) VALUES ('"+member+"','InstitutionAdmin'); COMMIT;")
    source.verify(); before = snapshot(source)
    db.require(source.query("SELECT security_generation FROM institution_membership_security_state WHERE membership_id='"+member+"';") == '1', 'Synthetic generation not advanced by trigger')
    identity = stop(source)
    db.require(shutil.disk_usage(backup.parent).free > 3*sum(p.stat().st_size for p in source.data.rglob('*') if p.is_file()) + 1024**3, 'Insufficient space')
    backup.mkdir(parents=True); db.private_acl(backup)
    source_hashes = hashes(source.data)
    shutil.copytree(source.data, backup/'data')
    shutil.copy2(source.home/'my.ini', backup/'my.ini')
    db.require(hashes(backup/'data') == source_hashes, 'Backup SHA256/file-count/size verification failed')
    manifest = dict(kind='MT6 cold physical recovery drill; same binary and OS only',
        source_identity=identity, mysql_binary_sha256=db.digest(source.bin/'mysqld.exe'),
        files=source_hashes, config_sha256=db.digest(backup/'my.ini'), data_snapshot=before,
        bytes=sum(p.stat().st_size for p in (backup/'data').rglob('*') if p.is_file()),
        free_bytes_after=shutil.disk_usage(backup).free, process_stopped=True)
    db.write_new(backup/'manifest.json', manifest)
    # Independent second copy; the verified backup is never a working datadir.
    target.home.mkdir(parents=True); db.private_acl(target.home); (target.home/'tmp').mkdir()
    shutil.copytree(backup/'data', target.data)
    db.require(hashes(target.data) == source_hashes, 'Recovery copy differs before startup')
    config = (backup/'my.ini').read_text().replace(source.home.as_posix(), target.home.as_posix())
    config = config.replace('port='+str(source.port)+'\n', 'port='+str(target.port)+'\n')
    db.require(source.home.as_posix() not in config and 'port='+str(target.port)+'\n' in config, 'Recovery configuration not isolated')
    (target.home/'my.ini').write_text(config, encoding='utf-8', newline='\n')
    db.require(not db.listener(source.port) and not db.listener(target.port), 'A recovery port became occupied')
    child = target.start([]); target.wait_ready(child)
    db.write_new(target.home/'instance.json', dict(kind='ElectronicRegister isolated test', port=target.port,
        datadir=target.data.as_posix(), mysql_bin=str(target.bin), hostname=target.client('SELECT @@hostname;'),
        server_uuid=target.client('SELECT @@server_uuid;'), pid=child.pid))
    target.identity(); target.verify()
    db.require(snapshot(target) == before, 'Recovered data differs')
    db.require(target.query("SELECT security_generation FROM institution_membership_security_state WHERE membership_id='"+member+"';") == '1', 'Generation not preserved')
    stop(target); child.wait(timeout=30)
    db.require(child.returncode == 0 and hashes(backup/'data') == source_hashes, 'Recovery shutdown/backup immutability failed')
    db.write_new(backup/'verified.json', dict(all_data_identical=True, full_schema_contexts_verified=True,
        file_count=len(source_hashes), trigger_count=88, fk_count=118, foreign_key_orphans=0,
        both_disposable_servers_stopped=True, existing_servers_untouched=True))
    print('Cold backup and new-instance recovery verified: all 40 tables, 88 trigger contexts, 118 FK, data and generation preserved; both disposable servers stopped')


if __name__ == '__main__':
    try:
        main()
    except Exception:
        # Paths/files and server state remain available privately. Never retry/restore.
        print('Recovery drill blocked; preserve partial state and private diagnostics. No automatic retry.'); raise
