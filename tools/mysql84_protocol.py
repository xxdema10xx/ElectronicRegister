"""Real persistent-client acceptance on a NEW disposable CI schema only."""
import json
import uuid
import subprocess
import threading
import queue
import time
from pathlib import Path
from mysql84_release import Mysql, require


def reproduce_legacy(db):
    """Keep stdin open, as in the failing runner. Never replay its CREATE."""
    schema = 'er84_legacy_' + uuid.uuid4().hex[:12]
    delimiter = '$$' + uuid.uuid4().hex
    marker = 'legacy_' + uuid.uuid4().hex
    process = subprocess.Popen(db.args, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    output = queue.Queue()
    readers = []
    def drain(stream):
        for line in stream: output.put(line.rstrip('\n'))
    for stream in (process.stdout, process.stderr):
        reader = threading.Thread(target=drain, args=(stream,), daemon=True); reader.start(); readers.append(reader)
    try:
        process.stdin.write(f"DELIMITER {delimiter}\nCREATE DATABASE `{schema}`\n{delimiter}\nDELIMITER ;\nSELECT '{marker}';\n")
        process.stdin.flush()
        deadline = time.monotonic()+2
        while time.monotonic() < deadline:
            try:
                require(output.get(timeout=max(0.01, deadline-time.monotonic())) != marker, 'Legacy deadlock did not reproduce')
            except queue.Empty:
                break
        require(process.poll() is None, 'Legacy client exited rather than hanging')
        # A missing acknowledgement is NOT evidence that CREATE was uncommitted.
        committed = db.scalar("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME='"+schema+"'")
        require(committed == '1', 'Legacy CREATE did not reach the committed boundary')
        return dict(status='REPRODUCED', missingMarker=True, clientAlive=True, createCommitted=committed == '1', schema=schema)
    finally:
        if process.poll() is None: process.kill()
        process.wait(timeout=5)
        for reader in readers: reader.join(timeout=1)
        for stream in (process.stdin, process.stdout, process.stderr): stream.close()


def verify(config, output):
    require(config.get('isolatedCI') is True, 'Disposable CI only')
    schema = 'er84_protocol_' + uuid.uuid4().hex[:12]
    db = Mysql(config)
    try:
        require(db.scalar('SELECT VERSION()') == '8.4.13', 'Wrong protocol test engine')
        legacy = reproduce_legacy(db)
        require(db.scalar('SELECT\n  40 +\n  2') == '42', 'Multiline SELECT failed')
        db.execute('CREATE DATABASE `' + schema + '`')
        db.execute('USE `' + schema + '`')
        db.execute('CREATE TABLE probe(id INT PRIMARY KEY, value INT NOT NULL)')
        db.execute('CREATE TRIGGER probe_insert BEFORE INSERT ON probe FOR EACH ROW\nBEGIN\n SET NEW.value = NEW.value + 1;\nEND')
        db.execute('INSERT INTO probe VALUES (1, 41)')
        require(db.scalar('SELECT value FROM probe WHERE id=1') == '42', 'Real trigger did not run')
        db.execute('CREATE PROCEDURE probe_read()\nBEGIN\n SELECT value FROM probe;\nEND')
        require(db.scalar('CALL probe_read()') == '42', 'Real routine did not run')
    finally:
        db.close()
    failed = Mysql(config)
    try:
        try:
            failed.execute('SELECT * FROM `' + schema + '`.missing_table')
        except RuntimeError as error:
            require('SQL error' in str(error) or 'client EOF' in str(error), 'SQL failure misclassified: '+str(error))
            require('1146' in str(error), 'SQL errno missing')
        else:
            raise RuntimeError('Invalid SQL accepted')
        try:
            failed.execute('SELECT 1')
        except RuntimeError as error:
            require('unusable' in str(error), 'Broken connection reused')
        else:
            raise RuntimeError('SQL error connection remained usable')
    finally:
        failed.close()
    slow = Mysql(config)
    try:
        try:
            slow.execute('SELECT SLEEP(10)', timeout=0.2)
        except RuntimeError as error:
            require('MySQL timeout;' in str(error) and 'processlist' in str(error), 'Missing real timeout diagnostic')
        else:
            raise RuntimeError('Slow read did not time out')
    finally:
        slow.close()
    Path(output).write_text(json.dumps(dict(status='PASS', version='8.4.13', schema=schema,
        legacy=legacy, checks=['SELECT','multiline','CREATE DATABASE','trigger DDL and DML','routine','SQL error','real SLEEP timeout','no replay'])), encoding='utf-8')


if __name__ == '__main__':
    import sys
    verify(json.loads(Path(sys.argv[1]).read_text()), sys.argv[2])
