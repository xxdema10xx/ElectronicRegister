"""Merge CI diagnostics; missing mandatory jobs/reports are blocking failures."""
import json
import os
from pathlib import Path
from ci_diagnostic import failed


EXPECTED = {'static': {'restore', 'build', 'unit', 'fixture-config', 'python', 'whitespace', 'organization',
                       'mysql84-release', 'test-release-manifest', 'test-mysql84-release',
                       'test-mysql84', 'mysql84-remediation', 'test-mysql84-remediation'},
            **{name: {'restore', 'build', name} for name in
               ('calendar', 'formative', 'email', 'imports', 'iam', 'onboarding', 'tenants')},
            **{'runtime-'+mode: {'tools', 'predecessor', 'build', 'bind', 'predicates', 'runtime', 'csharp'}
               for mode in ('fresh', 'upgrade')}}


def main():
    output = Path('.diagnostics'); output.mkdir(exist_ok=True)
    rows = []; artifacts = []; sources = []; suites = {}
    for path in sorted((output/'downloaded').rglob('*diagnostic.json')):
        try:
            report=json.loads(path.read_text())
            # Preserve readable findings even when the envelope is incomplete.
            for row in report['problems']:
                rows.append(dict(row, originalId=row['id'], id=path.parent.name+':'+row['id']))
            artifacts.extend(report.get('artifacts',[]))
            suite = report.get('suite')
            if suite not in EXPECTED or suite in suites:
                raise ValueError('Unknown or duplicate report suite: '+str(suite))
            actual = {row['id'] for row in report['problems'] if row.get('required', True)}
            missing = EXPECTED[suite] - actual
            if missing:
                raise ValueError('Missing mandatory checks in '+suite+': '+', '.join(sorted(missing)))
            suites[suite] = str(path)
            sources.append(str(path))
        except Exception as error:
            rows.append(dict(id='unreadable-report',status='FAIL',required=True,error=str(error),file=str(path),line=1,category='aggregation',test='report-read',cause='Invalid diagnostic report',priority='P1',dependents=[]))
    for name in ('release','diagnostic','integration'):
        value=json.loads(os.environ.get('JOB_RESULTS','{}')).get(name,{}).get('result','absent')
        rows.append(dict(id='job-'+name,status='PASS' if value=='success' else 'BLOCKED' if value in ('skipped','absent') else 'FAIL',required=True,error=value,file='.github/workflows/mysql84-remediation-candidate.yml',line=1,category='aggregation',test=name,cause='Job outcome, including setup and upload failures',priority='P1',dependents=[]))
    if len(sources) != 10:
        rows.append(dict(id='report-count',status='FAIL',required=True,error=f'Expected 10 reports (static, 2 runtime, 7 domains); found {len(sources)}',file='.diagnostics/downloaded',line=1,category='aggregation',test='report-presence',cause='Report absent or duplicate',priority='P1',dependents=[]))
    for suite in sorted(EXPECTED.keys() - suites.keys()):
        rows.append(dict(id='missing-suite-'+suite,status='BLOCKED',required=True,error='Mandatory suite absent: '+suite,file='.diagnostics/downloaded',line=1,category='aggregation',test=suite,cause='Report missing or invalid',priority='P1',dependents=[]))
    report=dict(format=1,problems=rows,artifacts=artifacts,sources=sources)
    (output/'MYSQL84_CI_DIAGNOSTIC_REPORT.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    lines=['# MySQL 8.4 CI diagnostic report','','Historical hashes retain their original outcomes and do not certify this release. Full errors and provenance are in the JSON.','','| ID | Category | File / line | Test | Error | Cause | Priority | Status | Required | Dependents |','|---|---|---|---|---|---|---|---|---|---|']
    for row in rows:
        values = [row['id'], row.get('category',''), str(row.get('file',''))+':'+str(row.get('line',1)),
                  row.get('test',''), row.get('error',''), row.get('cause',''), row.get('priority','P1'),
                  row['status'], row.get('required',True), ', '.join(row.get('dependents',[]))]
        lines.append('| '+' | '.join(str(v).replace('|','\\|').replace('\n','<br>') for v in values)+' |')
    (output/'MYSQL84_CI_DIAGNOSTIC_REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    return int(failed(rows))


if __name__ == '__main__': raise SystemExit(main())
