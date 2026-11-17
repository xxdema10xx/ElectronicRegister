# Grade scale — additive successor er-grade-scale-1

Voti da decimi (1–10, sufficienza 6, due decimali) a **trentesimi** (interi 0–30, sufficienza 18, niente lode),
più descrizione facoltativa dei voti e idempotenza dell'inserimento in blocco
(`POST /api/institution/grades/batch`, [contratto](../../docs/api/GRADE_BATCH.md)).

Target: **MySQL 8.4.13**. Predecessor: `er-quality-2-mysql84`. È indipendente da `er-platform-identity-1`:
dove quel successor è attivo si applica **dopo** di esso (ordine di `tools/mysql84-release.cjs`, fixture
`97-platform-identity.sql` → `97b-grade-scale.sql` → `98-new-release.sql`). Il contratto er-quality-2 e
`migrations/bootstrap/001_schema.sql` **non cambiano**: fanno parte della closure immutabile del predecessor, e le
nuove istanze ricevono questa migrazione come successor, come per platform identity.

## Cosa fa (`001_expand.sql`)

1. `grade`: colonne `description varchar(200) NULL` e `batch_event_id char(36) NULL`, chiave univoca
   `uq_grade_batch_student (institution_id, batch_event_id, student_id)`. L'`ALTER` è il primo passo: su un
   secondo tentativo fallisce (colonna duplicata, 1060) prima di toccare i dati.
2. In **una transazione** insieme al marker:
   - `grade.value` e `exam_submission.suggested_grade`: `nuovo = ROUND(vecchio × 3)`; se `vecchio < 6`,
     `nuovo = LEAST(nuovo, 17)` (un insufficiente resta insufficiente: 5.99 × 3 = 17.97 → 17);
   - `exam_submission.passed` delle consegne con voto ufficiale = `grade.value >= 18`; le consegne senza voto
     ufficiale mantengono il valore provvisorio;
   - marker `er-grade-scale-1` in `er_schema_release`, scritto per ultimo e nella stessa transazione.
   Ogni `UPDATE` è condizionato all'assenza del marker: la conversione avviene al massimo una volta.

Valori attesi: 1→3, 5.5→17, 5.99→17, 6→18, 7.33→22, 10→30. Nessun'altra colonna contiene voti: l'audit storico
(`audit_event.before_json/after_json`) resta com'era, in decimi, perché append-only. `mt3a_grade_update` richiede
solo che l'owner non cambi: l'`UPDATE` modifica il solo `value` (verificato dal test di migrazione con il trigger attivo).

La colonna resta `decimal(4,2)`: 30 ci sta, nessun cambio di tipo.

## Verifiche

- `002_validate.sql`, sola lettura: tutti e 7 i conteggi devono essere zero.
- `predecessor-contract.json`: `SHOW CREATE TABLE grade` catturato su 8.4.13 e inventario target
  (72 tabelle, 186 trigger, 201 FK, 309 indici con platform identity). `tools/release-preflight.cjs` lo usa come
  delta quando il marker è presente.
- `/health/ready` risponde 503 (`GRADE_SCALE_SCHEMA`) finché il marker manca: la nuova API legge le nuove colonne.
- Test: `tests/Integration/GradeScaleMigrationTests.cs` (conversione su un database di prova con DDL e trigger del
  predecessor, idempotenza, DDL e inventario della fixture, validazione), `tools/test-mysql84-release.cjs`,
  `tools/test-mysql84.cjs`, `tools/test_release_ec2.py`.

## Applicazione

Mai su database online senza la procedura di rilascio. `node tools/mysql84-release.cjs successor <binding.json> <NUOVA-dir> er-grade-scale-1`
scrive l'SQL legato al DEFINER (`01-grade-scale.sql`, `binding.json`) senza eseguire nulla.

Staging EC2 (`docs/operations/EC2_RELEASE.md`): la fase `deploy` esegue il passo host `successor`
(`infra/staging/host.py apply_successor`), che applica `er-platform-identity-1` se abilitato e poi sempre
`er-grade-scale-1`, una volta sola, sul database del nuovo staging tramite il socket root del suo container DB,
e verifica `002_validate.sql` (7/7 a zero). Un secondo passaggio trova il marker e non fa nulla. Il seed demo
(`infra/staging/demo_seed.py`) gira dopo e crea voti solo in trentesimi.

Un database esistente in decimi (es. istanza locale 5.7 su 13306) richiede: stop degli scrittori, backup completo
verificato, esecuzione unica di `001_expand.sql` con l'identità di migrazione, poi `002_validate.sql`. DDL implica
commit: non c'è rollback automatico; in caso di errore conservare istanza, log e backup per l'analisi.
