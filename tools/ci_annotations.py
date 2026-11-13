"""Publish a sanitized failure summary of one diagnostic report as GitHub annotations.

Job logs and artifacts need repository credentials to download; annotations are the
public failure surface. Only method names, outcomes and the first lines of each error are
emitted, with credentials, tokens, connection strings and user paths redacted, in at most
nine annotations per step (GitHub keeps ten).
"""
import json
from pathlib import Path
import re
import sys

REDACTIONS = [
    (re.compile(r'(?i)(password|pwd)\s*=\s*[^;"\'\s]+'), r'\1=***'),
    (re.compile(r'(?i)(user id|uid|server)\s*=\s*[^;"\'\s]+'), r'\1=***'),
    (re.compile(r'eyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}'), '<jwt>'),
    (re.compile(r'(?i)bearer\s+[A-Za-z0-9._-]+'), 'Bearer <token>'),
    (re.compile(r'/home/[^/\s]+'), '<home>'),
    (re.compile(r'[A-Z]:\\Users\\[^\\\s]+'), '<home>'),
]


def sanitize(text):
    for pattern, replacement in REDACTIONS:
        text = pattern.sub(replacement, text)
    return text


def escape(text):
    return text.replace('%', '%25').replace('\r', '%0D').replace('\n', '%0A')


def summary(report, limit=9, size=3500):
    lines = []
    for row in report.get('problems', []):
        if row.get('status') in ('PASS', 'SKIPPED') or not row.get('required', True):
            continue
        name = row.get('test') or row.get('id', '')
        error = ' '.join(sanitize(row.get('error') or '').split())[:420]
        lines.append(row['status'] + ' ' + name.replace('ElectronicRegisterAPI.Tests.Integration.', '') + ' :: ' + error)
    chunks, current = [], ''
    for line in lines:
        if current and len(current) + len(line) + 1 > size:
            chunks.append(current)
            current = ''
        current = (current + '\n' + line).strip()
    if current:
        chunks.append(current)
    if len(chunks) > limit:
        chunks = chunks[:limit - 1] + ['... ' + str(len(lines)) + ' non-passing rows in total; see the job log']
    return chunks


def main():
    path = Path(sys.argv[1])
    if not path.exists():
        print('::error title=diagnostic report missing::' + escape(str(path.name)))
        return 0
    report = json.loads(path.read_text(encoding='utf-8'))
    for index, chunk in enumerate(summary(report)):
        print('::error title=' + escape(str(report.get('suite')) + ' failures ' + str(index + 1)) + '::' + escape(chunk))
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
