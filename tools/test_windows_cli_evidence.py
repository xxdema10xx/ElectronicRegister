"""Adversarial parser/aggregation fixtures; these do not certify Windows execution."""
import copy
import hashlib
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from windows_cli_evidence import CHECKS, METHODS, PACKAGE, validate_report
import final_ci_diagnostic as final


class WindowsEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.directory = self.base/'.diagnostics/downloaded/windows'
        self.directory.mkdir(parents=True)
        self.report = dict(suite='windows-cli', package=PACKAGE, problems=[dict(id=m, status='PASS', required=True) for m in CHECKS],
                           provenance=dict(platform='win32', **{k: os.environ.get(k, '') for k in ('GITHUB_SHA','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT')}),
                           identity=dict(version='5.7.24', bind='127.0.0.1', port=24567, uuid='synthetic-parser-fixture', pid=42),
                           acl=[dict(owner='S-1-5-21-1-2-3-1001', trusted=['S-1-5-21-1-2-3-1001','S-1-5-18','S-1-5-32-544'], rules=[dict(sid='S-1-5-18')]) for _ in range(4)], trx=[],
                           aclNegative=[dict(stage=stage, owner='S-1-5-21-1-2-3-1001', sddl='parser-fixture', rules=[dict(sid='S-1-1-0', allow=True, rights=1)] if i == 1 else [])
                                        for i, stage in enumerate(('private-before','broad-refused-without-mutation','private-restored-and-accepted'))])
        for i, method in enumerate(METHODS):
            # Metadata fixture only; real NTFS behavior is tested separately.
            cls, name = method.rsplit('.', 1)
            path = self.directory/(str(i)+'.trx')
            path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                '<TestDefinitions><UnitTest id="one"><TestMethod className="'+cls+'" name="'+name+'" /></UnitTest></TestDefinitions>'
                '<Results><UnitTestResult testId="one" outcome="Passed" /></Results>'
                '<ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1" /></ResultSummary></TestRun>')
            self.report['trx'].append(dict(file=path.name, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
        for index, entry in enumerate(self.report['acl']):
            entry['protected'] = index == 0
            entry['rules'] = [dict(sid=sid, type='Allow', rights='FullControl', inherited=index != 0,
                                   inheritance='ContainerInherit, ObjectInherit' if index == 0 else 'None',
                                   propagation='None') for sid in entry['trusted']]

    def validate(self, report=None):
        validate_report(self.report if report is None else report, self.directory)

    def aggregate(self, job='success', report=True, duplicate=False):
        if report:
            text = json.dumps(self.report)
            (self.directory/'windows-cli-diagnostic.json').write_text(text)
            if duplicate:
                (self.directory/'duplicate-diagnostic.json').write_text(text)
        jobs = {n:dict(result='success') for n in ('release','diagnostic','integration')}
        if job is not None:
            jobs['windows-cli'] = dict(result=job)
        previous = Path.cwd()
        try:
            os.chdir(self.base)
            with patch.dict(os.environ, JOB_RESULTS=json.dumps(jobs)), patch.object(final, 'EXPECTED', {'windows-cli':CHECKS}), patch.object(final, 'SHARDS', {}):
                return final.main()
        finally:
            os.chdir(previous)

    def test_complete_raw_evidence_is_accepted(self):
        self.validate()
        self.assertEqual(0, self.aggregate())

    def test_primary_provision_failure_survives_missing_checks(self):
        self.report['problems'] = [dict(id='provision', required=True, status='FAIL',
                                       error='Private ACL setup/verification failed: Set-Acl original error')]
        with self.assertRaisesRegex(ValueError, 'Primary Windows failure.*provision.*Set-Acl original error'):
            self.validate()
        self.assertEqual(1, self.aggregate(job='failure'))
        summary = json.loads((self.base/'.diagnostics/MYSQL84_CI_DIAGNOSTIC_REPORT.json').read_text())
        self.assertTrue(any('Primary Windows failure [provision]' in r.get('error', '')
                            for r in summary['problems']))

    def test_blocked_checks_never_certify_execution(self):
        for row in self.report['problems']:
            if row['id'] in METHODS:
                row['status'] = 'BLOCKED'
        with self.assertRaisesRegex(ValueError, 'Every Windows check must PASS'):
            self.validate()

    def test_absent_skipped_failed_job_blocks(self):
        for job in (None, 'skipped', 'failure', 'cancelled'):
            with self.subTest(job=job):
                self.assertEqual(1, self.aggregate(job=job))

    def test_absent_report_blocks(self):
        self.assertEqual(1, self.aggregate(report=False))

    def test_incomplete_report_failed_skipped_optional_and_duplicate_rows_block(self):
        variants = []
        r = copy.deepcopy(self.report); r['problems'].pop(); variants.append(r)
        r = copy.deepcopy(self.report); r['problems'].append(r['problems'][0]); variants.append(r)
        for status in ('FAIL','SKIPPED','BLOCKED'):
            r = copy.deepcopy(self.report); r['problems'][0].update(status=status, justification='must not excuse'); variants.append(r)
        r = copy.deepcopy(self.report); r['problems'][0]['required'] = False; variants.append(r)
        for r in variants:
            with self.assertRaises(ValueError): self.validate(r)

    def test_failed_skipped_and_duplicate_raw_cases_block_even_when_report_says_pass(self):
        path = self.directory/self.report['trx'][0]['file']
        original = path.read_text()
        variants = [original.replace('outcome="Passed"', 'outcome="'+s+'"') for s in ('Failed','NotExecuted')]
        variants.append(original.replace('</Results>', '<UnitTestResult testId="one" outcome="Passed" /></Results>').replace('total="1" executed="1" passed="1"', 'total="2" executed="2" passed="2"'))
        for value in variants:
            path.write_text(value)
            self.report['trx'][0]['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
            with self.assertRaises(ValueError): self.validate()

    def test_missing_raw_trx_and_digest_tamper_block(self):
        path = self.directory/self.report['trx'][0]['file']
        path.write_text('tampered')
        with self.assertRaises(ValueError): self.validate()
        path.unlink()
        with self.assertRaises(FileNotFoundError): self.validate()

    def test_duplicate_evidence_and_duplicate_reports_block(self):
        r = copy.deepcopy(self.report); r['trx'][1] = r['trx'][0]
        with self.assertRaises(ValueError): self.validate(r)
        self.assertEqual(1, self.aggregate(duplicate=True))

    def test_broad_authority_acl_and_non_windows_block(self):
        r = copy.deepcopy(self.report); r['acl'][0]['rules'].append(dict(sid='S-1-1-0'))
        with self.assertRaises(ValueError): self.validate(r)
        r = copy.deepcopy(self.report); r['acl'][0]['trusted'].append('S-1-1-0')
        with self.assertRaises(ValueError): self.validate(r)
        r = copy.deepcopy(self.report); r['aclNegative'][1]['rules'] = []
        with self.assertRaises(ValueError): self.validate(r)
        r = copy.deepcopy(self.report); r['aclNegative'][2]['sddl'] = 'restoration-drift'
        with self.assertRaises(ValueError): self.validate(r)
        self.report['provenance']['platform'] = 'linux'
        with self.assertRaises(ValueError): self.validate()

    def test_owner_protection_rights_and_inheritance_drift_block(self):
        for index in range(4):
            mutations = [('protected', not self.report['acl'][index]['protected'])]
            for field, value in mutations:
                r = copy.deepcopy(self.report); r['acl'][index][field] = value
                with self.assertRaises(ValueError): self.validate(r)
            for field, value in [('type', 'Deny'), ('rights', 'Read'), ('inherited', index == 0),
                                 ('inheritance', 'ObjectInherit'), ('propagation', 'InheritOnly')]:
                r = copy.deepcopy(self.report); r['acl'][index]['rules'][0][field] = value
                with self.assertRaises(ValueError): self.validate(r)
            r = copy.deepcopy(self.report); r['acl'][index]['rules'].pop()
            with self.assertRaises(ValueError): self.validate(r)

    def test_wrong_run_provenance_and_traversal_block(self):
        with patch.dict(os.environ, GITHUB_RUN_ID='another-run'):
            with self.assertRaises(ValueError): self.validate()
        self.report['trx'][0]['file'] = '../0.trx'
        with self.assertRaises(ValueError): self.validate()


if __name__ == '__main__': unittest.main()
