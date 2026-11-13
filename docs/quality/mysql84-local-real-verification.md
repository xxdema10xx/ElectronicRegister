# Verifica locale reale — er-quality-2-mysql84

**NON READY.** MySQL 8.4.13 reale su Docker Desktop Windows 11. Nessun commit, push o deployment. Container e volumi temporanei conservati.

## Isolamento

Originale protetto: `2299602d4bce333ef0fb626e9b1fb22758ae15ed62edb637357c7ebd4d8bc1b2` (`mysql84-local`, volume `mysql84_data`, porta 3307).
Ambiente principale: `81497357df0973e293d65a19889476f6eeb6178715d394fc495d891220739829` (`er84-quality2-local-20261102-a71c`), UUID `45aff413-d704-11f1-b8d9-5e82fd3aef6c`, hostname `81497357df09`, porta host 13384 / server 3306, datadir `/var/lib/mysql/`, volume `er84-quality2-local-20261102-a71c-data`.
Immagine attestata: `mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b`. Mount distinti verificati. I controlli negativi target originale/volume originale/UUID errato sono PASS; il CREATE con UUID errato non ha creato lo schema.
Percorso locale distinto da GitHub Actions: controlli Docker su ID, immagine, etichetta, nome, volume, porta; verifica SQL versione/UUID/hostname/porta/datadir prima di ogni comando del runner. TLS richiesto per le connessioni TCP del runner locale. Riavvii soltanto sul container temporaneo e con approvazioni separate.
Controllo finale: 23 container temporanei e 23 volumi indipendenti conservati; originale in esecuzione con StartedAt invariato e RestartCount 0. Tre vecchi container temporanei sono stati arrestati con approvazioni esplicite per liberare risorse AIO; nessun volume eliminato. I tentativi bloccati da io_setup EAGAIN sono conservati nelle evidenze, senza PASS sostitutivi.

## Cause riprodotte e correzioni

- **PASS — identity-account-state-ddl**: Expected additive DDL carried CRLF from the Windows checkout; SHOW CREATE returned LF. Token-level DDL, collations and metadata agreed. Normalize only SQL line endings in candidate contract generation/loading; retain exact metadata and token comparisons.
- **PASS — guid-collation**: Only institution_email_mutex.id is incompatible with the old classifier: real COLUMN_TYPE=int, DATA_TYPE=int, NOT NULL, PRI, collation NULL. Classifier required int(11). Allow signed int/int(11) only for this exact singleton column; preserve NOT NULL/PRI and all resource GUID/collation controls.
- **PASS — trigger-order-json-type**: Historical ACTION_ORDER was JSON string 2; server JSON returned numeric 2. Derive numeric order in candidate contract only.
- **PASS — parsed-regexp-anchor**: New CREATE SQL escapes the absolute-end anchor with two backslashes; ACTION_STATEMENT exposes the parsed single backslash. Derive expected ACTION_STATEMENT only for generated REGEXP_LIKE absolute-end anchors; never rewrite observed bodies.
- **FAIL — hash-newline-char-coercion**: Real CHAR(64) assignment stores only 64 As from 64 As plus newline before the trigger; direct ICU predicate on the original 65-character string returns 0. Required DML rejection is not achieved. No production schema change or permissive test replacement. Failure retained for design/remediation.
- **PASS — implicit-foreign-key-metadata**: 22 canonical FKs omit both actions. The real 8.4.13 metadata returns NO ACTION/NO ACTION; six historical classifiers required RESTRICT. Only the reviewed implicit-rule query predicates now expect exact NO ACTION on 8.4.13 and retain the historical RESTRICT expectation elsewhere. Eight real regressions cover all six validators and rejection of explicit RESTRICT/CASCADE; no live metadata normalization.
- **PASS — formative-explicit-legacy-fixture**: The immutable fresh schema does not seed the historical MAMP fixture institution used by LegacyBackfillDoesNotRewriteFormativeSnapshots. Only the owned 8.4 test supplies the explicit synthetic institution, first asserting absent-owner rejection and unchanged snapshots, then asserting backfill preserves snapshots. Full formative suite repeated.

## Risultati reali

| Verifica | Esito | Evidenza |
|---|---|---|
| Schema/metadata fresh, trigger, routine, journal, marker e idempotenza | PASS | `.diagnostics/mysql84-local-real/fresh-2/` |
| Protocollo SQL reale, errore, timeout, no-reuse e legacy missing-ack | PASS | `protocol-result.json` |
| GUID positivo/negativi, lock, readiness, 3 CLI Windows con ACL | 13 PASS, 0 FAIL, 0 SKIP | `dotnet/targeted-after.trx` |
| Classificatori FK impliciti, RESTRICT/CASCADE espliciti rifiutati | 8 PASS, 0 FAIL, 0 SKIP | `dotnet/fk-metadata-final.trx` |
| Unit .NET ricompilati | 598 PASS, 0 FAIL, 0 SKIP | `dotnet/unit-final.trx`, durata test 305 ms |
| Python mysql84 offline | 16 PASS | unittest, 1.956 s |
| Manifest, predecessore immutabile, checksum, bundle, 12 controlli negativi | PASS | node tools/test-mysql84-release.cjs |
| Contratto derivato da letture sorgenti CRLF/LF | PASS | prova offline, nessuna modifica ai sorgenti |
| Trigger DML: hash con newline finale | FAIL | `acceptance-2/result.json`, `coercion-result.json` |
| iam-IdentityAccountTests | PASS: 12 PASS, 0 FAIL, 0 SKIP | 75.29 s, TRX nel JSON |
| iam-Mt5eIamHttpTests | PASS: 53 PASS, 0 FAIL, 0 SKIP | 87.95 s, TRX nel JSON |
| onboarding-Mt5cOnboardingTests | PASS: 59 PASS, 0 FAIL, 0 SKIP | 92.65 s, TRX nel JSON |
| email-InstitutionClassCrudTests | PASS: 39 PASS, 0 FAIL, 0 SKIP | 97.50 s, TRX nel JSON |
| imports-InstitutionClassCrudTests | PASS: 49 PASS, 0 FAIL, 0 SKIP | 84.15 s, TRX nel JSON |
| calendar-InstitutionFormativePlanTests | PASS: 50 PASS, 0 FAIL, 0 SKIP | 110.69 s, TRX nel JSON |
| calendar-SqlLockEvidenceTests | PASS: 5 PASS, 0 FAIL, 0 SKIP | 91.36 s, TRX nel JSON |
| formative-InstitutionFormativePlanTests | PASS: 87 PASS, 0 FAIL, 0 SKIP | 103.96 s, TRX nel JSON |
| tenants-InstitutionClassCrudTests | PASS: 304 PASS, 0 FAIL, 0 SKIP | 201.29 s, TRX nel JSON |
| tenants-Mt2IdentityTests | FAIL: 49 PASS, 2 FAIL, 0 SKIP | 73.87 s, TRX nel JSON |
| windows-cli-Mt5cOnboardingTests | PASS: 3 PASS, 0 FAIL, 0 SKIP | 67.21 s, TRX nel JSON |

