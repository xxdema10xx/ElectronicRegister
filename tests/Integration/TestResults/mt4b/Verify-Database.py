import json,subprocess
from pathlib import Path
folder=Path(__file__).resolve().parent
def query(sql):
 value=subprocess.check_output(['C:/xampp/mysql/bin/mysql.exe','--host=127.0.0.1','--user=root','--database=electronic_register_test','--batch','--raw','--skip-column-names','--execute='+sql]).decode('utf-8')
 return [r.split(chr(9)) for r in value.splitlines()]
assert query('SELECT DATABASE()')==[['electronic_register_test']]
holding='4d543341-0000-4000-8000-000000000001'
assert query('SELECT id,code,status FROM institution')==[[holding,'MT3A_LEGACY_TEST','provisioning']]
zero=['campus','course_edition','course_edition_campus','student_enrollment','institution_membership','institution_membership_role','institution_student_profile','institution_teacher_profile']
counts={t:int(query('SELECT COUNT(*) FROM `'+t+'`')[0][0]) for t in zero};assert all(v==0 for v in counts.values()),counts
owned=[r[0] for r in query("SELECT table_name FROM information_schema.columns WHERE table_schema=DATABASE() AND column_name='institution_id' ORDER BY table_name")]
violations={}
for table in owned:
 if table in zero:continue
 condition="institution_id IS NULL OR institution_id<>'"+holding+"'"
 if table=='audit_event':condition="(institution_id IS NOT NULL AND institution_id<>'"+holding+"') OR (institution_id IS NULL AND NOT(entity_type='platform' AND action LIKE 'platform.%' AND entity_id IS NULL AND before_json IS NULL AND after_json IS NULL))"
 violations[table]=int(query('SELECT COUNT(*) FROM `'+table+'` WHERE '+condition)[0][0])
assert not any(violations.values()),violations
fk=query("SELECT constraint_name,delete_rule,update_rule FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE() AND constraint_name LIKE 'fk_mt4_%' ORDER BY constraint_name")
assert len(fk)==7 and all(row[1:]==['RESTRICT','RESTRICT'] for row in fk),fk
composite=query("SELECT constraint_name FROM information_schema.key_column_usage WHERE constraint_schema=DATABASE() AND constraint_name LIKE 'fk_mt4_%' GROUP BY constraint_name HAVING COUNT(*)=2")
assert len(composite)==6
triggers=int(query('SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE()')[0][0]);assert triggers==48
mode=query('SELECT @@GLOBAL.sql_mode')[0][0];assert mode=='NO_ZERO_IN_DATE,NO_ZERO_DATE,NO_ENGINE_SUBSTITUTION'
result={'database':'electronic_register_test','version':query('SELECT VERSION()')[0][0],'holding':'MT3A_LEGACY_TEST Provisioning','temporaryRowCounts':counts,'ownerViolations':violations,'mt4ForeignKeys':fk,'compositeMt4FkCount':len(composite),'triggerCount':triggers,'globalSqlMode':mode}
(folder/'final-db-verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
