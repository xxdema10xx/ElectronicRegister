import hashlib,json,subprocess,sys
from pathlib import Path
p=Path(__file__).resolve().parent
def query(sql):return subprocess.check_output(['C:/xampp/mysql/bin/mysql.exe','--host=127.0.0.1','--user=root','--database=electronic_register_test','--batch','--raw','--skip-column-names','--execute='+sql])
assert query('SELECT DATABASE()').strip()==b'electronic_register_test'
if sys.argv[1]=='before':
 cols={}
 for line in query('SELECT table_name,column_name FROM information_schema.columns WHERE table_schema=DATABASE() ORDER BY table_name,ordinal_position').decode().splitlines():
  table,column=line.split(chr(9));cols.setdefault(table,[]).append(column)
 (p/'legacy-columns.json').write_text(json.dumps(cols,indent=2),encoding='utf-8')
else:cols=json.loads((p/'legacy-columns.json').read_text(encoding='utf-8'))
result={}
for table,columns in cols.items():
 rows=query('SELECT JSON_ARRAY('+','.join('`'+c+'`' for c in columns)+') FROM `'+table+'` ORDER BY '+','.join('`'+c+'`' for c in columns))
 result[table]={'rows':len(rows.splitlines()),'sha256':hashlib.sha256(rows).hexdigest()}
(p/('legacy-'+sys.argv[1]+'.json')).write_text(json.dumps(result,indent=2),encoding='utf-8')
if sys.argv[1]!='before':assert result==json.loads((p/'legacy-before.json').read_text(encoding='utf-8')),'Legacy cells changed'
print('Original column snapshot verified',len(result),'tables')
