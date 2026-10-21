# MT-5B — contratto di sicurezza delle mutazioni IAM

## Implementazione MT-5B-D (backend interno, nessuna nuova route)

Il manager amministrativo espone ora `MutateAsync(MembershipMutation)` per aggiunta/
rimozione ruolo e sospensione/riattivazione. Il protocollo sotto è implementato
dal repository IAM separato dai writer tecnici Provisioning. ExpectedRevision è
esattamente la security generation persistita: lettura separata sotto lock,
avanzamento dei soli trigger, nessun secondo contatore C#. No-op, revision obsolete
e riutilizzi conflittuali di EventId producono MembershipConflictException.
Pending/Left non sono onboarding o rejoin impliciti. Tutte le automutazioni sono negate.

Il ledger `institution_membership_event` conserva intento SHA256, attore verificato,
target owner-scoped, snapshot minimi e revision prima/dopo. Il replay rivalida sempre
l'attore corrente e lo stato di sicurezza del target, poi restituisce il risultato
originale. Mutazione, generation e audit condividono commit/rollback. FK composite,
immutabilità SQL, snapshot con ruoli stringa piatti e purge limitato alle sole fixture
completano la persistenza additiva. Nessun evento tecnico/storico inventato.

Le sezioni «storico/proposto/futuro» seguenti descrivono i checkpoint precedenti;
non rappresentano più lo stato del writer interno. Le route amministrative scriventi
restano assenti e chiuse. Migrazioni in `migrations/mt5/iam-d/`; risultati effettivi
nel report `tests/Integration/MULTITENANCY_MT5B_D_IMPLEMENTATION_REPORT.md`.

## Checkpoint B — persistenza pronta, workflow IAM ancora chiuso

**MT-5B PERSISTENCE READY.** Contratto e persistenza verificati su MAMP MySQL5.7.24,
porta13306: baseline711/711 e regressione stabilizzata **738/738 Passed** (27 nuovi),
zero Failed/Skipped/NotExecuted. Una prima regressione con4errori e conservata e
corretta senza indebolire assertion. MT5B complessiva resta PARZIALE.
[Report persistence](../../tests/Integration/MULTITENANCY_MT5B_PERSISTENCE_REPORT.md),
[migrazioni e procedura](../../migrations/mt5/README.md).

### Contratto operativo/tecnico applicato

I mutatori MT1 sono persistenza tecnica, non un manager amministrativo. Ora acquisiscono
Institution FOR UPDATE nella propria transazione e ammettono soltanto Provisioning;
Active/Suspended/Archived sono negati, senza consultare User.Role come privilegio.
Nessun controller li espone. Backfill/seeder sono preparazione offline con autorita
del processo, manifest/config verificati e finestra senza writer concorrenti; le
fixture sono setup/fault injection privilegiati. Non sono azioni umane attribuite
a InstitutionAdmin. Nessun ActorUserId finto, System flag client o onboarding aggiunto.

Le future mutazioni interattive richiederanno tutti i controlli del protocollo sotto,
compresa rivalidazione del caller sotto lock, self-administration vietata, ExpectedRevision,
EventId, LastAdmin e audit. Non esistono ancora quelle route o un writer operativo alternativo.

### Stato di sicurezza persistito

institution_membership_security_state ha PK(InstitutionId,MembershipId), UNIQUE MembershipId,
FK composita RESTRICT alla membership e BIGINT SecurityGeneration. Stato0 alla creazione;
trigger incrementano1 per ruolo effettivamente inserito/rimosso e per cambio di stato.
No-op/duplicate/rollback non avanzano il persistito. Owner/User/Id della membership e
identita della sidecar sono immutabili. Generation non puo diminuire; overflow blocca
la mutazione. Missing-state nega lettura e scrittura, senza fallback0.

I trigger modificano la sidecar, mai il parent gia letto da INSERT SELECT/DELETE JOIN.
La compatibilita e stata provata prima dell'EXPAND su tre oggetti isolati (19/19),
poi sullo schema reale. UPDATE di ruolo che cambia chiave e rifiutato: usare primitive
esplicite transazionali. L'unica fixture storica con tale UPDATE e stata adattata
senza modificare le assertion. Nuove forme SQL che leggano la sidecar nello stesso
statement che la modifica via trigger possono fallire chiuse sotto i limiti MySQL.
Il futuro writer deve leggere ExpectedRevision con statement separato sotto mutex,
non incorporare la sidecar nell'INSERT SELECT dei ruoli. Non incrementare anche in EF.

