# ElectronicRegister — MT-5B

## Checkpoint B successivo — persistenza verificata

**MT-5B PERSISTENCE READY; MT-5B complessiva PARZIALE.**
Sidecar security generation e guard SQL applicati/verificati sulla sola MAMP13306:
33tabelle/64trigger/99FK/141indici. Writer tecnici limitati a Provisioning sotto lock
Institution, nessun attore umano inventato. No reset/delete/recreate operativo,
missing-state fail-closed, overflow protetto e compatibilita INSERT SELECT/DELETE JOIN
provata su MySQL5.7.24 prima del DDL e sullo schema reale.

Baseline711/711;27nuovi Passed;118mirati MT1/MT2/MT5A verdi. Prima regressione con4Failed
conservata, corretti inventario offline/fixture senza indebolire assertion; regressione
stabilizzata **738/738 Passed** (51unit+687integration), zero Failed/Skipped/NotExecuted.
Le711identita precedenti sono preservate.377sorgenti compilati invariati nel run finale.

99FK senza orfani,53trigger precedenti preservati+11nuovi verificati, nessuna fixture
residua, GLOBAL e originale3306 invariati nei controlli. Cleanup DROP limitato ai soli
tre oggetti di prova nuovi/vuoti, esplicitamente autorizzato dall'utente; nessun DROP
applicativo/restore/reset. Nessuna modifica JWT, gate, frontend, esami o worker.

[Report completo e storia delle esecuzioni](MULTITENANCY_MT5B_PERSISTENCE_REPORT.md).
Restano futuri JWT monotoni, manager/HTTP, audit IAM, idempotenza e LastAdmin completo.
Nessuna nuova route scrivente, rollout chiuso, nessun MT5C o certificazione MariaDB.

## Storico checkpoint A

**MT-5B PARZIALE — MUTAZIONI IAM NON IMPLEMENTATE; SCRITTURE CHIUSE.**

Completati inventario, baseline e analisi del contratto di sicurezza (checkpoint A).
Non completati persistence, manager/HTTP e certificazione MT5B. Nessuna nuova route,
migrazione applicata, modifica JWT o promessa di Last Admin/revoca monotona.
La baseline verde certifica MT5A, **non** le funzionalita MT5B progettate.

## 1. Baseline e Git

Branch `refactor-database-entities`, HEAD `a002cb66cebecfdcdf892c099cf0518b796b7e1a`.
Workspace preesistente preservato; nessun reset/checkout/pull/commit/push.
[Stato acquisito prima dei test](TestResults/mt5b/initial-git-status.txt),
[HEAD](TestResults/mt5b/initial-head.txt).

Build completa soluzione senza errori, SDK 10.0.401; warning nullable preesistenti.
Nuova baseline realmente eseguita su MAMP MySQL 5.7.24:

| Progetto | Passed | Failed | Skipped/NotExecuted |
| --- | ---: | ---: | ---: |
| Unit | 51 | 0 | 0 |
| Integration | 660 | 0 | 0 |
| Totale | **711** | **0** | **0** |

Verificati outcome individuali e contatori TRX, nomi esattamente uguali ai 711 di
MT5A final-2. Zero test rimossi/rinominati/aggiunti/disabilitati in questa fase.
[Verifica, hash e nomi](TestResults/mt5b/baseline-verification.json),
[TRX Unit](TestResults/mt5b/baseline/Unit/),
[TRX Integration](TestResults/mt5b/baseline/Integration/).
I TRX falliti o verdi dei checkpoint precedenti non sono stati sovrascritti.

## 2. Ambiente e backup

Verificati dinamicamente pid-file/processo/listener, binario MAMP, VERSION/hostname/
porta/datadir/DATABASE e GLOBAL prima dell'esecuzione. Non assunto il PID storico.
Target unico dei test: `127.0.0.1:13306`, MySQL 5.7.24, DESKTOP-QUOAG32,
electronic_register_test, datadir privato mt5a-mamp-13306/data sotto il repository.
Nessun avvio/inizializzazione/restore o fallback 3306/XAMPP.

