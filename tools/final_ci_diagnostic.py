"""Merge CI diagnostics; missing mandatory jobs/reports are blocking failures."""
import json
import os
from pathlib import Path
from ci_diagnostic import failed, discovered_methods
from integration_partition import SHARDS, partition
from windows_cli_evidence import CHECKS as WINDOWS_CHECKS, validate_report as validate_windows_report


EXPECTED = {'windows-cli': WINDOWS_CHECKS, 'static': {'restore', 'build', 'unit', 'fixture-config', 'python', 'whitespace', 'organization',
                       'mysql84-release', 'test-release-manifest', 'test-mysql84-release',
                       'test-mysql84', 'mysql84-remediation', 'test-mysql84-remediation'},
            **{name+('-'+str(i) if count > 1 else ''): {'restore', 'build', 'docker', 'discovery', name}
               for name, count in SHARDS.items() for i in range(count)},
            **{'runtime-'+mode: {'tools', 'predecessor', 'build', 'bind', 'predicates', 'runtime', 'csharp'}
               for mode in ('fresh', 'upgrade')}}


def summarize(rows):
    methods = {}
    for row in rows:
        method = row.get('test', '')
        if not method.startswith('ElectronicRegisterAPI.Tests.Integration.'):
            continue
        if row['status'] == 'PASS': continue
        item = methods.setdefault(method, dict(method=method, occurrences=0, statuses=[], sources=[]))
        item['occurrences'] += 1
        item['statuses'].append(row['status'])
        item['sources'].append(row['id'])
    causes = {}
    for row in rows:
        if row['status'] == 'PASS': continue
        error = row.get('error', '')
        if not row.get('required', True): category = 'historical-non-certifiable'
        elif row.get('infrastructure') or row.get('category') == 'infrastructure': category = 'infrastructure'
        elif 'INNODB_LOCK_WAITS' in error: category = 'lock-observer'
        elif any(s in error for s in ('Table DDL drift', 'Guid types/collations', 'Additive metadata drift')): category = 'migration-schema'
        elif 'MySQL timeout;' in error or 'queue.Empty' in error: category = 'sql-client-protocol'
        elif row['status'] == 'BLOCKED': category = 'not-executed'
        elif row['status'] == 'SKIPPED' and row.get('justification'): category = 'reviewed-exclusion'
        else: category = 'failure-requiring-analysis'
        causes.setdefault(category, []).append(row['id'])
    return dict(methods=sorted(methods.values(), key=lambda m: m['method']), categories=causes)


