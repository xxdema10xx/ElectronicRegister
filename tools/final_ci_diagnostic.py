"""Merge CI diagnostics; missing mandatory jobs/reports are blocking failures."""
import json
import os
from pathlib import Path
from ci_diagnostic import failed


def main():
    output = Path('.diagnostics'); output.mkdir(exist_ok=True)
    rows = []; artifacts = []; sources = []
    for path in sorted((output/'downloaded').rglob('*diagnostic.json')):
        try:
            report=json.loads(path.read_text())
            for row in report['problems']:
                rows.append(dict(row, originalId=row['id'], id=path.parent.name+':'+row['id']))
            artifacts.extend(report.get('artifacts',[])); sources.append(str(path))
        except Exception as error:
            rows.append(dict(id='unreadable-report',status='FAIL',required=True,error=str(error),file=str(path),line=1,category='aggregation',test='report-read',cause='Invalid diagnostic report',priority='P1',dependents=[]))
    for name in ('release','diagnostic','integration'):
        value=json.loads(os.environ.get('JOB_RESULTS','{}')).get(name,{}).get('result','absent')
        rows.append(dict(id='job-'+name,status='PASS' if value=='success' else 'BLOCKED' if value in ('skipped','absent') else 'FAIL',required=True,error=value,file='.github/workflows/mysql84-remediation-candidate.yml',line=1,category='aggregation',test=name,cause='Job outcome, including setup and upload failures',priority='P1',dependents=[]))
    if len(sources) != 10:
        rows.append(dict(id='report-count',status='FAIL',required=True,error=f'Expected 10 reports (static, 2 runtime, 7 domains); found {len(sources)}',file='.diagnostics/downloaded',line=1,category='aggregation',test='report-presence',cause='Report absent or duplicate',priority='P1',dependents=[]))
    report=dict(format=1,problems=rows,artifacts=artifacts,sources=sources)
    (output/'MYSQL84_CI_DIAGNOSTIC_REPORT.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    lines=['# MySQL 8.4 CI diagnostic report','','Historical hashes retain their original outcomes and do not certify this release. Full errors and provenance are in the JSON.','','| ID | Status | Required | Test | Error |','|---|---|---|---|---|']
    for row in rows:
        error=row.get('error','').replace('|','\\|').replace('\n','<br>')[:350]
        lines.append(f"| {row['id']} | {row['status']} | {row.get('required',True)} | {row.get('test','')} | {error} |")
    (output/'MYSQL84_CI_DIAGNOSTIC_REPORT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    return int(failed(rows))


if __name__ == '__main__': raise SystemExit(main())
