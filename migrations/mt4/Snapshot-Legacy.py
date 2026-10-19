# Run locally before EXPAND and immediately after VALIDATE, while writers are stopped.
# No row contents are written; manifest fixes the original column set across EXPAND.
import hashlib,json,subprocess,sys
from pathlib import Path
client='C:/xampp/mysql/bin/mysql.exe'
def query(sql):
    return subprocess.check_output([client,'--host=127.0.0.1','--user=root','--database=electronic_register_test','--batch','--raw','--skip-column-names','--execute='+sql])
assert query('SELECT DATABASE()').strip()==b'electronic_register_test'
folder=Path('tests/Integration/TestResults/mt4a');folder.mkdir(parents=True,exist_ok=True)
manifest=folder/'legacy-columns.json'
if sys.argv[1]=='before':
    columns={}
    for line in query("SELECT table_name,column_name FROM information_schema.columns WHERE table_schema=DATABASE() ORDER BY table_name,ordinal_position").decode().splitlines():
        table,column=line.split('\t');columns.setdefault(table,[]).append(column)
    manifest.write_text(json.dumps(columns,indent=2))
else: columns=json.loads(manifest.read_text())
result={}
for table,names in columns.items():
    rows=query('SELECT JSON_ARRAY('+','.join('`'+n+'`' for n in names)+') FROM `'+table+'` ORDER BY '+','.join('`'+n+'`' for n in names))
    result[table]={'rows':len(rows.splitlines()),'sha256':hashlib.sha256(rows).hexdigest()}
(folder/('legacy-'+sys.argv[1]+'.json')).write_text(json.dumps(result,indent=2))
if sys.argv[1]!='before':
    assert result==json.loads((folder/'legacy-before.json').read_text()),'Legacy data changed'
print('Legacy manifest verified:',len(result),'tables')
