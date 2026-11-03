"""Fail-closed Windows CLI certification, independently checked against raw TRX."""
import hashlib
import json
import os
import re
from pathlib import Path
import xml.etree.ElementTree as ET
from integration_partition import WINDOWS_METHODS
from ci_diagnostic import validate_trx

METHODS = sorted('ElectronicRegisterAPI.Tests.Integration.Mt5cOnboardingTests.'+m for m in WINDOWS_METHODS)
CHECKS = {'platform', 'restore', 'build', 'provision', 'acl', 'cleanup', *METHODS}
PACKAGE = json.loads(Path(__file__).with_name('windows-cli-mysql.json').read_text())


def require(value, message):
    if not value:
        raise ValueError(message)


def trx_methods(path):
    validate_trx(path)
    root = ET.parse(path).getroot()
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    definitions = {}
    for test in root.findall('t:TestDefinitions/t:UnitTest', ns):
        method = test.find('t:TestMethod', ns)
        require(method is not None and test.get('id') not in definitions, 'Duplicate/unmapped definition')
        definitions[test.get('id')] = method.get('className', '').split(',')[0]+'.'+method.get('name', '')
    results = root.findall('t:Results/t:UnitTestResult', ns)
    names = [definitions.get(r.get('testId')) for r in results]
    require(len(names) == 1 and names[0] in METHODS, 'Exactly one mandatory Windows fact required per TRX')
    return names[0]


def validate_report(report, directory):
    require(report.get('suite') == 'windows-cli', 'Windows suite required')
    rows = report['problems']
    failures = [r for r in rows if r.get('status') == 'FAIL']
    if failures:
        first = failures[0]
        raise ValueError('Primary Windows failure ['+str(first.get('id'))+']: '+str(first.get('error', 'No detail recorded')))
    require(len(rows) == len(CHECKS) and {r['id'] for r in rows} == CHECKS, 'Missing or duplicate Windows checks')
    require(all(r.get('required') is True and r.get('status') == 'PASS' for r in rows), 'Every Windows check must PASS; skips refused')
    provenance = report['provenance']
    require(provenance['platform'] == 'win32', 'Real Windows required')
    for key in ('GITHUB_SHA', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT'):
        if os.environ.get(key):
            require(provenance.get(key) == os.environ[key], 'Windows provenance mismatch: '+key)
    identity = report['identity']
    require(report['package'] == PACKAGE, 'Unpinned native binaries')
    require(identity['version'] == '5.7.24' and identity['bind'] == '127.0.0.1' and
            identity['port'] not in (3306, 3307, 13306) and identity['uuid'] and identity['pid'] > 0,
            'Native isolated identity missing')
    acl = report['acl']
    require(len(acl) == 4 and all(re.fullmatch(r'S-1-5-21-(?:\d+-){3}\d+', a['owner']) and
            set(a['trusted']) == {a['owner'], 'S-1-5-18', 'S-1-5-32-544'} and a['rules'] and
            all(r['sid'] in a['trusted'] for r in a['rules']) for a in acl), 'Untrusted authority ACL')
    negative = report['aclNegative']
    require([a['stage'] for a in negative] == ['private-before', 'broad-refused-without-mutation', 'private-restored-and-accepted'], 'ACL negative control evidence missing/duplicated')
    require(negative[0]['sddl'] == negative[2]['sddl'], 'Authority ACL was not restored exactly')
    for index, entry in enumerate(negative):
        broad = any(r['sid'] == 'S-1-1-0' and r['allow'] and r['rights'] & 1 for r in entry['rules'])
        require(broad == (index == 1), 'ACL negative control did not change Everyone ReadData')
        require(entry['owner'] == acl[0]['owner'] and entry['sddl'], 'ACL owner continuity missing')
    evidence = report['trx']
    require(len(evidence) == len(METHODS), 'Missing or duplicated TRX evidence')
    observed = []
    files = []
    for item in evidence:
        name = item['file']
        require(Path(name).name == name and '/' not in name and '\\' not in name, 'TRX must be an artifact sibling')
        path = Path(directory)/name
        require(hashlib.sha256(path.read_bytes()).hexdigest() == item['sha256'], 'TRX digest mismatch')
        observed.append(trx_methods(path))
        files.append(name)
    require(sorted(observed) == METHODS and len(set(files)) == len(files), 'Missing or duplicated methods')
