# ElectronicRegister - Multitenancy Migration Plan

## MT-3 PARZIALE — collegamenti profilo, non migrazione completa del dataset

SQL versionato `migrations/mt3/001_profile_expand.sql`: UNIQUE(institution_id,id) sulla
membership e due tabelle laterali con FK composite/RESTRICT. DDL eseguito solo su
electronic_register_test dopo dump completo leggibile, controllo DATABASE/VERSION/
sql_mode e SHA-256. Dump privato in mt3-evidence/private (ignorato, contiene hash
password); sole evidenze non sensibili nel report. Non e un backup/restore produttivo
certificato. Nessuna ownership delle 21 tabelle legacy imposta e nessun NOT NULL nuovo
su dati legacy. Non e stato eseguito un backfill permanente o cutover.

Database-first: `Scaffold-Mt3.ps1` genera candidati per i soli nuovi collegamenti e
parent necessari, poi -Apply copia soltanto due entity nuove e mapping partial. Non
copiare i candidati Student/Teacher/Membership generati; non sovrascrivere il context
legacy. Dopo rigenerazione MT1, rieseguire l'applicazione del mapping MT3 per ripristinare
il suo hook; dopo scaffolding completo riconciliare i sidecar, mai duplicarli alla cieca.

Backfill DML preparatorio: `Mt3ProfileBackfill.ExecuteAsync(connection,explicitLegacyId)`
richiede DB esatto test, Guid esplicito e record LEGACY_MT1 corrispondente. Nessun primo
istituto o nome inferito. SQL canonico `002_profile_backfill.sql` incorporato nel tooling,
non in DI/Program; non crea profili, account o membership. Richiede membership/ruoli
compatibili con i link globali legacy; ruolo incoerente o UNIQUE/FK collision interrompe
l'intera transazione DML. Idempotenza e rollback provati. Profili senza User non assegnati
silenziosamente: restano una decisione di migrazione, non diventano membri inventati.

Checkpoint operativo necessario prima di proseguire:

1. Inventario/manifest delle 21 tabelle e delle PK, finestra senza scritture/worker.
2. Sorgente legacy InstitutionId esplicita approvata; nessun uso dei Guid tecnici dei test
   come fondazione reale. Il record MT1 rimane provisioning finche non verificato.
3. EXPAND restante: ownership nullable, indici giustificati e FK tenant; cataloghi con
   UNIQUE tenant-specific preservando semantica bienni. Ancora SOLO PROGETTATO.
4. BACKFILL intero dataset, blocco ambiguita e confronto ogni cella preesistente. La prova
   consegnata dimostra solo che il backfill dei collegamenti non altera alcuna cella
   delle 21 tabelle; non e attribuzione dell'intero dataset.
5. VALIDATE profili, ownership, link tra Grade/Submission/Exam e audit, FK anche negative.
6. Migrare query/API/worker e account management. Disabilitare bridge pre-backfill prima
   di permanenza membership; nessun CRUD legacy su profili assegnati finche non adattato.
7. CONTRACT solo dopo suite A/B completa e cutover autorizzato. Non eseguito ora.

Recovery: MySQL 5.7 committa il DDL implicitamente. 001 non e uno script di ripresa
automatico: dopo fallimento inventariare indice/tabelle effettivamente presenti e
confrontare schema; non rilanciare senza revisione. Backfill e transazionale e non
annulla DDL. Backup esportato/verificato ma restore completo delle tabelle legacy
NON provato: richiede finestra test controllata prima di cutover, non promettere rollback
atomico o recovery universale. Il gate scolastico nuovo rimane chiuso.

## Aggiornamento MT-2: nessuna nuova migrazione DDL

MT-2 usa le quattro tabelle MT-1 senza modificarle. Entity generate, sidecar e script
SQL/database-first restano invariati. Revoca tramite rilettura DB, nessuna colonna
AuthorizationVersion o nuova infrastruttura. Le sezioni successive fotografano il
piano MT-1; i riferimenti a JWT non modificati valgono per quella fase storica.

Sequenza di adozione controllata:

1. Conservare backup, manifest Guid/contatori e lavoro audit/MT-1. Ripetere suite su
   electronic_register_test; MT-2 non esegue SQL di migrazione all'avvio.
2. Configurare chiave/issuer/audience JWT. Le due nuove audience sono derivate dalla
   base, non da input client. Servono almeno 32 byte di chiave; nessun segreto nel Git.
