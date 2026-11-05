import unittest
import tempfile
from pathlib import Path
from integration_partition import (SHARDS, EXTENDED_SHARDS, FULL_CLASSES, WINDOWS_METHODS, EXCLUDED_METHODS, EXCLUDED_CASES,
                                   case_exclusions, case_filters, exclusions, owner, partition)

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

    def test_reviewed_exclusions_are_exactly_the_locked_methods(self):
        # Changing the exclusion list requires changing this test in the same review.
        self.assertEqual(EXCLUDED_METHODS, {
            PREFIX+'Mt5bSecurityStateTests.Sql_FixtureGuardExcludesRealLegacyHolding':
                ('INC-012', 'Observes the real legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
            PREFIX+'Mt5dMembershipMutationTests.OfflineGraphClassifiesValidatedIamLedgerWithoutChangingData':
                ('INC-012', 'Validates the offline ownership graph of the legacy holding MT3A_LEGACY_TEST, absent from the fresh MySQL 8.4 bootstrap; decision in Lotto 6'),
            PREFIX+'Mt1IntegrityTests.CanonicalExpandIsRerunnableAndDoesNotChangeLegacyData':
                ('INC-003', 'Expects MySQL 5.7 delete_rule RESTRICT; MySQL 8.4 reports NO ACTION for the same composite FKs'),
            PREFIX+'Mt4aCourseStructureTests.InformationSchemaHasCompositeRestrictConstraints':
                ('INC-003', 'Expects MySQL 5.7 delete_rule RESTRICT; MySQL 8.4 reports NO ACTION for the same composite FKs'),
            PREFIX+'Phase4_ExamLifecycleTests.Audit_IdentityProfileFaultIsAtomicAndCannotChangeDidacticProfile':
                ('INC-012', 'Legacy EF insert without explicit owner relies on the MT3A legacy holding, absent from the fresh MySQL 8.4 bootstrap'),
            PREFIX+'HistoricalExpiryBoundaryTests.HistoricalOwnerRemainsTechnicalOnlyAndDeliveryIsIdempotent':
                ('INC-012', 'Reads mt3a_legacy_scope of the legacy holding, absent from the fresh MySQL 8.4 bootstrap'),
            PREFIX+'Mt4aCourseStructureTests.ExistingBackfillIsIdempotentWithAttributedMt4Graph':
                ('INC-012', 'Requires the legacy holding; with one database per method it is always absent'),
            PREFIX+'Mt4aCourseStructureTests.LegacyEfWritersStillReceiveTriggerDerivedOwners':
                ('INC-012', 'Legacy EF writers need the MT3A legacy holding; with one database per method it is always absent'),
            PREFIX+'Mt3aOwnershipTests.AmbiguousLegacyAuditAbortsBackfillAndLeavesEveryRealCellUntouched':
                ('INC-012', 'Legacy backfill tooling needs a local MySQL 8.0 mysqldump.exe (Windows), absent on the Linux runner'),
            PREFIX+'Mysql84ReleaseTests.PublishedReleaseAndEmbeddedOnboardingValidationRunOnExactServer':
                ('INC-017', 'Runs in the release (upgrade) job of the main workflow with ER_MYSQL84_RELEASE_CONFIG on the released server'),
        })

    def test_reviewed_case_exclusions_are_exactly_the_locked_cases(self):
        method = PREFIX+'Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock'
        reason = ('INC-012', 'The holding-active state needs mt3a_legacy_scope of the legacy holding, absent from the fresh MySQL 8.4 bootstrap')
        self.assertEqual(EXCLUDED_CASES, {
            method+'(state: "holding-active", workerFirst: True)': reason,
            method+'(state: "holding-active", workerFirst: False)': reason,
        })

    def test_case_filters_select_exactly_the_excluded_cases_or_fail_closed(self):
        method = PREFIX+'Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock'
        cases = [method+'(state: "'+state+'", workerFirst: '+first+')' for state in ('archived', 'suspended', 'holding-active') for first in ('True', 'False')]
        self.assertEqual(['DisplayName!~holding-active'], case_filters(cases, set(EXCLUDED_CASES)))
        self.assertEqual(set(EXCLUDED_CASES), set(case_exclusions(cases, 'full')))
        with self.assertRaises(ValueError): case_filters(cases, {cases[0]})
        with self.assertRaises(ValueError): case_exclusions(cases[:4], 'full')

    def test_full_suite_is_separate_and_owns_only_its_explicit_classes(self):
        self.assertEqual({'full': 14}, EXTENDED_SHARDS)
        self.assertNotIn('full', SHARDS)  # main workflow and final gate unchanged
        self.assertEqual(51, len(FULL_CLASSES))
        methods = [PREFIX+c[1:]+'.Case'+str(n) for c in FULL_CLASSES for n in range(2)]
        for method in methods: self.assertEqual('full', owner(method))
        shards = [partition(methods, 'full', i, 14)[1] for i in range(14)]
        self.assertEqual(sorted(m for shard in shards for m in shard), sorted(methods))
        with self.assertRaises(ValueError): owner(PREFIX+'BrandNewUnassignedTests.Case')
        with self.assertRaises(ValueError): partition(methods, 'full', 0, 13)

    def test_iam_security_owns_three_classes_and_capability_scope_with_exclusions_reported(self):
        self.assertEqual(4, SHARDS['iam-security'])
        classes = ['Mt5bJwtTests', 'Mt5bSecurityStateTests', 'Mt5dMembershipMutationTests', 'Mysql84CapabilityScopeTests']
        excluded = {m for m in EXCLUDED_METHODS if m.rsplit('.', 2)[-2] in classes}
        self.assertEqual(2, len(excluded))
        methods = [PREFIX+c+'.Case'+str(n) for c in classes for n in range(5)] + sorted(excluded)
        for method in methods: self.assertEqual('iam-security', owner(method))
        shards = [partition(methods, 'iam-security', i, 4)[1] for i in range(4)]
        assigned = [m for shard in shards for m in shard]
        self.assertEqual(sorted(assigned), sorted(set(methods) - excluded))
        self.assertEqual(excluded, set(exclusions(methods, 'iam-security')))
        self.assertEqual({}, exclusions(methods, 'iam'))
        with self.assertRaises(ValueError): exclusions(methods[:-1], 'iam-security')
        with self.assertRaises(ValueError): partition(methods, 'iam-security', 0, 3)

    def test_iam_owns_identity_account_and_provisioning_classes_in_two_shards(self):
        self.assertEqual(2, SHARDS['iam'])
        classes = ['Mt5eIamHttpTests', 'IdentityAccountTests', 'OwnerProvisioningTests', 'InstitutionAccountProvisioningTests']
        methods = [PREFIX+c+'.Case'+str(n) for c in classes for n in range(5)]
        for method in methods: self.assertEqual('iam', owner(method))
        shards = [partition(methods, 'iam', i, 2)[1] for i in range(2)]
        self.assertEqual(sorted(m for s in shards for m in s), sorted(methods))
        with self.assertRaises(ValueError): partition(methods, 'iam', 0, 1)

    def test_iam_security_uses_only_the_identity_fixture_family(self):
        from ci_diagnostic import integration_plan
        methods = [PREFIX+'Mt5bJwtTests.A', PREFIX+'Mysql84CapabilityScopeTests.B']
        with tempfile.TemporaryDirectory() as directory:
            checks = integration_plan(methods, directory, 'iam-security-0')
        self.assertEqual(['preflight-identity'], [c['id'] for c in checks if c['id'].startswith('preflight-')])
        self.assertTrue(all(c.get('needs') == ['preflight-identity'] for c in checks if c['id'] in methods))

    def test_full_gate_requires_every_shard_pass_and_exact_case_accounting(self):
        from integration_full_gate import evaluate
        dispatch = PREFIX+'Mt3dHttpTests.Release_WorkerDispatchRevalidatesTenantStateUnderLock'
        cases = [PREFIX+c[1:]+'.Case0' for c in FULL_CLASSES] + [m+'(value: 1)' for m in EXCLUDED_METHODS if owner(m) == 'full']
        cases += [dispatch+'(state: "'+state+'", workerFirst: '+first+')' for state in ('archived', 'holding-active') for first in ('True', 'False')]
        listing = chr(10).join('    '+c for c in cases)
        methods = sorted({c.split('(', 1)[0] for c in cases})
        def reports(status='PASS', drop=None, short=None):
            out = []
            for shard in range(14):
                if shard == drop: continue
                inventory, selected = partition(methods, 'full', shard, 14)
                expected = {m: sum(c.split('(', 1)[0] == m and c not in EXCLUDED_CASES for c in cases) for m in selected}
                rows = [dict(id='discovery', status='PASS', error=listing)]
                rows += [dict(id=m, status=status, cases=expected[m] - (1 if m == short else 0)) for m in selected]
                out.append(dict(suite='full-'+str(shard), problems=rows, assignment=dict(shard=shard, inventory=inventory, methods=selected, expectedCases=expected)))
            return out
        ok = evaluate(reports())
        self.assertEqual('PASS', ok['status'], ok['problems'])
        self.assertEqual(ok['cases'], ok['casesRun'] + ok['casesOfExcludedMethods'] + ok['casesExcluded'])
        self.assertEqual((2, len([m for m in EXCLUDED_METHODS if owner(m) == 'full'])), (ok['casesExcluded'], ok['methodsExcluded']))
        self.assertEqual('FAIL', evaluate(reports(drop=3))['status'])
        self.assertEqual('FAIL', evaluate(reports(status='FAIL'))['status'])
        self.assertEqual('FAIL', evaluate(reports(short=dispatch))['status'])

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
