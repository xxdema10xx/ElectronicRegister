"""Security contract tests for non-mutating refusals and fail-closed CI auditing."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class ReleaseSafetyTests(unittest.TestCase):
    def test_protected_mysql_ports_refused_before_any_connection_or_file_creation(self):
        for port in (3306, 13306):
            with self.subTest(port=port):
                result = subprocess.run([sys.executable, str(ROOT/'tools/verify-disposable-recovery.py'),
                    '--mysql-bin', 'must-not-be-used', '--source-data', 'must-not-be-used', '--source-port', str(port),
                    '--target-data', 'must-not-be-created', '--target-port', '23311', '--backup', 'must-not-be-created',
                    '--confirm-sacrificial'], capture_output=True)
                self.assertNotEqual(0, result.returncode)
                self.assertIn(b'sacrificial ports', result.stderr)

    def audit(self, payload):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'audit.json'
            path.write_text(json.dumps(payload), encoding='utf-8')
            return subprocess.run([sys.executable, str(ROOT/'tools/check-dependency-audit.py'), str(path)], capture_output=True).returncode

    def test_unavailable_or_missing_audit_cannot_pass(self):
        self.assertNotEqual(0, self.audit({}))
        self.assertNotEqual(0, self.audit({'version': 1, 'projects': [], 'problems': [{'level': 'error'}]}))

    def test_reported_transitive_vulnerability_blocks_release(self):
        self.assertNotEqual(0, self.audit({'version': 1, 'projects': [{'frameworks': [
            {'transitivePackages': [{'id': 'synthetic-advisory', 'vulnerabilities': [{'severity': 'High'}]}]}]}]}))

    def test_successful_empty_advisory_list_is_accepted(self):
        self.assertEqual(0, self.audit({'version': 1, 'projects': [{'frameworks': [{'framework': 'net10.0'}]}]}))

    def test_source_package_refuses_existing_destination(self):
        with tempfile.TemporaryDirectory() as folder:
            result = subprocess.run([sys.executable, str(ROOT/'tools/package-versioning-candidates.py'), folder], capture_output=True)
            self.assertNotEqual(0, result.returncode)
            self.assertIn(b'Existing destinations', result.stderr)


if __name__ == '__main__':
    unittest.main()
