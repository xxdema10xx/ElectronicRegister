"""Real NTFS regression tests; no simulated Windows certification."""
import os
import json
import subprocess
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from local_test_db import private_acl
from windows_cli_certification import acl_evidence


@unittest.skipUnless(os.name == 'nt', 'Requires real Windows NTFS ACLs')
class WindowsPrivateAclTests(unittest.TestCase):
    def test_setup_and_file_evidence_without_security_module_autoload(self):
        real_run = subprocess.run

        def no_autoload(args, **kwargs):
            # Keep JSON support available, then prohibit all implicit imports.
            prefix = "Import-Module Microsoft.PowerShell.Utility; $PSModuleAutoLoadingPreference='None'; "
            if '-File' in args:
                script = Path(args[args.index('-File')+1]).read_text()
            else:
                script = args[args.index('-Command')+1]
            suffix = "; if(Get-Module Microsoft.PowerShell.Security) { throw 'Security module loaded' }"
            return real_run(['powershell', '-NoProfile', '-NonInteractive', '-Command', prefix+script+suffix], **kwargs)

        with tempfile.TemporaryDirectory(prefix='ACL space Ω ') as temporary:
            base = Path(temporary)
            with patch('subprocess.run', side_effect=no_autoload):
                setup = private_acl(base)
                for index in range(3):
                    folder = base/str(index)
                    folder.mkdir()
                    (folder/'admin.json').write_text('private-content-must-not-appear')
                evidence = acl_evidence(base)
            self.assertEqual(4, len(evidence))
            self.assertTrue(evidence[0]['protected'])
            for entry in evidence:
                self.assertEqual(setup['user'], entry['owner'])
                self.assertEqual(set(entry['trusted']), {r['sid'] for r in entry['rules']})
                self.assertEqual(3, len(entry['rules']))
                self.assertTrue(all(r['type'] == 'Allow' and r['rights'] == 'FullControl' for r in entry['rules']))
            self.assertTrue(all(r['inherited'] for e in evidence[1:] for r in e['rules']))
            self.assertNotIn('private-content-must-not-appear', json.dumps(evidence))

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
            self.assertIn('DirectoryNotFoundException', diagnostic['error']['id'])
            self.assertIn('<target>', diagnostic['error']['message'])


if __name__ == '__main__':
    unittest.main()
