"""Real restart regression, two invocations around an explicitly approved restart.

prepare CONFIG EVIDENCE; docker restart <attested ID>; verify CONFIG EVIDENCE
Never starts/restarts/deletes a container. No mocks and no arbitrary SQL target.
"""
import copy
import json
import os
from pathlib import Path
import subprocess
import sys

from mysql84_release import Mysql, admin_identity, identity, require
from mysql84_recovery import container_binding, resume_channel, result_record, socket_args
from mysql84_runtime import Probes


def run(mode, config_path, evidence_path):
    config = json.loads(Path(config_path).read_text(encoding='utf-8-sig'))
    path = Path(evidence_path)
    if mode == 'ci':
        require(config.get('isolatedCI') is True and os.environ.get('GITHUB_ACTIONS') == 'true', 'Automatic restart requires disposable GitHub service')
        config['database'] += '_restart'
        config['apiUser'] += '_r'
        temporary = path.with_name('restart-config.json')
        temporary.write_text(json.dumps(config))
        run('prepare', temporary, path)
        subprocess.run(['docker', 'restart', config['mysqlContainer']], check=True, timeout=30)
        run('verify', temporary, path)
        return
    if mode == 'prepare':
        require(not path.exists(), 'New evidence required')
        config['recoveryBinding'] = container_binding(config)
        db = Mysql(config)
        try:
            db.execute('CREATE DATABASE `' + config['database'] + '`')
            server = identity(db, config)
            admin_identity(db)
            db.execute('CREATE TABLE restart_probe(id INT PRIMARY KEY, payload VARCHAR(30))')
            db.execute("INSERT INTO restart_probe VALUES(8,'persistent-before-restart')")
            db.execute("CREATE USER '"+config['apiUser']+"'@'%' IDENTIFIED BY 'Synthetic-ci-only-credential'")
            db.execute("GRANT SELECT ON `"+config['database']+"`.* TO '"+config['apiUser']+"'@'%'")
            db.execute("CREATE USER 'er84_restart_limited'@'localhost'")
            db.execute("GRANT CONNECTION_ADMIN ON *.* TO 'er84_restart_limited'@'localhost'")
            db.execute('SET PERSIST offline_mode=ON')
            path.write_text(json.dumps(dict(config=config, before=server), indent=2))
        finally:
            db.close()
        return
    require(mode == 'verify', 'Expected prepare or verify')
    evidence = json.loads(path.read_text())
    config = evidence['config']
    plain = Mysql(config)
    tcp_args = plain.tcp_args
    plain.close()
    recovered = resume_channel(config, tcp_args, path.with_name('targeted-restart-result.json'))
    db = Mysql(recovered)
    try:
        evidence['after'] = identity(db, recovered)
        require(db.scalar('SELECT payload FROM restart_probe WHERE id=8') == 'persistent-before-restart', 'Restart lost data')
        denied = Probes(db, config).api('SELECT 1')
        evidence['api'] = result_record(denied)
        require(denied.returncode != 0 and evidence['api']['mysqlErrno'] == 3032 and evidence['api']['sqlstate'] == 'HY000', 'API barrier lost')
        # Exercise the same readiness function with a genuinely refused TCP
        # account. It must retain 3032/HY000 while the authorized socket passes.
        resume_channel(config, tcp_args + ['--user='+config['apiUser'], '--password=Synthetic-ci-only-credential'],
                       path.with_name('refused-tcp-restart-result.json'))
        limited_args = [a.replace('--user=root', '--user=er84_restart_limited') for a in socket_args(config)]
        class LimitedClient:
            def execute(self, sql):
                result = subprocess.run(limited_args, input=sql+';\n', text=True, capture_output=True, timeout=10)
                require(result.returncode == 0, 'Limited CONNECTION_ADMIN connection failed')
                return result.stdout
        limited = LimitedClient()
        evidence['limitedIdentity'] = limited.execute('SELECT USER(),CURRENT_USER()')
        evidence['limitedGrants'] = limited.execute('SHOW GRANTS')
        try:
            admin_identity(limited)
        except RuntimeError as error:
            require('Administrative migration identity requires' in str(error), 'Wrong privilege rejection')
            evidence['insufficientPrivilegesRejected'] = True
        else:
            raise RuntimeError('Limited administrator accepted')
        negatives = []
        for field in ('container', 'volume', 'uuid'):
            bad = copy.deepcopy(config)
            if field == 'container':
                bad['recoveryBinding']['container'] = '0'*64
            elif field == 'volume':
                bad['recoveryBinding']['mounts'][0]['Name'] += '-different'
            else:
                bad['serverUuid'] = '00000000-0000-0000-0000-000000000000'
            try:
                resume_channel(bad, tcp_args, path.with_name('rejected-'+field+'-result.json'))
            except RuntimeError as error:
                require('binding changed' in str(error) or 'UUID/identity/barrier mismatch' in str(error), 'Wrong identity rejection')
                negatives.append(dict(field=field, error=str(error)))
            else:
                raise RuntimeError('Changed '+field+' accepted')
        evidence['identityRejections'] = negatives
        require(db.scalar('SELECT @@global.offline_mode') == '1', 'Negative probes removed barrier')
        # This regression has no interrupted DDL. All persistent checks passed.
        db.execute('SET PERSIST offline_mode=OFF')
    finally:
        db.close()
    db = Mysql(config)
    try:
        require(db.scalar('SELECT @@global.offline_mode') == '0', 'Barrier removal failed')
        require(db.scalar("SELECT VARIABLE_VALUE FROM performance_schema.persisted_variables WHERE VARIABLE_NAME='offline_mode'") == 'OFF', 'Barrier removal not persisted')
        require(Probes(db, config).api('SELECT 1').returncode == 0, 'API not restored')
        evidence.update(status='PASS', dataPreserved=True, persistentBarrier=True, barrierRemoved=True)
        path.write_text(json.dumps(evidence, indent=2))
    finally:
        db.close()


if __name__ == '__main__':
    run(*sys.argv[1:])
