"""Exercise actual pipes/processes offline; real engine acceptance is separate."""
import subprocess
import sys
import unittest
from unittest import mock
import mysql84_release as release


CLIENT = r'''
import re, sys, time
for line in sys.stdin:
    if line.startswith('DELIMITER '):
        assert len(line.split()[1]) <= 15, 'mysql delimiter truncated'
    if 'SIMULATE_ERROR' in line:
        print('ERROR 1064 (42000): synthetic syntax error', file=sys.stderr, flush=True)
        sys.exit(1)
    if 'SIMULATE_EOF' in line:
        sys.exit(0)
    if 'SIMULATE_WAIT' in line:
        time.sleep(20)
    if 'SIMULATE_FLOOD' in line:
        for _ in range(2000):
            print('diagnostic warning'*100, file=sys.stderr)
        sys.stderr.flush()
    m = re.search("SELECT '(er_[a-f0-9]+)'", line)
    if m:
        print('42', flush=True)
        print(m[1], flush=True)
'''


class ProtocolTests(unittest.TestCase):
    def client(self):
        real = subprocess.Popen
        config = dict(database='er84_offline', definerUser='owner', definerHost='%', apiUser='api', apiHost='%',
                      reviewUser='review', reviewHost='%', dedicatedServer=True, transport='socket',
                      socketFile='/var/run/mysqld/mysqld.sock', defaultsFile='unused')
        with mock.patch.object(release.subprocess, 'Popen', side_effect=lambda args, **kw: real([sys.executable,'-u','-c',CLIENT], **kw)):
            return release.Mysql(config)

    def test_success_multiline_short_delimiter_and_stderr_drain(self):
        db = self.client()
        try:
            self.assertEqual(db.scalar('SELECT\n 42'), '42')
            self.assertEqual(db.scalar('SIMULATE_FLOOD'), '42')
            self.assertEqual(db.scalar('CREATE TRIGGER t BEFORE INSERT ON x FOR EACH ROW BEGIN SET NEW.x=1; END'), '42')
        finally:
            db.close()
        self.assertIsNotNone(db.process.poll())

    def test_sql_error_and_eof_poison_connection_without_retry(self):
        for sql, reason in [('SIMULATE_ERROR','1064'), ('SIMULATE_EOF','client EOF')]:
            db = self.client()
            with mock.patch.object(release.subprocess, 'run', return_value=mock.Mock(stdout='', returncode=0)):
                with self.assertRaisesRegex(RuntimeError, reason): db.execute(sql)
            with self.assertRaisesRegex(RuntimeError, 'unusable'): db.execute('SELECT 1')
            db.close()

    def test_timeout_has_process_diagnostics_and_no_reuse(self):
        db = self.client()
        with mock.patch.object(release.subprocess, 'run', return_value=mock.Mock(stdout='7\tQuery\t1\tUser sleep', returncode=0)):
            with self.assertRaisesRegex(RuntimeError, 'MySQL timeout;') as error:
                db.execute('SIMULATE_WAIT', timeout=0.2)
        self.assertIn('processlist', str(error.exception))
        self.assertIn('statementHash', str(error.exception))
        self.assertIn('returnCode', str(error.exception))
        self.assertIsNotNone(db.process.poll())
        with self.assertRaisesRegex(RuntimeError, 'unusable'): db.execute('SELECT 1')
        db.close()


if __name__ == '__main__': unittest.main()
