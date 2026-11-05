import unittest
import tempfile
from pathlib import Path
from integration_partition import SHARDS, WINDOWS_METHODS, EXCLUDED_METHODS, exclusions, owner, partition

PREFIX = 'ElectronicRegisterAPI.Tests.Integration.'


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

    def test_reviewed_exclusions_are_exactly_two_inc012_methods(self):
        # Changing the exclusion list requires changing this test in the same review.
        self.assertEqual(EXCLUDED_METHODS, {
            PREFIX+'Mt5bSecurityStateTests.Sql_FixtureGuardExcludesRealLegacyHolding':
                ('INC-012', 'Observes the real legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
            PREFIX+'Mt5dMembershipMutationTests.OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData':
                ('INC-012', 'Validates the offline ownership graph of the legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
        })

    def test_iam_security_owns_three_classes_and_capability_scope_with_exclusions_reported(self):
        self.assertEqual(4, SHARDS['iam-security'])
        classes = ['Mt5bJwtTests', 'Mt5bSecurityStateTests', 'Mt5dMembershipMutationTests', 'Mysql84CapabilityScopeTests']
        methods = [PREFIX+c+'.Case'+str(n) for c in classes for n in range(5)] + list(EXCLUDED_METHODS)
        for method in methods: self.assertEqual('iam-security', owner(method))
        shards = [partition(methods, 'iam-security', i, 4)[1] for i in range(4)]
        assigned = [m for shard in shards for m in shard]
        self.assertEqual(sorted(assigned), sorted(set(methods) - set(EXCLUDED_METHODS)))
        self.assertEqual(set(EXCLUDED_METHODS), set(exclusions(methods, 'iam-security')))
        self.assertEqual({}, exclusions(methods, 'iam'))
        with self.assertRaises(ValueError): exclusions(methods[:-1], 'iam-security')
        with self.assertRaises(ValueError): partition(methods, 'iam-security', 0, 3)

    def test_iam_security_uses_only_the_identity_fixture_family(self):
        from ci_diagnostic import integration_plan
        methods = [PREFIX+'Mt5bJwtTests.A', PREFIX+'Mysql84CapabilityScopeTests.B']
        with tempfile.TemporaryDirectory() as directory:
            checks = integration_plan(methods, directory, 'iam-security-0')
        self.assertEqual(['preflight-identity'], [c['id'] for c in checks if c['id'].startswith('preflight-')])
        self.assertTrue(all(c.get('needs') == ['preflight-identity'] for c in checks if c['id'] in methods))

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
                dict(id='old', status='FAIL', required=False, error='historical'),
                dict(id='excluded', test=method+'3', status='SKIPPED', justification='INC-012: reason', error='reason')]
        result = summarize(rows)
        self.assertEqual(2, result['methods'][0]['occurrences'])
        self.assertEqual(['one','two'], result['categories']['lock-observer'])
        self.assertEqual(['blocked'], result['categories']['not-executed'])
        self.assertEqual(['old'], result['categories']['historical-non-certifiable'])
        self.assertEqual(['excluded'], result['categories']['reviewed-exclusion'])
        self.assertEqual('original stack INNODB_LOCK_WAITS', rows[0]['error'])


if __name__ == '__main__': unittest.main()
