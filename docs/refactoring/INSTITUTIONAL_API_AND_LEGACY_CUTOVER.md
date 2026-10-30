# Contratti istituzionali e cutover - COMPLETED

Stato finale30ottobre2026: i paragrafi storici sotto descrivono le tappe precedenti, non API ancora mantenute. Fonte corrente: [superficie API](INSTITUTIONAL_API_SURFACE.json), OpenAPI e discovery HTTP verificati, [report conclusivo](RESPONSIBILITY_REFACTORING_REPORT.md).

## Ingressi supportati

| Area | Contratto canonico |
|---|---|
| Identita/account | `/api/Auth/identity/*`, Selection e selezione esplicita della membership; `/api/identity/accounts`, `/api/identity/account`, `/api/identity/account/password` |
| IAM/amministrazione/audit | Directory e mutazioni membership, inviti/onboarding, acquisizione esplicita dei profili e audit nei controller istituzionali esistenti; nessun controllo tenant delle credenziali globali |
| Anagrafiche/catalogo | Student/Teacher/Subject/Class e catalogo nei controller istituzionali scoped; CourseEdition/Enrollment, piani e calendario restano workflow distinti |
| Esami | `/api/institution/exams`: letture consentite, creazione/modifica draft, assegnazioni ed eligible-students, publish/close/cancel/delete, assigned e start |
| Submission | `/api/institution/submissions/{id}`: session, PUT answers, POST submit/submit-expired, GET work, review/finalize, result e violations secondo ruolo/ownership |
| Voti | `/api/institution/grades`: voto ufficiale, count, paged, filters, statistics e mutazioni consentite; query sempre scoped |
| Import/governance | Contratti istituzionali esistenti con capability, generation/revoca, idempotenza, audit e rivalidazioni transazionali preservati |

GET `/api/institution/submissions/{id}/work` e una vista Student proprietario sulle proprie risposte, redatta dallo stesso engine: nessuna correttezza delle opzioni, commento docente o punteggio provvisorio. GET/PUT review e finalize conservano il proprio confine docente/amministratore. Session e risultato ufficiale restano distinti.

## Incompatibilita intenzionali

Tutte73azioni globali ritirate sono unsupported:43della prima fase piu30Exam/Grade. `/api/Exam` e `/api/Grade`, con ogni suffisso/metodo precedente, non sono piu nel routing/OpenAPI e restituiscono404. Non esiste un adapter di compatibilita per i vecchi frontend. Le richieste canoniche per risorse non possedute non ne rivelano l'esistenza; liste e filtri applicano lo scope (anche quando un filtro chiede uno studente estraneo). Le bozze non sono scopribili dallo studente.

I vecchi claim di profilo e UserRole non autorizzano nulla. I tipi CLR ClaimsContext/UserRole e il generatore JWT globale non esistono piu; `user.role` persiste solo come dato storico. I JWT globali firmati sono rifiutati dalle API canoniche. Le membership non sono dedotte da email, ruolo, nomi o ordinali.

Il salvataggio canonico delle risposte e PUT `/api/institution/submissions/{id}/answers`; submit usa l'ID submission e la revision richiesta. Le query voti per studente/data/materia usano gli ID e i filtri canonici, non vecchi lookup globali per nome. Payload/status di tutti gli ingressi sono nell'indice e nella OpenAPI eseguita nei test.

## Composizione e worker

Un solo IInstitutionExamManager, engine condivisi e repository factory bound. Nessuna DI di manager globali o repository esami/voti/anagrafiche unbound in produzione. InstitutionEndpointGate conserva i controlli di marker/policy/rollout e rifiuta gli ingressi MVC non classificati. Il default di autenticazione di Program e InstitutionAccess.

Il worker e tecnico: stessa scansione keyset limitata, owner risolto da persistenza, SubmissionExpiryWork opaco, repository bound, row lock, transazione e audit ActorUserId null. `Multitenancy:InstitutionWorker:Enabled` governa il timer. Le impostazioni `Identity:Legacy:*` sono obsolete e non possono riattivare ingressi o lane applicative.

La holding storica resta provisioning, senza membership inventate: puo consegnare solo submission il cui owner e grafo coincidano con il suo marker persistito. Non diventa un tenant utente; portarla active non rende valido lo scope tecnico. Nessuna migrazione inventa un'istituzione per quei dati.

