import unittest
import tempfile
from pathlib import Path
from integration_partition import SHARDS, WINDOWS_METHODS, owner, partition


class PartitionTests(unittest.TestCase):
    def test_all_193_methods_assigned_once_with_balanced_shards(self):
        methods = ['ElectronicRegisterAPI.Tests.Integration.InstitutionClassCrudTests.Test'+str(n) for n in range(193)]
        shards = [partition(methods, 'tenants', i, 6)[1] for i in range(6)]
        assigned = [m for shard in shards for m in shard]
        self.assertEqual(sorted(assigned), sorted(methods))
        self.assertEqual(len(assigned), len(set(assigned)))
        self.assertLessEqual(max(map(len, shards))-min(map(len, shards)), 1)
        self.assertEqual(shards[0], partition(list(reversed(methods)), 'tenants', 0, 6)[1])

    def test_domain_duplicates_have_one_owner_and_windows_remains_required(self):
        for name, domain in [('Calendar_Concurrent','calendar'), ('EmailPolicy_Rollback','email'), ('BulkImport_Fault','imports')]:
            self.assertEqual(owner('ElectronicRegisterAPI.Tests.Integration.InstitutionClassCrudTests.'+name), domain)
        for name in WINDOWS_METHODS:
            self.assertEqual(owner('ElectronicRegisterAPI.Tests.Integration.Mt5cOnboardingTests.'+name), 'windows-cli')

    def test_invalid_and_empty_partitions_fail_closed(self):
        for index, count in [(6, 6), (-1, 6), (0, 7)]:
            with self.assertRaises(ValueError): partition([], 'tenants', index, count)
        with self.assertRaises(ValueError): partition([], 'iam', 0, 1)

    def test_partial_theory_cannot_certify_method(self):
        from ci_diagnostic import batch_methods
        method = 'ElectronicRegisterAPI.Tests.Integration.Fixture.Theory'
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'current.trx'
            for observed, expected in [(1, 'FAIL'), (2, 'PASS')]:
                cases = ''.join('<UnitTestResult testId="one" outcome="Passed" />' for _ in range(observed))
                path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                    '<TestDefinitions><UnitTest id="one"><TestMethod className="ElectronicRegisterAPI.Tests.Integration.Fixture" name="Theory" /></UnitTest></TestDefinitions>'
                    '<Results>'+cases+'</Results><ResultSummary outcome="Completed"><Counters total="'+str(observed)+'" executed="'+str(observed)+'" /></ResultSummary></TestRun>')
                rows = batch_methods(dict(id='invocation', members=[method], trx=str(path), expectedCases={method: 2}),
                                     dict(status='PASS', error=''), directory)
                self.assertEqual(expected, rows[0]['status'])

    def test_report_distinguishes_duplicates_blocked_and_history(self):
        from final_ci_diagnostic import summarize
        method = 'ElectronicRegisterAPI.Tests.Integration.Fixture.Test'
        rows = [dict(id='one', test=method, status='FAIL', error='original stack INNODB_LOCK_WAITS'),
                dict(id='two', test=method, status='FAIL', error='original stack INNODB_LOCK_WAITS'),
                dict(id='blocked', test=method+'2', status='BLOCKED', error='budget'),
                dict(id='old', status='FAIL', required=False, error='historical')]
        result = summarize(rows)
        self.assertEqual(2, result['methods'][0]['occurrences'])
        self.assertEqual(['one','two'], result['categories']['lock-observer'])
        self.assertEqual(['blocked'], result['categories']['not-executed'])
        self.assertEqual(['old'], result['categories']['historical-non-certifiable'])
        self.assertEqual('original stack INNODB_LOCK_WAITS', rows[0]['error'])


if __name__ == '__main__': unittest.main()
