# MT-5B-D — gestione amministrativa IAM

21 ottobre 2026. **Implementazione backend completata: 820/820 Passed, tutte le
772 identità precedenti presenti e superate, zero Failed/Skipped/NotExecuted.**

## Contratto implementato

`IInstitutionMembershipAdminManager.MutateAsync` accetta una `MembershipMutation`
tipizzata: MembershipId, EventId, ExpectedRevision, Action, Role opzionale, Reason.
Nessun InstitutionId o attore proveniente dalla richiesta. Le quattro azioni sono
AddRole, RemoveRole, Suspend, Reactivate; ruoli del catalogo esistente
InstitutionAdmin/Teacher/Student. Pending/Left non aprono onboarding o rejoin.
Motivo obbligatorio, massimo 500 caratteri, senza caratteri di controllo.

ExpectedRevision coincide con la security generation persistita già prevista dal
contratto architetturale. Nessuna nuova revision parallela o modifica C# alla
generation. Una mutazione effettiva deve produrre esattamente +1 dai trigger MT5B;
no-op, revision obsolete e overflow sono conflitti. Tutte le automutazioni sono
negate, includendo il caso dell'unico amministratore.

`InstitutionMembershipMutationRepository.MutateAsync` apre una transazione e usa
Institution FOR UPDATE come prima lettura. Blocca membership, ruoli e security state
in ordine deterministico degli ID; rivalida poi User, Institution attiva, membership
attiva, ruoli esatti, generation corrente e InstitutionAdmin. L'autorizzazione del
manager precedente al lock non viene considerata sufficiente. Target e query sono
owner-scoped; un target esterno è indistinguibile da uno assente.

Prima della riduzione degli amministratori conta gli altri admin attivi, con User
valido nello stesso tenant. Self-denial, mutex e rivalidazione impediscono due
revoche reciproche: il secondo attore perde l'autorizzazione dopo il primo commit.
Non servono profili Teacher/Student per essere admin e User.Role globale non concede
privilegi tenant. I writer tecnici esistenti restano separati e limitati a Provisioning.

EventId è univoco per istituzione. SHA256 canonico dell'intento tipizzato comprende
istituzione, attore User/membership, target, EventId, ExpectedRevision, azione, ruolo
e motivo esatto. Non include la generation del JWT dell'attore, che viene comunque
ricontrollata a ogni tentativo. Stesso evento/intento restituisce il risultato
storico originale; contenuto diverso produce MembershipConflictException. Anche il
replay richiede attore corrente autorizzato e security state del target presente.
Nessun retry automatico o trattamento di una revoca come successo idempotente.

Mutazione, incremento SQL e append audit condividono la stessa transazione.
Un errore lascia rollback applicativo; non viene promesso rollback delle DDL.

## File implementati

- Domain: MembershipMutation/Action/Result, MembershipMutationRules,
  MembershipConflictException, IInstitutionMembershipMutationRepository;
  estensione di IInstitutionMembershipAdminManager.
- Application: InstitutionMembershipAdminManager.MutateAsync, autorizzazione scoped.
- Infrastructure: InstitutionMembershipMutationRepository; entity
  InstitutionMembershipEvent, sidecar DB-first ElectronicRegisterContext.Mt5d,
  registrazione DI e configurazione del modello parziale.
- Tooling offline: Mt5dMembershipEventIntegrity.ValidateIfPresentAsync e aggancio
  read-only in Mt3aOwnershipMigration.ValidateGraphAsync; nessun SQL canonico precedente modificato.
- Test: MembershipMutationRulesTests e Mt5dMembershipMutationTests.
- Inventari route Development/Production: aggiornati soltanto i riferimenti DI
  del manager; nessuna route o autorizzazione aggiunta.
- Persistenza: migrations/mt5/iam-d, due manifest separati, verificatore SHA256,
  eccezioni gitignore limitate e attributi LF per stabilità degli hash al checkout.

Nessun controller, endpoint, frontend, bypass LegacyEndpointGate o modifica JWT
produttiva aggiunti in questa fase. Le due GET directory precedenti restano le sole
action del controller membership. Nessun IAM storico fittizio.

## Preflight e modifiche SQL realmente eseguite

