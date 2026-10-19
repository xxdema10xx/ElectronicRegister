# Lifecycle amministrativo MT-4C

MT-4C completata: due suite 675/675 Passed, zero Failed/Skipped; 644 identita precedenti preservate. Rollout chiuso. Questa sezione aggiorna il modello MT4B conservato
sotto come snapshot del checkpoint precedente. [Lifecycle e audit](ENROLLMENT_LIFECYCLE_AND_AUDIT.md),
[report MT4C](../../tests/Integration/MULTITENANCY_MT4C_REPORT.md).

StudentEnrollment conserva una sola relazione logica studente/edizione e la UNIQUE originale.
Revision persistente default0 distingue gli aggiornamenti successivi. Soltanto enrolled puo diventare
completed, withdrawn o cancelled, con data amministrativa esplicita e motivazione; una rettifica
motivata puo cambiare esclusivamente CompletionDate terminale. Stato/date non sono dedotti da
Class, Biennium, calendario edizione o clock server. Student.ClassId e tutti i dati Esami rimangono
indipendenti. Nessun rientro/secondo episodio o correzione EnrollmentDate/stato/owner.

course_administration_event conserva gli eventi successivi all'introduzione MT4C: attore globale,
membership istituzionale verificata, riferimenti tenant-aware, valori precedenti/nuovi, motivo,
revision e istante UTC. Stato corrente, date amministrative note e sequenza degli eventi disponibili
sono concetti distinti. Nessun evento retroattivo certifica la storia mancante delle iscrizioni legacy.
Le creazioni MT4B in tutti i quattro stati continuano a funzionare; una data terminale prima ignota
puo essere rettificata esplicitamente mantenendo il precedente null nello storico.

Mutazione e registrazione sono atomiche. ExpectedRevision impedisce gli aggiornamenti persi;
EventId identifica un'intenzione idempotente. Lo stesso registro documenta creazioni/descrizioni
edizioni, collegamenti Campus e prime attribuzioni Class senza duplicare gli eventi lifecycle.
Normali UPDATE/DELETE degli eventi sono negati anche da SQL; la manutenzione privilegiata delle
sole fixture sintetiche nel DB test e i limiti contro DBA sono documentati nel modello dedicato.
Lo storico amministrativo e riservato a InstitutionAdmin corrente; /me rimane una vista personale
dello stato attuale, senza motivazioni interne. Non sono stati aggiunti trasferimenti o unlink.

---

# Utilizzo applicativo MT-4B

**Checkpoint completato: due suite 644/644, zero Failed/Skipped; rollout chiuso.**

Le definizioni persistenti MT-4A sottostanti restano valide. Il modello e ora esposto da 15 action
istituzionali con rollout CourseApi && SchoolApi, entrambi default false. InstitutionAdmin della
membership corrente puo creare/consultare edizioni, aggiornare descrizioni con controllo versione,
collegare sedi, attribuire esplicitamente un gruppo coerente e registrare/consultare iscrizioni.
Student consulta soltanto le proprie iscrizioni dal profilo della membership selezionata; Teacher
non acquisisce accesso amministrativo. Nessun cambiamento automatico a Student.ClassId.

Creazione con date/stati espliciti; aggiornamento edizione non cambia Status/StudyPath/owner.
Una Class gia attribuita non passa a un'altra edizione; le legacy nullable restano valide. Collegamenti
sedi senza sede principale/unlink, inattivita senza rimozione. Un solo rapporto Student/edizione,
iscrizioni a edizioni differenti anche sovrapposte; nessun account necessario per l'iscrizione.

Le policy di transizione/reingresso/rettifica rimangono sospese: non sono prerequisiti per le sole
operazioni sicure e separabili esposte qui. Non e stato inventato un workflow per chiudere il checkpoint.
L'audit MT3A riconosce soltanto exam/exam_submission e platform di forma stretta; i nuovi tipi non
sono supportati. Creazioni/descrizioni non hanno un evento amministrativo append-only certificato;
nessuna attribuzione fittizia sotto EntityType esistente. Prima di rettifiche o cambi stato occorrono
policy approvata, supporto audit con owner/actor/evento e atomicita, controllo concorrenza dedicato.

Nessuna nuova migrazione o backfill MT4B. I manifest legacy e la storia mancante non vengono
ricostruiti da ClassId. Snapshot attuale, date conosciute e stato non equivalgono a storia certificata.
Nessun MT-4C/onboarding o cutover avviato. [Contratti HTTP](../api/COURSE_FRONTEND_CONTRACTS.md),
[report](../../tests/Integration/MULTITENANCY_MT4B_REPORT.md).