Backup della sola copia isolata acquisito prima dei test, con mysqldump 8.0.44,
single transaction, trigger/routine, column-statistics=0/no-tablespaces/GTID OFF.
Verificati footer, 32 definizioni tabelle e 53 trigger. Dump privato, non pubblicato.
SHA256 `CBF0900FA61175BF709F051980E67B02543A5FD90FE66B8003AF7920A5D9024D`.
[Evidenza backup](TestResults/mt5b/dump-client-probe.json).

Connessione recuperata dal client.ini privato; nessuna password nuova o stampata.
Override soltanto nei figli: TEST_MYSQL_CONNECTION verso 13306, strict session=1,
mysqldump MySQL e logging EventLog disabilitato per test. Ambiente ripristinato in
finally, nessuna configurazione operativa o GLOBAL sql_mode modificata.

## 3. Schema e migrazioni

Schema resta MT4C: **32 tabelle, 53 trigger, 98 FK, 139 indici**.
Una bozza locale di EXPAND e stata esaminata e ritirata prima di qualunque esecuzione:
il trigger ruoli proposto avrebbe aggiornato il parent letto da statement preesistenti.
Non viene lasciata una migrazione eseguibile nota come incompatibile.
Nessun ALTER/CREATE/DROP/TRUNCATE, backfill, scaffold o ledger IAM applicato.
Nessun evento storico inventato e nessun vincolo/trigger disabilitato.

## 4. Blocco tecnico e contratto da definire

Due punti critici impediscono di considerare pronto il progetto persistence:

1. I writer MT1 AddAsync/AddRoleAsync/SuspendAsync non ricevono ActorUserId/
   ActorMembershipId, EventId o ExpectedRevision. Sono oggi utilizzati anche dai test
   tecnici/backfill. Non si puo attribuire loro un attore amministrativo inventato,
   oppure ignorarli e dichiarare Last Admin/audit validi per tutti i writer ammessi.
   Va definita la distinzione fra mutazioni operative attribuite e preparazione tecnica
   Provisioning, compresa l'identita autorizzata di quest'ultima.
2. Il backfill dei ruoli legge institution_membership in INSERT SELECT; esistono anche
   DELETE JOIN del parent nelle fixture. Il trigger dei ruoli non puo aggiornare quel
   parent gia letto dallo statement iniziale (restrizione MySQL stored program).
   Non e stato riprodotto un errore 1442 su schema modificato: il rischio e stato
   rilevato prima del DDL attraverso sorgenti e documentazione ufficiale.

L'alternativa proposta e uno stato di sicurezza separato per membership, da verificare
insieme ai writer e alla protezione da reset/delete/recreate del contatore. Non e stata
implementata o certificata. La modifica non puo essere ridotta a manager nuovo + COUNT
fuori transazione o incremento della sola lista ruoli nel JWT.

[Inventario, protocollo e decisioni dettagliate](../../docs/architecture/INSTITUTION_MEMBERSHIP_MUTATIONS_AND_AUDIT.md).

## 5. Versione monotona JWT e inventario writer

Nessun meccanismo equivalente persistito rilevato nel modello attuale. MT2 confronta
stati e set ruoli correnti, senza generazione monotona. Il rischio di riabilitazione
del vecchio JWT dopo ripristino resta presente: **non viene dichiarato risolto**.
Non modificati TenantContext, claim, emissione, selezione, autenticazione o resolver.

Inventariati repository MT1, backfill canonico, repository di autorizzazione, manager
MT2, writer diretti EF/SQL delle fixture e flussi legacy User. Non aggiunta una nuova
tabella User, nessun ruolo globale o copia dell'identita condivisa.

## 6. Last Admin, ruoli e sospensioni

Protocollo progettato Institution FOR UPDATE -> rivalidazione caller -> membership
ordinate -> ruoli -> controllo admin persistiti dello stesso tenant -> mutazione/audit
-> commit. Self-administration vietata; due revoche reciproche devono rivalidare il
secondo caller sotto lock, senza sommare ruoli di B. Nessun lock globale o admin fittizio.