Verificati entrambi i processi, listener e identità SQL: MySQL 5.7.24, hostname
CES01622, eseguibile C:\MAMP\bin\mysql\bin\mysqld.exe. Baseline 127.0.0.1:3306,
datadir C:\MAMP\db\mysql, sola lettura; isolata 127.0.0.1:13306, datadir privato
`migrations/mt4/private/mt5a-mamp-13306/data/`. Database esatto electronic_register_test.

Fingerprint iniziale isolata uguale alla certificazione C:
`7D6FFC3A080ADA02D08B87BBCDBA58FBC6006F2D0584D1055D543B33021A6B90`.
33 tabelle/64 trigger/99 FK/141 indici, nessun orfano, validator MT4A 7,
MT4C 5, MT5B 6+2 tutti zero, nessun writer/transazione/evento concorrente.
Baseline 3306 verificata semanticamente identica prima di ciascuna DDL.
ACL private controllate e circa 294 GB liberi sul disco C:.

Evidenze, credenziali, journal non sovrascrivibili, dump e output sensibili esclusi
da Git in `migrations/mt4/private/mt5a-mamp-13306/mt5bd-20261021/`.
Client MAMP 5.7, password nel solo ambiente del processo figlio, nessun --force.
Hash e identità controllati immediatamente prima di ogni file; nessun replay DDL.

| Passaggio | Esito effettivo |
| --- | --- |
| 001_membership_event.sql | Una tabella append-only e tre trigger nuovi; exit 0 |
| 002_validate.sql | Quattro regole a zero |
| 003_event_snapshot_shape.sql | Un trigger additivo BEFORE INSERT FOLLOWS il precedente; exit 0 |
| 004_validate_snapshot.sql | Due regole a zero, ordine verificato |

Nessun oggetto esistente eliminato, ricreato o alterato. Nessuna migrazione precedente
rieseguita. GLOBAL sql_mode, account e password invariati. Nessun restore/reset.

La seconda aggiunta irrigidisce la forma degli snapshot: una SELECT read-only sul
server reale ha dimostrato che JSON_CONTAINS accetta ricorsivamente un array annidato.
Una nuova guardia richiede array di stringhe piatte del catalogo, senza sostituire
il trigger già installato. Test reale di INSERT malevolo rifiutato con 1644 e
messaggio della nuova guardia. Nessun errore o stato DDL parziale si è verificato.

Ledger: PK istituzione/EventId; UNIQUE istituzione/target/revision_after; FK composite
attore/istituzione/membership/User e target/owner, entrambe RESTRICT. Snapshot minimi,
motivo, hash e timestamp UTC_TIMESTAMP(6) server-side. Validazione SQL di azione,
stati, snapshot, revisioni e stato effettivo dopo la mutazione. UPDATE sempre negato;
DELETE solo per fixture root con owner espliciti, porta/datadir/versione esatti e
codice MT5BP_<GUID>. Holding reale esclusa. Non è una difesa contro un DBA che cambi
schema o identità; nessun writer produttivo imposta questa capability.

SHA256 delle sorgenti congelate:

| File | SHA256 |
| --- | --- |
| 001_membership_event.sql | 75433A668EAE7F924340FCF903DE75268997194D4EFF669ABF312113EC0EAB6F |
| 002_validate.sql | 4EB07A653C8D99716FAE0E2414860324EA0C69F1B992177A7750A1D0FC93BC5F |
| 003_event_snapshot_shape.sql | A95129E530BC76B8C964A5540CED2000CBA91A81BB9628F27080BC80AB4CB107 |
| 004_validate_snapshot.sql | 1CB8B3B3BAB35C91E08D662DD8ED750B0DA194088C1E92BBD7C6A8F33AE2975E |

Backup privati preventivi, exit 0/footer/inventario/hash verificati:

| Dump | SHA256 |
| --- | --- |
| before-iam.sql (33 tabelle/64 trigger) | A344A52B720C700F391D6C73FE83DB5637FEBCDADCEDD000199D6B11653106CB |
| before-snapshot-shape.sql (34/67) | 00A6ABA9BB2138C2144301EAE58C0DC987ABB2E761A8F61DB4A8D5B024AC9C76 |

