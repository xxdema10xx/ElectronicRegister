> Current contract: MT-5C onboarding and MT-5D governance/recovery are implemented in code.
> See [current governance](INSTITUTION_GOVERNANCE_AND_RECOVERY.md) and the current implementation reports.
> The planning/status text below is retained as historical MT-5A/early MT-5B evidence.

> Aggiornamento MT5B: **PARZIALE, scritture non implementate e chiuse**. Baseline
> nuova 711/711 Passed su MAMP MySQL 5.7.24 isolato 13306; nessun DDL o codice
> applicativo modificato. Restano da definire il contratto attribuito dei writer
> tecnici MT1 e da verificare un contatore monotono compatibile con INSERT SELECT/
> DELETE JOIN esistenti. Nessuna garanzia Last Admin/audit/revoca monotona nuova.
> [Design e blocchi MT5B](INSTITUTION_MEMBERSHIP_MUTATIONS_AND_AUDIT.md),
> [report MT5B](../../tests/Integration/MULTITENANCY_MT5B_REPORT.md).
> MT5A resta completata (due711/711); le precedenti descrizioni PARZIALE sotto sono
> storiche. Rollout chiuso, nessuna nuova POST o API audit, nessun MT5C/cutover.

# Governance istituzionale e membership — MT-5A

MT-5A introduce esclusivamente letture amministrative. Rollout default false, nessuna
migrazione, nessun cutover. La certificazione completa e **PARZIALE** per il disallineamento
del database disponibile: vedere il [report](../../tests/Integration/MULTITENANCY_MT5A_REPORT.md).
Le regole per le mutazioni sotto sono requisiti MT-5B, non garanzie gia implementate.

## Identita e inventario effettivo

User e un'identita globale. Institution e la Fondazione ITS Academy; Campus, Class,
StudyPath e CourseEdition non sono tenant. User non viene copiato per fondazione.
La directory non inventa un nome visualizzato: User ha Email ma non FirstName/LastName.
Non risolve nomi attraverso profili scolastici o collegamenti legacy.

