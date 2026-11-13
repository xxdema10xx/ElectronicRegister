# Staff istituzionale — Fase 1

Stato delle verifiche: consultare [report](../../tests/Integration/INSTITUTION_STAFF_ROLE_REPORT.md).
Il codice non è una certificazione di completamento delle verifiche elencate nel report.

## Identità, appartenenza e autorizzazione

`InstitutionRole.Staff = 3`. I valori esistenti restano Admin=0, Teacher=1, Student=2.
L'ENUM SQL conserva l'ordine `InstitutionAdmin,Teacher,Student,Staff`.
Il tipo CLR `UserRole` e il percorso autorizzativo globale sono ritirati. La colonna storica `user.role` e conservata ma non conferisce membership o capability. Identita globale e membership non sono intercambiabili.
La gerarchia organizzativa non produce ereditarietà di permessi.

Staff gestisce anagrafiche locali, voti ed esami della Fondazione selezionata.
Non gestisce account globali, privilegi, sicurezza, configurazione istituzionale o catalogo strutturale.
Non è un InstitutionAdmin e non rappresenta un docente fittizio.
`InstitutionPermissions` definisce capability esplicite; ownership, profili e business rule
restano condizioni aggiuntive. Non confrontare numericamente i ruoli.

## Matrice

| Operazione | InstitutionAdmin | Staff | Teacher senza Staff | Student senza Staff |
|---|---|---|---|---|
| Leggere anagrafiche locali | Sì | Sì | Visibilità preesistente | Profilo personale e letture docenti preesistenti |
| Creare Student/Teacher | Sì | Sì | No | No |
| Modificare nome/cognome Student/Teacher | Sì | Sì | No | No |
| Eliminare Student/Teacher | Vincoli esistenti | No | No | No |
| Leggere voti | Tenant | Tenant | Assegnazioni e carriera autorizzate | Propri |
| Creare/modificare voti | Regole esistenti | Regole esistenti, riferimenti immutabili | Permessi esistenti | No |
| Eliminare voti | Sì, esclusi voti di esame protetti | No | No | No |
| Lettura amministrativa esami | Tenant | Tenant | Assegnazioni proprie | Esami assegnati tramite percorso studente |
| Creare esami | Sì | Sì | Assegnazioni proprie | No |
| Modificare struttura/assegnazioni | Solo Draft | Solo Draft senza submission | Regole esistenti | No |
| Pubblicare/chiudere | Business rule | Business rule | Business rule e ownership | No |
| Eliminare fisicamente esami | Regole esistenti | No | Regole esistenti, Draft senza storico | No |
| Cancellare amministrativamente esami | Regole esistenti | No | Regole esistenti | No |
| Leggere submission amministrative | Sì | Sì | Esami propri | Risultato/sessione propri |
| Review/finalizzazione docente | Regole esistenti | No | Regole esistenti e profilo docente | No |
| Risposte/submission studente | Nessun bypass | Nessun bypass | Nessun bypass | Workflow personale esistente |
| Membership/ruoli/inviti privilegiati | Workflow IAM esistente | No | No | No |
| Configurazione Fondazione | Sì | No | No | No |
| Trasferimento studenti | Sì | No | No | No |
| Modifica catalogo strutturale | Sì | No | No | No |

## Membership con più ruoli

* Staff: CRU amministrativo senza TeacherProfile o StudentProfile.
* Staff + Teacher: CRU Staff; review/finalizzazione solo nel percorso docente reale,
  con TeacherProfile e ownership. DELETE e cancel esami restano vietati.
* Staff + Student: CRU Staff; i percorsi espliciti studente richiedono il suo vero profilo.
* Staff + Teacher + Student: valgono entrambe le condizioni precedenti; nessun DELETE.
* InstitutionAdmin + Staff: conserva i privilegi espliciti di InstitutionAdmin.

Possedere Staff in A e Teacher in B non combina le autorizzazioni tra Fondazioni.
Un User legacy Admin con membership Staff non diventa InstitutionAdmin.
Le letture delle altre risorse conservano gli scope preesistenti dei rispettivi ruoli.

## JWT e IAM

Usare login identità, Selection e selezione Fondazione già presenti per ottenere InstitutionAccess.
Staff è un claim istituzionale canonico, distinto dai claim legacy. Parsing rigoroso,
membership attiva, Fondazione attiva, insieme esatto dei ruoli e security generation corrente
sono verificati dal workflow esistente. Token sospesi, revocati, falsificati o obsoleti falliscono.
Non esiste token legacy Staff. La revoca richiede una nuova selezione prima di agire con ruoli residui.