La retention SQL vieta normalmente DELETE di membership/stato, quindi delete/recreate
dello stesso ID non puo ripristinare una generation precedente. Non si assume che i
GUID siano sufficienti. Eccezione **DBA per sole fixture**: root, DB/porta/datadir isolati,
owner espliciti di sessione e codici sintetici GUID. La holding reale e esclusa;
nessun codice produttivo/HTTP imposta questa variabile. Non e protezione contro un DBA.

Backfill iniziale deterministico solo degli stati mancanti, nessun evento storico.
Non e un tool di riparazione automatica dopo la futura attivazione JWT. Ledger IAM,
idempotenza amministrativa e controllo LastAdmin non sono implementati qui.

### Protocollo futuro concretamente integrabile

Institution FOR UPDATE -> rivalidare caller/Institution/membership/ruoli/generation
persistiti -> membership ordinate -> ruoli -> admin residui dello stesso tenant ->
mutazione -> evento IAM -> commit. I test dimostrano mutex fra connessioni reali e
lettura aggiornata del secondo commit; non dichiarano ancora LastAdmin implementato.
I writer tecnici non possono modificare il tenant operativo. L'attivazione futura di
un Institution deve seguire il medesimo protocollo, senza aprire percorsi tecnici paralleli.

Il ledger futuro distinguera actor umano verificato (FK tenant/membership/User) da
attore tecnico (processo/run/manifest verificato, nessun umano inventato). Un evento
storico sconosciuto non viene ricostruito. Non si usa course_administration_event.
L'autorizzazione tecnica non deriva dal ruolo globale admin o da un booleano inviato
dal client; i metadati di processo non conferiscono accesso didattico globale.

### Integrazione JWT — MT-5B-C

Login globale e selection ticket restano separati dall'accesso istituzionale.
SelectAsync legge generation nullable, ruoli, membership, User e Institution in
un solo statement/snapshot AsNoTracking/AsSingleQuery. Missing-state non e zero:
impedisce selezione ed emissione. InstitutionAccess contiene obbligatoriamente
`security_generation`, numero JSON Int64 canonico non negativo, senza array,
duplicati, stringhe numeriche o fallback. Il ticket di selezione non puo contenerlo.

Il parser e l'authorization handler richiedono generation e identita complete;
ogni richiesta protetta confronta generation, identita e set ruoli con il persistito,
oltre agli stati attivi di membership/Institution. Generation vecchie sono negate
anche dopo ripristino di ruoli/status della membership. Mutazioni A non revocano B
dello stesso User. Nessuna cache di revoche, privilegio da User.Role globale o
fallback legacy. Un TenantContext interno privo di generation non puo autorizzare
o emettere un JWT; null resta distinto da zero.

I JWT precedenti senza claim vengono rifiutati: occorre una nuova selezione
legittima. Un eventuale deployment richiede attivazione coordinata su tutti i nodi,
senza validator vecchi o accettazione permissiva v1. Non viene effettuato un
deployment esterno qui. Writer amministrativi, LastAdmin e audit IAM restano
chiusi. Evidenze di implementazione e test reali nel
[report MT-5B-C](../../tests/Integration/MULTITENANCY_MT5B_C_IMPLEMENTATION_REPORT.md).

## Storico checkpoint A — analisi precedente alla persistenza

**Progetto, non implementazione. MT-5B PARZIALE; scritture chiuse.**
Non esistono nuove POST, migration applicate, SecurityVersion persistita o ledger IAM.
La directory MT5A rimane quella certificata, con rollout default false.

## Inventario dei writer e confine da risolvere

| Percorso reale | Operazioni attuali | Controlli mancanti per IAM |
| --- | --- | --- |
| InstitutionMembershipRepository.AddAsync | Crea membership e ruoli, salva EF | Nessun attore, EventId, revision o lock tenant; nessuna verifica dello stato Institution |
| InstitutionMembershipRepository.AddRoleAsync | Verifica owner, aggiunge ruolo, salva EF | Verifica e mutazione separate; nessun attore, audit o mutex Institution |
| InstitutionMembershipRepository.SuspendAsync | ExecuteUpdate scoped di status e SuspendedAt | Nessun attore, Last Admin, audit o mutex Institution |
| Mt1SchemaMigration.BackfillAsync / migrations/mt1/002_backfill.sql | INSERT SELECT di membership/ruoli della holding Provisioning | Flusso tecnico storico senza attore IAM; non e un pannello amministrativo |
| InstitutionRepository.AddAsync | Solo Institution Provisioning | Non autorizza attivazione o primo admin; preservare questa chiusura |
| MembershipAuthorizationRepository | Legge User/membership/Institution/ruoli in un solo snapshot | Attualmente nessuna generazione monotona |
| InstitutionAuthManager / InstitutionAuthentication / InstitutionTokens | Login, selection, access e rivalidazione stato/set ruoli | Ripristino stato/set ruoli puo rendere nuovamente valido un token non scaduto |
| InstitutionMembershipAdminManager/Controller | Due GET scoped | Nessun writer o audit; non aprire nuove action senza invarianti complete |
| UserRepository/legacy UserController | Operazioni sull'identita globale sotto gate | Nessuna autorizzazione istituzionale; FK membership->User RESTRICT impedisce cascata |
| Fixture MT1/MT2/MT3/MT4/MT5A | EF diretto, ExecuteUpdate/Delete, SQL parametrico e teardown | Operazioni privilegiate sintetiche: non assumerle equivalenti a chiamate IAM attribuite |