## Copertura e limiti

590/590Unit,1458/1458Integration SQL/HTTP su nuova istanza MySQL5.7.24;0Failed/Skipped, discovery/TRX identici, Roslyn0violazioni. Le21incompatibilita iniziali sono tutte risolte. [Evidenze](validation/current-cutover/final/README.md).

Account personale/password/revoca/acquisizione sono gia implementati e restano invariati. Recovery e cambio email globali non sono esposti senza proof verificata del destinatario; nessun reset globale con ruolo tenant o privilege nuovo PlatformOperator. Acquisizioni ambigue producono requires-review e nessun link inventato. Schema aggiuntivo account/acquisizione da distribuire con autorizzazione nell'ambiente destinazione, non applicato qui a database preesistenti.

## Storico dei contratti e delle decisioni precedenti

## Implementazione corrente: account e primo cutover verticale

Questa sezione sostituisce le classificazioni storiche sotto per i componenti effettivamente rimossi. Lo stato complessivo resta **PARTIAL** durante l'intervento: i controller/adapter esami e voti globali e la lane tecnica legacy non sono ancora ritirati. Non vengono mantenuti per compatibilità con React/Web; occorre completare la migrazione delle relative verifiche e del contesto tecnico senza perdere lavoro pendente.

Sono rimossi i controller globali Auth, Users, Student, Teacher, Subject e Biennium (43 azioni), i relativi manager/interfacce, i manager StudyArea/StudyPath, i servizi User/Student/Teacher/Biennium/StudyArea/StudyPath con le interfacce, i repository globali Biennium/StudyPath/StudyArea con le interfacce e il generatore JWT globale con la relativa DI. Le anagrafiche, le password, i link, le tabelle e i ruoli membership non vengono cancellati. Il catalogo, CourseEdition, Enrollment, piani e calendario restano aggregate separati.

### Contratto identità/account implementato

| Metodo e route | Autenticazione | Input | Risultato |
|---|---|---|---|
| POST `/api/identity/accounts` | Nessun token | Email, Password | 201 con nuovo UserId; 409 collisione; nessun profilo o membership |
| POST `/api/Auth/identity/login` | Password locale verificata | LoginDto | Selection ticket di 300 secondi |
| POST `/api/Auth/identity/microsoft-login` | Provider/tenant/object binding esplicito | MicrosoftLoginDto | Selection ticket; mai collegamento per coincidenza email |
| GET `/api/identity/account` | Selection corrente | Nessun target client | UserId, Email, DisplayName, HasLocalPassword |
| PUT `/api/identity/account` | Selection corrente | Solo DisplayName | 204, audit atomico; nessuna modifica alle anagrafiche didattiche |
| PUT `/api/identity/account/password` | Selection corrente e password attuale | CurrentPassword, NewPassword | 204; revoca globale e nuovo login necessario |
| GET `/api/Auth/institutions` | Selection corrente | Nessuno | Sole membership selezionabili |
| POST `/api/Auth/select-institution` | Selection corrente | InstitutionId, MembershipId, eventuale RequestedRole | InstitutionAccess di 900 secondi; membership verificata |
| GET `/api/Auth/context` | InstitutionAccess corrente | Nessuno | Contesto verificato, distinto dall'account globale |

I DTO mutabili rifiutano campi sconosciuti. UserId/tenant/ruoli/generation non sono mutabili dal profilo. Il display name globale non aggiorna Student o Teacher. Email e binding esterni non sono modificabili con questo workflow: manca un workflow di verifica del nuovo recapito/binding, quindi non viene esposto un aggiornamento che possa cambiare l'identità sulla sola base del bearer token.

La registrazione non verifica il possesso della casella email e non lo usa come prova di autorità. Non associa account a inviti, tenant o profili per email. Il recipient esplicito dell'onboarding deve essere verificato con i meccanismi già previsti. La collisione su email viene rifiutata, senza recuperare o fondere account. Gli account storici accedono usando le proprie credenziali già persistite; nessuna conversione UserRole è eseguita dal login canonico.

### Credenziali, revoca e autorità amministrativa

