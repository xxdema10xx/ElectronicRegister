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


class PlatformIdentityStagingTests(unittest.TestCase):
    """Staging contract of er-platform-identity-1: opt-in runtime, secret keys, successor step order, seed fiscal codes."""

    @staticmethod
    def check_char(code):
        odd = (1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20, 11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23)
        value = lambda c: int(c) if c.isdigit() else ord(c) - 65
        return chr(65 + sum(odd[value(c)] if i % 2 == 0 else value(c) for i, c in enumerate(code[:15])) % 26)

    def test_check_character_matches_the_documented_example(self):
        self.assertEqual(self.check_char('RSSMRA80A01H501U'), 'U')

    def test_demo_fiscal_codes_are_valid_deterministic_and_distinct(self):
        codes = []
        for inst in demo_seed.INSTITUTES:
            admins, staff, teachers, students = demo_seed.people(inst)
            codes += [demo_seed.fiscal_code(p['email']) for p in [*admins, staff, *teachers, *students[0], *students[1]]]
        self.assertEqual(len(codes), len(set(codes)))
        for code in codes:
            self.assertRegex(code, r'^[A-Z]{6}[0-9]{2}[ABCDEHLMPRST][0-9]{2}[A-Z][0-9]{3}[A-Z]$')
            self.assertEqual(code[15], self.check_char(code))
        self.assertEqual(demo_seed.fiscal_code('a@b'), demo_seed.fiscal_code('a@b'))

    def test_runtime_is_opt_in_and_never_carries_key_material(self):
        plain = host.runtime_configuration('staging.example.test', '172.30.252.1')
        self.assertNotIn('PlatformIdentity', plain['Multitenancy'])
        on = host.runtime_configuration('staging.example.test', '172.30.252.1', platform_identity=True)['Multitenancy']['PlatformIdentity']
        self.assertTrue(on['Enabled'])
        self.assertEqual(on['EmailDomain'], host.PLATFORM_EMAIL_DOMAIN)
        self.assertFalse([k for k in on if k in ('FiscalCodeHmacKey', 'FiscalCodeEncryptionKey')])

    def test_keys_are_mounted_as_api_secrets_only(self):
        compose = (ROOT / 'infra/staging/compose.yaml').read_text()
        api = compose.split('  api:')[1].split('  ops:')[0]
        self.assertIn('target: Multitenancy__PlatformIdentity__FiscalCodeHmacKey', api)
        self.assertIn('target: Multitenancy__PlatformIdentity__FiscalCodeEncryptionKey', api)
        self.assertNotIn('FiscalCode', compose.split('  ops:')[1].split('  root-password:')[0])

    def test_successor_runs_after_the_er_quality_2_certification_and_before_any_schema_audit(self):
        steps = rel.HOST_STEPS
        self.assertLess(steps.index('release-preflight'), steps.index('successor'))
        self.assertLess(steps.index('successor'), steps.index('seed'))
        self.assertLess(steps.index('successor'), steps.index('start'))

    def _apply_successors(self, state):
        """Runs host.apply_successor against a fake DB container; returns (node calls, SQL sent, stdout)."""
        applied, nodes, sent = set(), [], []

        def fake_sql(args, input, **_kwargs):
            sent.append(input)
            if input.startswith("SELECT COUNT(*) FROM er_schema_release WHERE version='"):
                name = input.split("'")[1]
                return mock.Mock(returncode=0, stdout='1' if name == 'er-quality-2-mysql84' or name in applied else '0', stderr='')
            for _r, source, _f, checks, _p in host.SUCCESSORS:
                if input == (host.ROOT / 'migrations' / source / '002_validate.sql').read_text(encoding='utf-8'):
                    return mock.Mock(returncode=0, stdout='\n'.join(f'check{i}\t0' for i in range(checks)), stderr='')
            applied.update(r for r, *_ in host.SUCCESSORS if f"VALUES('{r}')" in input)
            return mock.Mock(returncode=0, stdout='', stderr='')

        def fake_node(args, **_kwargs):
            nodes.append([str(a) for a in args])
            release = str(args[-1])
            file = next(f for r, _s, f, *_ in host.SUCCESSORS if r == release)
            Path(args[-2]).mkdir(parents=True)
            Path(args[-2], file).write_text(f"INSERT INTO er_schema_release(version) VALUES('{release}');", encoding='utf-8')

        labels = json.dumps({'io.er.staging.id': state['id']})
        with tempfile.TemporaryDirectory() as folder, mock.patch('sys.stdout', new_callable=io.StringIO) as out, \
                mock.patch.object(host, 'compose', return_value='c' * 12), \
                mock.patch.object(host, 'output', side_effect=['f' * 64, labels]), \
                mock.patch.object(host, 'run', side_effect=fake_node), \
                mock.patch.object(host, 'write', side_effect=lambda path, text, mode=None: Path(path).write_text(text, encoding='utf-8')), \
                mock.patch.object(host.subprocess, 'run', side_effect=fake_sql):
            (Path(folder) / 'ops').mkdir()
            host.apply_successor(Path(folder), state)
        return nodes, sent, out.getvalue()

    def test_grade_scale_successor_is_applied_even_without_platform_identity(self):
        nodes, sent, out = self._apply_successors(dict(id='a' * 32))
        self.assertIn('er-platform-identity-1 skipped', out)
        self.assertEqual([n[-1] for n in nodes], ['er-grade-scale-1'])
        self.assertIn('er-grade-scale-1 applied and validated (7/7 checks at zero).', out)
        self.assertFalse(any("VALUES('er-platform-identity-1')" in sql for sql in sent))

    def test_production_flow_never_replays_a_successor_already_applied_by_the_staging_step(self):
        import production_flow
        flow = object.__new__(production_flow.Flow)
        applied, nodes, sent = {'er-quality-2-mysql84', 'er-grade-scale-1'}, [], []

        def root_sql(statement):
            return '1' if statement.split("'")[1] in applied else '0'

        def fake_node(args, **_kwargs):
            nodes.append(str(args[-1]))
            file = next(f for r, _s, f, *_ in host.SUCCESSORS if r == str(args[-1]))
            Path(args[-2]).mkdir(parents=True)
            Path(args[-2], file).write_text(f"VALUES('{args[-1]}');", encoding='utf-8')

        def fake_mysql(args, input, **_kwargs):
            sent.append(input)
            applied.update(r for r, *_ in host.SUCCESSORS if f"VALUES('{r}')" in input)
            return mock.Mock(returncode=0, stdout='', stderr='')

        with tempfile.TemporaryDirectory() as work:
            flow.work, flow.folder, flow.root_sql = Path(work), Path(work), root_sql
            with mock.patch.object(host, 'compose', return_value='c' * 12), \
                    mock.patch.object(host, 'write', side_effect=lambda path, text, mode=None: Path(path).write_text(text, encoding='utf-8')), \
                    mock.patch.object(host, 'run', side_effect=fake_node), \
                    mock.patch.object(production_flow.subprocess, 'run', side_effect=fake_mysql):
                flow.apply_successor()
        # er-grade-scale-1 was already applied by `host.py successor`: only platform identity runs.
        self.assertEqual(['er-platform-identity-1'], nodes)
        self.assertEqual(1, len(sent))
        self.assertIn('er-grade-scale-1', applied)

    def test_successors_run_in_order_when_platform_identity_is_enabled(self):
        nodes, _sent, out = self._apply_successors(dict(id='a' * 32, platform_identity=True))
        self.assertEqual([n[-1] for n in nodes], ['er-platform-identity-1', 'er-grade-scale-1'])
        self.assertIn('er-platform-identity-1 applied and validated (13/13 checks at zero).', out)


if __name__ == '__main__':
    unittest.main(verbosity=1)