def main():
    output = Path('.diagnostics'); output.mkdir(exist_ok=True)
    rows = []; artifacts = []; sources = []; suites = {}; assignments = []
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
            if missing and suite != 'windows-cli':
                raise ValueError('Missing mandatory checks in '+suite+': '+', '.join(sorted(missing)))
            if suite == 'windows-cli':
                validate_windows_report(report, path.parent)
            elif suite not in ('static', 'runtime-fresh', 'runtime-upgrade'):
                assignment = report['assignment']
                inventory, selected = partition(assignment['inventory'], assignment['domain'],
                                                assignment['shard'], assignment['shards'])
                expected_suite = assignment['domain'] + ('-'+str(assignment['shard']) if assignment['shards'] > 1 else '')
                discovery = next(r for r in report['problems'] if r['id'] == 'discovery')
                discovered_inventory, _ = partition(discovered_methods(discovery['error']), assignment['domain'],
                                                    assignment['shard'], assignment['shards'])
                if suite != expected_suite or inventory != discovered_inventory:
                    raise ValueError('Suite/discovery binding mismatch: '+suite)
                if assignment['methods'] != selected or assignment['inventory'] != inventory:
                    raise ValueError('Shard assignment drift: '+suite)
                method_rows = [r for r in report['problems'] if r['id'] in selected]
                if sorted(r['id'] for r in method_rows) != sorted(selected):
                    raise ValueError('Missing or duplicate method evidence: '+suite)
                if any(r.get('cases') != assignment['expectedCases'][r['id']]
                       for r in method_rows if r['status'] == 'PASS'):
                    raise ValueError('Theory case accounting mismatch: '+suite)
                assignments.append(assignment)
            suites[suite] = str(path)
            sources.append(str(path))
        except Exception as error:
            rows.append(dict(id='unreadable-report',status='FAIL',required=True,error=str(error),file=str(path),line=1,category='aggregation',test='report-read',cause='Invalid diagnostic report',priority='P1',dependents=[]))
    for name in ('release','diagnostic','integration','windows-cli'):
        value=json.loads(os.environ.get('JOB_RESULTS','{}')).get(name,{}).get('result','absent')
        rows.append(dict(id='job-'+name,status='PASS' if value=='success' else 'BLOCKED' if value in ('skipped','absent') else 'FAIL',required=True,error=value,file='.github/workflows/mysql84-remediation-candidate.yml',line=1,category='aggregation',test=name,cause='Job outcome, including setup and upload failures',priority='P1',dependents=[]))
    for domain in SHARDS:
        domain_assignments = [a for a in assignments if a['domain'] == domain]
        inventories = [a['inventory'] for a in domain_assignments]
        if inventories and any(i != inventories[0] for i in inventories):
            rows.append(dict(id='inventory-'+domain, status='FAIL', required=True, error='Discovery inventories differ between shards'))
        if len(domain_assignments) == SHARDS[domain] and inventories:
            assigned = [m for a in domain_assignments for m in a['methods']]
            if sorted(assigned) != inventories[0]:
                rows.append(dict(id='coverage-'+domain, status='FAIL', required=True, error='Shard union is incomplete or duplicated'))
    if len(sources) != len(EXPECTED):
        rows.append(dict(id='report-count',status='FAIL',required=True,error=f'Expected {len(EXPECTED)} reports; found {len(sources)}',file='.diagnostics/downloaded',line=1,category='aggregation',test='report-presence',cause='Report absent or duplicate',priority='P1',dependents=[]))
    for suite in sorted(EXPECTED.keys() - suites.keys()):
        rows.append(dict(id='missing-suite-'+suite,status='BLOCKED',required=True,error='Mandatory suite absent: '+suite,file='.diagnostics/downloaded',line=1,category='aggregation',test=suite,cause='Report missing or invalid',priority='P1',dependents=[]))
    summary = summarize(rows)
    report=dict(format=1,problems=rows,artifacts=artifacts,sources=sources,summary=summary)
    (output/'MYSQL84_CI_DIAGNOSTIC_REPORT.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    lines=['# MySQL 8.4 CI diagnostic report','','Historical hashes retain their original outcomes and do not certify this release. Full errors and provenance are in the JSON.','','| ID | Category | File / line | Test | Error | Cause | Priority | Status | Required | Dependents |','|---|---|---|---|---|---|---|---|---|---|']
    heading = ['## Methods requiring attention', '', '| Method | Occurrences | Outcomes |', '|---|---|---|']
    heading += ['| '+m['method']+' | '+str(m['occurrences'])+' | '+', '.join(m['statuses'])+' |' for m in summary['methods']]
    heading += ['', 'Categories are diagnostic groupings; full original errors remain in JSON and raw artifacts.', '']
    lines[4:4] = heading
    for row in rows:
        values = [row['id'], row.get('category',''), str(row.get('file',''))+':'+str(row.get('line',1)),
                  row.get('test',''), row.get('error','')[:400], row.get('cause',''), row.get('priority','P1'),
                  row['status'], row.get('required',True), ', '.join(row.get('dependents',[]))]
        lines.append('| '+' | '.join(str(v).replace('|','\\|').replace('\n','<br>') for v in values)+' |')
    (output/'MYSQL84_CI_DIAGNOSTIC_REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    return int(failed(rows))


if __name__ == '__main__': raise SystemExit(main())
