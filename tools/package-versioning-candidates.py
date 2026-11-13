"""Create a NEW public-only backend reproduction package, without staging/commits.

Only Git-visible source/explicit manifest dependencies are selected. Private,
ignored, diagnostic and build outputs are not inputs. Output inventory is SHA256.
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def candidates():
    names = subprocess.check_output(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'], cwd=ROOT).decode().split('\0')
    paths = set(json.loads((ROOT/'migrations/bootstrap/manifest.json').read_text(encoding='utf-8'))['files'])
    paths.update(['migrations/bootstrap/manifest.json', '.gitignore', '.gitattributes', '.dockerignore', 'README.md',
                  'Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx'])
    for name in filter(None, names):
        p = Path(name)
        if p.name == 'packages.lock.json':
            paths.add(name)
        if name.startswith(('Backend/ElectronicRegisterAPI/src/', 'tests/Unit/', 'tests/Integration/')) and p.suffix in ('.cs', '.csproj', '.props', '.targets', '.resx'):
            paths.add(name)
        if name.startswith(('tools/', 'infra/', '.github/', 'docs/')) and p.suffix in ('.cs', '.csproj', '.json', '.md', '.py', '.ps1', '.yml', '.yaml'):
            paths.add(name)
        if name in ('global.json', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props', 'NuGet.Config', 'infra/Dockerfile'):
            paths.add(name)
    return sorted(paths)


def main():
    destination = Path(sys.argv[1]).absolute().resolve()
    assert not destination.exists(), 'Existing destinations must be preserved'
    paths = candidates()
    for name in paths:
        p = (ROOT/name).resolve(strict=True)
        assert ROOT in p.parents and 'private' not in p.parts and '.local.' not in p.name, name
        assert subprocess.run(['git', 'check-ignore', '--no-index', '-q', name], cwd=ROOT).returncode == 1, 'Ignored dependency: '+name
    destination.mkdir(parents=True)
    hashes = {}
    for name in paths:
        target = destination/name
        target.parent.mkdir(parents=True, exist_ok=True)
        raw = (ROOT/name).read_bytes()
        target.write_bytes(raw)
        hashes[name] = hashlib.sha256(raw).hexdigest().upper()
    (destination/'public-source-manifest.json').write_text(json.dumps(hashes, indent=2)+'\n', encoding='utf-8', newline='\n')
    print(str(len(paths))+' public candidate files packaged; no private inputs, staging or commits')


if __name__ == '__main__':
    main()
