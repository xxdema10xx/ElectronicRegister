# MT-5B checkpoint B — persistenza IAM

**MT-5B PERSISTENCE READY. MT-5B complessiva resta PARZIALE.**

Persistenza verificata su MAMP MySQL5.7.24 isolato. Nessuna attivazione di generation
nei JWT, nessuna nuova API, ledger IAM o workflow amministrativo pubblico.

## 1. Git, ambiente e baseline

Branch `refactor-database-entities`, HEAD `a002cb66cebecfdcdf892c099cf0518b796b7e1a`.
Workspace preesistente preservato, nessun commit/push/reset. SDK10.0.401, build completa
zero errori e11warning nullable preesistenti. Nessuna modifica frontend/esami/worker.
[Git iniziale](TestResults/mt5b-persistence/initial-git-status.txt).

Prima delle operazioni: pid-file/processo/listener verificati dinamicamente, binario
C:\MAMP\bin\mysql\bin\mysqld.exe; VERSION5.7.24, hostnameDESKTOP-QUOAG32,
porta13306, DB electronic_register_test, datadir privato sotto
migrations/mt4/private/mt5a-mamp-13306/data. GLOBAL sql_mode strict invariato.
MT4C32tabelle/53trigger/98FK, holding MT3A_LEGACY_TEST Provisioning verificati.
Nessun avvio di istanze, credenziali nuove, fallback3306 o utilizzo di XAMPP.
[Identita iniziale](TestResults/mt5b-persistence/preflight-identity.json).

Nuova baseline **711/711 Passed**:51unit/660integration, zero Failed/Skipped/NotExecuted.
Nomi esatti confrontati con MT5A final-2. Connessione e strict session solo nei figli;
credenziali dal client.ini privato, mysqldump8.0.44 flavorMySQL, ambiente ripristinato.
[Verifica baseline](TestResults/mt5b-persistence/baseline-verification.json).

## 2. Writer tecnici e operativi: contratto applicato

| Writer effettivo | Invocazione/HTTP/istituti | Attore e lock | Generation |
| --- | --- | --- | --- |
| InstitutionMembershipRepository.AddAsync | DI tecnico/fixture; nessun consumer HTTP; ora solo Provisioning | Nessun umano inventato; transazione, Institution FOR UPDATE prima di letture/mutazione | Stato0 alla creazione, +1 per ruolo iniziale |
| AddRoleAsync | Come sopra; Active/Suspended/Archived negati | Stesso mutex; controllo owner scoped mantenuto | +1 soltanto per INSERT effettivo |
| SuspendAsync | Come sopra; Active/Suspended/Archived negati | Stesso mutex e update scoped | +1 se cambia lo stato |
| Mt1SchemaMigration / canonical backfill | Tool offline test/migrazione, non registrato/invocato da Program; holding Provisioning verificata | Manifest/config espliciti e manutenzione con writer quiescenti; non una chiamata IAM interattiva | Trigger su INSERT SELECT; ripetizione senza nuove righe non incrementa |
| Seeder/fixture dirette | Preparazione, fault injection e teardown privilegiati sintetici | Autorita del processo di test/DB, non User.Role; nessun ActorUserId finto | Tutti i cambi di ruoli/stato attraversano gli stessi trigger |
| Manager legacy/Auth/User | Nessun nuovo writer membership individuato nei manager HTTP | Gate legacy invariato; User globale non attribuisce autorita tecnica | Nessuna nuova scrittura User/credenziali |
| Futuro manager IAM | Non implementato, nessun HTTP mutante | Caller InstitutionAccess verificato, EventId/ExpectedRevision/audit/LastAdmin obbligatori | Incremento SQL, non duplicato nel manager |

Il repository tecnico non e un nuovo canale amministrativo per tenant operativi:
testa il vero stato Institution sotto lock e rifiuta anche User.Role globale admin.
I tool offline restano soggetti a manutenzione esclusiva, non sono dichiarati sicuri
per esecuzione concorrente con un'attivazione amministrativa futura. Nessun IsSystem
proveniente da client, SuperAdmin, primo amministratore o nuovo provisioning HTTP.
La tracciabilita tecnica usa processo/run/manifest/hash dei comandi e backup; non
viene fabbricata un'identita umana o un evento amministrativo retroattivo.

