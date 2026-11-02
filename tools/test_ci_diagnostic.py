import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from artifact_diagnostic import audit, sha
from ci_diagnostic import execute, failed


class AggregateDiagnosticTests(unittest.TestCase):
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
            rows=execute([dict(id='new-regression',command=[sys.executable,'-c','pass'],trx='aborted.trx')],root)
            self.assertEqual(rows[0]['status'],'FAIL')
            self.assertIn('did not complete',rows[0]['error'])

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
