# ElectronicRegister - MT-3B

## 1. Stato iniziale Git e baseline

Branch `refactor-database-entities`, HEAD iniziale `970d5d704283daf0c33e106b1ff17149360cfcf4`. Workspace gia modificato da audit e MT1/MT2/MT3/MT3A; preservato senza reset, checkout, pull, commit o push. Inventario iniziale: [initial-git-status.txt](TestResults/mt3b/initial-git-status.txt). Il workspace locale costituisce la baseline, non il remoto.

Build iniziale riuscita. Baseline reale: **48 unit + 339 integration = 387/387 Passed**, zero Failed/Skipped. I TRX sono separati per progetto e run in [baseline](TestResults/mt3b/baseline). Nessun test precedente eliminato o indebolito.

## 2. Holding legacy e preflight

Database verificato con SELECT DATABASE(): esclusivamente `electronic_register_test`, MySQL **5.7.24**, SQL mode strict con ONLY_FULL_GROUP_BY e STRICT_TRANS_TABLES. [Preflight](TestResults/mt3b/database-preflight.txt): unica Institution persistente `4d543341-0000-4000-8000-000000000001`, code `MT3A_LEGACY_TEST`, stato `provisioning`; 12 Student, 2 Teacher e 1 Subject attribuiti a questa holding, zero membership persistenti. Presenti 27 FK composite MT3A e 40 trigger di ownership; manifest `mt3a_legacy_scope` coerente con la holding. Sidecar MT1, Profiles e Ownership gia installati.

`LEGACY_MT1` e una convenzione del precedente manifest, non una prova di equivalenza con `MT3A_LEGACY_TEST`. Non esiste una seconda holding persistente; nessuna copia o trasferimento di ownership effettuato. Il manifest SQL seleziona il fallback dei writer storici: non costituisce autorizzazione MT2. La configurazione di esempio locale ha bridge disabilitato e InstitutionId vuoto; [configurazione esaminata](TestResults/mt3b/legacy-bridge-config.json). Non si deduce la configurazione di un deployment da questo esempio. Nessuna attivazione o riconciliazione persistente: un futuro deployment dovra verificare esplicitamente il proprio manifest di autenticazione. La holding tecnica resta non operativa. I test HTTP preesistenti configurano manifest temporanei separati.

## 3. Ambito completato

**MT-3B completata per Application/Infrastructure Student, Teacher e Subject. MT-3 complessiva resta PARZIALE: C/D/E non implementati.** Nessuna API scolastica multi-tenant operativa. Controller, LegacyEndpointGate, ExamManager, ExamSubmissionManager, GradeManager, AuditManager, worker e frontend invariati. Nessuna nuova migrazione DDL, dipendenza NuGet, cache o servizio esterno.

## 4. Autorizzazione applicativa

Ogni operazione parte dall'accessor scoped esistente e richiama IInstitutionAuthManager.AuthorizeAsync: verifica identita, membership e Institution Active, corrispondenza esatta dei ruoli correnti e contesto. Il guard condiviso InstitutionSchoolAuthorization verifica poi il permesso dell'operazione. Non usa User.Role, User.StudentId o User.TeacherId. Assenza/invalidita del contesto e ruoli insufficienti producono UnauthorizedAccessException; risorsa fuori tenant produce KeyNotFoundException generica; dipendenze e unicita producono BusinessRuleException. Nessun lookup globale preliminare.

## 5. TenantContext

Riutilizzati TenantContext immutabile e ICurrentTenant scoped di MT2. Nessun nuovo resolver globale, contesto amministrativo Guid.Empty o singleton tenant. I manager pubblici non accettano InstitutionId dal chiamante. L'accessor opzionale nel costruttore interno del guard consente ai vecchi host non HTTP di costruire DI: ogni operazione senza accessor fallisce in modo controllato, senza fallback. Il solo adapter di test inizializza una volta ciascuno scope.

## 6. Risoluzione dei profili

