# ElectronicRegister — MULTITENANCY MT-2

**MT-2 completata nel perimetro identita/autorizzazione.** Due suite definitive
consecutive: **327/327 Passed ciascuna, zero Failed e zero Skipped**. Nessun secondo
tenant abilitato ai dati scolastici; MT-3 non iniziata.

## 1. Commit iniziale e stato Git

Branch `refactor-database-entities`; HEAD iniziale `970d5d704283daf0c33e106b1ff17149360cfcf4`.
Preservate tutte le modifiche locali non committate dell'audit e MT-1: 19 file produttivi
gia modificati, nuovi test Audit/MT1, entity/repository/script/documenti MT-1. Nessun reset,
checkout, pull, commit o modifica frontend. La baseline non e stata dedotta dal remoto.

Prima delle modifiche: build riuscita; letti MULTITENANCY_DESIGN, MULTITENANCY_MIGRATION,
MULTITENANCY_MT1_REPORT e FINAL_BACKEND_AUDIT. Ricognizione auth iniziale e matrice
MT2/MT3/escalation: [INITIAL_AUTH_AUDIT.md](TestResults/mt2/INITIAL_AUTH_AUDIT.md).
Inventario riferimenti Role/StudentId/TeacherId/Authorize con file e righe:
[auth-reference-inventory.txt](TestResults/mt2/auth-reference-inventory.txt).

## 2. Baseline iniziale effettiva

**39 unit + 218 integration = 257/257 Passed, zero Failed/Skipped**, MySQL reale MAMP
`electronic_register_test`, MySQL 5.7.24, REPEATABLE-READ. Nessun database utenti/principale
utilizzato. La prima esecuzione con LogFileName condiviso ha sovrascritto il TRX unit:
baseline ripetuta prima delle modifiche, usando nomi generati distinti per conservare
entrambi i progetti in [baseline-preserved](TestResults/mt2/baseline-preserved/).

## 3. Architettura finale del login

Domain: DTO, TenantContext immutabile, record MembershipAuthorization, interfacce.
Business: MembershipAuthorizationRules, validita stati e uguaglianza esatta ruoli.
Application: InstitutionAuthManager orchestra credenziali, elenco, selezione, autorizzazione.
Infrastructure: repository read-only auth, JWT tipizzati, bridge legacy e binding Microsoft.
Api: controller dedicato, due schemi/policy, handler autorizzazione, filtro globale legacy.
Tutte le dipendenze request-scoped; nessun DbContext o TenantContext in singleton.

Non esiste AuthService: il servizio storico di validazione e UserService. Il flusso
legacy precedente era AuthController -> AuthManager -> UserRepository/hasher o
MicrosoftTokenValidator -> JwtTokenGenerator(User.Role/profili) -> JwtBearer ->
ApiControllerBase.CurrentCaller -> ClaimsContext -> manager didattici. Restano intatti
i manager didattici e la loro semantica, protetti dal nuovo confine.

## 4. Autenticazione e selezione

1. Password BCrypt o token Microsoft validato identifica User globale esistente.
2. Viene emesso soltanto un ticket di identita di 300 secondi, senza ruoli scolastici.
3. Il ticket permette elenco delle sole membership Active in Institution Active, con ruoli.
4. La selezione riceve InstitutionId/MembershipId desiderati, ma li verifica contro
   identita autenticata e database corrente, senza rivelare il motivo preciso del diniego.
5. Viene emesso un access token di 900 secondi per una sola membership.
6. Ogni richiesta istituzionale ricontrolla il DB prima dell'action.

Nessuna membership creata al login; nessun dominio email autorizzante, nessun Pending
autoattivato. Con zero membership valide il ticket restituisce elenco vuoto e nessun
token operativo. Cambio fondazione ripete selezione/verifica con ticket ancora valido
oppure richiede nuova autenticazione dopo la sua scadenza.

## 5. Contratti HTTP introdotti

| Metodo/route | Autorizzazione | DTO input | Output |
| --- | --- | --- | --- |
| POST /api/Auth/identity/login | credenziali locali | LoginDto | IdentityTicketDto |
| POST /api/Auth/identity/microsoft-login | token provider | MicrosoftLoginDto | IdentityTicketDto |
| GET /api/Auth/institutions | solo selection ticket | nessuno | array InstitutionChoiceDto |
| POST /api/Auth/select-institution | solo selection ticket | SelectInstitutionDto | InstitutionAccessDto |
| GET /api/Auth/context | solo access istituzionale + DB | nessuno | TenantContextDto |