## 3. Meccanismo e schema

Nuova tabella `institution_membership_security_state`:

- InstitutionId + MembershipId, PK composita e FK RESTRICT alla membership owner-aware.
- UNIQUE MembershipId: una sola identita di sicurezza per membership globale univoca.
- SecurityGeneration BIGINT signed NOT NULL DEFAULT0; nessun timestamp di revoca.

Creazione membership -> stato0 nella stessa transazione. Ogni effettivo INSERT/DELETE
di ruolo e cambio di membership.status -> incremento1 atomico nella sidecar. Stato
invariato, ruolo duplicato, INSERT IGNORE senza inserimenti e rollback non avanzano
il valore persistito. Ripristinare ruoli/stato non ripristina la generation precedente.
SuspendedAt senza cambio stato non e un nuovo evento di revoca.

Un UPDATE della generation non puo diminuirla; un avanzamento diretto e una revoca
piu restrittiva. Nessuna API o metodo repository consente di sceglierla. A Int64.MaxValue il
prossimo cambio rilevante fallisce e viene rollbackato, senza wrap o reset.
Il reader DI AsNoTracking richiede InstitutionId+MembershipId e nega stato assente:
nessun fallback0. Nessuna navigazione EF o enumerazione globale aggiunta.

## 4. MySQL1442 e prove preliminari reali

I trigger dei ruoli leggono il parent immutabile, ma aggiornano **solo la sidecar**.
Non aggiornano institution_membership gia letto da INSERT SELECT/DELETE JOIN.
UPDATE che cambia la chiave MembershipId/Role e rifiutato; sostituzioni autorizzate
devono usare primitive esplicite nella stessa transazione. Nessun bypass dei trigger.
Statement futuri che leggessero la sidecar mentre il trigger la modifica devono
essere evitati: controllare ExpectedRevision con lettura separata sotto mutex.

Prima dell'EXPAND: tre tabelle persistenti di prova con10trigger,19/19 test SQL Passed.
Verificati INSERT SELECT, multi-row, DELETE JOIN, no-op, ruolo/stato roundtrip,
rollback, missing-state, overflow, concorrenti e mutex Institution con nuova lettura
del secondo commit. Nessun1442/1213 o incremento perso osservato nei casi eseguiti;
non e una promessa di assenza di deadlock per ogni futuro statement arbitrario.
[Prova19/19 e hash](TestResults/mt5b-persistence/proof-verification.json).

L'utente ha autorizzato esplicitamente il cleanup delle sole tre nuove tabelle vuote
mt5b_probe_20261020_{role,security,membership} dopo successo. Verificati manifest,
target, righe0 e nessuna dipendenza esterna, rimossi quei tre oggetti; schema originale
ritornato32/53/98 prima del nuovo backup. Nessun altro DROP eseguito o autorizzato.
[Piano/comandi](TestResults/mt5b-persistence/SQL_PROOF_PLAN.md),
[cleanup](TestResults/mt5b-persistence/proof-cleanup.json).

## 5. Migrazione, backfill e scaffold

Script versionati in [migrations/mt5](../../migrations/mt5/README.md), senza modificare
i sei SQL canonici MT4. EXPAND/backfill/validate eseguiti una volta, nessun errore DDL,
restore o retry di migrazione. DDL con commit impliciti, nessun rollback globale promesso.
000preflight,001expand,002backfill,003validate,004guard contratto errore,005validate ordine.
004 e additivo: conserva l'errore1451 dei delete storici senza rimuovere trigger o
allentare assertion. Totale finale **33tabelle/64trigger/99FK/141indici**.

Backup completo verificato della sola13306 prima della baseline e nuovamente dopo
la prova, prima del DDL. Dump privati con definizioni32/53/footer e SHA256:

- Iniziale: B1BDC182437113ED2841D5F50223AC0B8AD80B6055D4A9DAFAD0529C5B0DAFBF.
- Pre-EXPAND: AE82F10E9EA39499FE9FCC1D8B2201485B384678C1B6F2F8D96AF38D087E0D7E.

