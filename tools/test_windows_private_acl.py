"""Real NTFS regression tests; no simulated Windows certification."""
import os
from pathlib import Path
import tempfile
import unittest

from local_test_db import private_acl


@unittest.skipUnless(os.name == 'nt', 'Requires real Windows NTFS ACLs')
class WindowsPrivateAclTests(unittest.TestCase):
    def test_inherited_and_protected_parent_and_repeated_application(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            for protected in (False, True):
                parent = base/str(protected)
                parent.mkdir()
                if protected:
                    private_acl(parent)
                target = parent/'target'
                target.mkdir()
                first = private_acl(target)
                after = first['snapshots']['after']
                self.assertTrue(after['protected'])
                self.assertEqual(first['user'], after['owner'])
                self.assertEqual({first['user'], 'S-1-5-18', 'S-1-5-32-544'},
                                 {rule['sid'] for rule in after['rules']})
                self.assertEqual(3, len(after['rules']))
                self.assertTrue(all(rule['type'] == 'Allow' and not rule['inherited']
                                    and rule['rights'] == 2032127 for rule in after['rules']))
                # Set-Acl on a protected target can request SACL privileges.
                # The writer must succeed repeatedly without that privilege.
                second = private_acl(target)
                self.assertEqual(after['sddl'], second['snapshots']['after']['sddl'])
                child = target/'child'
                child.mkdir()
                inherited = private_acl(child)['snapshots']['before']
                self.assertTrue(all(rule['inherited'] for rule in inherited['rules']))
                self.assertEqual({rule['sid'] for rule in after['rules']},
                                 {rule['sid'] for rule in inherited['rules']})

    def test_real_powershell_failure_keeps_operation_and_error(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaises(RuntimeError) as caught:
                private_acl(Path(temporary)/'missing')
            diagnostic = caught.exception.acl_diagnostic
            self.assertEqual('FAIL', diagnostic['status'])
            self.assertEqual('read-target', diagnostic['operation'])
            for field in ('message', 'type', 'hresult', 'id', 'category'):
                self.assertIn(field, diagnostic['error'])
            self.assertIn('PathNotFound', diagnostic['error']['id'])
            self.assertIn('<target>', diagnostic['error']['message'])


if __name__ == '__main__':
    unittest.main()
