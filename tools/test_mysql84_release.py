"""Offline failure/recovery guardrails. Runtime evidence is a separate CI gate."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import mysql84_release as release


class ReleaseTests(unittest.TestCase):
    def runner(self, tmp):
        db = mock.Mock()
        db.scalar.return_value = '1'
        db.execute.return_value = 'GRANT ALL PRIVILEGES ON *.* TO migration'
        config = dict(database='er84_unit', serverUuid='synthetic-uuid', definerUser='owner', definerHost='%')
        runner = release.Runner(db, config, Path(tmp)/'journal.json')
        return db, runner

    def state(self, runner):
        return dict(format=1, operation='upgrade', phase='triggers', database=runner.config['database'], serverUuid=runner.config['serverUuid'],
                    manifest=release.sha(json.dumps(runner.manifest, sort_keys=True)), triggers={}, fingerprint={})

    def test_partial_requires_explicit_recovery_without_ddl(self):
        with tempfile.TemporaryDirectory() as tmp:
            db, runner = self.runner(tmp)
            runner.state = self.state(runner)
            with mock.patch.object(release, 'identity', return_value={'offline':1}):
                with self.assertRaisesRegex(RuntimeError, 'explicit recover'):
                    runner.upgrade()
            self.assertEqual([mock.call('SHOW GRANTS')], db.execute.call_args_list)

    def test_drift_refused_before_maintenance_or_journal(self):
        with tempfile.TemporaryDirectory() as tmp:
            db, runner = self.runner(tmp)
            with mock.patch.object(release, 'identity', return_value={'offline':0}), mock.patch.object(release, 'verify_schema', side_effect=RuntimeError('drift')):
                with self.assertRaisesRegex(RuntimeError, 'drift'):
                    runner.upgrade()
            self.assertEqual([mock.call('SHOW GRANTS')], db.execute.call_args_list)
            self.assertFalse(runner.path.exists())

    def test_trigger_order_is_preserved(self):
        with tempfile.TemporaryDirectory() as tmp:
            _, runner = self.runner(tmp)
            self.assertIn('PRECEDES `mt5b_membership_security_delete`', runner.plan_create('mt5b_membership_delete_contract'))
            self.assertIn('FOLLOWS `mt5b_membership_delete_contract`', runner.plan_create('mt5b_membership_security_delete'))
            self.assertIn('PRECEDES `mt5d_event_snapshot_shape`', runner.plan_create('mt5d_event_insert'))

    def test_journal_tampering_and_sequence_refused(self):
        with tempfile.TemporaryDirectory() as tmp:
            _, runner = self.runner(tmp)
            runner.state = self.state(runner)
            runner.state['triggers']['unknown'] = {'phase':'done'}
            with self.assertRaisesRegex(RuntimeError, 'sequence'):
                runner.validate_state()
            runner.state = self.state(runner)
            runner.state['serverUuid'] = 'other-server'
            with self.assertRaisesRegex(RuntimeError, 'identity'):
                runner.validate_state()

    def test_completed_operation_only_verifies(self):
        with tempfile.TemporaryDirectory() as tmp:
            db, runner = self.runner(tmp)
            runner.state = self.state(runner)
            runner.state['phase'] = 'done'
            runner.state['triggers'] = {name:dict(phase='done', dropHash=release.sha('DROP TRIGGER `' + name + '`'), createHash=release.sha(runner.plan_create(name))) for name in runner.manifest['changedTriggers']}
            with mock.patch.object(release, 'identity'), mock.patch.object(release, 'verify_schema'):
                runner.upgrade()
            self.assertEqual([mock.call('SHOW GRANTS')], db.execute.call_args_list)

    def test_maintenance_refuses_remaining_sessions_and_does_not_reopen(self):
        with tempfile.TemporaryDirectory() as tmp:
            db, runner = self.runner(tmp)
            db.scalar.return_value = '1'
            with self.assertRaisesRegex(RuntimeError, 'Other sessions'):
                runner.maintenance()
            db.execute.assert_called_once_with('SET PERSIST offline_mode=ON')

    def test_persistent_save_roundtrip(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)/'journal.json'
            release.save(path, {'state':'drop-intent'})
            self.assertEqual({'state':'drop-intent'},json.loads(path.read_text()))
            self.assertFalse(path.with_suffix('.pending').exists())

    def test_recovery_after_lost_drop_ack_creates_without_replaying_drop(self):
        with tempfile.TemporaryDirectory() as tmp:
            db, runner = self.runner(tmp)
            first = runner.manifest['changedTriggers'][0]
            runner.state = self.state(runner)
            runner.state['triggers'][first] = dict(phase='drop-intent', dropHash=release.sha('DROP TRIGGER `' + first + '`'), createHash=release.sha(runner.plan_create(first)))
            db.scalar.side_effect = lambda sql: '1' if 'GET_LOCK' in sql or '@@global.offline_mode' in sql else '0'
            with mock.patch.object(release,'identity'), mock.patch.object(release,'trigger_rows',return_value={}), mock.patch.object(release,'verify_schema'):
                with self.assertRaisesRegex(RuntimeError,'Injected interruption'):
                    runner.upgrade(recover=True,fault=first+':after-create')
            statements = [c.args[0] for c in db.execute.call_args_list]
            self.assertFalse(any(s.startswith('DROP TRIGGER') for s in statements))
            self.assertEqual(1,sum(s.startswith('CREATE DEFINER') for s in statements))
            self.assertNotIn('SET PERSIST offline_mode=OFF',statements)

    def test_lost_create_ack_does_not_accept_metadata_drift(self):
        with tempfile.TemporaryDirectory() as tmp:
            db, runner = self.runner(tmp)
            first = runner.manifest['changedTriggers'][0]
            runner.state = self.state(runner)
            runner.state['triggers'][first] = dict(phase='create-intent', dropHash=release.sha('DROP TRIGGER `' + first + '`'), createHash=release.sha(runner.plan_create(first)))
            db.scalar.side_effect = lambda sql: '1' if 'GET_LOCK' in sql or '@@global.offline_mode' in sql else '0'
            observed = {first:{'body':release.expected_body(runner.new['triggers'][first])}}
            with mock.patch.object(release,'identity'), mock.patch.object(release,'trigger_rows',return_value=observed), mock.patch.object(release,'verify_schema',side_effect=RuntimeError('mode drift')):
                with self.assertRaisesRegex(RuntimeError,'mode drift'):
                    runner.upgrade(recover=True)
            statements = [c.args[0] for c in db.execute.call_args_list]
            self.assertFalse(any(s.startswith(('DROP TRIGGER','CREATE DEFINER')) for s in statements))
            self.assertEqual('create-intent',runner.state['triggers'][first]['phase'])


if __name__ == '__main__':
    unittest.main()