---

# ITS Academy: percorsi, edizioni e iscrizioni (MT-4A)

La Fondazione ITS Academy e il tenant Institution. Regione e Campus non sono tenant.
MT-4A aggiunge persistenza e repository interni, senza controller, manager pubblici o cutover.

## Inventario e significati

| Concetto | Modello e dati effettivi | Significato |
| --- | --- | --- |
| Fondazione | Institution, stato e identita legale opzionale | Owner del catalogo e della didattica |
| Sede | Campus, InstitutionId, nome/indirizzo, IsActive | Luogo; non edizione e non tenant |
| Area | StudyArea, nome/descrizione scoped | Catalogo locale |
| Percorso | StudyPath, nome/descrizione scoped | Definizione formativa riutilizzabile |
| Intervallo | Biennium, StartYear/EndYear DateOnly | Intervallo legacy; nessuna prova di una concreta offerta |
| Organizzazione legacy | BienniumStudyArea -> Biennium/StudyArea; BienniumStudyPath -> BienniumStudyArea/StudyPath | Associa catalogo e intervallo |
| Gruppo | Class -> BienniumStudyPath, nome | Gruppo didattico; non cambia semantica |
| Materia del gruppo | ClassSubject -> Class/Subject/Teacher | Assegnazione esistente, preservata |
| Profilo | Student, InstitutionId shadow, ClassId nullable | Anagrafica locale e assegnazione attuale |
| Identita | User globale -> InstitutionMembership -> ruoli | Accesso, distinto dalla carriera |
| Link profilo | InstitutionStudentProfile, FK membership/student nello stesso tenant | Opzionale; iscrizione non richiede account o membership |
| Offerta concreta | CourseEdition -> StudyPath/Institution | Istanza concreta identificata localmente |
| Iscrizione | StudentEnrollment -> Student/CourseEdition | Relazione individuale mantenuta anche dopo cessazione |

I dati legacy non contengono date di iscrizione, cronologia di classe o trasferimenti di sede.
Student.ClassId e uno snapshot corrente, non prova di frequenza pregressa. Biennium non viene
convertito in CourseEdition. ClassSubject, Exam, Submission, Grade e formule restano invariati.

## CourseEdition

Id globale tecnico, InstitutionId e StudyPathId obbligatori e immutabili. Code locale obbligatorio
(max 64), Name (max 200); UNIQUE(InstitutionId,Code), con collation utf8mb4_unicode_ci:
confronto senza distinzione maiuscole/minuscole come gli altri cataloghi. Il codice e un'etichetta
locale, non un codice ministeriale. DateOnly StartDate/EndDate opzionali: possono non essere
ancora note; se entrambe note EndDate >= StartDate. Nessun limite automatico di durata o
vincolo di due anni. Dominio date persistibile MySQL, anni >=1000.

Status tecnici: planned, active, completed, cancelled. Non sono una tassonomia normativa.
La persistenza valida l'insieme; non decide transizioni amministrative, prerequisiti di attivazione,
obblighi di calendario o relazione con lo stato della fondazione. CreatedAt/UpdatedAt sono timestamp
DB datetime(6), non date didattiche e non vengono retrodatati.

CourseEditionCampus usa PK(InstitutionId,CourseEditionId,CampusId), due FK composite RESTRICT.
Zero o piu sedi, nessuna sede principale implicita. Una sede puo servire piu edizioni. IsActive=false
non cancella relazioni. Owner e identita dei link non trasferibili. La relazione descrive sedi associate,
non intervalli di utilizzo, presenza individuale o una cronologia certificata di trasferimenti.
Nessun metodo di unlink/delete viene esposto in MT-4A.

## Class e percorso

Class.CourseEditionId e una shadow property nullable aggiunta in EXPAND, con FK composite
(InstitutionId,CourseEditionId). Una edizione puo avere piu gruppi; una classe puo avere zero o una
attribuzione. BienniumStudyPath e preservato e resta obbligatorio. Trigger e repository verificano
che StudyPath del gruppo e dell'edizione coincidano. Una attribuzione effettuata non si sostituisce
ne si annulla implicitamente; anche il percorso del BienniumStudyPath con classi attribuite e protetto.
Nessun vincolo impone che date del Biennium coincidano con il calendario dell'edizione.

Le Class legacy restano NULL: occorre un manifest verificato classe -> edizione, comprendente
owner, percorso e provenienza amministrativa. Non vengono create edizioni fittizie o spostati Student.
Un eventuale trasferimento di gruppo richiede progettazione separata: nessun nuovo storico di
classe e necessario per rappresentare fedelmente i soli dati oggi disponibili.

