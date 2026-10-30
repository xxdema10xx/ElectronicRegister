# Piani formativi ITS versionati

La Fase 4 aggiunge un catalogo tenant-scoped di piani riutilizzabili, snapshot didattici
versionati e binding espliciti a CourseEdition. Le prove effettivamente concluse sono
nel [report](../../tests/Integration/INSTITUTION_FORMATIVE_PLAN_REPORT.md).

## Modello e compatibilità

Institution è la Fondazione/tenant. StudyPath è una definizione riutilizzabile;
CourseEdition è un'offerta concreta; Biennium conserva il significato di intervallo legacy.
Il nuovo piano appartiene a uno StudyPath dello stesso tenant. Non esiste inferenza
da anni, nomi, Class, Student.ClassId o Biennium.

Otto tabelle additive: formative_plan, formative_plan_version, formative_unit,
formative_module, formative_module_competency, formative_module_content,
course_edition_formative_plan e formative_plan_event. Nessuna modifica a tabelle,
alternate key o trigger legacy. Mapping EF Database First in un sidecar dedicato;
le FK composite rimangono enforceate in SQL, senza aggiungere alternate key EF
ai cataloghi con owner shadow o alterare i loro generatori legacy.
Il preflight del vecchio backfill riconosce le otto nuove tabelle solo dopo controlli
read-only dedicati; l'insieme delle tabelle da backfillare resta invariato. Nessun piano
o binding viene creato o ricostruito durante il backfill legacy.

Domain conserva valori e contratti; Business converte/normalizza la struttura tramite
IFormativeDraftBuilder; Application orchestra l'autorizzazione; Infrastructure possiede
unità di lavoro, SQL parametrizzato, mapping e audit; Api applica contratto HTTP e ruoli.
Le regole aritmetiche e di validazione restano condivise in FormativePlanRules.

Il codice del piano è immutabile e UNIQUE(InstitutionId,Code), con collation MySQL
utf8mb4_unicode_ci. Il nome amministrativo può cambiare con revisione e audit senza
riscrivere le denominazioni degli snapshot. Nessuna cancellazione di piano/versione.
I GUID sono globalmente univoci; i riferimenti sono comunque composite tenant-aware.

## Versioni e struttura

Stati: Draft e Published, senza archiviazione o transizioni implicite.
Numero progressivo per piano, assegnato nella transazione sotto lock persistente;
UNIQUE(InstitutionId,PlanId,VersionNumber) è l'ultima barriera contro collisioni.
Una versione Published è uno snapshot: nome, budget, UF, moduli, Subject, competenze,
contenuti e ordine sono immutabili, anche tramite normali INSERT/UPDATE/DELETE SQL.
Un nome del piano cambiato successivamente non cambia il nome della versione.

Clone consentito soltanto da Published e con revisione sorgente esatta. Genera una
Draft indipendente, revisione zero, nuovi ID per versione/UF/moduli/competenze/contenuti;
conserva Subject e proprietà didattiche. Un retry con lo stesso EventId restituisce
la stessa ricevuta; un altro intento può creare un'altra versione con numero distinto.
CreatedAtUtc e PublishedAtUtc vengono letti dal database e restituiti come UTC.

Una PUT della versione sostituisce **la struttura Draft completa in una transazione**.
Questa è l'operazione per aggiungere, modificare, rimuovere e riordinare UF/moduli,
competenze e contenuti; non ci sono endpoint di cancellazione fisica Published.
Un ID null crea un nuovo elemento; un ID presente deve appartenere allo stesso
parent già caricato. Gli ID non possono provenire da altre versioni, UF o collezioni.
Un trasferimento concettuale tra UF richiede rimozione e creazione con nuovi ID;
non viene riassegnato il parent di un'identità esistente. Gli ID conservati restano stabili.

Ordini interi non negativi e unici tra fratelli; intervalli sono ammessi. UF ordinate
per DisplayOrder/Id, moduli analogamente, competenze/contenuti analogamente.
Codice UF opzionale unico nella versione; codice modulo opzionale unico nella UF.
La validazione pura anticipa collisioni OrdinalIgnoreCase dopo trim; la collation
SQL resta autoritativa anche per equivalenze linguistiche ulteriori (conflitto 409).
I nomi non sono chiavi: possono ripetersi anche nella stessa UF se i codici/ordini sono distinti.

