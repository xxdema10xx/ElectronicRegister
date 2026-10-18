"""Build MT3A documentation from captured information_schema, never from assumptions.
No DB access. Emits only schema metadata, counts and digests (never cell values).
"""
from pathlib import Path
from collections import defaultdict
import csv, json, hashlib

BACKEND=Path(__file__).resolve().parents[2]
REPO=BACKEND.parents[1]
E=BACKEND/'mt3a-evidence'
S=BACKEND/'mt3a-staging'
def rows(name):
    with (E/name).open(encoding='utf-8-sig',newline='') as f: return list(csv.DictReader(f,delimiter='\t'))
columns=rows('schema-columns.tsv'); indexes=rows('schema-indexes.tsv'); fks=rows('schema-fks.tsv')
tables=sorted(set(r['TABLE_NAME'] for r in columns))
assert len(tables)==28, tables
ix=defaultdict(list); fk=defaultdict(list)
for r in indexes: ix[(r['TABLE_NAME'],r['INDEX_NAME'])].append(r)
for r in fks: fk[(r['TABLE_NAME'],r['CONSTRAINT_NAME'])].append(r)
for values in ix.values(): values.sort(key=lambda r:int(r['SEQ_IN_INDEX']))
for values in fk.values(): values.sort(key=lambda r:int(r['ORDINAL_POSITION']))