Ricerca dei writer effettuata sui sorgenti effettivi. Non risulta un metodo produttivo
di rimozione/trasferimento membership da aggiungere tacitamente al nuovo protocollo.
I writer di ruoli/stato esistenti non sono oggi autorizzazioni amministrative sicure.
Nessuna nuova API deve esporli direttamente.

## Decisione necessaria: identita del writer tecnico

Le firme MT1 non ricevono un caller. Non si puo attribuire retroattivamente a un User
o a una membership arbitraria una sospensione tecnica. `root`, il primo admin trovato,
il target stesso, User.Role o un SuperAdmin didattico non sono sostituti dell'attore.
Non viene introdotto un attore nullo/di sistema con privilegi impliciti per aggirare
la regola di attribuzione richiesta all'audit IAM.

Proposta sicura per la revisione del contratto:

1. Per Institution operative, ritirare l'uso dei mutatori MT1 senza attore. Instradare
   tutte le mutazioni ammesse nel protocollo IAM autenticato sotto lock Institution.
2. Per preparazione/backfill Provisioning, decidere esplicitamente se conservare un
   canale tecnico separato e quale identita di provisioning verificata lo autorizzi.
   Il modello di attore e audit tecnico deve essere distinto dalla membership fittizia.
3. Adeguare fixture e contratti dei test MT1 senza cambiare nomi, numero o invarianti
   di isolamento/conservazione. I test attuali certificano anche le firme tecniche.
4. Proibire percorsi operativi alternativi privi di audit/lock. Nessun bypass root
   generalizzato negli script o nelle API per conservare artificialmente il verde.

La scelta riguarda il canale tecnico esistente, non il nuovo caller InstitutionAdmin,
gia definito. Nessun onboarding o recupero senza admin viene implementato qui.

## Revoca monotona e restrizione MySQL 5.7