Competenze e contenuti sono elenchi distinti, liberi e ordinati; non sono certificazioni,
tassonomie normative o valutazioni studente. Pubblicazione richiede almeno un elemento
in ciascun elenco per modulo. Testo competenza/contenuto massimo 4000 caratteri;
nome 200, codice 64, descrizione 65535 byte UTF-8. Limiti tecnici: 500 UF/versione,
500 moduli/UF, 200 elementi per elenco; body HTTP massimo 2 MiB.

## Ore e validazione

Budget teorico positivo, budget tirocinio non negativo, durata modulo positiva:
minuti interi, massimo tecnico 10000000 per componente. Nessun requisito universale
1200/800: sono esempi sintetici; 1300/700, 1100/900 e altre combinazioni sono ammesse.
Totale complessivo deriva dalle componenti Int64; totale UF deriva dai moduli.
Le durate non intere in ore (45,75,90,135 minuti) sono rappresentabili esattamente.
Nessun conteggio ore svolte/programmate o certificazione di conformità normativa.

La Draft può essere incompleta, senza UF, con UF vuote, competenze/contenuti vuoti,
Subject null e totale diverso dal budget. Sono comunque rifiutati testi invalidi,
ordini duplicati, identità incoerenti, minuti invalidi e Subject inesistenti/fuori tenant.
Hour-summary/validate restituiscono budget, somma moduli, minuti mancanti/eccedenti,
issue code/resourceId e CanPublish. Nessun autoaggiustamento delle ore.

Publish richiede struttura completa e somma moduli **esattamente** uguale al budget
teorico. Il trigger di pubblicazione verifica anche somme, cardinalità e Subject
obbligatori; FK, indici e trigger dei figli garantiscono proprietà e valori validi.
Tirocinio è soltanto InternshipPlannedMinutes: nessuna azienda, tutor, placement,
presenza, diario o gestione operativa aggiunta.

## Transazioni, revision, audit e concorrenza

Scritture ReadCommitted, mutex Institution esclusivo, quindi lock IAM su Institution,
membership, ruoli e security state secondo la convenzione esistente. Autorizzazione
rivalidata sotto lock e prima del commit; nessun lock in memoria come garanzia.
Tutte le mutazioni della stessa Fondazione sono serializzate: scelta conservativa che
privilegia consistenza rispetto al parallelismo delle scritture amministrative.

EventId UUID è la chiave dell'intenzione, hash SHA256 include operazione, risorsa,
payload, utente, membership e tenant. Dopo i lock si consulta il ledger prima di ogni
scrittura: stessa intenzione restituisce la ricevuta originale anche dopo revisioni
successive; payload/attore diversi producono 409. EventId deve essere un UUID nuovo
per ogni intenzione (PK globale, lookup sempre tenant-scoped).

ExpectedRevision è obbligatoria per aggiornamenti, publish e clone. Ogni modifica Draft
incrementa la revisione della versione una sola volta; rename incrementa quella del piano.
Creazioni e clone partono da zero; creazione versione alloca il prossimo numero sotto
lock del piano. Binding usa la precondizione persistente «nessun binding precedente».
Revisioni appartengono alla rispettiva risorsa, non a una sequenza globale del ledger.

Audit atomico append-only: attori reali, tenant, risorse, operazione, revisioni prima/dopo,
motivo (obbligatorio, max 500), UTC, hash, BeforeJson e AfterJson. AfterJson contiene
Receipt per il replay e State con il nuovo stato pertinente. Per clone BeforeJson
identifica la versione sorgente. SQL vieta UPDATE/DELETE del ledger e controlla
membership Admin attiva e riferimenti coerenti alla creazione dell'evento.