IInstitutionProfileResolver partecipa ai flussi Teacher/Student e ai metodi separati GetOwnProfile. InstitutionProfileRepository filtra anche l'owner diretto del profilo in SQL, oltre a InstitutionId, MembershipId e UserId del collegamento. Identita globale comune, profili A/B distinti; profilo assente nega l'accesso. InstitutionAdmin non implica un profilo Teacher/Student. Nessun fallback ai campi legacy dell'identita globale e nessuna membership/ruolo creata dai nuovi servizi.

## 7. Repository Student

Nuova interfaccia dedicata IInstitutionStudentRepository: GetAll, GetById, GetByIds, GetByLastName, Count, GetOwn, Add, Update, Delete. Tutte le query iniziano da InstitutionId. Admin vede l'anagrafica del proprio tenant; Teacher solo studenti di classi assegnate attraverso ClassSubject; Teacher senza assegnazioni vede zero righe. Student non puo usare la directory: il proprio profilo e un'operazione distinta.

Add valorizza owner esplicito e verifica eventuale ClassId e membership nel tenant. Update modifica soltanto nome/cognome; owner e classe non sono trasferiti. Delete conserva il divieto storico di eliminare Student con Grade. Il manager apre la transazione dopo l'autorizzazione; la prima query blocca con SELECT FOR UPDATE esclusivamente il parent Student scoped. Il primo successivo consistent read verifica Grade; la FK impedisce nuovi insert Grade sul parent bloccato. DELETE ricontrolla l'assenza di Grade. Nessun lock globale o modifica dei lock Exam/Submission. Il repository richiede una transazione per questa cancellazione.

## 8. Repository Teacher

IInstitutionTeacherRepository espone directory, ID/insieme ID, cognome, Count, GetForClass, GetOwn e CRUD. Nomi/ID della directory restano visibili ai ruoli precedentemente ammessi ma solo entro il tenant; Student deve risolvere il proprio profilo e non ottiene Count amministrativo. GetForClass usa le assegnazioni autorizzate. Solo Admin scrive; ClassSubject dipendenti impediscono Delete. La creazione puo collegare una membership gia esistente, Active, nello stesso tenant e con ruolo Teacher: non duplica User e non assegna ruoli automaticamente.

## 9. Repository Subject

IInstitutionSubjectRepository copre liste, Count, ID/insieme ID, nome, byTeacher e CRUD. Admin vede il catalogo locale, Teacher le materie delle proprie assegnazioni, Student quelle della propria classe. Stesso nome ammesso in A/B, duplicato nello stesso istituto respinto anche in concorrenza dalla UNIQUE scoped MT3A. Dipendenze ClassSubject verificate nel tenant prima di Delete.

## 10. Class e ClassSubject

InstitutionSchoolQueries filtra direttamente ClassSubject, Class, Teacher e Subject per owner; Student/Class sono coerenti con la stessa Institution. Le visibilita usano EXISTS SQL su profili e assegnazioni. Subject/byTeacher interseca la medesima ClassSubject visibile allo Student con il Teacher richiesto: non basta condividere un SubjectId insegnato anche in un'altra classe. Test con stessa Matematica e diverso docente/classe dimostra questa distinzione. Student senza ClassId non ottiene materie attraverso TeacherId arbitrario. Nessuna nuova gestione Class, CourseEdition o StudentEnrollment.

## 11. Manager

IInstitutionStudentManager, IInstitutionTeacherManager e IInstitutionSubjectManager compongono guard condiviso, resolver, repository dedicati e ITransactionManager esistente. Registrazioni scoped in DI. Autorizzazione centralizzata per ogni operazione, non demandata a futuri controller. Validazioni dei nomi, dipendenze e policy amministrative mantenute; gli identificativi opzionali di classe/membership sono parametri interni, non nuove proprieta dei DTO HTTP. Le transazioni rendono atomici inserimento profilo e collegamento membership.

## 12. Ownership nelle scritture

AddOwned imposta EF Entry.Property<Guid>("InstitutionId") prima di SaveChangesAsync. I test intercettano INSERT e relativo parametro owner A, diverso dalla holding, poi verificano la riga con un nuovo DbContext. Non si deduce il corretto writer dal solo risultato del trigger. Update/Delete includono il tenant nel predicato SQL e non aggiornano owner. I trigger di compatibilita MT3A restano invariati; owner incoerente viene respinto con errore MySQL 1644. Collegamento ClassSubject A/Subject B respinto dalla FK con 1452.

