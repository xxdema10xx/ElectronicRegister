"""Offline tests for infra/staging/release_ec2.py, demo_seed.py and the host.py import flag.
Symlink and file-mode cases run on Linux only; no Docker, nginx or network needed."""
import argparse
import io
import json
import os
from pathlib import Path
import stat
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'infra/staging'))
import demo_seed  # noqa: E402
import host  # noqa: E402
import release_ec2 as rel  # noqa: E402

OLD, NEW = 'er-stage-14d2fd709a09', 'er-stage-aaaaaaaaaaaa'
linux_only = unittest.skipUnless(sys.platform.startswith('linux'), 'symlinks and POSIX modes: Linux (the EC2) only')


class NginxTree:
    def __init__(self, root):
        self.root = Path(root)
        for d in ('sites-available', 'sites-enabled'):
            (self.root / d).mkdir(parents=True)
        for name in ('default', *rel.vhost_names(OLD), *rel.vhost_names(NEW)):
            (self.root / 'sites-available' / name).write_text('server {}\n')
        os.symlink(str(self.root / 'sites-available/default'), self.root / 'sites-enabled/default')
        for name in rel.vhost_names(OLD):
            os.symlink(str(self.root / 'sites-available' / name), self.root / 'sites-enabled' / name)

    def enabled(self):
        return sorted(p.name for p in (self.root / 'sites-enabled').iterdir())


@linux_only
class LinkTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.tree = NginxTree(self.tmp.name)
        self.patch = mock.patch.object(rel, 'NGINX', Path(self.tmp.name))
        self.patch.start()

    def tearDown(self):
        self.patch.stop()
        self.tmp.cleanup()

    def test_swap_touches_only_the_namespace_links(self):
        rel.set_links(rel.vhost_names(NEW), rel.vhost_names(OLD), 'absolute')
        self.assertEqual(self.tree.enabled(), sorted(['default', *rel.vhost_names(NEW)]))
        self.assertTrue(all((Path(self.tmp.name) / 'sites-available' / n).is_file() for n in rel.vhost_names(OLD)))
        rel.set_links(rel.vhost_names(OLD), rel.vhost_names(NEW), 'absolute')
        self.assertEqual(self.tree.enabled(), sorted(['default', *rel.vhost_names(OLD)]))
        # Idempotent.
        rel.set_links(rel.vhost_names(OLD), rel.vhost_names(NEW), 'absolute')
        self.assertEqual(self.tree.enabled(), sorted(['default', *rel.vhost_names(OLD)]))

    def test_refuses_regular_files_and_foreign_links(self):
        target = Path(self.tmp.name) / 'sites-enabled' / rel.vhost_names(OLD)[0]
        target.unlink()
        target.write_text('copied, not a link')
        with self.assertRaises(rel.Fail):
            rel.set_links(rel.vhost_names(NEW), rel.vhost_names(OLD), 'absolute')
        target.unlink()
        os.symlink('/etc/passwd', target)
        with self.assertRaises(rel.Fail):
            rel.set_links(rel.vhost_names(NEW), rel.vhost_names(OLD), 'absolute')

    def test_relative_style_is_mirrored(self):
        rel.set_links(rel.vhost_names(NEW), rel.vhost_names(OLD), 'relative')
        self.assertEqual(os.readlink(Path(self.tmp.name) / 'sites-enabled' / rel.vhost_names(NEW)[0]), '../sites-available/' + rel.vhost_names(NEW)[0])
        self.assertTrue(rel.is_our_link(rel.vhost_names(NEW)[0]))


