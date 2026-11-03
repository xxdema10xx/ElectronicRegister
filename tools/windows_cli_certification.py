"""Create a private native MySQL instance and run the three real Windows facts.

No external connection strings, services, Docker, or existing datadirs are used.
Only owned process handles and a newly created directory are cleaned up.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import sys
import traceback
import uuid
from types import SimpleNamespace
from local_test_db import Setup, private_acl, verify_files
from windows_cli_evidence import METHODS, PACKAGE, require, trx_methods, validate_report

ROOT = Path(__file__).resolve().parents[1]


class OwnedSetup(Setup):
    def __init__(self, args, password):
        super().__init__(args, password)
        self.processes = []

    def start(self, extra):
        process = super().start(extra)
        self.processes.append(process)
        return process


def acl_evidence(run):
    code = r"""
$ErrorActionPreference='Stop'; $base=$env:ER_TEST_CLI_PRIVATE_ROOT;
$trusted=@([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18','S-1-5-32-544');
$paths=@((Get-Item -LiteralPath $base))+@(Get-ChildItem -LiteralPath $base -Recurse -Filter admin.json -File);
@($paths | ForEach-Object { $a=Get-Acl -LiteralPath $_.FullName;
  @{owner=$a.GetOwner([Security.Principal.SecurityIdentifier]).Value; trusted=$trusted;
    rules=@($a.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]) | ForEach-Object {
      @{sid=$_.IdentityReference.Value; rights=[string]$_.FileSystemRights; type=[string]$_.AccessControlType}});
    sddl=$a.Sddl} }) | ConvertTo-Json -Depth 6 -Compress
