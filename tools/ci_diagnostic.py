"""Run every independent check, record dependencies, and fail closed at the end."""
import argparse
import json
import re
from pathlib import Path
import subprocess
import sys
import traceback
import time
import os
import signal
from artifact_diagnostic import audit, issue


PREFLIGHT_TIMEOUT = 240  # image startup, release installation and real API startup
TEST_TIMEOUT = 300
RECOVERY_TIMEOUT = 900  # locks, concurrency barriers and fault recovery
SUITE_TIMEOUT = 3600
# Reviewed tests whose bodies leave no shared mutable scenario state. New tests
# are isolated until explicitly reviewed; naming alone never authorizes reuse.
REUSABLE_METHODS = {
    'ElectronicRegisterAPI.Tests.Integration.Mt5eIamHttpTests.BothRolloutFlagsAreRequiredAndGetRemainsReadOnly',
    'ElectronicRegisterAPI.Tests.Integration.Mt5eIamHttpTests.InvalidBodiesAre400AndNeverWrite',
    'ElectronicRegisterAPI.Tests.Integration.Mt5cOnboardingTests.UntrustedOperatorsAndInvalidSignedOperationsNeverCreateAnInstitution',
}


def run_command(command, cwd, timeout):
    options = dict(cwd=cwd, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                   text=True, encoding='utf-8', errors='replace')
    if os.name == 'nt':
        options['creationflags'] = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        options['start_new_session'] = True
    process = subprocess.Popen(command, **options)
    try:
        stdout, stderr = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        if os.name == 'nt':
            try:
                subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'], capture_output=True, timeout=10)
            except (OSError, subprocess.SubprocessError):
                pass
            finally:
                if process.poll() is None: process.kill()
        else:
            try: os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError: pass
        stdout, stderr = process.communicate(timeout=10)
        raise subprocess.TimeoutExpired(command, timeout, output=stdout, stderr=stderr) from None
    return subprocess.CompletedProcess(command, process.returncode, stdout, stderr)


def execute(checks, root, progress=None, suite_timeout=None, prerequisites=()):
    results = []
    by_id = {row['id']:row for row in prerequisites}
    started = time.monotonic()
    for check in checks:
        blocked = [name for name in check.get('needs', []) if by_id.get(name, {}).get('status') != 'PASS']
        row = issue('regression', check.get('file', check['command'][0]), '', check['id'])
        row.update(id=check['id'], required=check.get('required', True), command=check['command'])
        if suite_timeout is not None and time.monotonic()-started >= suite_timeout:
            row.update(status='BLOCKED', error='Suite deadline reached before invocation', cause='Suite budget exhausted; no application outcome inferred', infrastructure=True)
        elif blocked:
            row.update(status='BLOCKED', error='Prerequisites not PASS: '+', '.join(blocked), cause='Dependency failure')
            for name in blocked:
                if name in by_id: by_id[name]['dependents'].append(check['id'])
        else:
            try:
                # A previous run must never supply evidence for this invocation.
                if check.get('trx'):
                    (Path(root)/check['trx']).unlink(missing_ok=True)
                limit = check.get('timeout', 1200)
                if suite_timeout is not None:
                    limit = min(limit, max(0.01, suite_timeout-(time.monotonic()-started)))
                run = run_command(check['command'], cwd=root, timeout=limit)
                row.update(status='PASS' if run.returncode == 0 else 'FAIL', error=run.stdout+run.stderr, exitCode=run.returncode)
                if row['status'] == 'PASS' and check.get('trx'):
                    validate_trx(Path(root)/check['trx'])
            except subprocess.TimeoutExpired as error:
                def decoded(value):
                    return value.decode('utf-8', errors='replace') if isinstance(value, bytes) else value or ''
                row.update(status='FAIL', error=decoded(error.stdout)+decoded(error.stderr)+traceback.format_exc(),
                           cause='Command timed out; partial stdout and stderr preserved', outcome='TIMEOUT', infrastructure=True, timeoutSeconds=limit)
            except Exception:
                row.update(status='FAIL', error=traceback.format_exc())
        results.append(row); by_id[check['id']] = row
        if check.get('members'):
            results.extend(batch_methods(check, row, root))
        if progress: progress(results)
    return results