3. Prima del backfill, se occorre tenere il solo dataset legacy, abilitare esplicitamente
   Identity:Legacy con InstitutionId del manifest, UserIds allowlist controllata e
   AllowUnbackfilledAccounts=true. Senza questa configurazione il vecchio login e chiuso;
   nessun account o membership viene inventato per farlo funzionare.
4. Provare il backfill MT-1 in ambiente test. La holding resta provisioning per policy
   MT-1: non attivarla automaticamente. Verifica umana dell'istituto e attivazione
   controllata sono prerequisiti per usarla come contesto autenticato istituzionale.
5. Prima di usare membership permanenti, impostare AllowUnbackfilledAccounts=false:
   cosi rimozione della membership non riapre il percorso pre-backfill. Conservare
   Enabled/InstitutionId/UserIds soltanto per il bridge del dataset storico necessario.
6. Nuovo login disponibile solo a User esistenti; selezione solo con Institution Active,
   membership Active e almeno un ruolo persistito. Nessun ruolo globale importato durante
   login, nessuna autoattivazione Pending. Un account con membership esterna alla holding
   viene escluso dal bridge anche se allowlisted. Usare il nuovo flusso per tali identita.
7. Microsoft richiede binding server verificati tid/oid/UserId. Il vecchio collegamento
   email non viene mantenuto come scorciatoia. Configurazione assente -> nessun token;
   le credenziali locali restano indipendenti. Nessun provisioning automatico.
8. Le fondazioni secondarie possono essere usate per prove di identita/selezione,
   **non abilitate alle API scolastiche**. Token scoped A/B e ticket vengono rifiutati
   da tutte le 73 route legacy. L'unica API istituzionale operativa e il contesto auth.
9. MT-3 dovra materializzare ownership/profili, controllare FK/query/worker/audit e
   migrare ogni route con test A/B. Solo dopo rimuovere gate e bridge; conservare
   User.Role/StudentId/TeacherId e relazioni didattiche finche tali dipendenze esistono.

Recovery MT-2: non richiede rollback DDL; chiudere i nuovi login/servizio al confine,
revocare/sospendere membership, gestire chiavi e configurazione con deployment controllato.
**Non ripristinare semplicemente il vecchio binario pubblico**: accetta ancora il JWT
legacy e non ha il nuovo gate. Mantenere il confine chiuso durante qualunque rollback.
Non promettere un restore dei dati mai provato o rollback atomico DDL MySQL 5.7.
Rimangono validi backup/restore e gate prima del secondo tenant indicati sotto.

## Piano MT-1 conservato

## Stato e perimetro

Strategia: **EXPAND -> BACKFILL -> VALIDATE -> CONTRACT**. Solo EXPAND delle quattro nuove tabelle e tooling/backfill controllato sono implementati in MT-1. Nessuna colonna o FK legacy rimossa o rinominata. Nessuna API, JWT, formula voto, submit, finalizzazione, randomizzazione, override, audit submission, recupero, tentativo o Close modificati.

Database autorizzato per ogni esecuzione di questa fase: **electronic_register_test**. Mt1SchemaMigration rifiuta altri nomi prima di aprire la connessione e ricontrolla SELECT DATABASE(). Nessun fallback alla connessione principale. Lo script SQL da solo non e una barriera di sicurezza: eseguire tramite il tooling vincolato o una sessione di test verificata; non usare queste istruzioni su DB utenti/produzione. Un futuro rilascio operativo richiedera approvazione, ambiente e versione DB propri.

## Sorgente schema e database-first

Il repository iniziale contiene ElectronicRegisterContext/entity derivati da scaffolding e il solo script incrementale `migrations/20261016_add_override_reason.sql`; **non contiene un DDL completo autorevole dell'installazione legacy o un journal EF migrations**. TestDatabaseSchema crea solo un database vuoto tramite EnsureCreated e verifica colonne/indici/FK su uno esistente; non fa upgrade automatici.

Per MT-1 la sorgente versionata e `migrations/mt1/001_expand.sql`; gli altri script sono `002_backfill.sql` e `003_validate.sql`. Il `.gitignore` locale alla cartella rende tracciabili questi SQL nonostante l'ignore globale *.sql. Gli script sono incorporati come resource nell'Infrastructure per eseguire esattamente gli stessi testi nelle prove. Non vi e alcun hook di avvio che li esegua automaticamente.

