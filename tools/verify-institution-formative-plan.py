from calendar_schema_contract import phase4_contract
"""Offline Phase 4 artifact/schema/evidence verifier. Never executes SQL or HTTP.

Hash/contract checks do not constitute execution evidence; TRX files, if available,
are checked separately against recorded outcomes. Never infer coverage from test names.
"""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def require(value,message):
    if not value: raise ValueError(message)


def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def verify():
    manifest=json.loads((ROOT/'migrations/bootstrap/manifest.json').read_text(encoding='utf-8-sig'))
    for name,sha in manifest['files'].items():
        relative=Path(name)
        require(not relative.is_absolute() and '..' not in relative.parts,'Unsafe manifest path')
        path=(ROOT/relative).resolve(strict=True)
        require(ROOT in path.parents and digest(path)==sha,'Digest mismatch: '+name)
        require(subprocess.run(['git','check-ignore','--no-index','-q',name],cwd=ROOT).returncode==1,'Ignored artifact: '+name)
    contract=json.loads((ROOT/'migrations/bootstrap/schema-contract.json').read_text(encoding='utf-8-sig'))
    require(contract['inventory']==manifest['inventory'],'Manifest inventory mismatch')
    contract=phase4_contract(ROOT,contract)
    previous=json.loads((ROOT/'migrations/institution-formative-plan/predecessor-contract.json').read_text(encoding='utf-8-sig'))
    expected=dict(tables=60,triggers=153,fks=171,indexes=257)
    require(contract['inventory']==expected,'Wrong schema inventory')
    require(previous['inventory']==dict(tables=52,triggers=129,fks=158,indexes=218),'Wrong predecessor')
    require(len(contract['tables'])==60 and len(contract['triggers'])==len(contract['trigger_metadata'])==153,'Incomplete schema contract')
    for key in ('tables','trigger_metadata','triggers'):
        for name,value in previous[key].items(): require(contract[key][name]==value,'Legacy schema changed: '+name)
    tables={'formative_plan','formative_plan_version','formative_unit','formative_module',
            'formative_module_competency','formative_module_content','course_edition_formative_plan','formative_plan_event'}
    require(set(contract['tables'])-set(previous['tables'])==tables,'Wrong additive table set')
    migration=(ROOT/'migrations/institution-formative-plan/001_expand.sql').read_text(encoding='utf-8')
    require(set(re.findall(r'CREATE TABLE (\w+)',migration))==tables,'Migration table mismatch')
    require(len(re.findall(r'CREATE TRIGGER ',migration))==24,'Migration trigger mismatch')
    require(len(re.findall(r'FOREIGN KEY\(',migration))==13,'Migration FK mismatch')
    require('CHECK (' not in migration and 'DROP ' not in migration,'Unsupported or destructive SQL')
    bootstrap=(ROOT/'migrations/bootstrap/001_schema.sql').read_text(encoding='utf-8')
    require(migration in bootstrap,'Bootstrap is missing exact additive SQL suffix')
    for name in tables: require('`institution_id`' in contract['tables'][name],'Missing owner: '+name)
    for prefix in ('fu','fm','fmc','fmt'):
        for action in ('insert','update','delete'):
            body=contract['trigger_metadata'][prefix+'_'+action]['ACTION_STATEMENT']
            require('FOR UPDATE' in body and "version_state<>'Draft'" in body,'Child immutability lock missing')
    for prefix in ('fpe','cefp'):
        for action in ('update','delete'):
            require("SIGNAL SQLSTATE '45000'" in contract['trigger_metadata'][prefix+'_'+action]['ACTION_STATEMENT'],'Immutable history missing')
    require('fk_fm_subject' in contract['tables']['formative_module'],'Missing Subject FK')
    require('fk_cefp_version' in contract['tables']['course_edition_formative_plan'],'Missing binding FK')
    require('uq_fpv_number' in contract['tables']['formative_plan_version'],'Missing version uniqueness')
    evidence=json.loads((ROOT/'tests/Integration/INSTITUTION_FORMATIVE_PLAN_VERIFICATION.json').read_text(encoding='utf-8-sig'))
    require(evidence['status'] in ('IMPLEMENTED_AND_VERIFIED','IMPLEMENTED_PENDING_VERIFICATION'),'Unexpected evidence status')
    require(evidence['database']['inventory']==expected,'Evidence/schema inventory mismatch')
    checked=0
    for result in evidence['tests']['successfulRuns']:
        path=ROOT/result['path']
        if not path.exists(): continue
        require(digest(path)==result['sha256'],'TRX digest mismatch')
        root=ET.parse(path).getroot();namespace={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        counters=root.find('t:ResultSummary/t:Counters',namespace)
        require(counters is not None and int(counters.attrib['failed'])==0,'TRX failed')
        require(int(counters.attrib['passed'])==result['passed'] and int(counters.attrib['total'])==result['passed'],'TRX count mismatch or skipped tests')
        checked+=1
    print(f"{len(manifest['files'])} SHA256 artifacts and Phase 4 static SQL contracts verified offline; "
          f'{checked} recorded TRX checked locally. No SQL/HTTP executed by this validator.')


if __name__=='__main__': verify()