Una sola colonna sul parent, incrementata da trigger della tabella ruoli, non e una
soluzione compatibile con tutti gli statement esistenti. Il backfill usa INSERT SELECT
leggendo institution_membership; Mt3aOwnershipTests contiene un DELETE JOIN che legge
lo stesso parent. Un trigger dei ruoli che UPDATE quel parent entra nella restrizione
MySQL sulle tabelle gia usate dallo statement chiamante.
[Manuale ufficiale MySQL 5.7](https://downloads.mysql.com/docs/refman-5.7-en.pdf),
sezione Stored Program Restrictions. Questa e analisi statica sostenuta dal manuale,
**non** un errore 1442 riprodotto durante una migrazione: nessun DDL e stato applicato.
La bozza locale non sicura e stata ritirata, non lasciata come script eseguibile.

Alternativa proposta: stato di sicurezza separato per membership, con BIGINT >=0 e
incrementi atomici su ogni effettivo INSERT/DELETE di ruolo e cambio di stato.
Il trigger non deve scrivere un parent letto dallo statement iniziale. Verificare
esplicitamente anche UPDATE di ruolo, multi-row, INSERT SELECT, DELETE JOIN, overflow,
immutabilita owner/User/Id, cancellazione e possibile ricreazione degli identificatori.
Un writer incompatibile deve fallire chiuso, mai completare senza incremento.

La scelta della tabella separata richiede inoltre un protocollo per bootstrap della
riga di sicurezza, FK/cascade e teardown. Non promettere protezione monotona dopo
DELETE/recreate della riga se un writer ammesso puo azzerarne il contatore.
Backfill della sola versione iniziale deterministica; nessun evento storico inventato.

JWT InstitutionAccess dovra richiedere un claim versione univoco, intero non negativo
nel range Int64; manca/duplicato/malformato/overflow -> token rifiutato. Selection ticket
non concede tale accesso. Emissione dalla versione persistita, confronto su ogni richiesta
insieme agli attuali stato/owner/set ruoli, nessuna cache e nessun fallback per vecchi JWT.
Versione obsoleta resta invalida dopo sospensione/riattivazione e rimozione/ripristino ruoli.
La modifica di A incrementa solo la membership A dell'User globale condiviso.

## Protocollo operativo Last Admin

Tutti i writer operativi ammessi devono condividere stessa connessione/transazione:

1. Begin; prima locking read: Institution verificata FOR UPDATE, mutex solo di quel tenant.
2. Rivalidare Institution Active e caller User/Membership/ruoli/versione persistiti.
3. Lock caller/target in ordine MembershipId deterministico, poi ruoli; nessuna lettura
   consistente precedente al mutex che possa congelare uno snapshot obsoleto.
4. Target cercato con InstitutionId+MembershipId, fuori tenant indistinguibile da assente.
5. Vietare ogni modifica dei propri ruoli e autosospensione; nessun privilegio da User.Role.
6. Prima di ridurre gli admin, controllare altri admin Active con User valido nello stesso
   tenant; profili Teacher/Student non sono requisiti per essere InstitutionAdmin.
7. Controllare replay, ExpectedRevision/overflow, stato e ruolo; mutazione, incremento
   monotono, append evento IAM e commit. Ogni errore implica rollback della transazione.

Due revoche reciproche si serializzano sul tenant: il secondo caller viene rivalidato
dopo la prima revoca e non puo proseguire usando l'autorizzazione HTTP precedente.
I ruoli B non contano per A. Nessun lock globale, retry indiscriminato, admin sostitutivo
automatico o promessa di Last Admin finche il protocollo non copre i writer tecnici.

## Ledger IAM proposto, distinto dall'audit didattico

institution_membership_event: chiave tenant/EventId, ActorUserId/ActorMembershipId,
TargetMembershipId, Action, TargetRole opzionale, Previous/NewStatus e set ruoli minimo,
Reason, RevisionBefore/After, RequestHash, OccurredAtUtc assegnato server-side UTC.
FK composite tenant/actor membership/User e tenant/target; validazione SQL di shape,
stati, revisioni e snapshot. Nessuna password, token, credenziale o anagrafica duplicata.

UPDATE/DELETE eventi negati. Eventuale purge soltanto per fixture sintetiche identificate
esplicitamente sulla 13306 isolata, mai per holding o per il solo fatto di essere root;
non disabilitare FK/trigger. Nessuna API di modifica/cancellazione ledger.
Audit e mutazione devono condividere commit/rollback; testare fault prima/dopo append.
L'identita dell'attore tecnico resta da definire prima di ammettere quel writer nel ledger.

Replay: hash canonico di tenant, attore, target, action, ruolo/stato, motivo, revision.
Stesso EventId+intento -> risultato originale; intento diverso ->409; revision obsoleta
->409. Conservare unicita EventId nel tenant e serializzare il controllo con il mutex.
Nessun tentativo rifiutato viene registrato come mutazione riuscita.

## API previste, tutte non implementate e chiuse

Quattro POST esplicite roles/add, roles/remove, suspend, reactivate e GET audit sotto
/api/institution/admin/members/{membershipId}. DTO dedicati con EventId non vuoto,
ExpectedRevision >=0, motivo validato e ruolo del catalogo esatto, senza InstitutionId.
Pagine audit 1..100, ordine deterministico revision/data/EventId, query SQL owner-scoped.
Nessun PATCH generico, DELETE membership, invito, provisioning o modifica credenziali.

GET audit richiedera il flag directory MT5A. POST richiederanno entrambi i flag
MembershipAdminApi e MembershipWriteApi (default false), schema InstitutionAccess,
policy esatta e marker non combinabile validato da LegacyEndpointGate. Il secondo
flag non e ancora implementato: non esistono route scriventi cui possa concedere accesso.
Risposte previste:200/204,400 input,401 token,403 autorizzazione/self,404 owner/assente,
409 revision/ruolo/LastAdmin/idempotenza. Nessun SQL o dato cross-tenant negli errori.

## Gate per ripresa e certificazione

Definire il writer tecnico attribuito, implementare e verificare il meccanismo monotono
compatibile con i JOIN esistenti, poi EXPAND/backfill/VALIDATE/scaffold riproducibile
sulla sola 13306 con backup e preflight. DDL con commit impliciti: non promettere rollback
globale. Nessun nuovo DDL e stato eseguito in questo tentativo.

Poi test SQL/HTTP A/B reali per tutti i comportamenti richiesti, snapshot con nuovi
DbContext, revoche monotone, Last Admin concorrente, audit failure, replay e gate.
Preservare le 711 identita; freeze prima di due suite complete consecutive. Nessuna
certificazione MariaDB senza una nuova esecuzione su MariaDB. Nessun MT5C.