@linux_only
class SwitchRestoreTests(unittest.TestCase):
    """switch must put the old links back by itself when nginx -t or HTTPS fail."""
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        base = Path(self.tmp.name)
        self.tree = NginxTree(base / 'nginx')
        state = base / 'state'
        state.mkdir()
        for kind in ('http', 'https'):
            (state / f'nginx-{kind}.conf').write_text('server {}\n')
        record = dict(project=NEW, state=str(state), source=str(base), port=18081, steps=list(rel.HOST_STEPS))
        facts = dict(san=[rel.DOMAIN], days=90, keyMatches=True, fingerprint='x')
        self.patches = [mock.patch.object(rel, 'NGINX', base / 'nginx'), mock.patch.object(rel, 'NGINX_LOGS', base / 'logs'),
                        mock.patch.object(rel, 'old_state', return_value=dict(project=OLD, port=18080, source=base, revision='b' * 40)),
                        mock.patch.object(rel, 'new_version', return_value=record), mock.patch.object(rel, 'host_py'),
                        mock.patch.object(rel, 'cert_layout', return_value=dict(version=base)), mock.patch.object(rel, 'cert_facts', return_value=facts),
                        mock.patch.object(rel, 'release_dir', return_value=base), mock.patch.object(rel, 'nginx_reload'),
                        mock.patch.object(rel, 'compose'), mock.patch('sys.stdout', new_callable=io.StringIO),
                        mock.patch.object(rel, 'service_state', return_value='healthy')]
        for p in self.patches:
            p.start()

    def tearDown(self):
        for p in reversed(self.patches):
            p.stop()
        self.tmp.cleanup()

    def test_nginx_t_failure_restores_old_links_without_reload(self):
        with mock.patch.object(rel, 'nginx_test', side_effect=[(True, []), (False, ['duplicate log_format']), (True, [])]):
            report = rel.phase_switch()
        self.assertTrue(report.failed)
        self.assertEqual(self.tree.enabled(), sorted(['default', *rel.vhost_names(OLD)]))
        rel.nginx_reload.assert_not_called()
        rel.compose.assert_not_called()

    def test_https_failure_restores_old_links_and_reloads(self):
        with mock.patch.object(rel, 'nginx_test', return_value=(True, [])), \
             mock.patch.object(rel, 'wait_public', side_effect=[(False, (502, '', None)), (True, (200, 'ready', 'x'))]):
            report = rel.phase_switch()
        self.assertTrue(report.failed)
        self.assertEqual(self.tree.enabled(), sorted(['default', *rel.vhost_names(OLD)]))
        self.assertEqual(rel.nginx_reload.call_count, 2)
        rel.compose.assert_not_called()

    def test_success_installs_generated_vhosts_and_stops_old_version(self):
        with mock.patch.object(rel, 'nginx_test', return_value=(True, [])), \
             mock.patch.object(rel, 'wait_public', return_value=(True, (200, 'ready', 'x'))), \
             mock.patch.object(rel, 'service_state', side_effect=lambda project, service: 'stopped' if rel.compose.called else 'healthy'):
            report = rel.phase_switch()
        self.assertFalse(report.failed)
        self.assertEqual(self.tree.enabled(), sorted(['default', *rel.vhost_names(NEW)]))
        rel.compose.assert_called_once()
        self.assertEqual(rel.compose.call_args.args[2:], ('stop',))


@linux_only
class CertificateTests(unittest.TestCase):
    def test_copy_keeps_layout_hash_and_modes_and_never_writes_the_old_folder(self):
        with tempfile.TemporaryDirectory() as tmp, mock.patch.object(rel, 'NGINX', Path(tmp)), mock.patch('sys.stdout', new_callable=io.StringIO):
            old = Path(tmp) / OLD
            name = 'f' * 64
            (old / 'versions' / name).mkdir(parents=True)
            for item, mode in ((old, 0o700), (old / 'versions', 0o700), (old / 'versions' / name, 0o700)):
                item.chmod(mode)
            (old / 'versions' / name / 'fullchain.pem').write_text('CHAIN')
            (old / 'versions' / name / 'privkey.pem').write_text('KEY')
            (old / 'versions' / name / 'fullchain.pem').chmod(0o644)
            (old / 'versions' / name / 'privkey.pem').chmod(0o600)
            os.symlink('versions/' + name, old / 'active')
            os.symlink('active/fullchain.pem', old / 'fullchain.pem')
            os.symlink('active/privkey.pem', old / 'privkey.pem')
            before = {p: (p.stat().st_mtime_ns, p.lstat().st_mode) for p in old.rglob('*')}
            report = rel.Report('t')
            rel.install_certificate(NEW, report)
            rel.install_certificate(NEW, report)  # idempotent
            new = Path(tmp) / NEW
            self.assertEqual(os.readlink(new / 'active'), 'versions/' + name)
            self.assertEqual(os.readlink(new / 'privkey.pem'), 'active/privkey.pem')
            self.assertEqual((new / 'privkey.pem').read_text(), 'KEY')
            self.assertEqual(stat.S_IMODE((new / 'versions' / name / 'privkey.pem').stat().st_mode), 0o600)
            self.assertEqual(stat.S_IMODE(new.stat().st_mode), 0o700)
            self.assertEqual(before, {p: (p.stat().st_mtime_ns, p.lstat().st_mode) for p in old.rglob('*')})
            (new / 'versions' / name / 'privkey.pem').write_text('OTHER')
            with self.assertRaises(rel.Fail):
                rel.install_certificate(NEW, report)