[Backup pre-EXPAND](TestResults/mt5b-persistence/pre-expand-backup.json).
Backfill inizializza soltanto stati mancanti, non sovrascrive generazioni esistenti e
non crea eventi storici. Provato anche con record preesistenti/nonzero e ripetizione,
non soltanto sul DB quasi vuoto. Non usare backfill come riparazione automatica di
stati perduti dopo la futura attivazione JWT: tale assenza richiede diagnosi fail-closed.

Scaffold riproducibile del solo nuovo oggetto, via connessione nominata in ambiente
figlio (nessuna password CLI). Entity copiata dal candidato; User/context legacy mai
sovrascritti. FK esplicita nel sidecar perche il principal non selezionato e omesso
dallo scaffold single-table. Entity SHA25616BE2160FF0C49AC78DC32A06CB6267DCA743834542777B77DE16C5F105B1E4B.
[Scaffold](TestResults/mt5b-persistence/scaffold.log).

## 6. Delete/recreate, rollback e concorrenza

Identita membership Institution/User/Id immutabile; stato di sicurezza con identita
immutabile e FK RESTRICT. DELETE operativo di membership e DELETE/reset dello stato
negati, quindi lo stesso ID non puo essere normalmente ricreato con generation0.
Non ci si affida all'unicita probabilistica del GUID. Missing-state blocca sia letture
sia cambi di ruolo/stato. Trigger e DML restano nella stessa transazione InnoDB.

L'eccezione DBA di fixture richiede insieme root, DB, porta13306, datadir isolato,
lista esplicita di owner in variabile sessione e codici sintetici con suffisso GUID.
Holding reale esclusa; nessun codice HTTP/produttivo imposta quella variabile.
Il helper test la ripristina e chiude la connessione nello scope. I soli holding di
rehearsal vengono etichettati al teardown con ID/nome esatti del test. Non e una
difesa contro il DBA e non autorizza delete/recreate amministrativi in produzione.

Provate due mutazioni concorrenti senza incrementi persi, due sospensioni identiche
senza incrementi duplicati, A senza effetti sulla generation B dello stesso User e
mutex Institution fra due connessioni/transazioni reali. Il secondo legge la
generation del commit precedente. Il workflow Last Admin completo rimane futuro.

## 7. Test e regressioni preservate

27 nuovi casi finali in Mt5bSecurityStateTests: SQL reale, FK errata/orfana, guard
tecnici, provisioning, reader scoped/fail-closed, mapping e nessuna route nuova.
I19casi della prova preliminare hanno preceduto il DDL; i27finali usano lo schema reale.
118/118 mirati MT1/MT2/MT5A verdi. Build zero errori.

| Esecuzione | Unit Passed | Integration Passed/Totale | Failed | Altri outcome |
| --- | ---: | ---: | ---: | ---: |
| Baseline | 51 | 660/660 | 0 | 0 |
| Prima regressione | 51 | 683/687 | 4 | 0 |
| Mirati correzione | — | 4/4 | 0 | 0 |
| Regressione stabilizzata | 51 | 687/687 | 0 | 0 |

**738/738 Passed** nella regressione stabilizzata, zero Failed/Skipped/NotExecuted.
Tutte le711identita precedenti preservate; le27aggiunte coincidono con i mirati.
377sorgenti compilati congelati prima del run, invariati dopo; JWT/gate/worker e
i precedenti SQL MT4 invariati. Non sono richieste/eseguite due suite MT5B definitive.
[TRX/outcome/identita](TestResults/mt5b-persistence/regression-2-verification.json),
[certificazione](TestResults/mt5b-persistence/persistence-certification.json),
[manifest TRX](TestResults/mt5b-persistence/trx-sha256-manifest.json).