## StudentEnrollment e storia

Id, InstitutionId, StudentId, CourseEditionId, EnrollmentDate obbligatori e immutabili; CompletionDate
opzionale >= EnrollmentDate. Status: enrolled, completed, withdrawn, cancelled. CompletionDate
indica la fine amministrativa dell'iscrizione quando nota, anche per rinuncia; nessuna data viene
inventata quando lo stato cambia. Nessun obbligo di coincidenza con date dell'edizione, nessun
blocco a percorsi contemporanei, nessuna verifica basata sulla classe corrente.

Una relazione logica per (InstitutionId,StudentId,CourseEditionId), anche dopo cessazione: cambiare
stato non rimuove la riga. Lo stesso Student conserva tutte le relazioni a edizioni differenti.
Questa unicita evita la duplicazione della stessa iscrizione; non rappresenta tentativi o episodi multipli
nella stessa edizione. Eventuale reingresso con episodi separati richiede una policy e una futura
chiave episodio verificata, senza cancellare o sovrascrivere le iscrizioni attuali. Alternative sospese:
riattivazione della stessa relazione oppure episodi separati. MT-4A non implementa un workflow
amministrativo di reingresso e non ne deduce uno dai soli stati.

Enrollment conserva lo stato corrente di ciascuna iscrizione e le date conosciute; non e un registro
append-only di ogni transizione. Un event log completo richiederebbe actor, motivazioni, istanti ed
una policy di rettifica ancora non concordati. Aggiungerlo ora non ricostruirebbe la storia mancante.
MT-4B dovra concordare transizioni, audit e rettifiche; i timestamp di aggiornamento non certificano
uno storico di stati. Nessuna motivazione amministrativa obbligatoria o normativa inventata.

## SQL, EF e backfill

Script canonici in migrations/mt4: EXPAND 001, BACKFILL 002 intenzionalmente senza DML,
VALIDATE 003. Nessun bootstrap automatico o DROP. Wrapper richiede il database esatto loopback,
preflight e nuovo backup completo leggibile con SHA256 prima del DDL. DDL ha commit implicito:
interruzione richiede inventario dello stato parziale, nessun retry cieco o rollback completo promesso.
MariaDB 10.4 e MySQL 5.7 supportano i trigger SIGNAL e le FK usate; solo MariaDB reale e certificata
nelle prove locali. Non si dipende da CHECK ignorati su MySQL 5.7, ne si modifica GLOBAL sql_mode.

FK composite: edition -> StudyPath; edition/campus -> entrambi i parent; enrollment -> Student ed
edition; Class -> edition. Institution ha inoltre una FK diretta da edition. Tutte RESTRICT per update/delete.
Campus acquisisce UNIQUE(InstitutionId,Id). Le nuove tabelle hanno owner senza default holding.
Indici coprono lookup scoped, unicita e reverse lookup campus/edizione; nessun indice su status speculativo.
Trigger proteggono owner/chiavi immutabili, date, codici vuoti e stati anche fuori da C#.

Scaffold-Mt4.ps1 genera candidati senza overwrite; si copiano solo le tre nuove entity e si riconcilia
ElectronicRegisterContext.Mt4.cs tramite l'hook partial MT1. Entity legacy e sidecar MT1/MT3/MT3A
restano preservati. Navigazioni nuove mappate con le FK composite scaffoldate e RESTRICT, senza duplicare quelle
legacy preesistenti. Gli owner di StudyPath e Student restano shadow properties MT3A: le principal
key composite EF usano nomi stringa, preservando le entity generate e ValueGeneratedOnAdd.
Per questi owner chiave un TemporaryGuidValueGenerator impedisce Guid permanenti client:
i valori temporanei EF vengono omessi dagli INSERT e i trigger derivano gli owner reali. Test dedicati
verificano la persistenza da writer legacy e rilettura da nuovi context, oltre alle nuove relazioni.
Repository dedicato richiede TenantContext ad ogni metodo e filtra prima di materializzare, con
AsNoTracking per letture. Non riceve Guid tenant come prova di autorizzazione: l'autorizzazione del
caller e responsabilita dei futuri manager, come MT3B. Nessun consumer HTTP o legacy introdotto.

## Prerequisiti MT-4B

Manifest verificato delle attribuzioni legacy; policy amministrative di stati/reingresso/rettifica/audit;
autorizzazione con rivalidazione membership e ruoli; gestione concorrenza delle modifiche di stato;
contratti API e test di route. Nessuna di queste scelte abilita i tre rollout flag o il cutover.