Ogni bundle include metadati completi SHOW CREATE e overlay SQL_MODE archivistico,
mai applicato. Il solo dump MySQL 5.7 non basta a riprodurre i cinque trigger MT4C.

## Verifiche mirate e correzioni

Passati 16/16 nuovi casi unitari e 32/32 nuovi casi integrazione MySQL reale:

| Area | Prova |
| --- | --- |
| Ruoli/status | Add/remove, suspend/reactivate, revision +1 dai trigger, audit attribuito e isolamento stesso User A/B |
| Input/no-op | Catalogo, forma richiesta, motivo, revision, no-op e transizioni Pending/Left negate |
| Autorizzazione | Global admin senza ruolo tenant, attore sospeso/revocato, token/context obsoleto, institution non attiva, contesto contraffatto/incompleto |
| Isolamento | Target fuori tenant assente, EventId tenant-scoped, A non modifica B |
| Last Admin | Ultimo admin non auto-revocabile; revoche/sospensioni reciproche concorrenti lasciano un admin attivo |
| Concorrenza | Stesso evento applicato una sola volta; intenzioni conflittuali con stesso EventId; revision identiche concorrenti con un solo vincitore |
| Idempotenza | Risultato originale anche dopo altra mutazione; motivo/target/revision diversi conflittuali; replay di attore revocato negato |
| Atomicità | Fault prima dell'INSERT audit e dopo append prima del commit: nessun ruolo, incremento o audit persistiti |
| Fail-closed | Target senza security state nega anche replay; overflow non produce wrapping/audit |
| Audit | UPDATE/DELETE non autorizzati negati; owner errato negato; array annidati respinti dal trigger additivo |
| JWT reale | Program effettivo: token obsoleti rifiutati 403 dopo ruolo/status e dopo il ripristino; nuovo token valido, tenant B invariato |

I fault interceptor confermano esplicitamente di aver raggiunto il punto simulato;
prima del fault commit è verificata la presenza dell'evento nella stessa transazione.
Nessun test passa soltanto perché un errore preliminare impedisce l'operazione.
Le prove missing-state/overflow usano esclusivamente setup privilegiato delle proprie
fixture GUID: ripristino fault-injection esplicito prima della pulizia, nessun reset
operativo. Cleanup tramite trigger attivi e capability DBA sintetica verificata.

Primo run mirato: 16/18 Passed, due errori nelle aspettative di test. La fixture
dell'attore revocato non aveva più alcun ruolo e il costruttore TenantContext la
rifiutava prima del repository: conservato un ruolo Teacher nella sola fixture per
verificare il rifiuto del writer anche con un contesto costruibile. Il test JWT
aspettava 401 anziché il 403 previsto dal contratto C per token ben formato ma
revocato: aspettativa corretta, senza modificare il validator o accettare successo.
Riesecuzione 23/23, poi estensione a 31/31, tutte positive. Output falliti conservati.

## Regressione finale e integrità

**Regressione finale completa: 820/820 Passed**, composta da **69/69 unitari** e
**751/751 integrazione**. Zero Failed/Skipped/NotExecuted. Tutte le **772 identità
precedenti** verificate confrontando i nomi dei TRX C originari con i risultati D;
48 nuovi casi (16 unitari, 32 integrazione). Nessun test rimosso o disabilitato.

La prima regressione completa aveva 747/750 integrazione Passed e tre fallimenti:
Mt3aOwnershipTests.AmbiguousLegacyAuditAbortsBackfillAndLeavesEveryRealCellUntouched,
Mt3aOwnershipTests.OwnershipBackfillIsIdempotentAndPreservesAllLegacyCellsAndTenantRows,
Mt4aCourseStructureTests.ExistingBackfillIsIdempotentWithAttributedMt4Graph.
Tutti rifiutavano correttamente la nuova tabella non classificata, prima del backfill.
Il nuovo validatore read-only riconosce il ledger soltanto dopo verifica di ownership,
revisioni, FK composite, indici UNIQUE senza prefissi e guardie/ordine dei trigger.
La guardia sulle altre tabelle sconosciute resta attiva. Non è una whitelist permissiva
e non aggiunge scritture, bootstrap o registrazioni DI.

