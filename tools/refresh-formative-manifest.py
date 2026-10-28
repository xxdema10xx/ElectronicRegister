"""Refresh raw SHA256 closure after reviewing Phase 4 changes. No SQL or Git mutation."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
manifest_path=ROOT/'migrations/bootstrap/manifest.json'
prior=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
paths=set(prior['files'])
source=ROOT/'Backend/ElectronicRegisterAPI/src'
paths.update(p.relative_to(ROOT).as_posix() for p in source.rglob('*Formative*.cs'))
paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'tests/Unit').glob('Formative*.cs'))
paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'tests/Integration').glob('InstitutionFormative*.cs'))
paths.update(p.relative_to(ROOT).as_posix() for p in (ROOT/'migrations/institution-formative-plan').glob('*') if p.is_file())
paths.update([
 '.gitignore','.gitattributes','docs/api/INSTITUTION_FORMATIVE_PLANS.md','docs/architecture/ITS_FORMATIVE_PLAN_VERSIONING.md',
 'docs/development/INSTITUTION_ADMIN_COVERAGE_REPORT.md','tools/formative_test_db.py','tools/generate-formative-schema.py',
 'tools/refresh-formative-manifest.py','tools/test_formative_tools.py','tools/verify-institution-formative-plan.py',
 'tools/verify-institution-staff.py','tools/verify-institution-email.py','tools/verify-institution-import.py',
 'Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/Program.cs',
 'Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/appsettings.example.json',
 'Backend/ElectronicRegisterAPI/src/Application/ElectronicRegisterAPI.Application/DependencyInjection/AddApplicationManagers.cs',
 'Backend/ElectronicRegisterAPI/src/Business/ElectronicRegisterAPI.Business/DependencyInjection/ServiceCollectionExtensions.cs',
 'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs',
 'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.Multitenancy.cs',
 'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt3aOwnershipMigration.cs',
 'migrations/bootstrap/schema-contract.json','migrations/bootstrap/001_schema.sql'])
files={}
for name in sorted(paths):
    path=ROOT/name
    if not path.is_file(): raise ValueError('Missing artifact: '+name)
    raw=path.read_bytes()
    attribute=subprocess.check_output(['git','check-attr','eol','--',name],cwd=ROOT).decode().strip().rsplit(': ',1)[1]
    if attribute=='lf':
        normalized=raw.replace(b'\r\n',b'\n')
        if normalized!=raw: path.write_bytes(normalized);raw=normalized
    files[name]=hashlib.sha256(raw).hexdigest().upper()
contract=json.loads((ROOT/'migrations/bootstrap/schema-contract.json').read_text(encoding='utf-8-sig'))
value=dict(format=1,source='ITS formative plans Phase 4; legacy artifacts preserved; no report self-reference',inventory=contract['inventory'],files=files)
payload=(json.dumps(value,indent=2)+'\n').encode('utf-8')
manifest_path.write_bytes(payload)
(ROOT/'tests/Integration/INSTITUTION_FORMATIVE_PLAN_MANIFEST.json').write_bytes(payload)
print(f'{len(files)} artifact hashes refreshed; manifest SHA256 '+hashlib.sha256(payload).hexdigest().upper())