class MiscTests(unittest.TestCase):
    def test_backup_redacts_secret_environment_values(self):
        values = rel.redact_env(['MYSQL_ROOT_PASSWORD_FILE=/run/secrets/root-password', 'MYSQL_ROOT_PASSWORD=x', 'Jwt__Key=y', 'ASPNETCORE_ENVIRONMENT=Staging'])
        self.assertEqual(values, ['MYSQL_ROOT_PASSWORD_FILE=/run/secrets/root-password', 'MYSQL_ROOT_PASSWORD=<omesso>', 'Jwt__Key=<omesso>', 'ASPNETCORE_ENVIRONMENT=Staging'])

    def test_subnet_candidates_avoid_the_old_default(self):
        self.assertNotIn('172.30.252.0/24', rel.SUBNETS)
        with mock.patch.object(rel, 'used_subnets', return_value=[__import__('ipaddress').ip_network('172.30.252.0/24'), __import__('ipaddress').ip_network('172.30.253.0/24')]):
            self.assertEqual(rel.free_subnet(), '172.30.254.0/24')

    def test_ports_start_at_18081(self):
        with mock.patch.object(rel, 'docker_published_ports', return_value={18080, 18081}), mock.patch.object(rel, 'nginx_ports', return_value={18082}), \
             mock.patch.object(rel, 'port_free', return_value=True):
            self.assertEqual(rel.free_port(), 18083)

    def test_host_prepare_enables_import_only_when_asked(self):
        self.assertNotIn('InstitutionImport', host.runtime_configuration('x.example.invalid', '172.30.253.1')['Multitenancy'])
        runtime = host.runtime_configuration('x.example.invalid', '172.30.253.1', True)
        self.assertIs(runtime['Multitenancy']['InstitutionImport']['Enabled'], True)
        self.assertEqual({k for k, v in runtime['Multitenancy'].items() if v['Enabled']},
                         {'SchoolApi', 'CourseApi', 'ExamApi', 'MembershipAdminApi', 'MembershipWriteApi', 'InstitutionCalendar', 'InstitutionImport'})
        self.assertEqual(runtime['Deployment'], dict(Mode='Staging', RolloutApproved=True))
        self.assertEqual(runtime['Readiness']['ExpectedVersion'], '8.4.13')


class DemoTests(unittest.TestCase):
    def test_people_are_deterministic_unique_and_fake(self):
        emails = []
        for inst in demo_seed.INSTITUTES:
            admins, staff, teachers, students = demo_seed.people(inst)
            self.assertEqual((len(admins), len(teachers), [len(c) for c in students]), (2, 3, [6, 6]))
            group = [*admins, staff, *teachers, *students[0], *students[1]]
            self.assertEqual(group, [*demo_seed.people(inst)[0], demo_seed.people(inst)[1], *demo_seed.people(inst)[2], *demo_seed.people(inst)[3][0], *demo_seed.people(inst)[3][1]])
            self.assertTrue(all(p['email'].endswith('.example.invalid') for p in group))
            emails += [p['email'] for p in group]
        self.assertEqual(len(emails), len(set(emails)), 'email demo duplicate')
        self.assertEqual(len(emails), 36)

    def test_student_csv_is_stable_and_uses_the_documented_columns(self):
        inst = demo_seed.INSTITUTES[0]
        roster = demo_seed.people(inst)[3][0]
        content = demo_seed.student_csv(inst, 0, roster)
        self.assertEqual(content, demo_seed.student_csv(inst, 0, roster))
        lines = content.decode().splitlines()
        self.assertEqual(lines[0], 'Nome;Cognome;Classe;RiferimentoEsterno')
        self.assertEqual(len(lines), 7)
        self.assertTrue(all(';' + inst['classes'][0] + ';' in line for line in lines[1:]))

    def test_passwords_meet_the_api_rule(self):
        for _ in range(50):
            value = demo_seed.new_password()
            self.assertTrue(8 <= len(value.encode()) <= 72 and any(c in '!@#$%^&*()_-+=<>?/[]{}' for c in value))

    @linux_only
    def test_private_files_are_0600(self):
        with tempfile.TemporaryDirectory() as tmp:
            private = demo_seed.Private(Path(tmp) / 'demo')
            private.save('credentials.json', {'a': 1})
            self.assertEqual(stat.S_IMODE((Path(tmp) / 'demo/credentials.json').stat().st_mode), 0o600)
            self.assertEqual(stat.S_IMODE((Path(tmp) / 'demo').stat().st_mode), 0o700)
            self.assertEqual(private.load('credentials.json', None), {'a': 1})

    def test_event_ids_are_stable_per_staging(self):
        state = dict(id='a' * 32, project='er-stage-aaaaaaaaaaaa')
        with tempfile.TemporaryDirectory() as tmp:
            a = demo_seed.Seeder(Path(tmp), state, Path(tmp) / 'one', 'x.example.invalid')
            b = demo_seed.Seeder(Path(tmp), state, Path(tmp) / 'two', 'x.example.invalid')
            self.assertEqual(a.event('account', 'x@y'), b.event('account', 'x@y'))
            other = demo_seed.Seeder(Path(tmp), dict(state, id='b' * 32), Path(tmp) / 'three', 'x.example.invalid')
            self.assertNotEqual(a.event('account', 'x@y'), other.event('account', 'x@y'))

    def test_route_masks_identifiers(self):
        self.assertEqual(demo_seed.route('/api/institution/exams/0f8fad5b-d9cb-469f-a165-70867728950e/start?x=1'), '/api/institution/exams/{id}/start')


if __name__ == '__main__':
    unittest.main(verbosity=1)