JSON camelCase; Guid stringa; ruoli stringhe esatte InstitutionAdmin/Teacher/Student.
RequestedRole facoltativo deve essere posseduto, non e fonte di privilegi e non cambia
l'insieme completo dei ruoli della membership nel token. Token assente/strutturalmente
invalido -> 401; identita valida ma scelta/autorizzazione non valida -> 403 generico;
binding invalido -> 400. Nuovi errori ProblemDetails. Nessun dato didattico nel context.
OpenAPI effettivo include le cinque route; bootstrap Dev/Production conserva Scalar/
OpenAPI soltanto in Development. Nessun contratto CRUD scolastico rinominato.

## 6. Claim e validazione JWT

| Proprieta | Selection | Access |
| --- | --- | --- |
| Schema/policy | InstitutionSelection | InstitutionAccess |
| typ e purpose | er-selection+jwt | er-institution+jwt |
| Audience | Jwt:Audience + :institution-selection | Jwt:Audience + :institution-access |
| Durata | 300 secondi | 900 secondi |
| Claim comuni | sub, jti, iss, aud, exp, nbf, auth_time | stessi |
| Claim aggiuntivi | nessuno | institution_id, membership_id, institution_role multipli |

Firma HS256, issuer, audience, lifetime, signing key, algoritmo e typ verificati.
Skew zero nei nuovi schemi; minimo 32 byte per la chiave. Non modificato lo skew
legacy per facilitare test. Sub/istituto/membership/jti non vuoti; claim identificativi
singoli; ruoli distinti validi; proprieta JSON duplicate rifiutate anche se il reader
JWT potesse sovrascriverle. Audience multiple non ammesse nei nuovi token. Nessuna
password/hash, elenco istituti, claim Role globale o StudentId/TeacherId nei nuovi token.

Il ticket firmato e riutilizzabile nella finestra breve per una nuova selezione
server-side: **non monouso**. Jti non e una sessione persistita/revocabile; nessun
refresh token introdotto. Scopo/audience/tipo/policy impediscono confusione ticket/access,
in entrambe le direzioni. Trasporto HTTPS e custodia bearer restano prerequisiti.

## 7. Ruoli multipli

I ruoli vengono letti soltanto dalla membership selezionata. A puo restituire
InstitutionAdmin+Student e B Teacher per lo stesso User con User.Role globale admin.
InstitutionAdmin non diventa admin globale. Non esiste un ruolo operativo legacy
derivato dal contesto istituzionale: il nuovo token non raggiunge quei manager.
Rimozione **o aggiunta** di un ruolo invalida il vecchio insieme nel token; una nuova
selezione emette i ruoli correnti. Nessun SuperAdmin introdotto.

## 8. TenantContext

Domain TenantContext espone solo UserId/InstitutionId/MembershipId e FrozenSet di ruoli.
ICurrentTenant e scoped, inizializzato internamente dalla policy soltanto dopo verifica.
Getter senza verifica fallisce; nessun setter pubblico, singleton, Guid.Empty, prima
fondazione o header/DTO che autorizzi. Copia difensiva dei ruoli e richieste HTTP A/B
concorrenti provate. Le cinque nuove API usano le policy dedicate; solo GET context
richiede/esibisce TenantContext. Tutte le 73 legacy continuano con ClaimsContext.

## 9. Controllo membership e costo

MembershipAuthorizationRepository esegue letture AsNoTracking/AsSingleQuery, vincolate
a UserId e, per access/selezione, a InstitutionId+MembershipId. Ruoli e stato parent
sono letti nello stesso statement: nessuna collection tracked o snapshot separata
per i ruoli. FK MT-1 impediscono membership verso User/Institution inesistenti.

GET context: una query applicativa con lookup PK membership, parent e fino a tre
ruoli; zero strutture Exam/risposte. Ticket policy verifica User esistente, poi elenco/
selezione aggiungono una query. Listing usa l'indice UNIQUE(user_id,institution_id).
Legacy aggiunge lettura User/membership e, prima del backfill senza membership, lookup
holding. Rimane overhead ServerVersion.AutoDetect gia esistente, non eliminato qui.
Nessuna cache, indice o nuova infrastruttura. Il seed piccolo e l'EXPLAIN senza righe
([mysql-plan.txt](TestResults/mt2/mysql-plan.txt)) **non sono una misura prestazionale**:
nessun miglioramento di latenza o carico produttivo affermato.

## 10. Sospensione e revoca

