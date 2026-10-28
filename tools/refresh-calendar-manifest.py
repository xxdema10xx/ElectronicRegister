"""Refresh the Phase 5 artifact closure; historical manifests and reports are preserved."""
import hashlib,json,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'migrations/bootstrap/manifest.json'
prior=json.loads(p.read_text(encoding='utf-8-sig'));paths=set(prior['files'])
for directory in ['Backend/ElectronicRegisterAPI/src','tests/Unit','tests/Integration','tools']:
    for file in (ROOT/directory).rglob('*'):
        if file.is_file() and ('calendar' in file.name.lower()) and file.suffix in {'.cs','.py'} and not any(x in file.parts for x in ['bin','obj','TestResults','__pycache__']):paths.add(file.relative_to(ROOT).as_posix())
paths.update(x.relative_to(ROOT).as_posix() for x in (ROOT/'migrations/institution-calendar').iterdir() if x.is_file())
paths.update(['docs/architecture/ITS_INSTITUTION_CALENDAR.md','docs/api/INSTITUTION_CALENDAR.md'])
paths.add('Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Entities/InstitutionLessonRecord.cs')
paths.add('Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/InstitutionClassRepository.cs')
files={}
for name in sorted(paths):
    file=ROOT/name;raw=file.read_bytes()
    eol=subprocess.check_output(['git','check-attr','eol','--',name],cwd=ROOT).decode().strip().rsplit(': ',1)[1]
    if eol=='lf' and b'\r\n' in raw:raw=raw.replace(b'\r\n',b'\n');file.write_bytes(raw)
    files[name]=hashlib.sha256(raw).hexdigest().upper()
contract=json.loads((ROOT/'migrations/bootstrap/schema-contract.json').read_text())
value=dict(format=1,source='Institution calendar Phase 5 additive closure; historical manifests preserved',inventory=contract['inventory'],files=files)
payload=(json.dumps(value,indent=2)+'\n').encode();p.write_bytes(payload)
(ROOT/'tests/Integration/INSTITUTION_CALENDAR_MANIFEST.json').write_bytes(payload)
print(len(files),'artifact hashes refreshed')
