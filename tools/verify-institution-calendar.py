"""Offline evidence checks only. Does not execute HTTP, SQL, migrations or races."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET
from calendar_schema_contract import phase4_contract,TABLES

ROOT = Path(__file__).resolve().parents[1]
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def local(root, name):
    path = (root / name).resolve(strict=True)
    require(root.resolve() in path.parents, 'Artifact outside repository: ' + name)
    return path


def trx(path):
    tree = ET.parse(path).getroot()
    summary = tree.find('t:ResultSummary', NS)
    require(summary is not None and summary.attrib.get('outcome') == 'Completed', 'TRX run did not complete successfully')
    counters = tree.find('t:ResultSummary/t:Counters', NS)
    require(counters is not None, 'TRX counters missing')
    definitions = {d.attrib['id']: d.find('t:TestMethod', NS).attrib['className'].split(',')[0]
                   for d in tree.findall('t:TestDefinitions/t:UnitTest', NS)}
    results = tree.findall('t:Results/t:UnitTestResult', NS)
    require(len(results) > 0, 'Empty TRX')
    require(all(r.attrib['outcome'] == 'Passed' for r in results), 'Non-passing TRX results')
    identities = {definitions[r.attrib['testId']] + '::' + r.attrib['testName'] for r in results}
    require(len(identities) == len(results), 'Duplicate test identity in TRX')
    require(all(int(counters.attrib[k]) == len(results) for k in ('total', 'executed', 'passed')),
            'TRX counters differ from results')
    return identities


def verify(root, verification):
    report = json.loads(local(root, verification).read_text(encoding='utf-8'))
    if report['phaseCompleted']:
        require(report['status']=='IMPLEMENTED_AND_VERIFIED' and not report['remaining'],'Inconsistent completion claim')
        require(all(report['implemented'].values()),'Completion claims missing components')
        require(report['database']['independentBootstrapEqualsMigration'],'Independent bootstrap comparison missing')
        require(len(report['acceptance'])==32 and all(c['satisfied'] and c['evidence'] for c in report['acceptance']),'Acceptance evidence incomplete')
    else:
        require(report['status'] in ('INCOMPLETE','IMPLEMENTED_PENDING_VERIFICATION') and report['remaining'],'Missing incomplete work inventory')
    for artifact in report['artifacts']:
        require(sha(local(root, artifact['path'])) == artifact['sha256'], 'Artifact digest mismatch: ' + artifact['path'])
    runs = {}
    for run in report['tests']['runs']:
        path = local(root, run['path'])
        require(sha(path) == run['sha256'], 'TRX digest mismatch')
        identities = trx(path)
        require(len(identities) == run['passed'], 'TRX passed count mismatch')
        runs[run['name']] = identities
        if 'recordedIdentitySets' in report['tests']:
            require(set(report['tests']['recordedIdentitySets'][run['name']])==identities,'Recorded identity set mismatch')
    final = runs['unit'] | runs['integration']
    baseline = runs['baselineUnit'] | runs['baselineIntegration']
    require((baseline <= final) == report['tests']['fullBaselinePreserved'], 'Baseline preservation claim mismatch')
    require(len(baseline - final) == report['tests']['baselineMissing'], 'Missing baseline identity count mismatch')
    require(len(baseline) == report['tests']['baselineDistinct'], 'Baseline identity count mismatch')
    require(len(final) == report['tests']['currentDistinct'], 'Current identity count mismatch')
    require(len(final - baseline) == report['tests']['newDistinct'], 'New identity count mismatch')
    contract=json.loads(local(root,'migrations/bootstrap/schema-contract.json').read_text())
    phase4_contract(root,contract)
    migration=local(root,'migrations/institution-calendar/001_expand.sql').read_text(encoding='utf-8')
    bootstrap=local(root,'migrations/bootstrap/001_schema.sql').read_text(encoding='utf-8')
    require(bootstrap.endswith(migration),'Bootstrap missing exact calendar migration suffix')
    require(set(re.findall(r'CREATE TABLE (\w+)',migration))==TABLES,'Calendar migration table set mismatch')
    require(migration.count('CREATE TRIGGER ')==21 and migration.count('FOREIGN KEY(')==22,'Calendar migration inventory mismatch')
    require('DROP ' not in migration and 'CHECK (' not in migration,'Unsupported or destructive calendar DDL')
    return {'artifacts': len(report['artifacts']), 'baselineDistinct': len(baseline),
            'currentDistinct': len(final), 'newDistinct': len(final - baseline)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verification', default='tests/Integration/INSTITUTION_CALENDAR_VERIFICATION.json')
    args = parser.parse_args()
    result = verify(ROOT, args.verification)
    print(json.dumps(result, sort_keys=True))
    print('Offline hashes, additive schema contract and recorded TRX identities verified. No SQL/HTTP executed by this script.')


if __name__ == '__main__':
    main()
