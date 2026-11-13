"""Gate of the extended integration suite (integration-full.yml).

Fails closed unless every expected shard report is present exactly once, the shard
assignment matches an independent re-partition of the discovered inventory, every
non-excluded method has a PASS outcome with its complete theory cases, and the case
inventory adds up: discovered = executed + excluded methods' cases + excluded cases.
"""
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from ci_diagnostic import discovered_methods  # noqa: E402
from integration_partition import EXTENDED_SHARDS, case_exclusions, exclusions, partition  # noqa: E402

DOMAIN = 'full'
PREFIX = '    ElectronicRegisterAPI.Tests.Integration.'


def evaluate(reports):
    shards = EXTENDED_SHARDS[DOMAIN]
    problems, by_suite = [], {}
    for report in reports:
        suite = report.get('suite')
        if suite in by_suite:
            problems.append('duplicate report ' + str(suite))
        by_suite[suite] = report
    expected = {DOMAIN + '-' + str(i) for i in range(shards)}
    for suite in sorted(expected - by_suite.keys()):
        problems.append('missing report ' + suite)
    for suite in sorted(by_suite.keys() - expected):
        problems.append('unexpected report ' + str(suite))
    inventory = None
    cases = None
    methods = {}
    for suite in sorted(expected & by_suite.keys()):
        report = by_suite[suite]
        assignment = report.get('assignment')
        discovery = next((r for r in report['problems'] if r.get('id') == 'discovery'), None)
        if not assignment or not discovery:
            problems.append(suite + ': no assignment or discovery (blocked before partition)')
            continue
        listed = [line.strip() for line in discovery['error'].splitlines() if line.startswith(PREFIX)]
        found = discovered_methods(discovery['error'])
        own, selected = partition(found, DOMAIN, assignment['shard'], shards)
        if assignment['inventory'] != own or assignment['methods'] != selected or suite != DOMAIN + '-' + str(assignment['shard']):
            problems.append(suite + ': shard assignment drift')
        if inventory is None:
            inventory, cases = own, listed
        elif own != inventory or sorted(listed) != sorted(cases):
            problems.append(suite + ': discovery differs between shards')
        for method in selected:
            rows = [r for r in report['problems'] if r.get('id') == method]
            if len(rows) != 1:
                problems.append(suite + ': missing or duplicate outcome ' + method)
                continue
            row = rows[0]
            methods[method] = row
            if row['status'] != 'PASS':
                problems.append(suite + ': ' + row['status'] + ' ' + method)
            elif row.get('cases') != assignment['expectedCases'][method]:
                problems.append(suite + ': incomplete theory cases ' + method)
    summary = dict(domain=DOMAIN, shards=shards, reports=len(expected & by_suite.keys()))
    if inventory is not None:
        found = sorted({c.split('(', 1)[0] for c in cases})
        excluded = exclusions(found, DOMAIN)
        excluded_cases = case_exclusions(cases, DOMAIN)
        own_cases = [c for c in cases if c.split('(', 1)[0] in inventory or c.split('(', 1)[0] in excluded]
        of_excluded_methods = [c for c in own_cases if c.split('(', 1)[0] in excluded]
        executed = [c for c in own_cases if c.split('(', 1)[0] in methods and c not in excluded_cases]
        if sorted(methods) != sorted(inventory):
            problems.append('shard union does not cover the inventory exactly')
        if len(own_cases) != len(executed) + len(of_excluded_methods) + len(excluded_cases):
            problems.append('case accounting mismatch')
        summary.update(methods=len(inventory) + len(excluded), methodsRun=len(methods), methodsExcluded=len(excluded),
                       cases=len(own_cases), casesRun=len(executed), casesOfExcludedMethods=len(of_excluded_methods),
                       casesExcluded=len(excluded_cases),
                       failed=sorted(m for m, r in methods.items() if r['status'] != 'PASS'),
                       exclusions={**{m: list(v) for m, v in excluded.items()}, **{c: list(v) for c, v in excluded_cases.items()}})
    summary['problems'] = problems
    summary['status'] = 'PASS' if not problems else 'FAIL'
    return summary


def main():
    root = Path(sys.argv[1] if len(sys.argv) > 1 else '.diagnostics/downloaded')
    reports = []
    for path in sorted(root.rglob('*diagnostic.json')):
        try:
            reports.append(json.loads(path.read_text()))
        except ValueError:
            reports.append(dict(suite='unreadable:' + path.name, problems=[]))
    summary = evaluate(reports)
    out = Path('.diagnostics')
    out.mkdir(exist_ok=True)
    (out / 'INTEGRATION_FULL_REPORT.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
    lines = ['# Integration full report', '', 'Status: **' + summary['status'] + '**', '']
    for key in ('reports', 'methods', 'methodsRun', 'methodsExcluded', 'cases', 'casesRun', 'casesOfExcludedMethods', 'casesExcluded'):
        if key in summary:
            lines.append('- ' + key + ': ' + str(summary[key]))
    lines += ['', '## Problems', ''] + ['- ' + p for p in summary['problems']] + ['', '## Exclusions', '']
    lines += ['- ' + k + ': ' + ' - '.join(v) for k, v in summary.get('exclusions', {}).items()]
    (out / 'INTEGRATION_FULL_REPORT.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print('\n'.join(lines))
    return 0 if summary['status'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())
