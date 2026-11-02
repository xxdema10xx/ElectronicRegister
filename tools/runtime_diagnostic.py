"""Durable diagnostics for sequential phases sharing a disposable database."""
from contextlib import contextmanager
import json
from pathlib import Path
import traceback
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
        self.save()
        try:
            yield
        except BaseException:
            import sys
            location = traceback.extract_tb(sys.exc_info()[2])[-1]
            row.update(status='FAIL', error=traceback.format_exc(), cause='Runtime phase failed; subsequent phases share its database state')
            row.update(file=location.filename, line=location.lineno)
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
            self.save()
