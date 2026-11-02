"""Explicit local Docker attestation; never enables the GitHub Actions path.

No Docker creation, deletion or restart is performed by this entry point.
The caller must approve execution outside MXC separately.
"""
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]


def attest(config):
    from mysql84_release import require
    require(config.get('isolatedLocal') is True and not config.get('isolatedCI'), 'Distinct local mode required')
    require(os.environ.get('GITHUB_ACTIONS') != 'true', 'Local runner refuses GitHub impersonation')
    require(not os.environ.get('DOCKER_HOST') and not os.environ.get('DOCKER_CONTEXT'), 'Docker transport override refused')
    cid = config['mysqlContainer']
    original = config['protectedContainer']
    require(len(cid) == 64 and all(c in '0123456789abcdef' for c in cid) and cid != original, 'Container ID rejected')
    items = json.loads(subprocess.check_output(['docker', 'inspect', cid, original], text=True))
    target, protected = items
    image = json.loads((ROOT/'infra/staging/mysql-image.json').read_text())['image']
    require(target['Id'] == cid and protected['Id'] == original, 'Container identity mismatch')
    require(target['Config']['Image'] == image and target['State']['Running'], 'Image/state mismatch')
    require(target['Config']['Labels'].get('er84.local.attestation') == config['localAttestation'], 'Ownership label mismatch')
    require(target['Name'] == '/' + config['containerName'], 'Name mismatch')
    mounts = target['Mounts']
    require(len(mounts) == 1 and mounts[0]['Type'] == 'volume' and mounts[0]['Name'] == config['volumeName'] and mounts[0]['Destination'] == '/var/lib/mysql', 'Dedicated volume required')
    require(all(mounts[0]['Source'] != m['Source'] and mounts[0]['Name'] != m.get('Name') for m in protected['Mounts']), 'Shared data mount refused')
    require(target['NetworkSettings']['Ports']['3306/tcp'] == [{'HostIp':'127.0.0.1','HostPort':str(config['port'])}], 'Port binding mismatch')
    require(config['host'] == '127.0.0.1' and config['port'] not in (3306, 3307, 13306), 'Local target rejected')
    require(config['database'].startswith('er84_') or config['database'] == 'electronic_register_test', 'Schema namespace rejected')
    return dict(container=cid, protectedContainer=original, image=image, mounts=mounts, port=config['port'])


def main():
    from mysql84_release import Mysql, identity, install, verify_schema, load_contract, require, sha
    config = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8-sig'))
    output = Path(sys.argv[2])
    output.mkdir(parents=True, exist_ok=False)
    start = time.monotonic()
    result = dict(status='FAIL', attestation=attest(config))
    db = None
    try:
        subprocess.run(['node',str(ROOT/'tools/mysql84-release.cjs')],check=True)
        db = Mysql(config)
        if '--acceptance' in sys.argv or '--fresh-runtime' in sys.argv:
            from mysql84_runtime import Probes, fresh_install
            result['server'] = identity(db, config)
            if '--fresh-runtime' in sys.argv:
                fresh_install(db,config,output)
            else:
                probes = Probes(db,config)
                probes.seed()
                result['acceptance'] = probes.all()
            result['status'] = 'PASS'
            return
        if '--reconcile' in sys.argv:
            journal = json.loads(Path(sys.argv[sys.argv.index('--reconcile')+1]).read_text())
            require(journal['serverUuid'] == config['serverUuid'] and journal['database'] == config['database'], 'Journal target mismatch')
            require(journal['phase'] == 'maintenance-intent' and all(f['phase'] == 'done' for f in journal['files'].values()), 'Uncertain DDL requires manual reconciliation')
            result['server'] = identity(db,config)
            verify_schema(db,load_contract(),config)
            require(db.scalar("SELECT COUNT(*) FROM er_schema_release WHERE version='er-quality-2-mysql84'") == '1', 'Marker missing')
            db.execute('SET PERSIST offline_mode=OFF')
            result.update(status='PASS', reconciledWithoutDdlReplay=True)
            return
        # Identity guard in Mysql runs before this first CREATE too.
        db.execute('CREATE DATABASE `' + config['database'] + '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci')
        result['server'] = identity(db, config)
        for role in ('api','review'):
            db.execute("CREATE USER '"+config[role+'User']+"'@'%' IDENTIFIED BY 'Synthetic-ci-only-credential' REQUIRE SSL")
        bundle = output/'bundle'
        subprocess.run(['node',str(ROOT/'tools/mysql84-release.cjs'),'bundle',sys.argv[1],str(bundle)],check=True)
        install(db,config,output/'journal.json',bundle)
        install(db,config,output/'journal.json',bundle)
        result['status'] = 'PASS'
    except Exception as error:
        result['error'] = str(error)
        raise
    finally:
        if db: db.close()
        result['durationSeconds'] = round(time.monotonic()-start,3)
        (output/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')


if __name__ == '__main__':
    main()
