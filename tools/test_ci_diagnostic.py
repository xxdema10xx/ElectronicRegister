import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from artifact_diagnostic import audit, sha
from ci_diagnostic import execute, failed


class AggregateDiagnosticTests(unittest.TestCase):
    def test_timeout_preserves_partial_output_and_continues_independent_checks(self):
        with tempfile.TemporaryDirectory() as directory:
            rows = execute([
                dict(id='timeout', timeout=0.5, command=[sys.executable, '-c', 'import time; print("before timeout", flush=True); time.sleep(20)']),
                dict(id='independent', command=[sys.executable,'-c','pass'])], directory)
            self.assertEqual([row['status'] for row in rows], ['FAIL','PASS'])
            self.assertIn('before timeout', rows[0]['error'])

    def test_runtime_bootstrap_failure_records_all_dependent_phases(self):
        import os
        from unittest.mock import patch, MagicMock
        import mysql84_runtime
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            config = root/'config.json'
            config.write_text(json.dumps(dict(isolatedCI=True, mysqlContainer='a'*64, database='er84_fixture')))
            image = json.loads((mysql84_runtime.ROOT/'infra/staging/mysql-image.json').read_text())['image']
            database = MagicMock()
            database.execute.side_effect = RuntimeError('bootstrap failed')
            with patch.dict(os.environ, GITHUB_ACTIONS='true'), patch.object(mysql84_runtime, 'Mysql', return_value=database), patch.object(mysql84_runtime.subprocess, 'check_output', return_value=image), patch.object(mysql84_runtime.subprocess, 'run'):
                with self.assertRaisesRegex(RuntimeError, 'bootstrap failed'):
                    mysql84_runtime.run(config, root/'evidence')
            rows = json.loads((root/'evidence/runtime-phases.json').read_text())['problems']
            self.assertEqual(len(rows), 7)
            self.assertEqual([row['status'] for row in rows], ['FAIL']+['BLOCKED']*6)
            self.assertIn('bootstrap failed', rows[0]['error'])
            database.close.assert_called_once()

    def test_discovery_deduplicates_theories_and_rejects_empty_selection(self):
        from ci_diagnostic import discovered_methods
        prefix = '    ElectronicRegisterAPI.Tests.Integration.Fixture.'
        methods = discovered_methods('Test listing:\n'+prefix+'One(x: 1)\n'+prefix+'One(x: 2)\n'+prefix+'Two\n')
        self.assertEqual(methods, ['ElectronicRegisterAPI.Tests.Integration.Fixture.One', 'ElectronicRegisterAPI.Tests.Integration.Fixture.Two'])
        with self.assertRaisesRegex(ValueError, 'No integration'):
            discovered_methods('No tests match the filter')

    def test_runtime_phases_preserve_error_and_block_contaminated_dependents(self):
        from runtime_diagnostic import RuntimeDiagnostic
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'phases.json'
            diagnostic = RuntimeDiagnostic(path, ['install', 'ddl', 'acceptance'])
            with diagnostic.phase('install'):
                pass
            with self.assertRaisesRegex(RuntimeError, 'DDL failed'):
                with diagnostic.phase('ddl'):
                    raise RuntimeError('DDL failed')
            rows = json.loads(path.read_text())['problems']
            self.assertEqual([row['status'] for row in rows], ['PASS', 'FAIL', 'BLOCKED'])
            self.assertIn('RuntimeError: DDL failed', rows[1]['error'])
            self.assertEqual(rows[1]['dependents'], ['acceptance'])
            self.assertIn('ddl', rows[2]['error'])

    def test_clean_clone_reports_all_independent_errors_and_nonzero_exit(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory); source = base/'source'; source.mkdir()
            def git(*args, data=None):
                return subprocess.check_output(['git', *args],cwd=source,input=data)
            git('init','-q')
            files = {f'evidence/bad{i}.txt': 'actual' for i in range(3)}
            files.update({'tools/unregistered.py':'changed', 'evidence/removed1.txt':'gone','evidence/removed2.txt':'gone'})
            claims = {name: sha(b'expected') for name in files}
            claims['evidence/untracked.txt'] = sha(b'local')
            for name, value in files.items():
                path=source/name; path.parent.mkdir(parents=True,exist_ok=True); path.write_text(value)
            release=source/'migrations/mysql84-remediation/manifest.json';release.parent.mkdir(parents=True)
            release.write_text(json.dumps(dict(files={name:sha(b'expected') for name in files if 'unregistered' not in name},retiredArtifacts=[])))
            report=source/'tests/Integration/REPORT.json';report.parent.mkdir(parents=True)
            report.write_text(json.dumps(dict(files=claims)))
            markdown=source/'docs/quality/REPORT.md';markdown.parent.mkdir(parents=True)
            markdown.write_text('Evidence: `tests/unreadable.log`\n')
            git('add','.')
            tree=git('write-tree').strip()
            commit=git('-c','user.name=Fixture','-c','user.email=fixture@example.test','commit-tree',tree.decode(),data=b'Isolated synthetic fixture\n').strip()
            git('update-ref','refs/heads/fixture',commit.decode())
            clone=base/'clean'
            subprocess.run(['git','clone','-q','--branch','fixture',str(source),str(clone)],check=True)
            for i in (1,2): (clone/f'evidence/removed{i}.txt').unlink()
            (clone/'evidence/untracked.txt').write_text('local')
            (clone/'tools/unregistered.py').write_text('modified after clone')
            result=audit(clone)
            from unittest.mock import patch
            original_is_file = Path.is_file
            def is_file(path):
                if path.name == 'unreadable.log':
                    raise PermissionError('Unreadable historical evidence')
                return original_is_file(path)
            with patch.object(Path, 'is_file', is_file):
                unreadable = audit(clone)
            self.assertTrue(any(row['category']=='report-reference-unreadable' for row in unreadable['problems']))
            self.assertGreater(len(unreadable['artifacts']), 0)
            categories={(r['category'],r['file']) for r in result['problems']}
            for i in range(3): self.assertIn(('hash-mismatch',f'evidence/bad{i}.txt'),categories)
            for i in (1,2): self.assertIn(('missing',f'evidence/removed{i}.txt'),categories)
            self.assertIn(('historical-bytes-unrecoverable','evidence/untracked.txt'),categories)
            self.assertIn(('closure-omission','tools/unregistered.py'),categories)
            self.assertIn(('unmanifested-change','tools/unregistered.py'),categories)
            checks=[dict(id='independent-failure',command=[sys.executable,'-c','raise RuntimeError("independent failure")']),
                    dict(id='dependent',needs=['independent-failure'],command=[sys.executable,'-c','raise AssertionError("must not execute")']),
                    dict(id='independent-success',command=[sys.executable,'-c','print("executed")'])]
            rows=execute(checks,clone)
            self.assertEqual([r['status'] for r in rows],['FAIL','BLOCKED','PASS'])
            self.assertTrue(failed(result['problems']+rows))
            plan=base/'plan.json';plan.write_text(json.dumps(checks));output=base/'output.json'
            run=subprocess.run([sys.executable,str(Path(__file__).with_name('ci_diagnostic.py')),'--root',str(clone),'--audit','--plan',str(plan),'--output',str(output)],capture_output=True)
            self.assertNotEqual(run.returncode,0)
            saved=json.loads(output.read_text())
            self.assertGreaterEqual(len(saved['problems']),len(result['problems'])+3)
            self.assertTrue(categories <= {(r['category'],r['file']) for r in saved['problems']})
            outcomes={r['id']:r['status'] for r in saved['problems']}
            self.assertEqual(outcomes['independent-failure'],'FAIL')
            self.assertEqual(outcomes['dependent'],'BLOCKED')
            self.assertEqual(outcomes['independent-success'],'PASS')

    def test_final_gate_rejects_missing_reports_even_if_jobs_claim_success(self):
        import os
        with tempfile.TemporaryDirectory() as directory:
            env=dict(os.environ,JOB_RESULTS=json.dumps({name:{'result':'success'} for name in ('release','diagnostic','integration')}))
            run=subprocess.run([sys.executable,str(Path(__file__).with_name('final_ci_diagnostic.py').resolve())],cwd=directory,env=env,capture_output=True)
            self.assertNotEqual(run.returncode,0)
            report=json.loads((Path(directory)/'.diagnostics/MYSQL84_CI_DIAGNOSTIC_REPORT.json').read_text())
            self.assertTrue(any(row['id']=='report-count' and row['status']=='FAIL' for row in report['problems']))

    def test_aborted_or_empty_new_regression_cannot_pass_with_exit_zero(self):
        with tempfile.TemporaryDirectory() as directory:
            root=Path(directory)
            (root/'aborted.trx').write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult outcome="Passed" /></Results><ResultSummary outcome="Failed"><Counters total="1" executed="1" passed="1" /></ResultSummary></TestRun>')
            xml = (root/'aborted.trx').read_text()
            rows=execute([dict(id='new-regression',command=[sys.executable,'-c',
                'from pathlib import Path; Path("aborted.trx").write_text('+repr(xml)+')'],trx='aborted.trx')],root)
            self.assertEqual(rows[0]['status'],'FAIL')
            self.assertIn('did not complete',rows[0]['error'])

    def test_stale_trx_cannot_certify_a_new_invocation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root/'previous.trx').write_text('old evidence')
            rows = execute([dict(id='stale', command=[sys.executable,'-c','pass'], trx='previous.trx')],root)
            self.assertEqual(rows[0]['status'], 'FAIL')
            self.assertFalse((root/'previous.trx').exists())

    def test_final_gate_checks_suite_identity_and_contents_not_only_count(self):
        import os
        from final_ci_diagnostic import EXPECTED
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            reports = root/'.diagnostics/downloaded'; reports.mkdir(parents=True)
            env = dict(os.environ, JOB_RESULTS=json.dumps({name: {'result':'success'} for name in ('release','diagnostic','integration')}))
            command = [sys.executable,str(Path(__file__).with_name('final_ci_diagnostic.py').resolve())]
            for index, (suite, checks) in enumerate(EXPECTED.items()):
                path = reports/str(index); path.mkdir()
                (path/'suite-diagnostic.json').write_text(json.dumps(dict(suite=suite,problems=[dict(id=name,status='PASS',required=True) for name in checks])))
            self.assertEqual(subprocess.run(command,cwd=root,env=env,capture_output=True).returncode,0)
            path = reports/'0/suite-diagnostic.json'
            report = json.loads(path.read_text()); report['problems'] = []
            path.write_text(json.dumps(report))
            self.assertNotEqual(subprocess.run(command,cwd=root,env=env,capture_output=True).returncode,0)
            report['suite'] = 'calendar'; path.write_text(json.dumps(report))
            self.assertNotEqual(subprocess.run(command,cwd=root,env=env,capture_output=True).returncode,0)
            report['problems'] = [dict(id='observed-failure',status='FAIL',required=True,error='Full independent error')]
            path.write_text(json.dumps(report))
            self.assertNotEqual(subprocess.run(command,cwd=root,env=env,capture_output=True).returncode,0)
            combined = json.loads((root/'.diagnostics/MYSQL84_CI_DIAGNOSTIC_REPORT.json').read_text())
            self.assertTrue(any(row.get('originalId')=='observed-failure' and row['error']=='Full independent error' for row in combined['problems']))

    def test_original_clean_checkout_failure(self):
        root=Path(__file__).resolve().parents[1]
        old=subprocess.check_output(['git','show','cbd3ced9:tools/historical_artifacts.py'],cwd=root).decode()
        namespace={};exec(old,namespace)
        name='tests/Integration/TestResults/phase5-file-readers.trx'
        report=json.loads((root/'tests/Integration/INSTITUTION_CALENDAR_VERIFICATION.json').read_text())
        expected=next(a['sha256'] for a in report['artifacts'] if a['path']==name)
        with tempfile.TemporaryDirectory() as directory:
            clean=Path(directory)/'clone'
            subprocess.run(['git','clone','-q','--no-checkout','--shared',str(root),str(clean)],check=True)
            subprocess.run(['git','-C',str(clean),'-c','core.autocrlf=false','checkout','-q','--detach','cbd3ced9'],check=True)
            self.assertNotEqual(sha((clean/name).read_bytes()),expected)
            with self.assertRaisesRegex(ValueError,'Historical bytes not found'):
                namespace['verify_historical_artifact'](clean,name,expected)


if __name__ == '__main__': unittest.main()