`PasswordRules` conserva il minimo di otto caratteri e il carattere speciale, imponendo anche il limite BCrypt di 72 byte UTF-8 per evitare troncamenti. Hashing usa il componente BCrypt esistente. Il cambio richiede la vecchia password anche per account con binding esterno che possiedono una password locale; account senza password locale non possono crearne una presentando soltanto un ticket.

La lettura di hash e generation usa un singolo snapshot SQL. La scrittura blocca prima l'utente globale, poi lo stato identità; ricontrolla generation e hash attesi, aggiorna hash, incrementa generation e registra l'evento nella stessa transazione. Due cambi concorrenti dalla stessa generation non possono entrambi riuscire. Selection e InstitutionAccess vengono rivalidati rispetto alla generation globale ad ogni autenticazione HTTP; quelli precedenti al cambio vengono rifiutati. La selezione propaga la generation del ticket, senza promuovere un ticket revocato a una nuova sessione durante una race. Le richieste già autenticate prima della revoca restano soggette ai controlli transazionali IAM esistenti.

Lo schema aggiuntivo riproducibile è `migrations/identity-account/001_identity_account.sql`: due tabelle, FK RESTRICT, audit append-only e guard di avanzamento. Nessuna migrazione automatica all'avvio e nessuna applicazione a database preesistenti nella verifica. Prima di distribuire questo codice è necessaria l'applicazione autorizzata dello schema additivo.

InstitutionAdmin continua ad amministrare esclusivamente le membership con le capability esistenti. Non esistono reset password o override credenziali via ruolo tenant, ruolo globale o normale JWT. Le operazioni PlatformOperator già firmate restano nel control plane esistente; non sono stati inventati nuovi diritti sulle credenziali. Il recupero password non è pubblicato: non esiste qui un canale di recupero globale verificato e un contratto di prova del destinatario/revoca; i canali istituzionali degli inviti non vengono riutilizzati come autorità sull'account globale.

### Migrazione delle verifiche

### Acquisizione esplicita dei profili storici

`POST /api/institution/admin/members/{membershipId}/profile-acquisitions` richiede la membership amministrativa corrente e i flag directory/write già esistenti. Il body contiene soltanto EventId, ExpectedRevision (generation corrente della membership target), ProfileId, Kind (`Student` o `Teacher` esatti) e Reason. Non accetta UserId, InstitutionId o ruoli da applicare.

Il target deve essere una membership attiva dello stesso tenant, con il ruolo già attribuito. La risorsa deve appartenere realmente a quel tenant. Il link viene creato soltanto se il riferimento storico esplicito user.student_id/teacher_id corrisponde al ProfileId richiesto e non esiste un'associazione incompatibile. UserRole non viene letto. Nessun profilo, account, ruolo o tenant è dedotto da nomi o email. Una risorsa della holding non viene trasferita ad altre istituzioni.

Risposta200: EventId, MembershipId, ProfileId, Kind e Status. `linked` significa link coerente esistente o appena creato; `requires-review` significa **nessun link creato** e necessità di risolvere esplicitamente un riferimento storico assente/ambiguo o una collisione. Il ledger conserva l'intento e il motivo del tentativo reale; non inventa una precedente acquisizione. La risoluzione di un dato storico ambiguo richiede evidenze amministrative e un intervento dati esplicitamente autorizzato: non esiste un override che consenta a InstitutionAdmin di riscrivere l'identità globale o l'ownership storica.

`GET /api/institution/admin/members/{membershipId}/profile-acquisitions?page=1&size=20&status=requires-review` espone la coda/storia al solo Admin del tenant. Size1..100, page>=1, status assente oppure `linked`/`requires-review`. Membership assente o esterna404; Staff/Teacher/Student non leggono questa storia.

Le scritture e i replay prendono il mutex istituzionale e gli stessi lock IAM ordinati delle mutazioni membership, poi bloccano account e profilo; rivalidano l'attore anche per il replay. Link e audit append-only condividono commit/rollback. Stesso EventId/intento restituisce lo stesso risultato; intento diverso409. Nessuna modifica a password/hash, ruoli o generation del target. Una nuova operazione dopo risoluzione amministrativa usa un nuovo EventId, senza riscrivere l'esito storico `requires-review`.