def batch_methods(check, invocation, root):
    """Keep an outcome for every discovered method, including an aborted batch.
    A theory method passes only when all of its recorded cases passed.
    """
    import xml.etree.ElementTree as ET
    observed = {}
    parse_error = ''
    if invocation['status'] != 'BLOCKED':
        try:
            tree = ET.parse(Path(root)/check['trx']).getroot()
            ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
            summary = tree.find('t:ResultSummary', ns)
            counters = tree.find('t:ResultSummary/t:Counters', ns)
            recorded = tree.findall('t:Results/t:UnitTestResult', ns)
            if (invocation.get('outcome') == 'TIMEOUT' or summary is None or summary.get('outcome') == 'Aborted'
                    or counters is None or not recorded or
                    any(int(counters.get(k, '-1')) != len(recorded) for k in ('total','executed'))):
                raise ValueError('Incomplete batch; partial theory results cannot certify a method')
            definitions = {}
            for test in tree.findall('t:TestDefinitions/t:UnitTest', ns):
                method = test.find('t:TestMethod', ns)
                if method is not None:
                    definitions[test.get('id')] = method.get('className','').split(',')[0]+'.'+method.get('name','')
            for result in recorded:
                name = definitions.get(result.get('testId'))
                if name not in check['members']:
                    raise ValueError('Unexpected or unmapped batch result: '+str(name))
                observed.setdefault(name, []).append(result)
        except Exception:
            parse_error = traceback.format_exc()
    rows = []
    for method in check['members']:
        row = issue('regression', check.get('file','tests/Integration/Integration.Tests.csproj'), '', test=method)
        row.update(id=method, required=True, invocation=check['id'])
        cases = observed.get(method, [])
        if invocation['status'] == 'BLOCKED':
            row.update(status='BLOCKED', cause=invocation['cause'], error=invocation['error'])
        elif not cases or parse_error:
            row.update(status='FAIL', cause='Batch did not provide complete method evidence', error=parse_error or invocation['error'])
            if invocation.get('outcome') == 'TIMEOUT': row.update(outcome='TIMEOUT', infrastructure=True)
        else:
            row.update(status='PASS' if all(c.get('outcome') == 'Passed' for c in cases) else 'FAIL',
                       cause='All theory cases checked in current TRX', cases=len(cases),
                       error='\n'.join(ET.tostring(c, encoding='unicode') for c in cases if c.get('outcome') != 'Passed'))
        rows.append(row)
    return rows


def integration_plan(methods, root, suite):
    """Preflight the two distinct fixture families, once each. Never classify
    an assertion failure as infrastructure by matching arbitrary error text.
    Unknown/mutating methods remain individually isolated by default.
    """
    prefix = 'ElectronicRegisterAPI.Tests.Integration.'
    families = {m: ('identity' if m.startswith(prefix+('Mt5cOnboardingTests' if suite == 'onboarding' else 'Mt5eIamHttpTests')) else 'functional') for m in methods}
    checks = []
    def command(selection, directory):
        return ['dotnet','test','tests/Integration/Integration.Tests.csproj','-c','Release','--no-build',
                '--filter',selection,'--results-directory',str(directory),'--logger','trx;LogFileName=current.trx',
                '--blame-hang','--blame-hang-timeout','12m']
    for family in sorted(set(families.values())):
        name = ('IdentityDiagnosticPreflightTests' if family == 'identity' else 'DiagnosticPreflightTests')
        directory = Path(root)/'.diagnostics'/suite/('preflight-'+family)
        checks.append(dict(id='preflight-'+family, timeout=PREFLIGHT_TIMEOUT, trx=str(directory/'current.trx'),
                           command=command('FullyQualifiedName='+prefix+name+'.SchemaConnectionAndHost', directory)))
    batches = {}
    for index, method in enumerate(methods):
        directory = Path(root)/'.diagnostics'/suite/str(index)
        long = re.search(r'Concurr|Rollback|Recover|Fault|Deadlock|Interrup|Lock|Timeout|Race|Parallel', method, re.I)
        destructive = re.search(r'Delet|Purge|Drop|Destroy|Truncat|Cleanup', method, re.I)
        if method in REUSABLE_METHODS and not long and not destructive:
            batches.setdefault((families[method], method.rsplit('.', 1)[0]), []).append(method)
            continue
        checks.append(dict(id=method, needs=['preflight-'+families[method]], timeout=RECOVERY_TIMEOUT if long else TEST_TIMEOUT,
                           file='tests/Integration/Integration.Tests.csproj', trx=str(directory/'current.trx'),
                           command=command('FullyQualifiedName='+method, directory)))
    for index, ((family, cls), members) in enumerate(sorted(batches.items())):
        directory = Path(root)/'.diagnostics'/suite/('batch-'+str(index))
        checks.append(dict(id='batch-'+cls, members=members, needs=['preflight-'+family],
                           timeout=min(1800, TEST_TIMEOUT*len(members)), trx=str(directory/'current.trx'),
                           command=command('|'.join('FullyQualifiedName='+m for m in members), directory)))
    return checks