I tre casi corretti sono passati nella riesecuzione mirata. Aggiunto un test che
valida un ledger realmente popolato e ne verifica la conservazione. Il suo primo run
ha rilevato un cast nella fixture: MySqlConnector restituisce già Guid, non string.
Corretta la sola lettura; riesecuzione positiva. Il run mirato 34/35 più il nuovo
caso corretto 1/1 è stato seguito dalla regressione completa pulita 751/751.
Durante la revisione è stato reso esplicito anche il conflitto EventId per un nuovo
target inesistente/esterno; l'autorizzazione precede sempre il controllo del replay.

Freeze SHA256 dei sorgenti C# acquisito dopo le correzioni e verificato identico al
termine dell'intera regressione. Tutti i fallimenti precedenti e i TRX restano privati.
I warning nullable preesistenti non sono stati soppressi o usati per disabilitare test.

| Risultato finale | SHA256 TRX |
| --- | --- |
| Unitari 69/69 | D479BFDDD4FB5503636E646931B357D97CD69CA13EE0237897705D6DC7487AFF |
| Integrazione 751/751 | 7299E82A234A288944F175BB29D0006CA886E7765C65DAAE82AA55FA5149C429 |

Controlli SQL finali reali: **34 tabelle, 68 trigger, 101 FK, 144 indici**;
zero orfani su tutte le 101 FK, comprese le composite. Validator MT4A 7/7,
MT4C 5/5, MT5B 6/6+2/2, IAM 4/4+2/2 tutti a zero. Tutti i 64 trigger originari
e i quattro nuovi sono identici al rispettivo snapshot dopo l'installazione:
corpi completi, DEFINER, SQL_MODE, charset, collation e ACTION_ORDER preservati.
Tutti gli altri metadati dello schema sono invariati durante la regressione.
GLOBAL sql_mode invariato su entrambe le istanze. Nessuna istituzione/membership
sintetica e nessun evento IAM di fixture residuo. Gli eventi sono stati verificati
persistiti da connessioni nuove prima del teardown autorizzato delle proprie fixture.

Come nella suite C, alcuni scenari legacy fissi modificano valori di test nelle
tabelle audit_event, exam, exam_student, exam_submission, grade, submission_answer,
submission_answer_option: nessuna variazione del numero delle righe. Non si dichiara
immutabile il contenuto di quelle fixture della 13306. Nessuna modifica di dati
applicativi operativi o della baseline protetta.

Baseline **3306 invariata** su dati e metadati, comprese tutte le definizioni complete
dei 53 trigger; 32 tabelle/53 trigger/98 FK/139 indici. Fingerprint iniziale/finale:
`FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22`.

Fingerprint isolata finale: `A129C9DAE1B58BD7CBDF754613D7D6731BAD9C473A01B21BAC3BAD04CAFA34B0`.

Backup finale privato **after-iam.sql**, 191692 byte, 34 tabelle/68 trigger,
exit 0, footer/inventario/dati/hash verificati; bundle completo di metadati e overlay
SQL_MODE archivistico. SHA256: `A9B8BF5716C2C98950AD78863FBFC1F1D38ECC0B615B711C3E34CEDDF08169F8`.

Evidenze finali: final-certification.json, isolated-final.json, baseline-final.json,
isolated-triggers-final.json, after-iam.sql-bundle.json e i due TRX nell'area privata
indicata sopra. Nessun dump, credenziale o contenuto personale pubblicato.

## Funzionalità ancora escluse

Nessun nuovo endpoint HTTP, frontend, onboarding/primo admin, rejoin, invito,
recupero amministrativo, audit tecnico attribuito fittiziamente o deployment.
Nessuna modifica al LegacyEndpointGate. Nuove route e rollout HTTP richiederanno
una fase esplicita; il writer interno non le apre implicitamente.

## Stato Git

Branch refactor-database-entities, HEAD 92cb8141b9555a36c00e517cb4cddc50d0dd7485.
Modifiche precedenti C e report/candidati storici preservati; nessun commit, staging
o push. SQL visibili a Git solo tramite quattro eccezioni nominative; dump, log,
credenziali e directory privata restano ignorati. Candidate-r1/r2/r3 non modificati.

**MT-5B-D IAM IMPLEMENTED AND VERIFIED**