Nuova richiesta dopo commit di Pending/Suspended/Left, Institution Provisioning/
Suspended/Archived, cambio ruoli, cancellazione membership o User supportata -> 403
per JWT crittograficamente valido. Identificativi mancanti/alterati strutturalmente ->
401; identificativi validi ma incoerenti con DB -> 403. Nessun accesso dopo revoca
basato sulla sola firma. User non ha campo disabled: tale lifecycle non e inventato.

Punto di controllo: authorization prima dell'action, fuori dalle transazioni didattiche.
Non si promette interruzione/rollback di richieste gia autorizzate o revoca retroattiva.
Le prove cambiano dati reali in scope separati, verificano risposta successiva e stato
persistito; nessun lock Exam/submission, worker o retry generico e stato modificato.

## 11. Compatibilita User.Role legacy

Legacy e chiuso per default. Enabled richiede InstitutionId esplicito e allowlist UserIds
univoca/non vuota con validazione startup. User.Role e profili sono utilizzabili soltanto
nel dataset storico dichiarato, non come diritti su altre fondazioni. Il token deve
corrispondere a ruolo/profili correnti del User. Se esiste membership holding, devono
essere Active membership e Institution e ruolo coerente; qualunque membership esterna
alla holding esclude l'account dal bridge anche se allowlisted.

Prima del backfill, solo `AllowUnbackfilledAccounts=true` autorizza gli account
esplicitamente allowlisted senza inventare membership. InstitutionId rimane un
identificativo del manifest; se la riga esiste deve essere Active. **Disabilitare questo
flag prima del backfill permanente/cutover**: lasciarlo attivo potrebbe riammettere
account allowlisted dopo cancellazione di tutte le membership. Non e un fallback del
modello finale. Mai aggiungere identita dei tenant nuovi all'allowlist. Nuovi account
creati dal CRUD legacy non entrano automaticamente nel manifest e non sono nuovi tenant.

Configurazione/variabili e procedura in [MULTITENANCY_DESIGN.md](../../docs/architecture/MULTITENANCY_DESIGN.md)
e [MULTITENANCY_MIGRATION.md](../../docs/architecture/MULTITENANCY_MIGRATION.md).
L'esempio appsettings aggiunge soltanto opzioni disabilitate/liste vuote; non e un file
di configurazione runtime o un backfill permanente. Nel DB test non restano membership
nuove permanenti dopo teardown; il nuovo login non inventa i prerequisiti mancanti.

## 12. Microsoft e limiti

MicrosoftTokenValidator dell'audit preservato: firma, audience, expiry, issuer v2 del
tenant configurato; nessun ValidateIssuer=false. Nuovo MicrosoftIdentityLink usa solo
tid/oid validati e binding server espliciti TenantId/ObjectId/UserId. Sono gestiti i
claim originali e quelli mappati dal JWT handler, richiedendo unicita. Binding assente,
ambiguo o User inesistente -> nessun ticket. Email/preferred_username non autorizzano.
Nessun provisioning o membership automatica e nessuna equivalenza Entra/Institution.

**Modifica di sicurezza intenzionale:** anche il vecchio microsoft-login richiede
ora il binding stabile, oltre al gate legacy. Non viene lasciata una scorciatoia via
email. Occorre predisporre binding verificati prima di riattivarlo; lista default vuota.
Binding configurati sono un ponte amministrato, non persistenza ExternalIdentity o
onboarding implementati. Le eccezioni di token invalido diventano mancata autenticazione;
outage tecnici non sono falsamente dichiarati credenziali sbagliate e seguono il 500
controllato esistente. Nessun tenant Microsoft reale usato/richiesto nei test.

## 13. Protezione globale endpoint legacy

LegacyEndpointGate e filtro globale MVC, prima del model binding/action; non una patch
al solo ExamController. Copre Student, Teacher, Grade, Subject, Users, Biennium, Exam,
Auth/me e registrazioni amministrative. Le due login precedenti sono anonime solo per
ricevere credenziali; il server verifica modalita/allowlist prima di emettere JWT.
Il gate protegge anche nuove action legacy senza attributi specifici (fail-closed).

Ticket e scoped token hanno audience diverse dalla Bearer legacy: le 71 action legacy
protette restituiscono 401 prima di qualunque logica didattica. Token scoped A viene
rifiutato esattamente come B; nessun mapping a profili legacy. Token legacy validi
con modalita disabilitata/autorizzazione corrente assente -> 403. Wrong role conserva
403. Nessuna query globale e stata dichiarata tenant-safe grazie al solo claim.