Lo schema additivo002 aggiunge il ledger con FK composite a membership/attore e ai profili, guard delle forme e dei link storici e protezioni UPDATE/DELETE. È stato applicato una sola volta alla sola istanza creata in questa sessione. Undici prove SQL/HTTP coprono Student/Teacher, coda, idempotenza/concorrenza, collisioni, ownership, Staff, revision, overposting, append-only e rollback dopo SQL reale.

### Conservazione delle invarianti nei test ritirati

Le prove di visibilità dello studente senza assegnazioni sono trasferite al manager canonico in Mt3bSchoolTests (count/list/id/name), mantenendo il rifiuto di fallback sul ruolo globale. I vecchi test HTTP verificano ora 404 e assenza di mutazioni sulle route ritirate. Le prove di update account/profilo congiunto sono sostituite dal rollback del profilo globale e dalla verifica che email, ruolo e dati Student/Teacher restino invariati. I due vecchi flussi di registrazione con creazione implicita di profilo diventano una sola registrazione identità con prova di rollback e assenza di profili. Le invarianti IAM, esami, voti, import, calendario, piani e audit non vengono eliminate.

Checkpoint di implementazione del 29 ottobre 2026 della proposta approvata. Non sostituisce la proposta. React e Web saranno sostituiti; non sono vincoli permanenti. Nessun account, tenant o profilo viene dedotto da UserRole. Nessuna migrazione SQL o modifica dei dati è inclusa.

## Evidenze e precisione della copertura

L'inventario aggiornato censisce 724 sorgenti dei cinque layer, con hash, tipi dichiarati, slice, indicatori di transazione/audit e consumatori lessicali backend/test/tool. I consumatori lessicali includono registrazioni DI e riferimenti nei test: non sono un call graph Roslyn, né una prova di raggiungibilità dinamica. La classificazione per singolo sorgente è conservativa; le decisioni semantiche per workflow sono sotto. Non si dichiara una revisione esaustiva di ogni metodo o l'assenza di consumer esterni.

[INSTITUTIONAL_API_SURFACE.json](INSTITUTIONAL_API_SURFACE.json) riporta attributi di route, policy, permessi e firme dei 35 controller, inclusi quelli legacy, con righe sorgente. È un indice del contratto compilato, non un documento OpenAPI generato a runtime. Gli attributi di classe e i filtri custom si compongono con quelli dei metodi: i soli Roles non descrivono l'intera autorizzazione.

[LEGACY_RETIREMENT_MATRIX.json](LEGACY_RETIREMENT_MATRIX.json) enumera 80 componenti e interfacce, percorsi e consumatori lessicali. Le condizioni semantiche di dismissione sono quelle della tabella sotto; non autorizza cancellazioni automatiche.

## Ripartizione delle responsabilità nei cinque layer

| Slice | API / Application | Business / Domain | Infrastructure e invarianti da preservare | Decisione |
|---|---|---|---|---|
| Identità/IAM | Identity, inviti e membership sono ingressi distinti; AuthManager e UserManager mescolano ancora account globali e adapter legacy | MembershipAuthorizationRules canonico, ruoli membership e account distinti; hashing tramite porta | Password e link Microsoft, membership attuale, security generation, lock IAM-first, ledger IAM | Conservare account e credenziali; migrare amministrazione globale; ritirare adapter dopo copertura |
| Scuola/catalogo | Manager istituzionali con TenantContext e permessi; manager legacy con ClaimsContext | Validazioni anagrafiche/catalogo non equivalgono a creazione membership | SchoolQueries, repository scoped, ownership e revisioni; AdministrationPersistence conserva lock/audit | Sostituibili i confini HTTP legacy; repository e regole richiedono separazione dei consumatori condivisi |
| Corsi/iscrizioni | CourseManager, enrollment lifecycle e trasferimenti sono casi d'uso differenti | CourseEdition non è Biennium; Enrollment non è Class; TransferPolicy preserva intenzione e storico | CourseStructureRepository partial e TransferRepository, revisioni, transazioni e ledger dedicati | Keep: nessuna fusione semantica giustificata |
| Esami/submission/voti | InstitutionExamManager apre engine bound; ExamManager/ExamSubmissionManager/GradeManager e processor hanno parti condivise | ExamService, SubmissionGradingService e GradeRules sono algoritmi canonici; ExamPrincipal è adapter di contesto | Repository factory e scope, revisioni submission, lock, Grade ufficiale, audit, idempotenza | Ritirare entry point globali, conservare engine bound; nessuna seconda pipeline di grading |
| Piani formativi | Manager orchestra draft, versioni, pubblicazione e binding | FormativeDraftBuilder e regole pure; snapshot pubblicato immutabile | FormativePlanRepository rivalida rollout e IAM prima del commit | Keep; disponibilità runtime estratta dal Domain |
| Import | Manager orchestra preview/mapping/confirm; file reader condiviso con calendario | Regole anagrafiche e CalendarImportRules hanno semantiche diverse; limiti e validazione restano dati/regole pure | Staging, digest, TTL, prenotazioni e commit idempotente; controlli runtime ripetuti | Consolidare solo adattamento runtime; non fondere sessioni/ledger |
| Calendario | Manager separa pubblicazione, sostituzione e ore erogate | CalendarPermissions, CalendarRules, TeachingRules; monte ore non è copia del piano | Repository partial, pubblicazione atomica, storico e rivalidazioni sotto lock | Keep; disponibilità runtime estratta |
| Worker/platform/audit | Hosted worker e provisioning tool sono consumer reali anche senza frontend | Attore tecnico senza UserRole; PlatformOperator non è InstitutionAdmin | Store bounded tenant, canali owner-installed, firma platform, ledger distinti | Keep; la lane legacy del worker si ritira solo dopo esaurimento del work legacy |
| DTO/serializzazione | API mantiene binding e attributi input, Application mapping | DTO condivisi restano Domain perché Infrastructure li usa; JsonIgnore sugli input Exam resta invariato | Nessuna dipendenza Infrastructure → Application | Keep motivato, nessuno spostamento indiscriminato |

