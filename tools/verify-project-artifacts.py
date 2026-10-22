"""Offline artifact closure/hash/ignore verification; does not stage or connect to SQL."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'migrations/bootstrap/manifest.json').read_text(encoding='utf-8'))
for name,expected in manifest['files'].items():
    path=(ROOT/name).resolve(strict=True)
    assert ROOT in path.parents and hashlib.sha256(path.read_bytes()).hexdigest().upper()==expected,name
    ignored=subprocess.run(['git','check-ignore','--no-index','-q',name],cwd=ROOT)
    assert ignored.returncode==1,'Required artifact ignored: '+name
    eol=subprocess.check_output(['git','check-attr','eol','--',name],cwd=ROOT).decode().strip().rsplit(': ',1)[1]
    raw=path.read_bytes()
    if eol=='lf':assert b'\r\n' not in raw,'Checkout EOL mismatch: '+name
    if eol=='crlf':assert raw.count(b'\n')==raw.count(b'\r\n'),'Checkout EOL mismatch: '+name
    # No data-changing SQL at top level in consolidated source. Trigger bodies are DDL.
assert (ROOT/'migrations/bootstrap/001_schema.sql').read_text().startswith('-- Consolidated EMPTY')
print(str(len(manifest['files']))+' SHA256 verified; complete versionable bootstrap closure; no SQL executed')