PATHS={
 'user':('A GLOBAL_IDENTITY','Credenziali globali; User.Id','User.Role e link Student/Teacher sono solo bridge legacy; CRUD account richiede membership autorizzata in C.'),
 'institution':('D PLATFORM_INTERNAL','Registro tenant; id identifica la Fondazione','Non e identita globale autorizzante. Stato/ruoli verificati da MT2; attivazione/onboarding fuori scope.'),
 'campus':('B TENANT_OWNED_DIRECT','institution_id -> Institution','FK RESTRICT MT1; autorizzazione caller nel repository/service.'),
 'institution_membership':('B TENANT_OWNED_DIRECT','institution_id -> Institution; user_id -> User globale','SQL non autorizza ruolo/stato; MT2 rilegge Institution/Membership Active. Non spostare identita/istituto con CRUD generico.'),
 'institution_membership_role':('C TENANT_OWNED_INDIRECT','membership_id -> Membership -> Institution','Singolo parent rigoroso: InstitutionId duplicato non necessario. Nessun ruolo globale.'),
 'institution_student_profile':('B TENANT_OWNED_DIRECT','Institution + Membership + Student dello stesso istituto','Composito aggiuntivo allo Student, UNIQUE(student_id) conservata. Ruolo Student e identita caller verificati applicativamente.'),
 'institution_teacher_profile':('B TENANT_OWNED_DIRECT','Institution + Membership + Teacher dello stesso istituto','Composito aggiuntivo al Teacher, UNIQUE(teacher_id) conservata. Ruolo Teacher e identita caller verificati applicativamente.'),
 'biennium':('B TENANT_OWNED_DIRECT','InstitutionId diretto, manifest legacy esplicito','Coppia date unica per fondazione; significato del biennio invariato. Nessuna CourseEdition.'),
 'study_area':('B TENANT_OWNED_DIRECT','InstitutionId diretto, manifest legacy esplicito','Catalogo modificabile della fondazione; nome unico nel tenant. Nessun catalogo globale implicito.'),
 'study_path':('B TENANT_OWNED_DIRECT','InstitutionId diretto, manifest legacy esplicito','Catalogo modificabile della fondazione; nome unico nel tenant.'),
 'subject':('B TENANT_OWNED_DIRECT','InstitutionId diretto, manifest legacy esplicito','Nome unico nel tenant; stesso nome A/B, record e Guid distinti.'),
 'teacher':('B TENANT_OWNED_DIRECT','InstitutionId diretto; membership opzionale tramite link','Profilo storico senza User attribuito al manifest, senza inventare membership. Ownership immutabile.'),
 'student':('B TENANT_OWNED_DIRECT','InstitutionId diretto; Class coerente se presente; membership opzionale','class_institution_id nullable preserva SET NULL, trigger lo uguaglia al proprietario quando ClassId presente. Nessun enrollment nuovo.'),
 'biennium_study_area':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da Biennium, confrontato con StudyArea','Entrambi i parent devono essere dello stesso tenant; CASCADE Biennium e RESTRICT Area conservati.'),
 'biennium_study_path':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da BienniumStudyArea, confrontato con StudyPath','Entrambi i parent vincolati. Non reinterpretato come CourseEdition.'),
 'class':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da BienniumStudyPath','Class A -> percorso B impedito da FK composita.'),
 'class_subject':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da Class; Subject e Teacher concordi','Tre FK composite; ruoli correnti docente e diritti sul gruppo restano applicativi.'),
 'exam':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da ClassSubject; retake stesso tenant','retake_institution_id preserva SET NULL. Cicli/self-retake, stato, disponibilita e calcolo restano regole applicative.'),
 'exam_student':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da Exam; Student concorde','Ponte con PK originale (ExamId,StudentId); due FK composite. Iscrizione/classe ammessa resta applicativa.'),
 'exam_submission':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da Exam; Student e Grade concordi','grade_institution_id preserva SET NULL. Reviewer e User globale: ruolo/membership al momento della review da verificare in D.'),
 'grade':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da Student; ClassSubject concorde','Due FK composite. Coerenza stesso studente/materia del Grade ufficiale entro tenant resta applicativa; formule immutate.'),
 'question':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da Exam','Chiave (istituto,esame,id) per vincolare le risposte allo stesso Exam.'),
 'question_option':('B TENANT_OWNED_DIRECT','InstitutionId materializzato da Question','Chiave (istituto,domanda,id) per opzioni esatte; correttezza/grading e cardinalita selezioni restano applicativi.'),
 'submission_answer':('B TENANT_OWNED_DIRECT','InstitutionId + exam_id materializzati dalla Submission','FK triple a Submission e Question: stessa fondazione E stesso Exam, anche entro il medesimo tenant.'),
 'submission_answer_option':('B TENANT_OWNED_DIRECT','InstitutionId + question_id materializzati da Answer','Ponte senza DbSet esplicito: FK triple ad Answer e Option garantiscono la domanda esatta. Mapping dictionary legacy conservato.'),
 'exam_violation':('B TENANT_OWNED_DIRECT','InstitutionId materializzato dalla Submission','Un parent sarebbe sufficiente per ownership indiretta; scelta materializzazione uniforme e FK esplicita, senza UNIQUE tenant/id superflua.'),
 'audit_event':('E REQUIRES_DECISION (polimorfica: B oppure D per riga)','Exam/ExamSubmission -> Institution; platform esplicito -> NULL','Entita nota: B, owner storico immutabile anche dopo cancellazione. Solo platform.* senza EntityId/JSON: D. Sconosciuti non diventano globali: preflight/trigger rifiutano.'),
 'mt3a_legacy_scope':('D PLATFORM_INTERNAL','Manifest SQL esplicito -> Institution provisioning di test','ENUM/PK singleton legacy, FK RESTRICT. Solo compatibilita root legacy: nessun TenantContext HTTP, diritto globale o provisioning User.'),
}
assert set(PATHS)==set(tables)