## Contratto per il nuovo frontend

Il flusso obbligatorio è login identità → Selection → elenco istituzioni → selezione esplicita membership → InstitutionAccess. Il client scarta il contesto precedente quando cambia istituzione. Non invia UserRole per ottenere membership. Il server risolve tenant, membership, ruoli e profili verificati; il frontend non costruisce claim. Selection e JWT globale non autorizzano le API istituzionali. I token e le loro durate restano quelli del contratto attuale, senza nuove promesse di refresh.

| Funzionalità | Endpoint attuale canonico | Policy e dati | Copertura / gap |
|---|---|---|---|
| Login locale/Microsoft | POST `/api/Auth/identity/login`, `/identity/microsoft-login` | LoginDto/MicrosoftLoginDto → IdentityTicketDto | Disponibile; credenziali globali, nessuna membership inventata |
| Scelta istituzione | GET `/api/Auth/institutions`; POST `/api/Auth/select-institution` | Selection; InstitutionChoiceDto, SelectInstitutionDto → InstitutionAccessDto | Disponibile; membership attuale verificata |
| Contesto | GET `/api/Auth/context` | InstitutionAccess → TenantContextDto | Disponibile; sostituisce la parte tenant di Auth/me, non tutto UserDto |
| Membership/inviti/audit | Controller InstitutionMembershipAdmin, InstitutionInvitation, InvitationAcceptance, InstitutionMembershipAudit | Policy access/Selection e permessi specifici; revision/intenti e ledger | Disponibile; non sostituisce amministrazione password/account |
| Studenti/docenti/materie | `/api/institution/students`, `/teachers`, `/subjects`, incluse route me e filtri dell'indice | InstitutionSchoolAccess, ruoli e InstitutionPermission; ownership verificata | CRUD canonico; differenze di visibilità deliberate, non copiare privilegi globali |
| Catalogo/classi/sedi/assegnazioni | Controller InstitutionBiennium, StudyArea, StudyPath, relative associazioni, Class, Campus, ClassSubject | Policy di catalogo/scuola e revisione; tabelle possedute e ledger | CRUD canonico. Aggregazioni legacy active/inactive/byname/byteacher non sono tutte route omonime: definire query/proiezioni tenant prima di richiederle al nuovo client |
| Edizioni/iscrizioni/trasferimenti | Controller InstitutionCourseEdition, Enrollment, StudentTransfer | Access tenant, lifecycle, expected revision, storico | Disponibile; nessuna equivalenza fra classe corrente e iscrizione |
| Esami | `/api/institution/exams` e relative azioni | Access istituzionale, risorsa/assegnazione, ruoli per azione | Disponibile; usa engine condivisi |
| Sessione, risposte, review e risultato | Controller InstitutionSubmission | Student per sessione/submit; review/finalize limitati; redazione dati | Disponibile; route diverse dal vecchio Exam/submission |
| Voti/pagine/filtri/statistiche | `/api/institution/grades` con count/paged/filters/statistics | Tenant e visibilità studente/docente, GradeRules | Disponibile. Filtri legacy già coperti da GET paged con pageNumber, pageSize, subjectId, studentId e date (DateOnly), senza nuove letture globali |
| Piani/import/calendario/email | Controller istituzionali corrispondenti | Feature rollout + policy + permessi risorsa; versioni, digest, revisioni e audit | Disponibile secondo rollout; pubblicazione chiusa a default |
| Account personale | **Da implementare** GET `/api/identity/account` | Selection o identità autenticata senza tenant; id/email/link identity, nessun PasswordHash o ruolo globale autorizzativo | Auth/me offre oggi UserDto globale e nomi di profili: il contesto istituzionale copre solo parte della funzione |
| Cambio password personale | **Da implementare** PUT `/api/identity/account/password` | Identità autenticata, target sempre sub; vecchia password verificata, stesse regole di robustezza; politica revoca da rendere esplicita | Solo Users/updatepassword oggi. Un InstitutionAdmin non ottiene per questo autorità sulle credenziali globali |
| Amministrazione account/credenziali | **Da implementare** confine identity/platform dedicato | Autorità globale esplicita; unicità email, hashing, link profili verificati e audit; mai implicita dalla membership admin | Users CRUD e Auth/RegisterForAdmin non sono sostituiti dalle mutazioni membership |
| Self-registration | Auth/register legacy | Crea account e studente usando formato email storico | Da ritirare come creazione automatica di profilo holding; onboarding/invito canonico non è self-registration equivalente. Conservare ingresso finché il prodotto target non copre esplicitamente l'acquisizione account |