"""
    result = subprocess.run(['powershell', '-NoProfile', '-Command', code], capture_output=True, text=True, check=True)
    return json.loads(result.stdout)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--mysql-bin', required=True, type=Path)
    p.add_argument('--output', required=True, type=Path)
    a = p.parse_args()
    output = a.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    report = dict(format=1, suite='windows-cli', problems=[
        dict(id=name, test=name, required=True, status='BLOCKED', error='Not executed: prerequisite failed')
        for name in ['platform', 'restore', 'build', 'provision', *METHODS, 'acl', 'cleanup']], trx=[], acl=[], provenance={
        'platform': sys.platform, **{k: os.environ.get(k, '') for k in ('GITHUB_SHA', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT')}})
    def check(name, action):
        row = next(row for row in report['problems'] if row['id'] == name)
        row.update(status='FAIL', error='')
        try:
            result = action()
            row['status'] = 'PASS'
            return result
        except Exception as error:
            row['error'] = traceback.format_exc()
            if hasattr(error, 'acl_diagnostic'):
                report['aclSetupFailure'] = error.acl_diagnostic
            raise
    run = None
    setup = None
    previous = dict(os.environ)
    password = secrets.token_hex(32)
    def command(args):
        result = subprocess.run(args, cwd=ROOT, capture_output=True, timeout=600)
        # Keep credentials and private configuration out of diagnostic uploads.
        require(result.returncode == 0, 'Command failed: '+str(args[:3])+'; exit '+str(result.returncode)+'\n'+
                (result.stdout+result.stderr).decode('utf-8', errors='replace').replace(password, '[REDACTED]')[-16000:])
    try:
        check('platform', lambda: require(os.name == 'nt' and not os.environ.get('TEST_MYSQL_CONNECTION'), 'Windows and a fresh target required'))
        check('restore', lambda: command(['dotnet', 'restore', 'Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx', '--locked-mode']))
        # Tests invoke the real CLI with dotnet run --no-build (Debug default).
        check('build', lambda: command(['dotnet', 'build', 'Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx', '--no-restore', '-v:q']))
        def provision():
            nonlocal run, setup
            manifest = verify_files()
            for name, expected in PACKAGE['executables'].items():
                require(hashlib.sha256((a.mysql_bin/name).read_bytes()).hexdigest().upper() == expected, 'Unpinned MySQL executable: '+name)
            report['package'] = PACKAGE
            with socket.socket() as probe:
                probe.bind(('127.0.0.1', 0))
                port = probe.getsockname()[1]
            require(port not in (3306, 3307, 13306), 'Reserved port')
            run = ROOT/'.staff-private'/('windows-cli-'+uuid.uuid4().hex)
            # Preserve the historical suffix, but never use the historical instance.
            data = run/'migrations/mt4/private/mt5a-mamp-13306/data'
            args = SimpleNamespace(mysql_bin=str(a.mysql_bin), datadir=str(data), port=port,
                                   main_datadir=str(ROOT/'.staff-private/forbidden-existing-target'), confirm_isolated_test=True)
            setup = OwnedSetup(args, password)
            # Setup validates the full ancestry for reparse points before creation.
            run.mkdir(parents=True, exist_ok=False)
            report['aclSetup'] = private_acl(run)
            setup.create()  # password installed offline via random shared-memory channel
            setup.identity()  # PID, executable, socket, UUID, version, mode, exact datadir
            require(setup.client("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME NOT IN ('mysql','sys','information_schema','performance_schema');") == '0', 'Fresh instance required')
            setup.client('CREATE DATABASE electronic_register_test CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;')
            for index, relative in enumerate(manifest['requiredMigrations']):
                setup.identity()
                sql = (ROOT/relative).read_text(encoding='utf-8-sig')
                # Bind only root fixture teardown predicates, on fresh CREATE SQL.
                # Historical sources and business predicates remain unchanged.
                sql = sql.replace('@@port=13306', '@@port='+str(port))
                setup.query(sql)
                if index == 0:
                    setup.query("INSERT INTO institution(id,name,code,status) VALUES ('4d543341-0000-4000-8000-000000000001','Technical legacy test holding','MT3A_LEGACY_TEST','provisioning'); INSERT INTO mt3a_legacy_scope(scope,institution_id) VALUES ('legacy','4d543341-0000-4000-8000-000000000001');")
            require(setup.query('CALL er_schema_ready();') == '1', 'Schema release readiness failed')
            row = setup.identity()
            marker = json.loads((setup.home/'instance.json').read_text())
            report['identity'] = dict(version=row[0], uuid=row[4], port=port, pid=marker['pid'], bind=row[6], datadir=row[3])
            cli = run/'cli'
            cli.mkdir()
            os.environ.update(TEST_MYSQL_CONNECTION=f'Server=127.0.0.1;Port={port};Database=electronic_register_test;User ID=root;Password={password};Allow User Variables=true;SslMode=None',
                              ER_TEST_ISOLATED_PORT=str(port), ER_TEST_ISOLATED_DATADIR=str(data), ER_TEST_STRICT_SESSION='1', ER_TEST_CLI_PRIVATE_ROOT=str(cli))
        check('provision', provision)
        for index, method in enumerate(METHODS):
            def test(method=method, index=index):
                setup.identity()
                name = str(index)+'.trx'
                command(['dotnet', 'test', 'tests/Integration/Integration.Tests.csproj', '--no-build', '--no-restore',
                         '--filter', 'FullyQualifiedName='+method, '--results-directory', str(output), '--logger', 'trx;LogFileName='+name])
                path = output/name
                # TRX is kept structurally intact; redact the ephemeral password if a framework prints it.
                path.write_bytes(path.read_bytes().replace(password.encode(), b'[REDACTED]'))
                require(trx_methods(path) == method, 'Exact method evidence required')
                report['trx'].append(dict(file=name, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            check(method, test)
        def acl():
            report['acl'] = acl_evidence(run)
            report['aclNegative'] = [json.loads(line) for line in (run/'cli/authority-acl.jsonl').read_text().splitlines()]
            require(len(report['acl']) == 4, 'Three authority files plus private parent expected')
            require(all(a['owner'] in a['trusted'] and a['rules'] and all(r['sid'] in a['trusted'] for r in a['rules']) for a in report['acl']), 'Authority ACL is not private')
        check('acl', acl)
    except Exception:
        pass  # detailed failing check is retained; no success inferred
    finally:
        def cleanup():
            if setup:
                for process in setup.processes:
                    if process.poll() is None:
                        # The handle was created in this invocation; never look up/kill an unrelated PID.
                        process.terminate()
                        process.wait(timeout=30)
            if run:
                require(run.parent == ROOT/'.staff-private' and run.name.startswith('windows-cli-'), 'Cleanup outside owned directory refused')
                shutil.rmtree(run)
                require(not run.exists(), 'Private resource cleanup failed')
        try:
            check('cleanup', cleanup)
        except Exception:
            pass
        os.environ.clear()
        os.environ.update(previous)
        for row in report['problems']:
            row['error'] = row['error'].replace(password, '[REDACTED]')
        for path in output.glob('*.trx'):
            path.write_bytes(path.read_bytes().replace(password.encode(), b'[REDACTED]'))
        (output/'windows-cli-diagnostic.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    try:
        validate_report(report, output)
        return 0
    except Exception as error:
        print('Windows certification FAILED: '+str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