def collist(rs,key='COLUMN_NAME'): return ','.join(r[key] for r in rs)
summary=['| Tabella | Categoria | Percorso di attribuzione |','| --- | --- | --- |']
full=['# MT-3A: inventario effettivo MySQL e indici', '',
 'Rilevato su `electronic_register_test`, MySQL 5.7.24, dopo 003-011. **28 tabelle: 27 preesistenti e un manifest interno.**',
 'Le categorie sono A GLOBAL_IDENTITY, B TENANT_OWNED_DIRECT, C TENANT_OWNED_INDIRECT, D PLATFORM_INTERNAL, E REQUIRES_DECISION.',
 'Audit e intenzionalmente polimorfico: E indica classificazione obbligatoria per riga; i casi accettati sono B (didattico) oppure D (metadati platform). Non restano eventi storici non classificati nel dataset migrato.',
 'Tutte le 19 tabelle scolastiche, incluse le dipendenze/ponte, materializzano ownership B NOT NULL. Per i ponti multi-parent serve a chiudere le FK; per dipendenze con un solo parent e una scelta uniforme e verificabile, con costo di spazio/scrittura esplicito. Il solo ponte ruoli MT1 resta C, rigorosamente derivato dal singolo parent.',
 'La sezione per tabella riporta **tutte** le PK/UNIQUE/FK effettive, comprese le FK legacy conservate. Le nuove FK sono identificate da `fk_mt3a_`. I controlli applicativi comuni sono autorizzazione caller+tenant, stato/ruoli e filtri SQL in ogni query, ancora necessari prima di C/D/E.', '']
for table in tables:
    category,path,app=PATHS[table]
    summary.append(f'| `{table}` | {category} | {path} |')
    pks=ix[(table,'PRIMARY')]
    uniques=[(name,rs) for (t,name),rs in ix.items() if t==table and name!='PRIMARY' and rs[0]['NON_UNIQUE']=='0']
    relations=[(name,rs) for (t,name),rs in fk.items() if t==table]
    full += [f'## {table}', '',f'- **PK:** `({collist(pks)})`.',
             '- **UNIQUE:** '+('; '.join(f'`{name}({collist(rs)})`' for name,rs in uniques) or 'nessuna oltre PK')+'.',
             f'- **Ownership / attribuzione:** {category}; {path}.',
             f'- **Rischio, modifica e limite applicativo:** {app}',
             '- **FK SQL effettive:**']
    if not relations: full.append('  nessuna; ownership/catalogo o identita come indicato sopra.')
    for name,rs in relations:
        first=rs[0]
        full.append(f'  - `{name}`: `({collist(rs)}) -> {first["REFERENCED_TABLE_NAME"]}({collist(rs,"REFERENCED_COLUMN_NAME")})`; DELETE {first["DELETE_RULE"]}, UPDATE {first["UPDATE_RULE"]}.')
    full.append('')

full += ['## Indici nuovi o con scope modificato: analisi per indice', '',
 'Ogni riga descrive un B-tree mantenuto a ogni INSERT/DELETE e UPDATE delle colonne della chiave. I Guid sono CHAR(36) ASCII: circa 36 byte per componente Guid, oltre overhead InnoDB e PK; nomi UTF8 fino a 4 byte/carattere. Non sono misure di latenza o benchmark.',
 'Ordine institution_id prima delle risorse: uguaglianza tenant e poi parent/id; il tenant isolato usa il prefisso sinistro. Le scope nullable dei tre SET NULL hanno lo stesso ordine rispetto al riferimento. Gli indici legacy restano necessari a FK/navigazioni/query legacy, anche se alcune future query potranno usare indici scoped.', '',
 '| Tabella / indice | Ordine colonne | Predicato supportato e vincolo | Costo / ridondanza |',
 '| --- | --- | --- | --- |']