Le route **da implementare** sono contratti target, non endpoint già pubblicati. Il cambio password deve mantenere le asserzioni di identità, vecchia password e validazione attuali. Creazione account, invito e associazione profilo sono operazioni distinte, con mapping esplicito e verificabile. Nessuna necessità di ricreare l'amministrazione globale dei ruoli per compatibilità con i vecchi frontend.

## Matrice di dismissione per responsabilità

| Componenti (incluse interfacce/DI) | Responsabilità superata | Da preservare | Condizione esatta di rimozione |
|---|---|---|---|
| Student/Teacher/Subject Controller e Manager legacy | Routing e autorizzazione globale, query non tenant | Regole di validazione ancora condivise, anagrafiche e associazioni storiche | Nuovo client su API istituzionali; account/profili senza mapping risolti; nessun altro consumer supportato; poi rimuovere controller/manager e relative sole DI |
| Biennium/StudyArea/StudyPath Controller, Manager, Service legacy | CRUD globale e vecchie proiezioni | Dati di catalogo e query condivise dagli engine | Copertura proiezioni richiesta verificata; distinguere associazioni/revision/ledger; eliminare servizi solo dopo consumer zero semantico |
| GradeController | Trasporto HTTP globale | GradeManager bound, GradeService, GradeRules, repository e voti storici | Client su InstitutionGrade; rimuovere soltanto ingresso ClaimsContext e query globali senza consumer |
| ExamController | Trasporto e principal legacy | InstitutionExamManager, engine, review/submit/grading/finalize e audit | Client su InstitutionExam/Submission; mantenere worker ed engine scoped; testare stesse invarianti tenant/replay/concorrenza |
| ExamManager, ExamSubmissionManager, ExamSubmissionProcessor, ExamPrincipal | Overload e adapter ClaimsContext/UserRole | Core bound, redazione, elaborazione automatica e work tecnico | Rimuovere solo entry point legacy dopo ritiro HTTP e lane worker legacy; non cancellare intere classi condivise |
| AuthController/AuthManager | Emissione token globale e composizione me legacy | Verifica password, Microsoft identity link, account provisioning e mapping profili ancora necessari | Copertura account/password e acquisizione identità target implementata; poi spegnere login globale e self-registration legacy |
| UsersController/UserManager/UserService | CRUD con autorità admin globale e parsing UserRole | Unicità email, policy password, verifica vecchia password, hash, link e protezioni cancellazione | Prima estrarre casi d'uso account in confine identity/platform; non reinterpretare InstitutionAdmin come amministratore globale |
| IUserRepository/UserRepository, User e relativi mapper | Letture globali di ruolo usate per autorizzare tenant | Credenziali, identità, link Microsoft, account e dati storici | Conservare parti usate da InstitutionAuthManager e provisioning; nessun drop tabella/colonna in questa fase |
| Student/Teacher/Subject/catalog repository legacy | Query/mutazioni globali senza scope | Metodi bound, dipendenze di Auth/User, engine, factory e transazioni | Separare metodi per consumatore; rimuovere solo dopo acquisizione account migrata e nessun engine/tool/test di contratto supportato |
| IJwtTokenGenerator/JwtTokenGenerator, ClaimsContext, ApiControllerBase, LegacyAccess, LegacyEndpointGate | Contratto token globale e relativo gate | Token istituzionali e validazione live membership | Ultima funzione legacy resa raggiungibile dal percorso target; interrompere emissione, attendere massima validità token configurata più clock skew, chiudere route; verificare rifiuto legacy sulle route tenant; poi rimuovere gate/adapter/DI |
| UserRole, RoleMapper | Ruolo globale come privilegio di ingresso legacy | user.role storico e serializzazione dati eventualmente ancora letti; enum membership invariato | Dopo ultimi consumer di account/admin/JWT/worker e mapping esplicito; ritiro del codice distinto dal futuro cleanup SQL |
| Worker expiry legacy | Scansione holding/global work | Processor condiviso e store istituzionale bounded | Nessun work legacy da elaborare o procedura esplicita di gestione residui; non perdere submission pendenti |
| InvitationChannelRegistration e tool PlatformAdmin | Nessuna responsabilità dimostrata superata | Caricamento provider owner-installed, intenti firmati, autorità platform, provisioning | Keep; reflection/configuration sono consumer dinamici reali e non si eliminano su sola ricerca lessicale |

