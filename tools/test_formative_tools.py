"""Non-mutating safety checks for the disposable formative-plan database runner."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import unittest

from formative_test_db import Instance, ROOT


class FormativeRunnerSafetyTests(unittest.TestCase):
    def test_protected_ports_rejected_before_any_instance_creation(self):
        before=set((ROOT/'.staff-private').glob('formative-*'))
        for port in (3306,13306):
            result=subprocess.run([sys.executable,str(ROOT/'tools/formative_test_db.py'),'--create',str(port)],capture_output=True)
            self.assertNotEqual(0,result.returncode)
            self.assertIn(b'New port required',result.stderr)
        self.assertEqual(before,set((ROOT/'.staff-private').glob('formative-*')))

    def test_external_instance_path_rejected_before_credentials_or_connection(self):
        with self.assertRaisesRegex(RuntimeError,'Private workspace'):
            Instance(ROOT)

    def test_equal_inventory_does_not_hide_modified_predecessor_table(self):
        expected=json.loads((ROOT/'migrations/institution-formative-plan/predecessor-contract.json').read_text(encoding='utf-8-sig'))
        forged=copy.deepcopy(expected)
        forged['tables']['subject']+=' /* altered */'
        instance=object.__new__(Instance)
        with self.assertRaisesRegex(RuntimeError,'table definitions mismatch'):
            instance.verify_predecessor(forged)


if __name__=='__main__': unittest.main()