## 14. Prove A/B

Due Institution Active, User X globale admin con A InstitutionAdmin+Student e B Teacher,
User Y globale admin con solo Student in A, identita legacy allowlisted separata.
Verificati login, elenco due/una/zero membership, token di un solo contesto, ruoli non
combinati, selezione B da Y vietata, MembershipId di altro User vietato, ruolo non
posseduto/numerico vietato. Assenti profili scolastici fittizi per i token nuovi.

Inventariate tutte le 73 route legacy e inviate richieste alle 71 protette con ticket,
token A e token B: tutte 401, incluse mutazioni/admin, contatori persistiti invariati.
Login vecchio con credenziali corrette di X vietato nonostante allowlist, per membership B.
Controllo positivo account legacy e gate negativi dopo backfill/stato/ruoli; richieste
context A/B simultanee e scope nuovo senza contesto dimostrano separazione request-scoped.
Sono prove del **blocco preliminare**, non dell'isolamento completo dei repository.

## 15. File produttivi MT-2

Percorsi relativi a Backend/ElectronicRegisterAPI/src:

- Domain: Models/TenantContext.cs; DTOs/InstitutionAuthDtos.cs;
  Interfaces/Managers/IInstitutionAuthManager.cs; Repositories/IMembershipAuthorizationRepository.cs;
  Security/IInstitutionTokens.cs.
- Business: Services/MembershipAuthorizationRules.cs.
- Application: Managers/InstitutionAuthManager.cs nuovo; AuthManager.cs (gate legacy e
  Microsoft stabile); DependencyInjection/AddApplicationManagers.cs.
- Infrastructure: Repositories/MembershipAuthorizationRepository.cs;
  Security/InstitutionTokens.cs, LegacyAccess.cs, MicrosoftIdentityLink.cs;
  Options/IdentityMigrationOptions.cs; DependencyInjection/ServiceCollectionExtensions.cs.
- Api: Controllers/InstitutionIdentityController.cs; Security/InstitutionAuthentication.cs,
  LegacyEndpointGate.cs; Program.cs; DependencyInjection/ServiceCollectionExtensions.cs;
  appsettings.example.json.

Nessun nuovo pacchetto, schema/SQL, entity generata, migrazione didattica, manager esami,
ExamSubmissionProcessor, worker, grading, lock, audit submission o frontend modificato
da MT-2. I diff audit gia presenti su altri file restano lavoro precedente.

## 16. Nuovi test e adattamenti esistenti

**70 nuovi casi: 9 unit + 61 integration**.

| Classe | Casi | Garanzia |
| --- | ---: | --- |
| MembershipAuthorizationTests | 9 | stati non attivi, immutabilita, ruoli esatti, contesto non vuoto, identita/istituti differenti |
| Mt2IdentityTests | 50 | vero Program/Kestrel/MySQL, selezione, revoca 12 casi, JWT 22 casi, tutte le route legacy, bridge e contesti concorrenti |
| Mt2MicrosoftIdentityTests | 11 | firme RSA/metadati locali, issuer/audience/expiry/firma, binding stabile e negativo email, MVC reale e persistenza senza provisioning |

I test Mt2IdentityTests invocano l'entry point produttivo con DI/worker completi, porta
dinamica e arresto ordinato; nessun componente disabilitato. Bootstrap audit nei due
ambienti Development/Production resta nella suite e valida DI/scope, JWT, errori,
OpenAPI e arresto. Microsoft usa l'host HTTP controllato precedente, con discovery locale
iniettata e worker omesso come gia dichiarato da quell'host; non sostituisce i test di
bootstrap completo. Nessuna chiamata ad Azure live.

257 casi precedenti preservati. Adattamenti limitati:

- Phase2C_HttpHost: manifest single-tenant test esplicito (Guid tecnico, identita seed,
  opt-in pre-backfill), opzioni JWT/new schemes, hook DI opzionale per discovery Microsoft
  controllata. Nessuna assertion scolastica indebolita.
- AuditBootstrapTests: configurazione legacy esplicita dell'account temporaneo; inventario
  totale passa 73->78, ma mantiene assertion separata **73 legacy** e tutti i gate di
  ruolo originali. Le cinque route nuove sono coperte dai test MT-2 con proprie policy.

Il primo nuovo teardown tentava di risolvere IHostApplicationLifetime dopo che Main
aveva disposto il provider: errore del test, corretto acquisendolo prima di StopAsync e
garantendo cleanup in finally. Ripulite esclusivamente le righe MT2 temporanee lasciate
da quel tentativo; nessun dato legacy perso. Nessun test cancellato, saltato o indebolito.