| Componente | Identita, ruolo, owner e stato | Lettori e writer attuali; vincoli e rischio |
| --- | --- | --- |
| User / tabella user | Id globale; Email e PasswordHash; Role, StudentId, TeacherId legacy | Autenticazione legge credenziali. UsersController (file UserController.cs) conserva CRUD/count/password legacy sotto gate e allowlist. Email UNIQUE, PK Id, link legacy UNIQUE/FK. InstitutionAdmin non riceve accesso a questi flussi. Modificare User colpirebbe tutte le fondazioni: nessun nuovo writer globale. |
| Institution | Id tenant; Code UNIQUE; Status provisioning/active/suspended/archived; CreatedAt/UpdatedAt reali | Repository tecnico MT1 aggiunge/legge, nessun nuovo provisioning HTTP. MT2 seleziona solo Active. Nessun ruolo globale conferito da InstitutionId. |
| InstitutionMembership | Id membership, InstitutionId, UserId, Status pending/active/suspended/left, JoinedAt, SuspendedAt | MT2 legge le proprie; directory legge solo owner corrente. Repository MT1 AddAsync/SuspendAsync tecnici, non nuove API pubbliche. PK Id, UNIQUE(UserId,InstitutionId), UNIQUE(InstitutionId,Id), FK Institution/User. Non attribuire ownership da UserId soltanto. |
| InstitutionMembershipRole | MembershipId + Role InstitutionAdmin/Teacher/Student; PK composta | MT2 legge ruoli della membership selezionata; directory mostra ruoli correnti anche dei sospesi. AddRoleAsync tecnico verifica tenant; nessuna API pubblica di concessione/rimozione. FK alla membership, enum SQL. Nessuna union fra membership. |
| InstitutionStudentProfile | InstitutionId + MembershipId + StudentId | Resolver MT3 legge il profilo selezionato, manager scolastici possono collegare profili esistenti con autorizzazione. FK composite membership e Student, unicita profilo/membership nel tenant. Ruolo Student necessario nei percorsi personali, nessun fallback a User.StudentId. |
| InstitutionTeacherProfile | InstitutionId + MembershipId + TeacherId | Come Student, ruolo Teacher e owner scolastico controllati; nessun fallback a User.TeacherId o profilo B. Admin non implica profilo. |
| InstitutionIdentityController | Login globale -> selection ticket; institutions solo per sub; select verifica UserId/InstitutionId/MembershipId; context scoped | Solo il caller autenticato legge/seleziona le proprie membership selezionabili. RequestedRole non attribuisce ruoli: deve esistere; token contiene il set completo della membership. Nessuna scrittura di ruoli, User o membership. |
| InstitutionAuthManager | Credenziali locali IUserRepository/IPasswordHasher o identita Microsoft verificata | Login produce solo ticket; SelectAsync controlla membership Active, Institution Active, ruoli persistiti. AuthorizeAsync rilegge stato/set ruoli; non consulta User.Role come autorizzazione istituzionale. |
| InstitutionAuthentication / JWT | sub UserId; institution_id; membership_id; institution_role multipli | Schemi separati InstitutionSelection/InstitutionAccess, purpose/type/audience/firma/lifetime/claim duplicati validati; ruoli legacy nei JWT istituzionali negati. Handler chiama MT2 prima di inizializzare il contesto. Ticket non accede ai dati. |
| TenantContext / ICurrentTenant | Un solo oggetto immutabile con UserId/InstitutionId/MembershipId/set ruoli | Accessor scoped CurrentTenant inizializzato internamente dal solo handler dopo rivalidazione; assenza genera errore. Nessun owner da header/query/body. Un costruttore TenantContext non e una prova di autenticazione. |
| InstitutionProfileResolver | Membership e User verificati, ruolo richiesto e InstitutionId | Rivalida MT2 e risolve FK del medesimo tenant. I manager directory riusano AdminAsync di InstitutionSchoolAuthorization senza pretendere profili scolastici. |
| AuthController / AuthManager legacy | JWT Bearer legacy, User.Role/ClaimsContext | Login richiede gate/allowlist; register/RegisterForAdmin richiedono admin legacy e gate. Non sono onboarding istituzionale. Non aggiungono automaticamente InstitutionAdmin. |
| UsersController / UserManager legacy | Identita globale e autorizzazione legacy | Letture globali e scritture storiche rimangono nel ramo legacy chiuso per token istituzionali. UpdatePassword verifica caller. Non usare queste API come pannello della fondazione. Nessuna loro route viene aperta in MT5A. |
| MicrosoftIdentityLink | Binding server-side tid/oid -> UserId | Verifica provider e binding esplicito, non uguaglianza email o account provisioning automatico. Nessuna nuova credenziale/provider API. |
| LegacyEndpointGate | Marker + policy prevista + autenticazione + contesto verificato + rollout | Filtro globale MVC fail-closed, nessuna autorizzazione dal prefisso URL. Marker multipli, AllowAnonymous, schemi/policy alternativi negati. Il ramo legacy mantiene allowlist e isolamento holding. |
| Revoca MT2 | Stato corrente e uguaglianza esatta del set ruoli | Sospensione membership/Institution o modifica ruoli invalida le successive richieste, anche aggiungendo un ruolo. Nessuna cache autorizzazioni. I writer tecnici attuali non implementano last-admin/audit membership. |

Nello schema disponibile MT5A le prime tre tabelle hanno le FK/indici MT1; i 40 trigger
presenti proteggono principalmente ownership/grafi MT3A. Non certificano last-admin.
L'indice triplo introdotto per actor nel ledger MT4C non e presente in questo DB arretrato.
Inventario reale e differenze nel preflight; nessuna entity/colonna duplicata introdotta.

## Matrice delle autorizzazioni

| Ruolo nella membership corrente | Directory e dettaglio | Profili scolastici | Mutazioni membership |
| --- | --- | --- | --- |
| InstitutionAdmin | Si, con InstitutionAccess, membership/Institution Active e flag | Nessun profilo implicito | Nessuna in MT5A |
| Teacher | No | Teacher richiesto nei percorsi applicativi pertinenti | Nessuna |
| Student | No | Student richiesto nei percorsi applicativi pertinenti | Nessuna |
| Teacher + Student | No | Entrambi espliciti e indipendenti | Nessuna |
| InstitutionAdmin + altro ruolo | Si | L'altro ruolo non crea automaticamente il relativo profilo | Nessuna |
| admin legacy / selection / anonimo | No | Nessuna autorizzazione istituzionale | Nessuna |

Per User X admin in A e Teacher in B, solo la membership A consente la directory A.
Ogni token rappresenta una sola membership: non somma privilegi globali o di B.
Le membership sospese restano visibili all'admin autorizzato, con stato e ruoli correnti;
i loro utenti non possono aprire sessioni interattive istituzionali tramite quella membership.

