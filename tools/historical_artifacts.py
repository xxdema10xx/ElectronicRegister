"""Verify superseded raw-byte evidence without rewriting historical manifests.

The recorded bytes must exist in Git history (LF or CRLF representation), and
the current file must match the independently versioned NEW release closure.
This proves provenance, not that an old test run covered the new source.
"""
from functools import lru_cache
import hashlib
import json
from pathlib import Path
import subprocess


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


@lru_cache(maxsize=None)
def verify_historical_artifact(root, name, expected):
    root = Path(root).resolve()
    path = (root / name).resolve()
    if root not in path.parents:
        raise ValueError('Artifact escapes repository')
    current = path.read_bytes() if path.exists() else None
    if current is not None and digest(current) == expected:
        return True
    release = json.loads((root / 'migrations/mysql84-remediation/manifest.json').read_text())
    if current is None:
        if name not in release['retiredArtifacts']:
            raise ValueError('Unreviewed missing artifact: ' + name)
    elif digest(current.replace(b'\r\n', b'\n')) != release['files'].get(name):
        raise ValueError('Current release artifact drift: ' + name)
    revisions = subprocess.check_output(['git', 'log', '--format=%H', '--', name], cwd=root, text=True).splitlines()
    for revision in revisions:
        result = subprocess.run(['git', 'show', revision + ':' + name], cwd=root, capture_output=True)
        if result.returncode:
            continue
        lf = result.stdout.replace(b'\r\n', b'\n')
        if expected in {digest(result.stdout), digest(lf), digest(lf.replace(b'\n', b'\r\n'))}:
            return True
    raise ValueError('Historical bytes not found in Git history: ' + name)