Flusso seguito: backup/export canonico del test DB -> DDL nuovo solo sul test DB -> scaffolding delle quattro tabelle piu user verso cartella temporanea -> copia delle sole quattro nuove entity generate -> mapping in `ElectronicRegisterContext.Multitenancy.cs` via OnModelCreatingPartial. User generato temporaneo viene scartato; il mapping membership->User e unidirezionale (`WithMany()`), senza toccare User.cs esistente. Nessun file generato legacy viene editato a mano.

Rigenerazione ripetibile: `migrations/mt1/Scaffold-Mt1.ps1`, con TEST_MYSQL_CONNECTION esplicita. Di default produce candidati da revisionare, `-Apply` copia solo le quattro entity e il sidecar. Non usa --force e non elimina un candidato preesistente. L'EF CLI installata durante la prova e 10.0.12, runtime EF/Pomelo repository 9.0.8/9.0.0: generazione e build sono riuscite; nessun pacchetto cambiato. Prima di rigenerazioni future annotare la versione tool; verificare i diff del candidato. Warning su Student/Teacher non selezionati nel frammento User sono attesi, non applicare quel frammento alla entity User reale. Campus.IsActive viene generato bool? per il default DB, non perche la colonna sia nullable: il mapping lo rende Required e il test verifica che false venga persistito.

Se in futuro si riesegue uno scaffolding completo che gia include le nuove DbSet/configurazioni, rimuovere o integrare il sidecar per evitare duplicati; **non eseguire un overwrite indiscriminato**. Il flusso selettivo qui versionato resta ripetibile senza perdere logica custom.

## Preflight e backup

1. Verificare branch/stato e disponibilita integrale dell'audit; non fare reset/pull distruttivi. Baseline iniziale richiesta 236 casi: rieseguirla prima di modificare schema/codice.
2. Confermare SELECT DATABASE(), VERSION(), @@sql_mode, tipi/collation dei Guid/FK. Nella prova: MySQL MAMP 5.7.24; InnoDB; REPEATABLE-READ; STRICT_TRANS_TABLES; User.Id char(36)/ascii_general_ci. Nessun CHECK assunto efficace su MySQL 5.7.
3. Fermare scritture e worker per una migrazione di dataset operativo; una transazione DML non rende consistente un backfill se nuove righe legacy vengono create fuori da una finestra quiescente. Il test collection e serializzato, le prove non simulano traffico concorrente di migrazione.
4. Esportare schema+dati, conservare in archivio ristretto fuori da Git, calcolare SHA-256 e verificare contenuto/leggibilita e tabella per tabella. Password hash sono dati sensibili: niente dump nei report/TRX/Git.
5. Salvare un manifest canonico con tabella, PK/composite PK o hash di tutte le righe, conteggi e SHA-256; associare immutable migration ID e InstitutionId del record legacy. Verificare recovery prima di un cutover operativo.

Comando di backup effettivamente usato (password via ambiente temporaneo, non nel file):

```text
mysqldump --host=127.0.0.1 --port=3306 --user=er_test --single-transaction
  --column-statistics=0 --no-tablespaces --set-gtid-purged=OFF
  --skip-add-drop-table --result-file=<private-path>/legacy-before-expand.sql
  electronic_register_test
```

Opzioni client qui corrispondono al mysqldump 8 installato contro server 5.7; adattare e provare sulla coppia client/server effettiva. Non interpretare --single-transaction come transazione del DDL: e un export consistente InnoDB se non interviene DDL concorrente.

Backup locale eseguito prima dell'EXPAND, SHA-256 `02C7820A5960EB1E0EBD09BA74D9CBDF70348DAA84549BA2D5A431768CF76B76`. Esportati separatamente dati delle 21 tabelle legacy con ordine PK e INSERT complete; hash pre/post EXPAND identico `AFB4C08687EFD292B5B38604EEF538DED02CF0B957E7369A24E4BEDC8FDEABBB`. Questi controlli verificano export e conservazione dati, **non attestano un restore completo da disaster recovery**. Il dump privato resta locale/ignorato da Git; prima di ogni migrazione operativa e obbligatoria una prova di ripristino sul solo database di test autorizzato in finestra dedicata. Non e stato cancellato/ricreato il database per fingere tale prova.

## EXPAND - sequenza SQL implementata

Ordine di `001_expand.sql`:

1. institution, UNIQUE(code), nessuna FK a regione.
2. campus, FK institution_id NOT NULL -> institution, RESTRICT.
3. institution_membership, FK institution_id/UserId e UNIQUE(user_id,institution_id), RESTRICT.
4. institution_membership_role, PK(membership_id,role), FK membership RESTRICT.

