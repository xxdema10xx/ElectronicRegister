"""Aggregate provenance audit. Never repairs or re-signs historical evidence."""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import re
import subprocess


def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root)


def sha(data):
    return hashlib.sha256(data).hexdigest().upper()


def variants(data):
    lf = data.replace(b'\r\n', b'\n')
    plain = lf.removeprefix(b'\xef\xbb\xbf')
    return {label: sha(value) for label, value in {
        'raw': data, 'LF': lf, 'CRLF': lf.replace(b'\n', b'\r\n'),
        'noBOM-LF': plain, 'BOM-LF': b'\xef\xbb\xbf'+plain,
        'noBOM-CRLF': plain.replace(b'\n', b'\r\n'),
        'BOM-CRLF': b'\xef\xbb\xbf'+plain.replace(b'\n', b'\r\n')}.items()}


def claims(value):
    if isinstance(value, dict):
        name = value.get('path', value.get('file'))
        expected = value.get('sha256', value.get('hash'))
        if isinstance(name, str) and isinstance(expected, str) and re.fullmatch('[a-fA-F0-9]{64}', expected):
            yield name, expected.upper()
        for key, child in value.items():
            if isinstance(child, str) and re.fullmatch('[a-fA-F0-9]{64}', child) and ('/' in key or '.' in key):
                yield key, child.upper()
            yield from claims(child)
    elif isinstance(value, list):
        for child in value:
            yield from claims(child)


def issue(category, file, error, test='artifact-audit', status='FAIL', **extra):
    return dict(id='', category=category, file=file, line=1, test=test, error=error,
                cause='See byte variants and Git blob provenance', priority='P1', status=status,
                dependents=[], **extra)