Il responsabile tecnico di esecuzione del cutover è il confine indicato per riga (identity/platform, scuola/catalogo, engine didattico o worker). La scadenza operativa è il rilascio del nuovo frontend con le funzionalità target: non viene inventata una data di spegnimento. Prima di tale rilascio occorre registrare proprietario operativo nominativo, consumer esterni, durata token effettiva e comportamento delle route ritirate. Questi dati non sono ricavabili dai soli sorgenti.

## Consolidamenti eseguiti in questo checkpoint

La sola implementazione di valutazione callback è ora `Infrastructure.Services.RuntimeFeatureAvailability`. CalendarFeature e FormativePlanFeature sono adapter nominati per la DI e condividono quella implementazione. Il Domain espone IFeatureAvailability, ICalendarFeature e IFormativePlanFeature, senza delegati. InstitutionImportFeature conserva solo snapshot e Validate; InstitutionImportRuntimeFeature implementa la porta IInstitutionImportFeature e separa Settings dalla disponibilità corrente. Manager e repository consumano le porte. Nessun riferimento Infrastructure → Application.

I sei consumer (manager/repository calendario, piani e import) sono migrati. I punti di rivalidazione dei repository e le letture sotto lock non sono rimossi, spostati o memorizzati in cache. Le chiavi, i default fail-closed, i limiti, le regole Validate e i DTO HTTP restano invariati. Cambia il contratto CLR interno di composizione: consumer .NET esterni devono usare le porte; non viene promessa compatibilità binaria dei vecchi costruttori callback.

Eliminati i due record callback Domain CalendarFeature/FormativePlanFeature e il file Infrastructure storico fuori progetto contenente solo whitespace, senza tipi né registrazioni. Non è stato classificato morto alcun servizio registrato soltanto perché privo di riferimenti lessicali diretti. I consolidamenti IAM-MATCH, GradeRules e visibilità submission già presenti nel working tree precedente restano preservati e non sono attribuiti a questo checkpoint.

Resta aperta la verifica esaustiva metodo per metodo delle classificazioni Keep e dei consumer esterni. La nuova mappa e la matrice rendono espliciti questi residui; non costituiscono certificazione di equivalenza SQL o prova completa di assenza di duplicazioni.
