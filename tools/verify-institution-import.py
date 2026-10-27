"""Offline institutional bulk-import schema/artifact verifier. Never connects to SQL."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def verify():
    manifest = json.loads((ROOT / 'migrations/bootstrap/manifest.json').read_text(encoding='utf-8'))
    for name, digest in manifest['files'].items():
        path = (ROOT / name).resolve(strict=True)
        assert ROOT in path.parents, name
        assert hashlib.sha256(path.read_bytes()).hexdigest().upper() == digest, name
        assert subprocess.run(['git', 'check-ignore', '--no-index', '-q', name], cwd=ROOT).returncode == 1, name
    contract = json.loads((ROOT / 'migrations/bootstrap/schema-contract.json').read_text(encoding='utf-8'))
    assert contract['inventory'] == manifest['inventory'] == dict(tables=52, triggers=129, fks=158, indexes=218)
    assert len(contract['tables']) == 52 and len(contract['triggers']) == len(contract['trigger_metadata']) == 129
    previous = json.loads((ROOT / 'migrations/institution-import/predecessor-contract.json').read_text(encoding='utf-8'))
    assert previous['inventory'] == dict(tables=48, triggers=117, fks=145, indexes=199)
    for name, ddl in previous['tables'].items():
        assert contract['tables'][name] == ddl, name
    for name, trigger in previous['trigger_metadata'].items():
        assert contract['trigger_metadata'][name] == trigger, name
    expected = {'institution_import_' + suffix for suffix in ('batch', 'item', 'event', 'external_reference')}
    migration = (ROOT / 'migrations/institution-import/001_expand.sql').read_text(encoding='utf-8')
    assert set(re.findall(r'CREATE TABLE (institution_import_\w+)', migration)) == expected
    assert len(re.findall(r'CREATE TRIGGER import_\w+', migration)) == 12
    assert (ROOT / 'migrations/bootstrap/001_schema.sql').read_text(encoding='utf-8').endswith(migration)
    for name in expected:
        assert 'institution_id' in contract['tables'][name]
    for suffix in ('batch', 'item', 'event', 'external'):
        for action in ('update', 'delete'):
            body = contract['trigger_metadata'][f'import_{suffix}_{action}']['ACTION_STATEMENT']
            assert "SIGNAL SQLSTATE '45000'" in body
    assert 'fk_import_batch_policy' in contract['tables']['institution_import_batch']
    for suffix in ('student', 'teacher', 'assignment', 'external', 'batch'):
        assert 'fk_import_item_' + suffix in contract['tables']['institution_import_item']
    assert 'PRIMARY KEY (`institution_id`,`category`,`reference`)' in contract['tables']['institution_import_external_reference']
    assert 'uq_import_event_revision' in contract['tables']['institution_import_event']
    domain = (ROOT / 'Backend/ElectronicRegisterAPI/src/Domain/ElectronicRegisterAPI.Domain/Models/InstitutionImport.cs').read_text(encoding='utf-8')
    fields = re.search(r'enum ImportField\s*\{([^}]+)\}', domain).group(1)
    assert {value.strip() for value in fields.split(',')} == {'Ignore', 'FirstName', 'LastName', 'Class', 'ExternalReference'}
    assert 'JsonUnmappedMemberHandling.Disallow' in domain
    reader = (ROOT / 'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Services/InstitutionImportFileReader.cs').read_text(encoding='utf-8')
    assert 'new UTF8Encoding(false,true)' in reader and 'MaxFileBytes' in reader and 'MaxDataRows' in reader
    workbook = (ROOT / 'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Services/InstitutionImportFileReader.Xlsx.cs').read_text(encoding='utf-8')
    assert 'SpreadsheetDocument.Open' in workbook and 'OpenXmlReader.Create' in workbook
    assert 'DtdProcessing.Prohibit' in workbook and 'XmlResolver=null' in workbook
    assert 'MaxUncompressedBytes' in workbook and 'MaxXmlNodes' in workbook
    assert 'FormulaNotAllowed' in workbook and 'ExternalReferenceNotAllowed' in workbook
    for relative in ('Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/packages.lock.json',
                     'Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/packages.lock.json',
                     'tests/Integration/packages.lock.json', 'tools/PlatformAdmin/packages.lock.json'):
        lock = json.loads((ROOT / relative).read_text(encoding='utf-8'))
        for framework in lock['dependencies'].values():
            assert framework['DocumentFormat.OpenXml']['resolved'] == '3.5.1'
            assert framework['DocumentFormat.OpenXml.Framework']['resolved'] == '3.5.1'
    return len(manifest['files'])


if __name__ == '__main__':
    print(f'{verify()} SHA256 files and institutional bulk-import static contract verified; no SQL executed')