## Confine read-only

IInstitutionMembershipAdminManager acquisisce ICurrentTenant attraverso l'autorizzazione
esistente AdminAsync, rivalida MT2 e chiama soltanto IInstitutionMembershipDirectoryRepository.
List e Get non accettano InstitutionId dal client. Ogni query parte dal filtro owner SQL;
Get combina owner e MembershipId. AsNoTracking, proiezioni limitate, EXISTS ruolo,
JOIN User della sola membership, una query risorsa con ruoli senza N+1. Nessun count.
Pagine default20/max100; ordine JoinedAt/Id; offset controllato contro overflow Int32.
Ricerca sottostringa letterale Email solo su identita gia associate, lunghezza max100.
Le date reali sono rappresentate UTC; non vengono aggiunte date di invito o revoca inesistenti.

Policy InstitutionMembershipAdminAccess usa esclusivamente InstitutionAccess, requisito MT2
e InstitutionAdmin. Marker dedicato verificato dal gate. Flag server-side
Multitenancy:MembershipAdminApi:Enabled assente/falso/non valido nega; indipendente da SchoolApi,
CourseApi, ExamApi e InstitutionWorker: la directory e governance identita, non didattica.
Il flag apre una superficie, non assegna ruoli. Nessun appsettings operativo attivato.

## Regole obbligatorie per MT-5B

Ogni mutazione deve acquisire l'attore dal TenantContext verificato, controllare lo stato corrente
nel medesimo tenant e negare target esterni come inesistenti. User.Role non puo concedere
privilegi; account creation globale non implica membership o InstitutionAdmin. Nessuna cancellazione
o modifica credenziali dell'account condiviso da parte dell'amministratore di una fondazione.
Email conosciuta o uguale non prova possesso, identita legale o consenso all'associazione.

La rivalidazione MT2 gia interrompe nuove operazioni quando stato o set ruoli cambiano, ma non
termina retroattivamente una richiesta gia autorizzata. I futuri writer devono rivalidare sotto lock
prima del commit. Se stato e ruoli vengono ripristinati identici prima della scadenza (900s), un
vecchio JWT puo tornare valido secondo MT2: decidere se serve una generation monotona per
revoche permanenti prima di aprire mutazioni. MT5A non inventa colonne o un secondo sistema JWT.

## Last Admin: progetto transazionale MT-5B

Per una fondazione utilizzabile deve rimanere almeno una membership Active con ruolo
InstitutionAdmin e User esistente. Nessuna dipendenza da profili Teacher/Student o User.Role.
L'accesso effettivo richiede anche Institution Active; il conteggio dei candidati per una futura
riattivazione non puo essere azzerato per il solo fatto che Institution sia sospesa.

Percorsi da proteggere: rimozione del ruolo InstitutionAdmin, sostituzione completa dei ruoli,
sospensione/left/eliminazione della membership, spostamento owner o User (da vietare), eliminazione
account globale e futuro blocco identita capace di rendere inutilizzabili tutti gli admin.
Anche concessioni, riattivazioni, bootstrap e modifiche di stato Institution devono aderire al protocollo.
La sospensione dell'intera fondazione e un'operazione di governance separata: non crea admin fittizi,
non consente ripartenza senza un amministratore valido e rimane senza nuova API MT5A.

Protocollo proposto su InnoDB, stessa connessione/DbContext/transazione e per ogni writer:

1. SELECT id FROM institution WHERE id=@verifiedTenant FOR UPDATE, prima di letture consistenti
   sui ruoli. Questo record gia esistente e il mutex per tenant; nessun lock globale o Redis.
2. Rileggere con locking/current reads attore, membership, User e ruoli dello stesso tenant.
   Lock membership in ordine Id crescente, poi ruoli in ordine MembershipId/Role; eventuali
   inviti dopo questi lock. Nessun percorso puo invertire l'ordine Institution -> membership -> ruoli.
3. Leggere tutti i candidati admin Active scoped nello stato corrente sotto lo stesso protocollo;
   simulare l'effetto minimo e negare se il numero residuo e zero. Non usare count cached,
   snapshot creato prima del lock o conteggi di altri istituti. Nessun admin sostitutivo automatico.
4. Applicare mutazione, inserire evento audit distinto e commit atomico. Eccezione -> rollback.
   Per operazioni eccezionali su piu tenant (non ammesse alle API ordinarie), lock Institution Id
   in ordine crescente prima degli altri lock, con workflow separato approvato.

