import hashlib,json,sys
from pathlib import Path
root=Path.cwd();folder=root/'tests/Integration/TestResults/mt4b'
files=[]
for base in ['Backend/ElectronicRegisterAPI/src','tests/Unit','tests/Integration']:
 for f in (root/base).rglob('*.cs'):
  if not {'bin','obj','TestResults'}.intersection(f.parts): files.append(f)
values={str(f.relative_to(root)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(files)}
file=folder/'frozen-source-hashes.json'
if sys.argv[1]=='freeze':file.write_text(json.dumps(values,indent=2),encoding='utf-8')
else:assert values==json.loads(file.read_text(encoding='utf-8')),'Sources changed after freeze'
print('Source snapshot verified',len(values),'files')
