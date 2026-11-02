"""Durable diagnostics for sequential phases sharing a disposable database."""
from contextlib import contextmanager
import json
from pathlib import Path
import traceback
import time
import subprocess
from artifact_diagnostic import issue


class RuntimeDiagnostic:
    def __init__(self, output, phases):
        self.output = Path(output)
        self.phases = phases
        self.rows = []

    def save(self):
        self.output.parent.mkdir(parents=True, exist_ok=True)
        self.output.write_text(json.dumps(dict(problems=self.rows), indent=2)+'\n', encoding='utf-8')

    @contextmanager
    def phase(self, name):
        if name != self.phases[len(self.rows)]:
            raise ValueError('Unexpected runtime phase: '+name)
        row = issue('runtime-phase', 'tools/mysql84_runtime.py', '', test=name)
        row.update(id=name, required=True, status='BLOCKED', cause='Phase started but did not finish')
        self.rows.append(row)
        started = time.monotonic()
        self.save()
        try:
            yield
        except BaseException as error:
            import sys
            location = traceback.extract_tb(sys.exc_info()[2])[-1]
            row.update(status='FAIL', error=traceback.format_exc(), cause='Runtime phase failed; subsequent phases share its database state')
            row.update(file=location.filename, line=location.lineno)
            row['elapsedSeconds'] = round(time.monotonic()-started, 3)
            if isinstance(error, subprocess.TimeoutExpired) or 'MySQL timeout;' in str(error):
                row.update(outcome='TIMEOUT', infrastructure=True)
                if isinstance(error, subprocess.TimeoutExpired):
                    def decoded(value):
                        return value.decode('utf-8', errors='replace') if isinstance(value, bytes) else value or ''
                    row.update(partialStdout=decoded(error.stdout), partialStderr=decoded(error.stderr), timeoutSeconds=error.timeout)
            row['dependents'] = self.phases[len(self.rows):]
            for dependent in row['dependents']:
                blocked = issue('runtime-phase', 'tools/mysql84_runtime.py', 'Blocked by: '+name, test=dependent,
                                status='BLOCKED')
                blocked.update(id=dependent, required=True, cause='Prerequisite failed; contaminated state is not reused')
                self.rows.append(blocked)
            self.save()
            raise
        else:
            row.update(status='PASS', cause='Runtime phase completed')
            row['elapsedSeconds'] = round(time.monotonic()-started, 3)
            self.save()
