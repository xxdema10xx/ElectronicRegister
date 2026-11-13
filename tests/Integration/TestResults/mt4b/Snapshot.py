import hashlib,json,subprocess,sys
from pathlib import Path
folder=Path('tests/Integration/TestResults/mt4b')
def query(sql):
 return subprocess.check_output(['C:/xampp/mysql/bin/mysql.exe','--host=127.0.0.1','--user=root','--database=electronic_register_test','--batch','--raw','--skip-column-names','--execute='+sql])
assert query('SELECT DATABASE()').strip()==b'electronic_register_test'
result={}
for line in query("SELECT table_name FROM information_schema.tables WHERE table_schema=DATABASE() AND table_type='BASE TABLE' ORDER BY table_name").decode().splitlines():
 names=query("SELECT column_name FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='"+line+"' ORDER BY ordinal_position").decode().splitlines()
 rows=query('SELECT JSON_ARRAY('+','.join('`'+n+'`' for n in names)+') FROM `'+line+'` ORDER BY '+','.join('`'+n+'`' for n in names))
 result[line]={'rows':len(rows.splitlines()),'sha256':hashlib.sha256(rows).hexdigest()}
(folder/('persisted-'+sys.argv[1]+'.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
reference=sys.argv[2] if len(sys.argv)>2 else 'before'
if sys.argv[1]!='before' and reference!='capture':assert result==json.loads((folder/('persisted-'+reference+'.json')).read_text()),'Persisted data changed'
print('Snapshot verified',len(result),'tables')