**Non implementato.** I writer tecnici restano quelli MT5A; nessuna garanzia nuova
Last Admin viene loro attribuita. Non aggiunte POST add/remove/suspend/reactivate,
nessun profilo Teacher/Student creato automaticamente, nessuna modifica User.Role.

## 7. Audit IAM, idempotenza e concorrenza

Progettato ledger distinto con FK composite di actor/User/target e owner, eventi
append-only, UTC server-side, snapshot minimo, motivo e revisioni. Attore tecnico
non inventato. EventId+hash canonico identico deve riprodurre il risultato; intento
diverso o revision obsoleta devono essere409. Mutazione/audit nello stesso commit.

**Nessun ledger, mapping o writer implementato.** Nessuna prova runtime di atomicita,
concorrenza Last Admin, overflow o replay MT5B. course_administration_event invariato;
non e stato usato per eventi IAM o eventi retroattivi.

## 8. API, sicurezza A/B e compatibilita legacy

Restano soltanto le due GET MT5A. Nessuna POST/DELETE/PATCH o GET audit nuova;
nessuna policy/marker/schema alternativo, nessuna apertura legacy o provisioning.
MembershipAdminApi default false invariato. MembershipWriteApi default false e
progettato ma non ancora introdotto; non esiste una superficie scrivente da abilitare.

Le 711 prove storiche, incluse MT5A A/B/JWT/revoche/gate/read-only e MT3/MT4/worker,
sono realmente Passed nella baseline corrente. Non costituiscono le nuove prove
richieste su revoche monotone, sospensione IAM, audit o Last Admin concorrente.

## 9. Test SQL/HTTP MT5B e due suite finali

**Non eseguiti:** funzionalita non implementate, nessuna nuova identita di test.
Nessuna coppia di suite finali MT5B e nessun source freeze per una certificazione
inesistente. Gli hash storici sono usati soltanto per verificare assenza di modifiche
applicative e ai sei SQL canonici MT4 durante questo incarico.
Nessuna assertion indebolita e nessun rerun indiscriminato per ottenere il verde.

## 10. Stato finale database e sorgenti

Connessioni nuove, tutte98FK senza orfani e definizioni/regole invariate; 53trigger
invariati, 139indici invariati. Ledger/revision MT4C conservati, sette controlli MT4A
e cinque MT4C senza violazioni. Solo holding Provisioning,15User, nessuna fixture
istituzionale residua. Nessuna pulizia manuale o restore dopo la baseline.
[Integrita](TestResults/mt5b/stopped-integrity.json),
[FK](TestResults/mt5b/stopped-fk-integrity.json).

Originale 3306 interrogato solo in lettura per il confronto finale con il riferimento
MT5A: identita/schema/stati/conteggi identici, SHA256
`599891D4994047BAD77F86F3BE3AE09FFFA9C5322B53CFCF7748768E93136CE0`.
Nessuna fixture/DDL/DML/teardown, modifica servizi, credenziali o impostazioni sul3306.
Non si pretende un digest nuovo di ogni cella originale o l'immutabilita di ogni cella
didattica della copia: le fixture legacy modificano intenzionalmente dati di test.

370 sorgenti C# e sei SQL canonici MT4 invariati rispetto alla certificazione MT5A.
Solo documentazione/evidenze/wrapper privati aggiunti, nessuna modifica applicativa.
git diff --check superato; nessun commit/push/cutover.

## 11. Limitazioni e prerequisiti MT5C

MT5B non completata: mancano implementazione e verifica di versione monotona,
copertura writer, Last Admin, audit attribuito/atomico, idempotenza, concorrenza,
policy/flag POST, test nuovi e due suite definitive.
Risolvere il contratto del writer tecnico e provare il design di persistence prima del
DDL; poi proseguire checkpoint B/C/D conservando le711identita e senza bypass insicuri.
Nessuna equivalenza MySQL/MariaDB dichiarata. MT5C, onboarding e inviti non iniziati.

**MT-5B PARZIALE — SCRITTURE CHIUSE.**
