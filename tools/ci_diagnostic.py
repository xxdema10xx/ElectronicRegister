"""Run every independent check, record dependencies, and fail closed at the end."""
import argparse
import json
import re
from pathlib import Path
import subprocess
import sys
import traceback
from artifact_diagnostic import audit, issue


def execute(checks, root, progress=None):
    results = []
    by_id = {}
    for check in checks:
        blocked = [name for name in check.get('needs', []) if by_id.get(name, {}).get('status') != 'PASS']
        row = issue('regression', check.get('file', check['command'][0]), '', check['id'])
        row.update(id=check['id'], required=check.get('required', True), command=check['command'])
        if blocked:
            row.update(status='BLOCKED', error='Prerequisites not PASS: '+', '.join(blocked), cause='Dependency failure')
            for name in blocked:
                if name in by_id: by_id[name]['dependents'].append(check['id'])
        else:
            try:
                # A previous run must never supply evidence for this invocation.
                if check.get('trx'):
                    (Path(root)/check['trx']).unlink(missing_ok=True)
                run = subprocess.run(check['command'], cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=check.get('timeout', 1200))
                row.update(status='PASS' if run.returncode == 0 else 'FAIL', error=run.stdout+run.stderr, exitCode=run.returncode)
                if row['status'] == 'PASS' and check.get('trx'):
                    validate_trx(Path(root)/check['trx'])
            except subprocess.TimeoutExpired as error:
                def decoded(value):
                    return value.decode('utf-8', errors='replace') if isinstance(value, bytes) else value or ''
                row.update(status='FAIL', error=decoded(error.stdout)+decoded(error.stderr)+traceback.format_exc(),
                           cause='Command timed out; partial stdout and stderr preserved')
            except Exception:
                row.update(status='FAIL', error=traceback.format_exc())
        results.append(row); by_id[check['id']] = row
        if progress: progress(results)
    return results


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
        checks.append(dict(id='discovery',needs=['build','docker'],command=[
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
                isolated = []
                for index, method in enumerate(methods):
                    directory = a.root/'.diagnostics'/a.integration/str(index)
                    isolated.append(dict(id=method, file='tests/Integration/Integration.Tests.csproj',
                        trx=str(directory/'current.trx'), command=[
                            'dotnet','test','tests/Integration/Integration.Tests.csproj','-c','Release','--no-build',
                            '--filter','FullyQualifiedName='+method,'--results-directory',str(directory),
                            '--logger','trx;LogFileName=current.trx']))
                before = list(report['problems'])
                rows = execute(isolated, a.root, lambda rows: a.output.write_text(
                    json.dumps(dict(report, problems=before+rows),indent=2)+'\n',encoding='utf-8'))
                report['problems'] += rows
                domain.update(status='FAIL' if failed(rows) else 'PASS',
                              error=f'{len(methods)} isolated method invocations; see individual outcomes',
                              cause='Every method runs in a new process and Testcontainers fixture')
            except Exception:
                domain.update(status='FAIL',error=traceback.format_exc(),cause='Integration orchestration failed')
        report['problems'].append(domain)
    a.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print('Historical anomalies retain FAIL and NON_CERTIFIABLE outcomes; they do not certify the new release.')
    for row in report['problems']:
        if row.get('required',True) or row['category'] == 'regression': print(row['id'],row['status'],row['test'])
    return int(failed(report['problems']))


if __name__ == '__main__': raise SystemExit(main())