char(36) ASCII identico alla collation User.Id; UTF8 per nomi/indirizzi. Nessuna nuova unique su nome sede, regione o codice fiscale. ENUM ruoli limitato a tre valori e stati espliciti. IsActive NOT NULL. Nessuna aggiunta alle 21 tabelle legacy. CREATE TABLE IF NOT EXISTS consente ripresa dopo DDL parziale, **non corregge uno schema omonimo ma diverso**: controllare DDL effettivo/indici/FK prima di proseguire e fallire su drift. TestDatabaseSchema vede i quattro modelli e verifica presenza strutture; il test MT-1 verifica inoltre le quattro FK con delete/update RESTRICT.

Invocazione controllata, non da Program/API:

```csharp
await Mt1SchemaMigration.ExpandAsync(testConnectionString);
// Solo dopo preflight/backup; la routine rifiuta qualsiasi DB diverso da electronic_register_test.
```

Non creare un secondo tenant attivo. InstitutionRepository.Add consente solo provisioning e non esiste metodo API di attivazione. Naturalmente un amministratore SQL puo cambiare i dati: non e una prova che l'isolamento delle API sia pronto.

## BACKFILL - identita legacy e membership

L'identita di migrazione va fornita esplicitamente come Guid stabile e nome di holding dichiaratamente tecnico. Il codice riservato `LEGACY_MT1` identifica **il record di migrazione**, non una fondazione reale verificata o un codice ministeriale. Il nome di una fondazione vera, sede, regione o codice fiscale non viene inventato. Il successivo onboarding verificato riconciliera quel record con l'identita legale prima dell'attivazione.

```csharp
// Valori provenienti dal manifest/configurazione approvati della migrazione, mai da una richiesta HTTP.
await Mt1SchemaMigration.BackfillAsync(testConnectionString, legacyInstitutionId,
    "Legacy migration holding record - not a verified legal institution");
```

La routine apre una transazione **DML**, esegue preflight di conflitti ID/code/nome/stato e ruoli legacy, poi il testo `002_backfill.sql`:

1. INSERT Institution se assente, sempre provisioning.
2. INSERT membership mancanti per ogni User globale; Id nuovi, UserId originali. Stato active riflette account legacy ma non abilita le API tenant: Institution resta provisioning e JWT legacy non legge membership.
3. INSERT del ruolo quando assente: admin -> InstitutionAdmin **di quel solo istituto**; teacher -> Teacher; student -> Student.
4. Eseguire tutte le SELECT violazioni di `003_validate.sql`; qualunque valore non zero fa rollback DML. Commit solo dopo validazione.

Nessun UPDATE/DELETE legacy, password hash o Guid rigenerato. Utenti senza profilo restano tali; profili senza User non vengono persi ne inventati account. Rerun preserva membership Id/JoinedAt/SuspendedAt/stato/ruoli: una membership sospesa NON viene riattivata. Se i ruoli esistenti sono in conflitto o molteplici rispetto alla mappatura singola legacy, abortire per revisione, non sovrascrivere e non sommare privilegi. Un diverso LegacyInstitutionId con codice LEGACY_MT1 gia presente fallisce; nessuna seconda holding silenziosa.

**Attribuzione dati legacy non ancora materializzata in InstitutionId:** il manifest del dataset associa tutte le PK originali delle 21 tabelle alla sola LegacyInstitutionId, anche righe senza collegamenti (Subject/Teacher/Student senza classe/Audit.EntityId sconnesso). Non si puo dedurre tutto dalla sola membership e non si deve attribuire un tenant arbitrario durante una richiesta. In MT-1 questa e un'associazione di migrazione verificabile, non un filtro SQL operativo. La futura fase aggiungera InstitutionId nullable e valorizzera **tutte e solo** le righe del manifest, verificando che non siano comparse righe fuori inventario prima del NOT NULL/cutover.

La prova realizzata usa un dataset rappresentativo nel DB di test, incluso Exam con risposte, Grade e audit creati attraverso i flussi esistenti. Esegue il vero backfill, verifica le nuove righe persistite da nuovi scope, sospende e riesegue, confronta ogni cella legacy e prova il conflitto di configurazione. Il teardown rimuove soltanto righe MT-1 della prova: **non lascia una fondazione operativa o membership permanenti nel seed single-tenant**. Questo evita di invalidare il reset storico dei test, che elimina/ricrea account scenario. Nessun seed o assertion dei 236 test e stato modificato. L'applicazione permanente al dataset operativo e un gate successivo, non un side effect dell'avvio/test.

