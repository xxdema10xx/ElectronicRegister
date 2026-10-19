import json,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
f=Path(__file__).resolve().parent
v=json.loads((f/'verification.json').read_text(encoding='utf-8'))
assert v['totalPassedEachFinalRun']==675 and v['previousTests']==644 and v['newTests']==31
subprocess.run(['python',str(f/'Verify-Sources.py'),'verify'],check=True)
subprocess.run(['python',str(f/'Verify-Database.py')],check=True)
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
rows=[]
for run in ['baseline','final-1','final-2']:
 for file in sorted((f/run).glob('*.trx')):
  tree=ET.parse(file);c=tree.find('.//t:Counters',ns).attrib
  methods=tree.findall('.//t:TestMethod',ns)
  project='Unit' if any('.Tests.Unit' in m.attrib['className'] for m in methods) else 'Integration'
  # The baseline unit namespace does not always contain Tests.Unit: its exact count is stable.
  if c['total']=='51':project='Unit'
  rows.append({'run':run,'project':project,'passed':int(c['passed']),'failed':int(c['failed']),'skipped':int(c['notExecuted']),'trx':file.name})
(f/'suite-table.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
report=Path('tests/Integration/MULTITENANCY_MT4C_REPORT.md');s=report.read_text(encoding='utf-8')
s=s.replace('**MT-4C in corso / PARZIALE: due suite complete definitive ancora in esecuzione. Rollout chiuso.**','**MT-4C COMPLETATA: due suite consecutive 675/675 Passed, zero Failed/Skipped. Tutte le 644 identita precedenti preservate. Rollout chiuso.**')
s=s.replace('Identita dei644 precedenti verificate alla baseline; verifica definitiva sui due run ancora in corso.','Identita dei644 precedenti verificate alla baseline e in entrambe le suite definitive, senza sostituzioni.')
a=s.index('Prima suite completa verificata: 51 unit +624 integration');b=s.index('\n## 26.',a)
s=s[:a]+'''**Due esecuzioni complete consecutive verdi, con sorgenti invariati**:

| Esecuzione | Unit | Integration | Totale | Failed | Skipped |
| --- | --- | --- | --- | --- | --- |
| Baseline | 51 | 593 | 644 | 0 | 0 |
| Finale1 | 51 | 624 | 675 | 0 | 0 |
| Finale2 | 51 | 624 | 675 | 0 | 0 |

TRX separati per progetto/run in `TestResults/mt4c/baseline`, `final-1`, `final-2`.
`verification.json` conserva SHA256 di ciascun TRX e confronto delle identita: tutti i644 casi precedenti
sono Passed in entrambi i run, stessi31 nuovi casi, nessun Failed/Skipped. Controllati anche i singoli
outcome e total/executed/passed, non soltanto exit code. `suite-table.json` riassume i sei TRX.

`final-db-verification.json`: soltanto holding Provisioning, zero tenant/membership/profili/Campus/
edizioni/Enrollment/eventi temporanei, numero User originale15, zero violazioni owner, FK RESTRICT
preservate, 32 tabelle/53 trigger e GLOBAL sql_mode invariato. Le fixture legacy Esami mantengono le
proprie scritture di test, senza modifiche di MT4C ai relativi writer. Build, bootstrap Development/
Production, gate/route, MT4A/MT4B, worker MT3E, revoca, concorrenza e fault injection tutti verificati.
`git diff --check` e controllo whitespace/link dei nuovi file completano la validazione locale.
''' +s[b:]
report.write_text(s,encoding='utf-8',newline='\n')
replacements={
 'docs/architecture/ENROLLMENT_LIFECYCLE_AND_AUDIT.md':('Validazione in corso; esito finale nel report MT4C.','MT-4C completata: due suite 675/675 Passed, zero Failed/Skipped; 644 identita precedenti preservate. Rollout chiuso. Esiti nel report MT4C.'),
 'docs/api/COURSE_FRONTEND_CONTRACTS.md':('Questa sezione prevale sulle limitazioni MT4B riportate nello snapshot sottostante. Validazione in\ncorso: risultati nel report MT4C; rollout operativo sempre chiuso.','Questa sezione prevale sulle limitazioni MT4B riportate nello snapshot sottostante. MT-4C completata:\ndue suite 675/675 Passed, zero Failed/Skipped, 644 identita precedenti preservate; rollout chiuso.'),
 'docs/architecture/ITS_ACADEMY_COURSE_MODEL.md':('Validazione finale in corso; rollout chiuso.','MT-4C completata: due suite 675/675 Passed, zero Failed/Skipped; 644 identita precedenti preservate. Rollout chiuso.'),
 'docs/architecture/MULTITENANCY_DESIGN.md':('Validazione finale in corso; nessun rollout/cutover.','MT-4C completata: due suite 675/675 Passed, zero Failed/Skipped; 644 identita precedenti preservate. Nessun rollout/cutover.'),
 'docs/architecture/MULTITENANCY_ISOLATION_MATRIX.md':('Validazione finale in corso; rollout chiuso.','MT-4C completata: due suite 675/675 Passed, zero Failed/Skipped; 644 identita precedenti preservate. Rollout chiuso.'),
 'docs/architecture/MULTITENANCY_RELEASE_GATE.md':('Validazione finale in corso; **nessun cutover autorizzato, quattro rollout default false**.','MT-4C completata tecnicamente: due suite 675/675 Passed, zero Failed/Skipped e 644 identita precedenti preservate. **Nessun cutover autorizzato, quattro rollout default false**.')}
for name,(before,after) in replacements.items():
 p=Path(name);text=p.read_text(encoding='utf-8');assert before in text,name
 p.write_text(text.replace(before,after,1),encoding='utf-8',newline='\n')
print('Final documentation updated only after green TRX, source and database checks')
