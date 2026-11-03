"""Attested disposable-container recovery. No restart, DDL, or barrier removal here."""
import json
import os
import re
import socket
import subprocess
import time
from pathlib import Path

from mysql84_release import ROOT, require


def container_binding(config):
    require(config.get('isolatedCI') is True or config.get('isolatedLocal') is True,
            'Recovery socket is restricted to disposable certification servers')
    require(not os.environ.get('DOCKER_HOST') and not os.environ.get('DOCKER_CONTEXT'), 'Recovery Docker transport override refused')
    if config.get('isolatedCI'):
        require(os.environ.get('GITHUB_ACTIONS') == 'true' and config['host'] == '127.0.0.1' and config['port'] not in (3306, 3307, 13306), 'Recovery CI target rejected')
    cid = config['mysqlContainer']
    require(re.fullmatch(r'[a-f0-9]{64}', cid), 'Recovery container identity rejected')
    if config.get('isolatedLocal'):
        from mysql84_local import attest
        attest(config)
    observed = json.loads(subprocess.check_output(['docker', 'inspect', cid], text=True, timeout=10))[0]
    require(observed['Id'] == cid and observed['State']['Running'], 'Recovery container identity/state mismatch')
    require(observed['Config']['Image'] == json.loads((ROOT/'infra/staging/mysql-image.json').read_text())['image'],
            'Recovery image mismatch')
    mounts = observed['Mounts']
    require(sum(m['Destination'] == '/var/lib/mysql' and m['Type'] == 'volume' for m in mounts) == 1,
            'Recovery requires an attested data volume')
    return dict(container=cid, image=observed['Image'], hostname=observed['Config']['Hostname'],
                mounts=[{k: m.get(k) for k in ('Type', 'Name', 'Source', 'Destination', 'RW')} for m in mounts],
                ports=observed['HostConfig']['PortBindings'])


def check_binding(config):
    require(container_binding(config) == config['recoveryBinding'], 'Recovery container/volume binding changed')


def socket_args(config):
    check_binding(config)
    require(config.get('recoverySocketAccount') == 'root@localhost', 'Explicit recovery account required')
    # Both certification entry points provision the disposable image's local root.
    # Never reuse a host defaults file, environment password, or an implicit user.
    return ['docker', 'exec', '-i', config['mysqlContainer'], 'mysql', '--no-defaults', '--user=root',
            '--protocol=SOCKET', '--socket=/var/run/mysqld/mysqld.sock', '--skip-password',
            '--batch', '--raw', '--skip-column-names', '--unbuffered', '--skip-reconnect',
            '--default-character-set=utf8mb4', '--connect-timeout=5']


def result_record(result):
    match = re.search(r'ERROR (\d+) \(([A-Z0-9]{5})\)', result.stderr)
    return dict(returncode=result.returncode, stdout=result.stdout, stderr=result.stderr,
                mysqlErrno=int(match[1]) if match else None, sqlstate=match[2] if match else None)


def resume_channel(config, tcp_args, evidence):
    """Bounded read-only probes; a refused login is never retried as DDL."""
    from mysql84_release import Mysql, identity, admin_identity
    records = []
    def save():
        Path(evidence).write_text(json.dumps(records, indent=2), encoding='utf-8')
    deadline = time.monotonic() + 60
    query = 'SELECT VERSION(),@@server_uuid,@@hostname,@@port,@@datadir,@@global.offline_mode,USER(),CURRENT_USER();\n'
    while True:
        check_binding(config)
        record = dict(container=config['mysqlContainer'])
        try:
            with socket.create_connection((config['host'], config['port']), timeout=3):
                record['tcpReachable'] = True
        except OSError as error:
            record.update(tcpReachable=False, tcpError=type(error).__name__)
        ready = subprocess.run(socket_args(config), input=query, text=True, capture_output=True, timeout=10)
        record['socket'] = result_record(ready)
        records.append(record)
        save()
        if ready.returncode == 0:
            break
        require(record['socket']['mysqlErrno'] in (None, 2002, 2003, 2013),
                'Recovery administrative socket refused; diagnostic=' + json.dumps(record))
        require(time.monotonic() < deadline, 'Recovery server/socket unavailable; diagnostic=' + json.dumps(record))
        time.sleep(1)
    require(ready.stdout.strip().split('\t') == ['8.4.13', config['serverUuid'], config['recoveryBinding']['hostname'],
            '3306', '/var/lib/mysql/', '1', 'root@localhost', config['recoverySocketAccount']],
            'Recovery server UUID/identity/barrier mismatch')
    # Preserve the original TCP failure, even when the socket is already usable.
    tcp = subprocess.run(tcp_args, input=query, text=True, capture_output=True, timeout=10)
    record['administrativeTcp'] = result_record(tcp)
    save()
    recovered = dict(config, recoverySocket=True)
    db = Mysql(recovered)
    try:
        identity(db, recovered)
        admin_identity(db)
        require(db.scalar('SELECT CURRENT_USER()') == config['recoverySocketAccount'], 'Recovery account mismatch')
        require(db.scalar("SELECT VARIABLE_VALUE FROM performance_schema.persisted_variables WHERE VARIABLE_NAME='offline_mode'") == 'ON',
                'Recovery maintenance barrier is not persistent')
        record['grants'] = db.execute('SHOW GRANTS').splitlines()
        require(any(re.search(r'\bSYSTEM_USER\b', line.split(' ON ')[0]) and ' ON *.*' in line for line in record['grants']),
                'Recovery root requires SYSTEM_USER')
        record['status'] = 'PASS'
        save()
    finally:
        db.close()
    return recovered