## 13. Query generate

[SQL reale della lista Subject Student](TestResults/mt3b/generated-subject-query.sql.txt), catturato da DbCommandInterceptor senza valori sensibili. EF.Property<Guid> utilizza le shadow properties effettive; non sono duplicati i mapping delle FK composite. AsNoTracking, proiezioni SQL, COUNT(*) lato server, filtri prima della materializzazione. Nessun IgnoreQueryFilters o caricamento globale seguito da filtro in memoria. Interceptor controlla anche i parametri GUID degli INSERT.

## 14. Test A/B

39 nuovi casi xUnit reali MySQL in Mt3bSchoolTests, con Institution A/B temporanee Active, utenti globali condivisi, membership/ruoli separati, profili distinti, classi assegnate/non assegnate, cataloghi omonimi. Usano i manager reali, resolver, authorization e repository EF, nessun mock di repository. Verificano letture/ricerche/Count, self-profile, CRUD, membership incoerente, rollback, tipi di errore e digest di B invariato. Verifica delle scritture con scope e DbContext nuovi. Teardown parametrico e circoscritto agli ID delle fixture, senza DROP, reset o disabilitazione FK.

## 15. Revoca e ruoli multipli

Test di membership/Institution sospese, ruolo Teacher rimosso o aggiunto rispetto al contesto, profilo mancante, contesto inventato e accesso non HTTP senza contesto. Una nuova operazione rilegge lo stato corrente. Admin+Teacher conserva entrambi i permessi: la directory amministrativa funziona, mentre l'operazione Teacher richiede il relativo profilo. Ruoli non sommati tra A e B. Concorrenza verificata tra scope A/B, due Admin distinti nello stesso tenant, aggiornamento illecito A verso B contemporaneo all'update legittimo B, duplicazione concorrente di Subject. Nessuna perdita di contesto o trasferimento owner. La revoca non annulla retroattivamente un'operazione gia autorizzata in corso.

## 16. Compatibilita legacy

Vecchie interfacce e registrazioni rimangono separate; vecchi manager/controller e dipendenze Exam/Grade continuano a usare percorsi globali legacy. I nuovi manager espongono soltanto repository istituzionali. Test verifica che IStudentManager mantenga StudentManager. Nessuna sostituzione silenziosa o allargamento dei contratti legacy. Tutti i 387 casi precedenti preservati per identita del test nei TRX.

## 17. HTTP

Nessun controller migrato o nuova route creata. LegacyEndpointGate resta attivo: JWT scoped A/B e ticket continuano a essere rifiutati dalle route scolastiche legacy. Le cinque API istituzionali MT2 e le relative verifiche restano funzionanti nella suite esistente. Bootstrap reale Program Development e Production passa in entrambe le run definitive. Questo dimostra compatibilita e gate chiuso, non isolamento positivo delle future API scolastiche MT3C.

## 18. Persistenza e integrita

Solo electronic_register_test utilizzato per SQL e test. Nessuna operazione su produzione e nessun DDL nuovo MT3B. FK/trigger MT3A preservati e testati con errori MySQL specifici, non eccezioni generiche. Stato finale in [final-state.json](TestResults/mt3b/final-state.json): solo holding provisioning, nessun tenant temporaneo o membership residua, nessuna riga scolastica fuori holding. Guard di scaffolding MT1/Profiles verificati: Apply parziali respinti prima della copia, digest di tutti i sorgenti Persistence invariati; [evidenza](TestResults/mt3b/scaffold-guards.json). Nessuna entity generata modificata.

## 19. Performance

Test verifica SQL con InstitutionId, EXISTS e COUNT(*), assenza di tracking e numero costante di comandi per lista: fino a 2 per Admin e 4 per Teacher/Student includendo rivalidazione e risoluzione profilo. Non c'e una query per studente o materia. Gli indici esistenti MT3A con prefisso InstitutionId e gli indici dei riferimenti compositi supportano lo scope e le assegnazioni; nessun nuovo indice speculativo. Non sono presentate misure di throughput o EXPLAIN su dataset piccolo come benchmark realistico. Nessun caching.