I trigger dei figli prendono FOR UPDATE sulla versione prima di modificare elementi:
un writer SQL ordinario non può superare una pubblicazione concorrente. La pubblicazione
prende lo stesso lock. Le letture gerarchiche usano una snapshot RepeatableRead per
evitare mescolanze tra revisione e figli di modifiche concorrenti.
Fault observer registrato no-op in produzione consente fault injection nei test dopo
modulo, struttura, audit e prima del commit. Ogni eccezione lascia rollback della
transazione, inclusi clone e binding. Nessuna promessa di protezione contro DBA che
rimuovano i trigger o disabilitino esplicitamente l'integrità.

## Binding e accesso

Binding iniziale solo a Published, stessa Institution e stesso StudyPath dell'edizione.
UNIQUE tenant/edizione e trigger impediscono sostituzione, unlink e DELETE. Nessuna PUT
generica cambia il binding. Una futura rettifica richiederà workflow separato motivato
che consideri dati didattici storici. Pubblicare una nuova versione non sposta edizioni.
Legacy CourseEdition senza binding e Class senza CourseEdition restano valide.

Admin gestisce e legge audit; Staff legge catalogo, struttura e budget senza mutare.
Teacher legge soltanto il binding di edizioni con assegnazione ClassSubject reale a
una classe; Student soltanto edizioni delle proprie StudentEnrollment, anche storiche.
Profilo sempre della membership corrente; nessuna prova basata su User.TeacherId legacy.
Le viste personali non enumerano catalogo, persone o audit. Staff+Teacher non è Admin;
PlatformOperator e i ruoli globali storici non conferiscono privilegi istituzionali; UserRole e il percorso applicativo globale sono ritirati.
Risorse fuori tenant e inesistenti producono la stessa assenza, senza dettagli SQL.

Tre flag: FormativePlans.Enabled, SchoolApi.Enabled, CourseApi.Enabled, default false.
Con FormativePlans disabilitato all'avvio il controller non viene pubblicato; attivazione
richiede restart. La disabilitazione runtime blocca anche manager/repository e commit.
InstitutionAccess, membership/Institution attive, insieme dei ruoli e SecurityGeneration
sono rivalidati in tutti i percorsi. Nessuna modifica alle policy legacy esami/import/email.

## Fase 5 e limiti

GET edizione/formative-plan risolve la versione e la gerarchia; GET del modulo sotto
la stessa edizione verifica che appartenga esattamente al binding autorizzato.
ID modulo, Subject e PlannedMinutes preparano l'import calendario. Moduli distinti con
Subject uguale non generano ClassSubject duplicati. Exam e Grade continuano a referenziare
ClassSubject e non ricevono FormativeModuleId. Nessuna attribuzione retroattiva di esami,
voti o carriere, nessuna modifica a enrollment, Class o Student.ClassId dal binding.

Non implementati perché esclusi: frontend, import piani/calendari, Lesson, disponibilità,
generazione orari, ore effettive, certificazione competenze e tirocinio operativo.

## Migrazione e recovery

`migrations/institution-formative-plan/001_expand.sql` è additiva MySQL 5.7.24;
`002_validate.sql` è read-only. Predecessore conservato in predecessor-contract.json.
Bootstrap canonico incorpora esattamente lo script. Inventario finale: 60 tabelle,
153 trigger, 171 FK e 257 indici. Nuovi oggetti: 8/24/13/39.

`tools/formative_test_db.py` opera soltanto su nuove istanze dentro .staff-private,
porte diverse da 3306/13306. Controlla versione, PID/binary path, UUID, porta, datadir,
predecessore completo e assenza di altri client; produce backup leggibile con SHA256
prima del DDL e confronta tabelle/trigger legacy dopo. DDL MySQL ha commit impliciti:
in caso di errore fermarsi, conservare log, backup e datadir, inventariare lo stato
parziale; nessun replay, DROP/reset o promessa di rollback transazionale DDL.
Per recovery ripristinare il backup solo in un'altra nuova istanza sacrificabile,
verificare il predecessore e pianificare esplicitamente l'intervento; il runner non
esegue restore automatici e non è uno strumento di deployment produttivo.

I guard legacy di teardown delle sole fixture vengono parametrizzati nel testo
installato **prima del CREATE iniziale** per porta/datadir esatti; nessun trigger
esistente viene sostituito e nessuna regola produttiva viene indebolita.
La regressione storica richiede la credenziale root privata della sola istanza test.
