"""Meaningful offline safety/closure tests; actual Windows bootstrap tested separately."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('db_setup',Path(__file__).with_name('local_test_db.py'))
db=importlib.util.module_from_spec(spec);spec.loader.exec_module(db)

class BootstrapSafety(unittest.TestCase):
    def test_main_port_refused(self):
        with self.assertRaises(RuntimeError):
            db.validate_target(3306,Path('C:/repo'+db.SUFFIX),Path('C:/MAMP/db/mysql'))

    def test_noncanonical_data_refused(self):
        with self.assertRaises(RuntimeError):
            db.validate_target(13306,Path('C:/MAMP/db/mysql'),Path('C:/MAMP/db/mysql'))

    def test_dedicated_path_and_port(self):
        db.validate_target(23306,Path('C:/repo'+db.SUFFIX),Path('C:/MAMP/db/mysql'))

    def test_journal_never_overwrites(self):
        with tempfile.TemporaryDirectory() as temp:
            file=Path(temp)/'phase.json';db.write_new(file,{'started':True})
            with self.assertRaises(FileExistsError):db.write_new(file,{'started':False})
            self.assertIn('true',file.read_text())

    def test_artifact_closure_hashes(self):
        contract=__import__('json').loads((db.ROOT/'migrations/bootstrap/schema-contract.json').read_text())
        self.assertEqual(len(contract['tables']),db.verify_files()['bootstrapInventory']['tables'])
        self.assertTrue({'institution_membership_security_state','institution_membership_event',
            'institution_invitation','institution_onboarding_event','platform_operation_nonce',
            'institution_provisioning','institution_recovery','institution_recovery_approval'}<=set(contract['tables']))

    def test_only_auto_increment_watermark_is_normalized(self):
        self.assertEqual('BIGINT AUTO_INCREMENT ENGINE=InnoDB',db.normalize_ddl('BIGINT AUTO_INCREMENT ENGINE=InnoDB AUTO_INCREMENT=27'))

if __name__=='__main__':unittest.main()