## 20. File modificati

- Domain: nuove Interfaces/Managers/IInstitutionSchoolManagers.cs e Interfaces/Repositories/IInstitutionSchoolRepositories.cs.
- Application: tre Institution*Manager, Services/InstitutionSchoolAuthorization.cs, registrazioni AddApplicationManagers; commento di InstitutionProfileResolver aggiornato.
- Infrastructure: InstitutionSchoolQueries e tre Institution*Repository, filtro owner in InstitutionProfileRepository, registrazioni in DependencyInjection/ServiceCollectionExtensions.AddInfrastructure.
- Test: solo nuovo Mt3bSchoolTests.cs; report ed evidenze MT3B.
- Architettura: MULTITENANCY_DESIGN.md e MULTITENANCY_ISOLATION_MATRIX.md aggiornati preservando gli snapshot precedenti.
- Strumenti locali: SyncMt3bArtifacts.ps1, staging e verify_mt3b_evidence.py per riproduzione e verifica TRX. Nessuna modifica SQL/schema/sidecar/HTTP o pacchetto NuGet.

## 21. Casi aggiunti

39 casi parametrizzati raggruppati in 20 metodi: directory Student; Teacher senza assegnazioni; Student self/class/byteacher; identita condivisa; visibilita Subject per tre ruoli; CRUD per tre famiglie; sei mutazioni cross-tenant; tre writer negati; cinque revoche; quattro contesti invalidi; multiruolo; membership/classe incoerenti e rollback; link Student senza duplicazione User; due sospensioni Admin; profilo/accessor mancanti; dipendenze/unicita; vincoli SQL; concorrenza scope/Admin/update; duplicato concorrente; traduzione SQL/Count/N+1. [Run mirata](TestResults/mt3b/targeted): 39/39.

## 22. Validazione definitiva

Build soluzione riuscita; warning preesistenti di nullability, nessun nuovo warning introdotto. Due suite consecutive senza modifiche al codice tra le run:

| Run | Unit | Integration | Totale Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline | 48 | 339 | 387 | 0 | 0 |
| Final-1 | 48 | 378 | 426 | 0 | 0 |
| Final-2 | 48 | 378 | 426 | 0 | 0 |

TRX distinti per progetto in [final-1](TestResults/mt3b/final-1) e [final-2](TestResults/mt3b/final-2). [verification.json](TestResults/mt3b/verification.json) confronta contatori XML, outcome di ogni caso, hash dei file e identita: tutti i 387 precedenti presenti, insiemi finali identici. Include MT1, MT2, Profiles (21), Ownership (39), nuovi scoped (39) e bootstrap Development/Production. git diff --check e controllo whitespace dei nuovi sorgenti/report superati; evidenza [git-diff-check.txt](TestResults/mt3b/git-diff-check.txt).

## 23. Rischi e responsabilita residue

SQL garantisce ownership e relazioni composite, non permessi di lettura. Repository garantiscono predicati tenant e visibilita assegnazioni; richiedono un TenantContext verificato dal manager, non validano autonomamente JWT. Manager garantisce ruoli, stato corrente e business policy. Autenticazione MT2 garantisce token/context; HTTP scolastico rimane legacy con gate chiuso. Le famiglie non migrate continuano a richiedere C/D/E. La holding non e un istituto operativo e l'eventuale futura riconciliazione del bridge richiede una decisione esplicita. Dopo rollback fallito lo scope/unita di lavoro va dismesso, non riutilizzato con entita tracked residue. Nessun tentativo di annullare retroattivamente autorizzazioni in-flight. Nessuna garanzia di isolamento positivo HTTP dichiarata.

## 24. Prerequisiti MT-3C

Adottare selettivamente i nuovi manager nei controller, mantenere l'accessor/autenticazione MT2, definire le autorizzazioni HTTP per ogni operazione e aggiungere test A/B positivi/negativi prima di qualsiasi apertura del gate. Gestire esplicitamente il manifest di deployment e la holding senza attivazioni implicite. Pianificare separatamente Exam/Submission/Grade/Audit/worker nei checkpoint previsti. MT-3C non iniziata in questa consegna.