Copertura suite obbligatorie: 355/355 metodi dell’inventario originale eseguiti; 710 casi PASS, 2 FAIL, 0 SKIP. Aggiunti ed eseguiti quattro metodi di regressione GUID/FK (12 casi PASS). Ogni suite/classe usa il normale ciclo di vita xUnit; questa prova non certifica la diversa partizione CI per singolo metodo.

Upgrade: bootstrap del predecessore immutabile, errore 3995 riprodotto, snapshot, drift rejection, recovery dopo DROP/CREATE, maintenance persistita dopo riavvio, dati conservati e idempotenza risultano nel report delle fasi. L’accettazione successiva fallisce sul newline, quindi il percorso runtime complessivo non è PASS. Il JSON distingue le esecuzioni e i manifest: dopo le correzioni ai classificatori/test, tutti gli SQL fresh e CREATE upgrade sono risultati identici per hash, con ulteriore preflight schema/metadata/marker PASS sul manifest definitivo.

## File modificati in questa verifica

- `InstitutionFormativePlanLifecycleTests.cs`: prerequisito legacy sintetico esplicito, rifiuto del proprietario assente e conservazione snapshot prima/dopo backfill.
- Sei classificatori `*Integrity.cs` e `Mysql84ForeignKeyMetadataTests.cs`: regole FK implicite attese per 8.4.13 e controlli negativi, senza alterare DDL o autorizzare regole alternative.
- `tools/mysql84_local.py`: percorso locale attestato, fresh install e riconciliazione senza replay DDL.
- `tools/mysql84_release.py`, `tools/mysql84-release.cjs`: terminatori SQL, rappresentazioni numeriche/escape attese, diagnostica e guardia identità locale.
- `tools/mysql84_runtime.py`: percorso locale distinto, TLS account, avvio legacy con protocollo confermato, handshake per approvazione separata del riavvio, diagnostica negativa precisa.
- `Mt3aOwnershipMigration.cs`, `Mysql84GuidMetadataTests.cs`: eccezione signed-int limitata al singleton e regressioni reali positive/negative.
- `IntegrationFixture.cs`, `Mysql84FixtureTarget.cs`: container con nome/volume dedicati, retention esplicita, evidenze identità e binding CLI.
- `tools/PlatformAdmin/Program.cs`, `LocalMysql84Target.cs` e i tre test CLI: target Docker locale attestato dopo controllo ACL, con verifica UUID prima della mutazione.
- `tools/test_mysql84_release.py`, manifest/contratto candidati e questi report. Le modifiche preesistenti del run-7 sono conservate; nessun artefatto storico er-quality-1 è stato riscritto.

## Manifest e durata

Manifest candidato SHA256 (byte del file): `ce67d3d27aae0fb3c7ab0d8a5a93b1f3694fc0fe9abd7eb282ca52e3f8b6a53d`. Chiusura: 1102 file. Checksum normalizzato LF e risultati completi nel JSON.
Tempo trascorso da inizio provisioning: circa 51.4 minuti; durate reali delle fasi e dei test nel JSON. Fresh completa: 115.875 s; protocollo: 2.938 s.

## Residui

- Required trigger DML negative case with trailing newline fails on CHAR(64).
- Additional MT3A run has 39 failures (legacy fixture purge target, FK metadata and legacy data assumptions); full raw TRX retained.
- No GitHub Actions rerun or full original run-7 diagnostic download is claimed.
- Two mandatory tenant cases (membership-deleted/user-deleted) fail at MembershipFixturePurge: the helper authorizes only the historical 5.7/13306 instance. It was not broadened and immutable release DELETE guards remain intact.
- Trigger acceptance stops at the first failing newline probe; remaining acceptance matrix and fresh-after-upgrade step remain BLOCKED. Separate complete fresh install is PASS.
- Dedicated MySQL84ReleaseTests HTTP gate remains BLOCKED behind fresh-after-upgrade. Its GitHub-only setup was not impersonated; local SQL/fresh/protocol gates and all original domain methods have separate actual evidence.

Evidenze grezze: `.diagnostics/mysql84-local-real/`. Configurazione client privata protetta da ACL, esclusa dal report. Tutte le identità delle fixture conservate sono nel JSON.