Due revoche concorrenti di admin diversi prendono lo stesso lock Institution: la seconda vede
il commit della prima e viene negata se lascia zero admin. MT5B deve provarlo su MariaDB con
due transazioni reali, insieme a rollback audit, self-revocation e indipendenza A/B.
Nessun controllo e oggi aggiunto ai writer tecnici MT1: non sono un pannello amministrativo.
Decisione da approvare: self-revocation se resta un altro admin e recupero d'emergenza con
verifica legale fuori banda. Finche non definita, nessuna API di tale tipo.

## Audit membership futuro e distinto

Non usare course_administration_event o EntityType didattici fittizi. Proporre un ledger
istituzionale separato append-only per membership.created, membership.suspended/reactivated,
role.added/removed, invitation.created/accepted/revoked/expired e decisioni last-admin.
TargetMembershipId e nullable solo per eventi invito precedenti alla membership; un riferimento
invito tipizzato e separato evita identificatori inventati. Nessun DDL/evento retroattivo in MT5A.

Campi minimi: InstitutionId, EventId, ActorUserId, ActorMembershipId, TargetMembershipId,
Action, Before/After limitati a stato/ruoli necessari, OccurredAtUtc, RequestId e motivo se richiesto.
FK composite tenant/membership/User dell'attore e tenant/target; nessun payload credenziali,
token invito, password, refresh token, anagrafica globale duplicata o contenuto HTTP integrale.
Bootstrap del primo admin richiede un attore di provisioning verificato con semantica esplicita,
non una membership fittizia: scelta del modello di attore tecnico ancora da approvare.

Evento e mutazione devono condividere transazione e rollback. EventId/idempotenza, retention,
lettori autorizzati, motivi dei rifiuti (compreso last-admin), gestione esiti commit incerti e audit
di sicurezza dei tentativi negati richiedono design distinto: un rifiuto non produce un evento
di mutazione riuscita. Non usare il ledger didattico come scorciatoia.

## Onboarding e inviti proposti: nessuna implementazione MT5A

1. Verifica legale della Fondazione ITS Academy con fonti istituzionali, rappresentante/delega,
   canale verificato e doppia approvazione proposta. Stabilire chi verifica, evidenze, retention,
   responsabilita e contestazione. Nessun self-service pubblico che crei Institution Active.
2. Provisioning controllato crea Institution in Provisioning dopo approvazione. Un processo
   operativo con privilegi limitati, separato dall'accesso didattico, autorizza il primo admin.
   Non introdurre SuperAdmin globale per leggere dati scolastici.
3. Primo admin verifica identita e possesso email, autentica account globale e accetta la
   nomina esplicita; attivazione atomica membership/ruolo/audit e controlli last-admin.
   Nessun privilegio basato sul body RegisterForAdmin o sulla sola creazione User.
4. Admin del tenant puo proporre invito locale per ruoli consentiti. Token casuale ad alta
   entropia, hash persistito, scadenza breve definita, uso singolo e nessun ruolo elevabile dal
   destinatario. Nessun lookup pubblico che riveli se l'email appartiene a un account globale.
5. User esistente autentica l'identita e prova possesso dell'indirizzo destinatario attraverso
   challenge verificato. User nuovo completa registrazione/verifica separata prima di accettare.
   La coincidenza dell'email nel database non e una prova; vietato collegare automaticamente.
6. Accettazione lega UserId verificato a una membership di quel tenant con ruoli approvati.
   Account gia membro di B resta lo stesso User; ruoli/profili/credenziali di B non cambiano.
   UNIQUE(UserId,InstitutionId), lock tenant e invito, consumo e audit atomici/idempotenti.
7. Inviti revocati/scaduti non sono accettabili. Richieste ripetute hanno esito pubblico neutro;
   riemissione revoca il precedente challenge secondo policy da definire. Nessun token in log.
8. Revoca membership lascia account globale e altre membership intatti. Conservare storico
   e FK; cancellazione account condiviso richiede processo separato e gestione riferimenti.

Decisioni ancora necessarie: autorita legale di provisioning e primo admin, autenticazione forte
e recupero, ruolo concedibile e doppio controllo per admin, durata/rate limit dei challenge,
riemissioni, email cambiata, minori/consenso, privacy/retention, attori di bootstrap e revoche
permanenti. Queste funzionalita rimangono chiuse fino a MT5B/MT5C approvati.