Assegnazione/revoca usano `/api/institution/admin/members/{membershipId}/mutations`,
riservato a InstitutionAdmin: EventId, ExpectedRevision, motivazione, last Admin,
idempotenza, generation monotona e audit IAM conservati. Gli inviti Membership
accettano Staff e fino a quattro ruoli distinti; FirstAdmin/Recovery restano Admin-only.
Non esiste vincolo Last Staff. Le nuove scritture Staff prendono lock condivisi IAM-first
e rivalidano l'appartenenza nella transazione; nessuna cache di permessi.

## Contratti API utilizzabili dal futuro frontend

Richiedono i rollout esistenti SchoolApi/ExamApi abilitati e Bearer InstitutionAccess.
Non inviare InstitutionId, MembershipId dell'attore o ruoli nel corpo delle risorse.

* `/api/institution/students` e `/teachers`: GET elenco, count, dettaglio e ricerca
  esistenti; POST `{ firstName, lastName }`; PUT `/{id}` con gli stessi campi.
  GET `?classId={id}` filtra entrambe le anagrafiche per classe nel tenant corrente.
  Student POST può indicare `classId` iniziale in query. PUT non trasferisce lo studente.
  Staff non può usare `membershipId` per associare profili: il collegamento resta Admin-only.
* `/api/institution/grades`: GET, `/{id}`, count, paged, filters, statistics;
  POST `{ studentId, subjectId, value, date }`; PUT `/{id}` `{ subjectId, value, date }`.
  SubjectId deve risolvere al ClassSubject originale: non è un comando di riassegnazione.
  La paginazione esistente usa pageNumber/pageSize. Le directory anagrafiche conservano
  gli elenchi non paginati esistenti, filtrati dal tenant in SQL.
* `/api/institution/exams`: GET elenco/dettaglio, `/{id}/eligible-students`, POST e PUT
  con i DTO esistenti; PUT `/{id}/students`, POST `/{id}/publish` e `/{id}/close`.
  Il client deve fornire un ClassSubjectId noto: non viene aggiunto un endpoint catalogo Staff.
  ClassSubjectId dell'esame esistente è immutabile. Le modifiche Draft non possono toccare storico.
* GET `/api/institution/exams/{id}/submissions` e
  `/api/institution/submissions/{id}/review`, `/result`, `/violations` sono letture amministrative.
  PUT review e POST finalize non sono permessi Staff.

La creazione di un'anagrafica non crea User, credenziali, password, mailbox o membership.
La nuova identità autenticabile resta dipendente dal provisioning/verifica identità già previsti
e dalla futura fase email/onboarding; non è implementato un provisioning globale Staff.
Gli update locali non cambiano dati condivisi del User globale.

## Voti, esami e audit

Restano range, riferimenti tenant, duplicazioni e protezioni Grade/ExamSubmission esistenti.
Staff non modifica un voto di esame tramite l'endpoint generico. Non può cancellare il voto,
spostarlo ad altro studente/ClassSubject o riscrivere submission e risposte.

Il motore conserva Draft/Published/Closed/Cancelled, retake, domande randomizzate persistenti,
autosave/revision, worker, grading e trasferimenti. La cancellazione amministrativa è una
capability separata da Update. Sostituire domande/opzioni e assegnazioni è consentito allo Staff
solo su Draft privo di submission; nessuno storico viene rimosso da questo percorso.

Le nuove scritture Staff su voti ed esami aggiungono eventi `staff.*` nella stessa transazione,
con vero ActorUserId, ActorMembershipId nel JSON e snapshot precedente/successivo.
UPDATE/DELETE di tali eventi sono negati dai trigger. Nessun evento storico viene inventato.
L'audit precedente dei voti non copriva tutte le modifiche; il nuovo tracciamento è mirato alle
operazioni Staff, non ricostruisce il passato e non amplia l'audit delle scritture Teacher-only.
Il ledger IAM preesistente resta distinto e append-only.

## Errori e operazioni vietate

* 400: input non valido, campi extra proibiti, proprietà immutabili, parametri malformati.
* 401: token mancante, non valido, Selection/legacy sul percorso InstitutionAccess.
* 403: ruolo/capability assente, rollout spento, membership o generation non più autorizzata.
* 404: risorsa inesistente oppure di altra Fondazione, senza distinzione osservabile.
* 409: conflitti di concorrenza, duplicati, ciclo di vita o integrità/business rule.

Non presentare azioni DELETE/cancel, IAM, trasferimenti o configurazioni allo Staff.
Il frontend deve comunque trattare 403/404/409 come decisioni server, rivalidando lo stato.
Nessuna importazione file, email istituzionale, UF/moduli, monte ore, calendario o frontend
è inclusa in questa fase.