for (table,name),rs in sorted(ix.items()):
    if not ('mt3a' in name or (table,name) in {('biennium','uq_biennium'),('subject','uq_subject_name'),('study_area','uq_study_area_name'),('study_path','uq_study_path_name')} or table=='mt3a_legacy_scope'): continue
    cols=collist(rs); unique=rs[0]['NON_UNIQUE']=='0'
    supported=[]
    names=[r['COLUMN_NAME'] for r in rs]
    for (t,fn),fr in fk.items():
        if t==table and names[:len(fr)]==[r['COLUMN_NAME'] for r in fr]: supported.append(fn)
        if fr[0]['REFERENCED_TABLE_NAME']==table and names==[r['REFERENCED_COLUMN_NAME'] for r in fr]: supported.append('parent '+fn)
    if table=='audit_event': predicate='institution_id = ? AND created_at tra estremi; ordinamento audit per tenant'
    else: predicate=' AND '.join(x+' = ?' for x in names)
    constraint=('UNIQUE; ' if unique else '')+', '.join(supported)
    full.append(f'| `{table}.{name}` | `{cols}` | {predicate}; {constraint or "manifest singleton"} | {len(names)} componenti; B-tree per scrittura. '+('Unicita scoped obbligatoria.' if unique else 'Supporto FK/predicato esplicito.')+' |')
full += ['', 'Sei indici transitori di 006 sono rimossi da 010/011, dopo avere verificato i sostituti:', '',
 '| Rimosso | Sostituto effettivo |', '| --- | --- |',
 '| question_option.uq_mt3a_question_option_owner | uq_mt3a_option_question; PRIMARY(id) per lookup id |',
 '| submission_answer.uq_mt3a_submission_answer_owner | uq_mt3a_answer_question; PRIMARY(id) per lookup id |',
 '| exam_violation.uq_mt3a_exam_violation_owner | idx_mt3a_exam_violation_submission per FK Institution; PRIMARY(id) |',
 '| exam_submission.idx_mt3a_exam_submission_exam | uq_mt3a_submission_exam(institution_id,exam_id,id), stesso prefisso |',
 '| question.idx_mt3a_question_exam | uq_mt3a_question_exam(institution_id,exam_id,id), stesso prefisso |',
 '| question_option.idx_mt3a_question_option_question | uq_mt3a_option_question(institution_id,question_id,id), stesso prefisso |', '',
 'Le FK non sono rimosse: InnoDB usa i B-tree sostitutivi. Nessun indice speculativo su stato/date del worker, nessun Redis/cache o EXPLAIN prestazionale dichiarato. Il PK(scope) e UNIQUE(institution_id) del manifest tecnico limitano configurazioni duplicate; non indicizzano una query scolastica.', '']
(S/'architecture'/'MULTITENANCY_MT3A_OWNERSHIP_INVENTORY.md').write_text('\n'.join(full),encoding='utf-8')
(E/'ownership-summary.md').write_text('\n'.join(summary)+'\n',encoding='utf-8')
before=json.loads((E/'legacy-before.json').read_text(encoding='utf-8-sig'))
after=json.loads((E/'legacy-after-backfill.json').read_text(encoding='utf-8-sig'))
assert before==after, 'Legacy migration window differs'
preservation={'tables':len(before),'allLegacyColumnsCountsAndCellDigestsEqual':True,
              'tablesManifestSha256':hashlib.sha256(json.dumps(before,sort_keys=True,separators=(',',':')).encode()).hexdigest(),
              'counts':{k:v['Count'] for k,v in before.items()}}
(E/'legacy-preservation.json').write_text(json.dumps(preservation,indent=2)+'\n',encoding='utf-8')
for name,prefix in [('MULTITENANCY_DESIGN.md','design-prefix.md'),('MULTITENANCY_MIGRATION.md','migration-prefix.md'),('MULTITENANCY_ISOLATION_MATRIX.md','matrix-prefix.md')]:
    original=S/'notes'/('.original-'+name)
    if not original.exists(): original.write_text((REPO/'docs'/'architecture'/name).read_text(encoding='utf-8-sig'),encoding='utf-8')
    addition=(S/'notes'/prefix).read_text(encoding='utf-8').replace('{{OWNERSHIP_SUMMARY}}','\n'.join(summary))
    (S/'architecture'/name).write_text(addition+'\n'+original.read_text(encoding='utf-8'),encoding='utf-8')
print('Inventory: 28 tables; complete PK/UNIQUE/FK and each new index. Legacy 21-table digests identical.')