def audit(root):
    root = Path(root).resolve()
    tracked = set(git(root, 'ls-files', '-z').decode().split('\0')) - {''}
    release_path = 'migrations/mysql84-remediation/manifest.json'
    release = json.loads((root/release_path).read_text(encoding='utf-8-sig'))
    current = release['files']; retired = set(release.get('retiredArtifacts', []))
    declarations = defaultdict(set)
    problems = []; records = []
    for source in sorted(tracked):
        if not source.endswith('.json') or not any(x in source.lower() for x in ('manifest', 'verification', 'report', 'hash')):
            continue
        try:
            data = json.loads((root/source).read_text(encoding='utf-8-sig'))
            for name, expected in claims(data):
                declarations[name, expected].add(source)
        except (ValueError, OSError) as error:
            problems.append(issue('manifest-read', source, str(error), state='active' if source == release_path else 'historical'))
    references = []
    for source in sorted(tracked):
        if not source.endswith('.md') or not ('report' in source.lower() or source.startswith('docs/quality/')):
            continue
        try:
            text = (root/source).read_text(encoding='utf-8', errors='replace')
        except OSError as error:
            problems.append(issue('report-read', source, str(error), state='historical'))
            continue
        for line_number, line in enumerate(text.splitlines(), 1):
            paths = re.findall(r'`((?:tests|tools|migrations|docs|\.staff-private|\.formative-private)/[^`\s]+)`', line)
            hashes = re.findall(r'\b[A-Fa-f0-9]{64}\b', line)
            if len(paths) == 1 and len(hashes) == 1:
                declarations[paths[0], hashes[0].upper()].add(source)
            for name in paths:
                target=(root/name).resolve()
                if root not in target.parents: continue
                references.append(dict(file=name, source=source, line=line_number,
                                       exists=target.is_file(), tracked=name in tracked,
                                       included=name in current, state='retired' if name in retired else 'historical'))
                if not target.is_file():
                    problems.append(issue('report-reference-missing', name, 'Report reference missing: '+source+':'+str(line_number), state='historical', sources=[source]))
    objects = defaultdict(set)
    # Raw history retains every pathname, including identical blobs at multiple paths.
    history = git(root, '-c', 'core.quotepath=false', 'log', '--all', '--root', '--raw', '--no-renames', '--no-abbrev', '--format=')
    for row in history.decode('utf-8').splitlines():
        match = re.match(r'^:[0-9]+ [0-9]+ ([a-f0-9]+) ([a-f0-9]+) [A-Z][0-9]*\t(.+)$', row)
        if match:
            before, after, name = match.groups()
            objects[name].update(oid for oid in (before, after) if set(oid) != {'0'})
    blob_cache = {}
    wanted = sorted({oid for name, _ in declarations for oid in objects.get(name, ())})
    if wanted:
        stream = subprocess.check_output(['git', 'cat-file', '--batch'], cwd=root,
                                         input=('\n'.join(wanted)+'\n').encode())
        offset = 0
        for oid in wanted:
            end = stream.index(b'\n', offset)
            header = stream[offset:end].split()
            length = int(header[2]); offset = end+1
            blob_cache[oid] = variants(stream[offset:offset+length])
            offset += length+1
    for (name, expected), sources in sorted(declarations.items()):
        path = (root/name).resolve()
        if root not in path.parents:
            problems.append(issue('unsafe-path', name, 'Historical reference outside repository; not read', sources=sorted(sources), state='active' if release_path in sources else 'historical')); continue
        try:
            raw = path.read_bytes() if path.is_file() else None
        except OSError as error:
            problems.append(issue('artifact-read', name, str(error), state='active' if release_path in sources else 'historical'))
            raw = None
        forms = variants(raw) if raw is not None else {}
        historical = []; canonical = []; transformations = []
        # Exact Git blobs are authoritative; EOL/BOM variants are diagnostic only.
        for oid in sorted(objects.get(name, ())):
            if oid not in blob_cache:
                blob_cache[oid] = variants(git(root, 'cat-file', 'blob', oid))
            if blob_cache[oid]['LF'] == expected: canonical.append(oid)
            matches = [label for label, value in blob_cache[oid].items() if value == expected]
            if 'raw' in matches: historical.append(oid)
            elif matches: transformations.append(dict(blob=oid, variants=matches))
        state = 'retired' if name in retired else 'active' if release_path in sources else 'historical'
        record = dict(file=name, expected=expected, actual=forms.get('raw'), variants=forms,
                      sources=sorted(sources), state=state, tracked=name in tracked,
                      cleanCheckout=name in tracked and bool(objects.get(name)),
                      included=name in current, exactHistoricalBlobs=historical, canonicalHistoricalBlobs=canonical,
                      transformedHistoricalBlobs=transformations,
                      historicalCertification='RECOVERABLE_EXACT_BYTES' if historical else 'NON_CERTIFIABLE',
                      byteProfile=dict(bom=raw.startswith(b'\xef\xbb\xbf'), crlf=raw.count(b'\r\n'), lf=raw.count(b'\n')-raw.count(b'\r\n')) if raw is not None else None)
        records.append(record)
        def add(kind, message):
            problems.append(issue(kind, name, message, expected=expected, sources=sorted(sources), state=state))
        if raw is None: add('missing', 'Declared artifact missing from checkout')
        elif forms['raw'] != expected: add('encoding-difference' if state == 'active' and forms['LF'] == expected else 'hash-mismatch', 'Expected '+expected+'; actual '+forms['raw'])
        if not historical and not (state == 'active' and canonical): add('historical-bytes-unrecoverable', 'No exact Git blob; transformed candidates: '+json.dumps(transformations))
        if name not in tracked: add('clean-checkout-unavailable', 'Not tracked; unavailable in a clean checkout')
        if raw is not None and name not in current and name not in retired: add('closure-omission', 'Not included in new closure or retired inventory')
        if state == 'retired' and name in current: add('state-conflict', 'Both active and retired')
        if release_path in sources and raw is not None and sha(raw.replace(b'\r\n', b'\n')) != expected:
            add('active-drift', 'Current normalized bytes differ from new release closure')
    changed = set(git(root, 'diff', '--name-only', 'HEAD').decode().splitlines())
    changed.update(git(root, 'ls-files', '--others', '--exclude-standard').decode().splitlines())
    for name in sorted(changed):
        if name.startswith(('tools/', 'Backend/', 'tests/', 'infra/')) and name.endswith(('.py','.cjs','.cs','.sql','.json','.csproj')) and name not in current:
            problems.append(issue('unmanifested-change', name, 'Changed release input absent from new manifest', state='active'))
    for index, problem in enumerate(problems, 1): problem['id'] = 'ART-'+str(index).zfill(5)
    return dict(format=1, revision=git(root, 'rev-parse', 'HEAD').decode().strip(), artifacts=records, references=references, problems=problems,
                certification='Historical records are provenance only; never certify this release')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(args.root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(len(result['artifacts']), 'declarations;', len(result['problems']), 'anomalies')
    return int(bool(result['problems']))


if __name__ == '__main__':
    raise SystemExit(main())