Le quattro regressioni iniziali sono conservate integralmente: tre tool offline
rifiutavano la tabella non classificata; ora l'inventario la valida fail-closed prima
di riconoscerla. Una fixture sostituiva il ruolo via UPDATE; ora usa DELETE+INSERT
atomici, con stesso intento e tutte le assertion originali. Nessun test rinominato,
saltato o indebolito; nessun errore applicativo mascherato.
[Prima regressione fallita](TestResults/mt5b-persistence/regression-verification.json),
[verifiche mirate](TestResults/mt5b-persistence/targeted-verification.json).

Tutti i gruppi MT1—MT5A, inclusi MT3E worker37/37 e MT4C31/31, sono Passed nei nomi
TRX effettivi; bootstrap reale Development/Production e gate restano verdi.
[Matrice](TestResults/mt5b-persistence/regression-2-module-summary.json),
[bootstrap](TestResults/mt5b-persistence/regression-2-bootstrap-identities.json).

## 8. Integrita e stato finale

Nuove connessioni: tutte99FK senza orfani, precedenti98definizioni preservate;53trigger
precedenti invariati e11nuovi confrontati con i corpi SQL canonici. Il client rimuove
i commenti standalone: confronto eseguibile normalizzato, non esclusione delle guardie.
Due errori del solo verificatore (quotatura PowerShell/commenti non eseguibili) sono
stati diagnosticati con SELECT e corretti; nessuna modifica del DB per farlo passare.
Validatori MT4A7/MT4C5 e MT5B6+2 tutti0; runner pubblico Validate provato con successo.

Solo holding Provisioning,15User legacy,0membership/state/profili/edizioni/iscrizioni/
ledger didattico istituzionali temporanei,0tabelle di prova. GLOBAL invariato;
istanza13306 lasciata attiva. Nessuna pulizia manuale dopo i test, solo teardown previsto.
[99FK](TestResults/mt5b-persistence/final-fk-integrity.json),
[integrita](TestResults/mt5b-persistence/final-integrity.json),
[nuovi trigger](TestResults/mt5b-persistence/final-new-triggers.json).

MAMP originale3306 interrogata soltanto in lettura: identita/schema/stati/conteggi
coincidono con il riferimento precedente, SHA256
599891D4994047BAD77F86F3BE3AE09FFFA9C5322B53CFCF7748768E93136CE0.
Nessuna DDL/DML/fixture/restore sul3306 o modifica di servizi/porte/credenziali.
Non si afferma un digest nuovo di ogni cella originale o l'immutabilita di ogni cella
didattica isolata, modificabile intenzionalmente dalle fixture legacy.
git diff --check superato. Log/dump privati, TRX separati e verificati senza segreti.

## 9. JWT e audit ancora da integrare

Emissione/validazione JWT, TenantContext e MT2 **non modificati**. La generation
persistita non e ancora consultata dal validator: non si dichiara risolto oggi il
rischio del vecchio JWT che torna valido dopo ripristino ruoli/stato.
Il prossimo contratto dovra emettere la generation letta con membership/ruoli,
richiederla univoca/non negativa/range Int64 e confrontarla ad ogni richiesta senza
cache/fallback0. Dopo attivazione, i token gia emessi senza generation saranno
rifiutati: nuova autenticazione/selezione legittima, nessun fallback legacy.

Audit IAM futuro distinto da course_administration_event: attore umano verificato
con FK membership/User/tenant; evento tecnico attribuito a processo/run/manifest,
senza fingersi InstitutionAdmin; passato sconosciuto non ricostruito. Nessun ledger
completo/evento retroattivo introdotto in questo checkpoint.

## 10. Prerequisiti del prossimo checkpoint

Integrare JWT in modo coordinato fail-closed su tutti i nodi, poi manager/ledger,
ExpectedRevision/EventId, audit atomico append-only, revalidation sotto lock,
self-administration negata e Last Admin concorrente. I writer operativi futuri
devono acquisire Institution prima di membership/ruoli/sidecar e non rileggere lo
stato dentro lo statement che ne attiva il trigger. Audit failure -> rollback.
Le API scriventi rimangono assenti; rollout MT5A default false invariato.

Queste prove certificano MySQL5.7.24, non MariaDB10.4. Nessun MT5C, onboarding,
invito, cutover o prontezza produzione dichiarati. MT5B complessiva resta PARZIALE.
