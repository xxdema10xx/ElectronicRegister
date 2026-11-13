import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('calendar_evidence', Path(__file__).with_name('verify-institution-calendar.py'))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class CalendarEvidenceTests(unittest.TestCase):
    def test_path_escape_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'root'
            root.mkdir()
            (Path(directory) / 'outside').write_text('evidence')
            with self.assertRaises(ValueError):
                MODULE.local(root, '../outside')

    def test_failed_trx_is_not_certified(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            path.write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<TestDefinitions><UnitTest id="1"><TestMethod className="Tests" /></UnitTest></TestDefinitions>
<Results><UnitTestResult testId="1" testName="FailedTest" outcome="Failed" /></Results>
<ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="0" /></ResultSummary></TestRun>''')
            with self.assertRaises(ValueError):
                MODULE.trx(path)

    def test_inflated_trx_counters_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            path.write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<TestDefinitions><UnitTest id="1"><TestMethod className="Tests" /></UnitTest></TestDefinitions>
<Results><UnitTestResult testId="1" testName="Test" outcome="Passed" /></Results>
<ResultSummary outcome="Completed"><Counters total="9" executed="9" passed="9" /></ResultSummary></TestRun>''')
            with self.assertRaises(ValueError):
                MODULE.trx(path)

    def test_aborted_run_cannot_certify_its_passing_subset(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            path.write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
<TestDefinitions><UnitTest id="1"><TestMethod className="Tests" /></UnitTest></TestDefinitions>
<Results><UnitTestResult testId="1" testName="Test" outcome="Passed" /></Results>
<ResultSummary outcome="Failed"><Counters total="1" executed="1" passed="1" /></ResultSummary></TestRun>''')
            with self.assertRaises(ValueError):
                MODULE.trx(path)


if __name__ == '__main__':
    unittest.main()