## VALIDATE

- 21 tabelle legacy: audit_event, biennium, biennium_study_area, biennium_study_path, class, class_subject, exam, exam_student, exam_submission, exam_violation, grade, question, question_option, student, study_area, study_path, subject, submission_answer, submission_answer_option, teacher, user.
- Confrontare conteggi e hash di ogni cella con il manifest originario; hash include PK/GUID, password hash, punteggi e payload storici. Conservare anche PK esplicite nel manifest operativo se occorrono riconciliazioni per riga, in storage ristretto. Test/TRX contengono solo conteggi e digest, non password hash in chiaro.
- Nuove righe: un solo record di migrazione per codice, una membership per User/Institution, ruolo corretto scoped, assenza orfani; niente SuperAdmin. FK 1452 su riferimento inesistente; 1451 su eliminazione genitore storico; 1062 su duplicati verificati da MySQL.
- Verificare che la holding resti provisioning, API rimangano 73 e JWT/ClaimsContext invariati; suite completa e bootstrap dell'audit continuano a passare.
- Confrontare i nomi dei 236 casi baseline nei TRX finali, non soltanto il totale. Zero Skipped e due run completi consecutivi.

## Recovery e MySQL 5.7

MySQL 5.7 puo committare implicitamente ciascun CREATE/ALTER. Non esiste rollback atomico di tutte e quattro le CREATE; una transazione esterna non cambia questa proprieta. Dopo errore DDL fermare il rollout, inventariare le tabelle realmente create e confrontare con SQL/versione; non proseguire con uno schema parziale senza verifica.

Se dati legacy integri e tabelle nuove senza dati utili: preferire correggere/riprendere EXPAND revisionato dopo backup. Non fornito un down automatico distruttivo e non fare DROP di legacy. Se un nuovo backfill DML fallisce, il tooling fa rollback delle sole nuove scritture della transazione; non annulla DDL gia committati. Se e gia committato ma da annullare, classificare le righe create tramite manifest/migration ID: prima role, poi membership, campus e institution, con verifica di nessun uso successivo. Non cancellare record storici di utenti reali o altre migrazioni per comodita.

Recovery da danno esterno/errore strutturale grave: fermare app/worker, conservare copia dello stato fallito, verificare SHA-256 e destinazione test, ripristinare dal dump in una finestra di manutenzione del database di test dedicata, ripetere verifica schema+manifest e 236 regressioni prima di riaprire. La prova di restore completo richiede autorizzazione/finestra esplicita per ricreare il solo DB di test; non e stata effettuata una migrazione distruttiva in MT-1. Un backup con hash corretto non basta a promettere RPO/RTO, backup operativo o ripristino produttivo.

## CONTRACT - NON eseguito

Eliminabili soltanto dopo migrazione completa di auth/JWT, TenantContext, manager, repository/raw SQL, worker, audit, UI e dati:

- User.Role e User.StudentId/TeacherId, dopo migrazione a ruoli membership e profili multipli per fondazione.
- Student.ClassId dopo enrollment/gruppi storici verificati.
- Relazioni Class/Biennium/BienniumStudyArea/BienniumStudyPath solo dopo crosswalk approvato verso CourseEdition e ricollegamento completo.
- Eventuali UNIQUE globali sostituiti da vincoli tenant-specific; FK legacy sostituite solo quando composite valide e applicazione aggiornata.

Non eliminare Grade o legame submission per semplificare la migrazione; non unificare voti originali/recuperi. Non modificare ordini lock Start/Close o Submit/Finalize per inserire il tenant senza un disegno concorrente e prove riproducibili.

## Gate MT-2 e prima di abilitare una seconda fondazione

MT-2 dovra definire e implementare TenantContext/autenticazione scoped e revoca, ma **non abilitare da solo il secondo tenant** se repository legacy non sono ancora tutti filtrati. Piano di release a feature disabilitata fino a tutti i gate: ownership completa e FK composite; profili/enrollment; manager e tutti i 73 endpoint; raw SQL/aggregati/audit; worker senza JWT e tenant verificato da submission; test A/B e sospensione/concorrenza; frontend e configurazione operativa. Il solo possesso di InstitutionId in JWT firmato non supera la sospensione persistita. PlatformOperator e control plane separato e non acquisisce diritti didattici.