def validate_trx(path):
    import xml.etree.ElementTree as ET
    root = ET.parse(path).getroot()
    ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    summary = root.find('t:ResultSummary', ns)
    if summary is None or summary.get('outcome') != 'Completed':
        raise ValueError('New regression did not complete: '+str(path))
    results = root.findall('t:Results/t:UnitTestResult', ns)
    counters = root.find('t:ResultSummary/t:Counters', ns)
    if not results or any(r.get('outcome') != 'Passed' for r in results) or counters is None:
        raise ValueError('New regression empty, skipped, blocked or failed: '+str(path))
    if any(int(counters.get(k, '-1')) != len(results) for k in ('total','executed','passed')):
        raise ValueError('New regression counters mismatch: '+str(path))


def failed(rows):
    return any(r.get('required', True) and r['status'] != 'PASS' and not (r['status'] == 'SKIPPED' and r.get('justification')) for r in rows)


def discovered_methods(output):
    # dotnet --list-tests prints fully qualified names, with optional theory data.
    methods = set()
    for line in output.splitlines():
        if not line.startswith('    ElectronicRegisterAPI.Tests.Integration.'):
            continue
        name = line.strip().split('(', 1)[0]
        if not re.fullmatch(r'[A-Za-z_][\w.]+', name):
            raise ValueError('Unrecognized discovered test: '+line)
        methods.add(name)
    if not methods:
        raise ValueError('No integration test methods discovered')
    return sorted(methods)