## 17. Validazione finale

Build finale riuscita: zero errori e zero warning incrementali. Le ricompilazioni
mostrano gli 11 warning nullable gia presenti nella baseline, nessuno nuovo MT-2.
Prima suite completa di controllo: 48 unit + 279 integration = **327/327**, zero
Failed/Skipped. Le due esecuzioni definitive successive, senza modifiche a codice o
test fra loro, sono state verificate nei quattro TRX:

| Esecuzione | Unit Passed/Total | Integration Passed/Total | Total | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: | ---: |
| Baseline effettiva | 39/39 | 218/218 | 257/257 | 0 | 0 |
| Definitiva 1 | 48/48 | 279/279 | 327/327 | 0 | 0 |
| Definitiva 2 consecutiva | 48/48 | 279/279 | 327/327 | 0 | 0 |

[verification.json](TestResults/mt2/verification.json) conserva total/executed/passed/
failed/notExecuted e SHA-256 di ogni TRX. Confronto dei nomi: tutti i **257 casi
precedenti presenti in entrambi i run**, insiemi finali identici, 9 nuovi unit e 61
nuovi integration. Verificati anche i due casi bootstrap Dev/Production e le cardinalita
50/11 delle classi MT-2. Evidenze in [final-1](TestResults/mt2/final-1/) e
[final-2](TestResults/mt2/final-2/). `git diff --check` senza errori; warning LF/CRLF
del checkout Windows non sono errori di whitespace.

Comandi dalla cartella Backend/ElectronicRegisterAPI, con TEST_MYSQL_CONNECTION nella
sessione verso electronic_register_test (nessuna password nel report):

```powershell
dotnet build ElectronicRegisterAPI.slnx
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./mt2-evidence/final-1
dotnet test ElectronicRegisterAPI.slnx --no-build --logger:trx --results-directory ./mt2-evidence/final-2
git diff --check
```

Nomi TRX generati distinti per evitare la sovrascrittura tra progetti. Le due suite
definitive includono tutti i test HTTP, Microsoft, unit, MySQL e bootstrap Dev/Prod.

## 18. Rischi aperti

| Livello / categoria | Stato e limite |
| --- | --- |
| HIGH, limite funzionale intenzionale | API/repository didattici non isolati: blocco token nuovi attivo; vietata apertura operativa secondo tenant fino a MT-3 |
| MEDIUM, configurazione | Bridge pre-backfill solo temporaneo, allowlist amministrata e flag da spegnere al backfill; nessun fallback nel modello finale |
| MEDIUM, configurazione/operativita | HTTPS/proxy, CORS permissivo storico, rate limit login, segreti/rotazione, backup/restore e MySQL supportato restano gate audit non risolti da MT-2 |
| MEDIUM, identita | Nessun disabled User, refresh/logout/session store; ticket bearer riutilizzabile entro cinque minuti; binding Microsoft solo configurati, nessuna prova Entra live |
| MEDIUM, garanzia temporale | Revoca su richieste nuove al punto authorization; operazioni gia autorizzate non interrotte |
| MEDIUM, prestazioni non misurate | Query aggiuntive DB per autorizzare; no carico/staging p95, ServerVersion.AutoDetect preesistente |
| LOW, integrazione | ClaimsContext/ruolo singolo legacy e DTO scolastici non migrati; non convertirli implicitamente dal TenantContext |

Nessuna nuova vulnerabilita Critical/High aperta dimostrata nel flusso MT-2 verificato.
Il limite operativo MT-3 e dichiarato e protetto, non spacciato per isolamento risolto.

## 19. Prerequisiti MT-3 e giudizio

Prima di abilitare un secondo tenant alle API scolastiche: ownership InstitutionId
materializzata su dati legacy, profili Student/Teacher per membership, FK coerenti,
filtro SQL/autorizzazione risorsa su ogni repository/lookup/raw SQL/aggregato/CRUD,
worker e audit tenant-aware, policy per operazioni sensibili dopo revoca, test A/B
HTTP/persistenza completi. Conservare User.Role/StudentId/TeacherId finche necessari;
nessuna modifica al workflow/grading esami per facilitare la migrazione.

MT-2 realizza il confine di identita/autorizzazione e il blocco dei tenant non migrati.
**ElectronicRegister NON e ancora multi-tenant operativo o MULTI-TENANT PRODUCTION READY.**
MT-3 non iniziata.
