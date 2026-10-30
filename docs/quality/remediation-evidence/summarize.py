"""Read-only discovery/TRX multiset reconciliation; never synthesizes test results."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import xml.etree.ElementTree as ET

EVIDENCE = Path(__file__).resolve().parent
ROOT = EVIDENCE.parents[2]
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}

def read(path):
    data = path.read_bytes()
    return data.decode('utf-16' if data.startswith(b'\xff\xfe') else 'utf-8-sig')

def normalized(name):
    decoded = re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m[1], 16)), name)
    return decoded.encode('utf-16-le', 'surrogatepass').decode('utf-16-le', 'surrogatepass')

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def results(path):
    root = ET.parse(path).getroot()
    entries = root.findall('.//t:UnitTestResult', NS)
    return root, entries, Counter(normalized(e.attrib['testName']) for e in entries)

instance = json.loads(read(EVIDENCE/'instance.json'))
baseline = json.loads(read(ROOT/'docs/refactoring/validation/current-cutover/final/test-summary.json'))
records = []
for suite in ['Unit', 'Integration']:
    path = EVIDENCE/'Unit-final/result.trx' if suite == 'Unit' else Path(instance['Run'])/'final-complete/result.trx'
    root, entries, actual = results(path)
    counters = root.find('t:ResultSummary/t:Counters', NS).attrib
    discovery = Counter(normalized(line.strip()) for line in read(EVIDENCE/(suite.lower()+'-discovery.log')).splitlines() if line.startswith('    ElectronicRegisterAPI.'))
    previous = Path(next(s['path'] for s in baseline['suites'] if s['suite'] == suite))
    _, _, old = results(previous)
    removed, added = old-actual, actual-old
    valid = discovery == actual and not removed and len(entries) == sum(discovery.values()) and all(e.attrib['outcome'] == 'Passed' for e in entries) and int(counters['passed']) == len(entries) and int(counters['failed']) == 0 and int(counters['notExecuted']) == 0
    records.append(dict(suite=suite, path=str(path), sha256=sha(path), discovered=sum(discovery.values()), results=len(entries), counters=counters, baselinePath=str(previous), baselineSha256=sha(previous), baselineResults=sum(old.values()), removedOrRenamed=dict(removed), added=dict(added), discoveryOnly=dict(discovery-actual), trxOnly=dict(actual-discovery), valid=valid))
    if suite == 'Integration':
        observations = []
        for output in root.findall('.//t:StdOut', NS):
            for line in (output.text or '').splitlines():
                if line.startswith('{"Shape":') and 'FanoutRows' in line:
                    observations.append(json.loads(line))
        (EVIDENCE/'exam-fanout-observation.json').write_text(json.dumps(observations, indent=2)+'\n', encoding='utf-8')

summary = dict(suites=records, noUnexpectedFailuresOrSkips=all(s['valid'] for s in records), mysql=dict(version='5.7.24', port=instance['Port'], directory=instance['Run'], newTaskCreatedInstance=True), tlsReleaseValidation='OPEN: local build rejects TLS 1.2; no client weakening', dockerRuntime='NOT RUN: engine unavailable')
(EVIDENCE/'test-summary-final.json').write_text(json.dumps(summary, indent=2, ensure_ascii=True)+'\n', encoding='utf-8')
for suite in records:
    print(f"{suite['suite']}: {suite['results']} Passed; discovery multiset valid={suite['valid']}; baseline removed={sum(suite['removedOrRenamed'].values())}; added={sum(suite['added'].values())}")
if not summary['noUnexpectedFailuresOrSkips']:
    raise SystemExit('Missing, changed, failed or skipped test evidence')