def static_checks():
    checks = [dict(id=Path(name).stem, command=['node','tools/'+name]) for name in (
        'mysql84-release.cjs','test-release-manifest.cjs','test-mysql84-release.cjs',
        'test-mysql84.cjs','mysql84-remediation.cjs','test-mysql84-remediation.cjs')]
    checks += [dict(id='historical-'+name, required=False, command=[sys.executable,'tools/verify-institution-'+name+'.py']) for name in ('email','import','formative-plan','calendar','staff')]
    checks += [dict(id='python',command=[sys.executable,'-m','unittest','discover','-s','tools','-p','test_*.py']),
        dict(id='restore',command=['dotnet','restore','Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx','--locked-mode']),
        dict(id='build',needs=['restore'],command=['dotnet','build','Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx','-c','Release','--no-restore']),
        dict(id='unit',needs=['build'],command=['dotnet','test','tests/Unit/Unit.Tests.csproj','-c','Release','--no-build']),
        dict(id='fixture-config',needs=['build'],timeout=120,command=['dotnet','test','tests/Integration/Integration.Tests.csproj','-c','Release','--no-build','--filter','FullyQualifiedName~MysqlFixtureConfigurationTests']),
        dict(id='organization-restore',command=['dotnet','restore','tools/CodeOrganization/CodeOrganization.csproj','--locked-mode']),
        dict(id='organization',needs=['organization-restore'],command=['dotnet','run','--project','tools/CodeOrganization','--no-restore','--','check','.']),
        dict(id='whitespace',command=['git','diff','--check'])]
    return checks


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[1])
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--plan',type=Path)
    p.add_argument('--audit',action='store_true')
    p.add_argument('--integration')
    a = p.parse_args(); report = dict(format=1, problems=[], artifacts=[])
    a.output.parent.mkdir(parents=True,exist_ok=True)
    if a.audit:
        try:
            report = audit(a.root)
        except Exception:
            report['problems'].append(issue('audit-infrastructure', 'migrations/mysql84-remediation/manifest.json', traceback.format_exc(), state='active'))
        for row in report['problems']:
            # Historical claims stay failed, but have no authority over the new release.
            row['required'] = (row.get('state') == 'active' and row['category'] != 'encoding-difference') or row['category'] in ('active-drift','state-conflict')
    report['suite'] = a.integration or ('static' if a.audit else 'custom')
    checks = json.loads(a.plan.read_text()) if a.plan else static_checks()
    if a.integration:
        filters = {
            'calendar':'FullyQualifiedName~Calendar_',
            'formative':'FullyQualifiedName~InstitutionFormativePlanTests&FullyQualifiedName!~Calendar_',
            'email':'FullyQualifiedName~EmailPolicy_',
            'imports':'FullyQualifiedName~BulkImport_',
            'iam':'FullyQualifiedName~Mt5eIamHttpTests|FullyQualifiedName~IdentityAccountTests',
            'onboarding':'FullyQualifiedName~Mt5cOnboardingTests',
            'tenants':'FullyQualifiedName~Mt2IdentityTests|FullyQualifiedName~InstitutionClassCrudTests'}
        checks = [c for c in static_checks() if c['id'] in ('restore','build')]
        checks.append(dict(id='docker', command=['docker','info']))
        checks.append(dict(id='discovery',needs=['build'],command=[
            'dotnet','test','tests/Integration/Integration.Tests.csproj','-c','Release','--no-build',
            '--list-tests','--filter',filters[a.integration]]))
    initial = list(report['problems'])
    def progress(rows):
        partial = dict(report, problems=initial+rows)
        a.output.write_text(json.dumps(partial,indent=2)+'\n',encoding='utf-8')
    progress([])
    report['problems'] += execute(checks,a.root,progress)
    if a.integration:
        discovery = report['problems'][-1]
        domain = issue('integration', 'tests/Integration/Integration.Tests.csproj', '', test=a.integration)
        domain.update(id=a.integration, required=True)
        if discovery['status'] != 'PASS':
            domain.update(status='BLOCKED', error='Blocked by: discovery', cause='Build or discovery prerequisite failed')
            discovery['dependents'].append(a.integration)
        else:
            try:
                methods = discovered_methods(discovery['error'])
                isolated = integration_plan(methods, a.root, a.integration)
                for check in isolated:
                    if check['id'].startswith('preflight-'):
                        check['needs'] = ['build', 'docker']
                before = list(report['problems'])
                rows = execute(isolated, a.root, lambda rows: a.output.write_text(
                    json.dumps(dict(report, problems=before+rows),indent=2)+'\n',encoding='utf-8'), suite_timeout=SUITE_TIMEOUT,
                    prerequisites=before)
                report['problems'] += rows
                domain.update(status='FAIL' if failed(rows) else 'PASS',
                              error=f'{len(methods)} discovered methods; see individual outcomes and shared fixture preflights',
                              cause='Failed preflight blocks only its dependent fixture family')
                outcomes = [r for r in rows if r['id'] in methods]
                require_counts = len(outcomes) == len(methods) and len({r['id'] for r in outcomes}) == len(methods)
                if not require_counts:
                    raise ValueError('Discovered method accounting mismatch')
                domain['counts'] = dict(discovered=len(methods), **{s.lower():sum(r['status']==s for r in outcomes) for s in ('PASS','FAIL','BLOCKED')})
            except Exception:
                domain.update(status='FAIL',error=traceback.format_exc(),cause='Integration orchestration failed')
        report['problems'].append(domain)
    a.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('Historical anomalies retain FAIL and NON_CERTIFIABLE outcomes; they do not certify the new release.')
    for row in report['problems']:
        if row.get('required',True) or row['category'] == 'regression': print(row['id'],row['status'],row['test'])
    return int(failed(report['problems']))


if __name__ == '__main__': raise SystemExit(main())
