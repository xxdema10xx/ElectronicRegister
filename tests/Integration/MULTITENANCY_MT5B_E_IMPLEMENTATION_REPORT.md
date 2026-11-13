# MT-5B-E — endpoint HTTP IAM

21 ottobre 2026. **MT-5B-E implementata e verificata: 873/873 Passed**, incluse tutte
le **820 identità precedenti**, zero Failed/Skipped/NotExecuted. Nessuna DDL,
migrazione, restore, reset, modifica GLOBAL o intervento su XAMPP/3306.

## Superficie e contratto effettivi

Un solo endpoint nuovo: **POST /api/institution/admin/members/{membershipId:guid}/mutations**.
Le GET directory e dettaglio sono preservate. Nessuna creazione membership,
onboarding, invito, gestione globale utenti o frontend.

Body JSON tipizzato MembershipMutationRequest, con proprietà estranee rifiutate:

```json
{
  "eventId": "2c0f43cb-03b4-44e0-8261-97950123ee2d",
  "expectedRevision": 1,
  "action": "AddRole",
  "role": "Student",
  "reason": "Assegnazione del ruolo istituzionale"
}
```

EventId, ExpectedRevision, Action e Reason sono obbligatori. Action accetta soltanto
AddRole, RemoveRole, Suspend, Reactivate. Role è richiesto per le prime due azioni e
deve essere assente/null per le altre: InstitutionAdmin, Teacher o Student.
Enum esclusivamente stringa, esatti e case-sensitive: niente numeri, alias o valori
fuori catalogo. Motivo 1..500 caratteri non bianchi, senza controlli; Guid non vuoti,
revision non negativa Int64. Le regole semantiche sono quelle Domain già esistenti.
MembershipId proviene solo dalla route; InstitutionId, ActorUserId, MembershipId nel
body e SecurityGeneration da applicare non sono campi accettati.

200 con MembershipMutationResponse: eventId, revision, status (active/suspended),
roles (stringhe del catalogo), derivati dal risultato reale del Manager.
Retry con EventId/intento identici restituisce lo stesso body storico, anche dopo
altre mutazioni; non viene presentato come uno snapshot corrente del target.

## Autorizzazione e transazione

Policy amministrativa InstitutionMembershipAdminAccess originale: solo schema
InstitutionAccess, attore corrente valido, generation e ruoli verificati sul DB,
InstitutionAdmin tenant-scoped. JWT globale, selection ticket e token malformati
non entrano nel flusso; token ben formato ma revocato mantiene il contratto 403.
Nessun privilegio da User.Role globale.

Il POST porta InstitutionMembershipWriteEndpoint, marker supplementare non
autorizzante. LegacyEndpointGate rimane fail-closed: richiede il marker amministrativo
originale e la sua policy/scheme senza alternative, un solo marker write su POST,
contesto verificato e **MembershipAdminApi && MembershipWriteApi**. Entrambi i flag
sono false per default; appsettings.example.json documenta il nuovo flag chiuso.
Le GET richiedono il solo flag directory, come prima. Il marker write da solo,
combinato con superfici alternative o con un verbo diverso non autorizza accesso.

Il controller inietta soltanto IInstitutionMembershipAdminManager e delega a
MutateAsync. Non usa DbContext o repository e non aggiorna ruoli, status, sidecar o
ledger. Il writer D rivalida l'attore dentro la transazione sotto mutex Institution,
gestisce owner, Last Admin, automutazione, ExpectedRevision, EventId e audit.
L'incremento della generation resta esclusivamente nei trigger. Nessuna duplicazione
di queste regole nel controller e nessun fallback a generation zero.

## Errori e riservatezza

| HTTP | Contratto |
| --- | --- |
| 400 | JSON/DTO invalido, catalogo non valido, campi estranei o errore semantico Domain |
| 401 | Autenticazione assente/invalida, JWT globale o selection ticket sul POST |
| 403 | Attore non autorizzato, ruolo tenant insufficiente, JWT revocato, self, rollout chiuso |
| 404 | Target assente o fuori tenant, stesso ProblemDetails |
| 409 | MembershipConflictException: revision, EventId conflittuale, no-op e conflitti Domain; concorrenza SQL riconosciuta |
| 500 | Errore inatteso, risposta opaca senza messaggi interni/SQL/stack |

GlobalExceptionHandler traduce centralmente MembershipConflictException.
MembershipApiErrors, limitato al nuovo POST, traduce DbUpdateConcurrencyException
e MySQL 1062/1205/1213 (anche nel wrapper EF), senza retry automatico e senza esporre
SQL. Altri errori inattesi restano 500. Un EventId già usato con intento diverso
produce 409 indipendentemente dall'esistenza del nuovo target; ciò non rivela tenant
esterni. Autorizzazione sempre prima del replay, secondo il contratto D.

## File

- API Contracts/MembershipMutationRequest.cs: DTO richiesta/risposta e converter locale stretto.
- Controllers/InstitutionMembershipAdminController.Mutate: unica action nuova.
- Security/InstitutionMembershipWriteEndpointAttribute e LegacyEndpointGate: capability aggiuntiva chiusa.
- ExceptionHandling/GlobalExceptionHandler e MembershipApiErrorsAttribute: mapping opaco.
- appsettings.example.json e contratto architetturale: configurazione e superficie correnti.
- Mt5eIamHttpTests: 53 nuovi casi HTTP; ProgramHost permette fault interceptor nel vero Program.
- MembershipHttpSurface e inventari storici: due GET esatte più un POST esatto, policy originali.

Manager/Repository D, JWT C, modello EF, SQL e trigger non modificati.

## Test reali e regressione

53 nuovi casi HTTP con Program ASP.NET Core, Kestrel, middleware, login/selection,
JWT effettivi e MySQL 5.7.24: nessun Manager mockato. Fixture A/B sintetiche GUID,
stesso User su entrambe le istituzioni, anche utenti global admin con soli ruoli
Teacher/Student o senza membership. Cleanup tramite le guardie root/owner/ambiente/
codice esistenti, mai disabilitando trigger/FK e mai usando la holding reale.

Copertura: tutte le quattro azioni, audit attribuito e revision +1, entrambe le GET,
Teacher/Student/global JWT/selection/assenza/malformed negati, target B/assente
indistinguibili, stale revision, no-op, replay storico identico, intento differente,
due POST concorrenti identici senza duplicati, sospensioni reciproche degli admin,
Last Admin/self protetti, attore revocato e ripristino ruolo che richiede nuovo JWT,
JWT vecchi permanentemente invalidi e B non revocato dalle modifiche di A.

27 varianti input invalido e sei casi rollout absent/false/invalid. Errori reali
simulati tramite DbCommandInterceptor prima e dopo l'INSERT audit nel vero host:
interceptor raggiunto esplicitamente, HTTP 500 opaco, nessun ruolo/generation/evento
persistito dopo rollback. Il caso dopo append verifica affected rows=1 prima del fault.

Primo run 52/53: il solo test legacy-gate puntava alla route inesistente /api/User;
corretto verso /api/Auth/me mantenendo il rifiuto 401/403 previsto da C. Nessuna
autorizzazione o assertion di isolamento indebolita. Run fallito conservato privato.
La campagna successiva HTTP/JWT/IAM/inventari è interamente positiva.

Le identità storiche restano uguali. I conteggi inventario sono ora 157 action MVC,
esattamente le precedenti 156 più il POST: due GET membership e tutte le altre 154
action preservate. I test storici prima HTTP-neutral verificano ora questa sola
estensione autorizzata, non permettono superfici generiche. Nei test manuali del
gate impostato esplicitamente GET per continuare a esercitare le vecchie guardie
di policy, senza farli passare soltanto tramite il nuovo controllo del verbo.

Build completa ElectronicRegisterAPI.slnx: exit 0. Freeze SHA256 di tutti i sorgenti
C# prima della regressione, confermato immutato al termine.
**69/69 unitari + 804/804 integrazione = 873/873 Passed**. Tutte le 820 identità D
presenti e Passed; 53 aggiunte, zero Failed/Skipped/NotExecuted. Nessun test eliminato
o disabilitato. Warning nullable preesistenti non soppressi.

TRX unit-final: **69 Passed**, SHA256 `DCDE151F90FA430BD1E1199C06057B557E797E81E17CC1287507A44BAE8F5552`.

TRX integration-final: **804 Passed**, SHA256 `01BFCD15A5DFF8F105DE6A11F30FD75F15356C45EEFB7BDFEE2705059136CC6E`.

## SQL reale, backup e Git

Entrambe le istanze: MySQL 5.7.24, hostname CES01622, binario
C:\MAMP\bin\mysql\bin\mysqld.exe; PID 9408/3306 e 2156/13306 correlati ai listener.
Baseline C:\MAMP\db\mysql solo lettura; target isolato datadir
`migrations/mt4/private/mt5a-mamp-13306/data/`, electronic_register_test.
Identità SQL/processo/listener verificate prima dei test e alla conclusione, senza fallback.

**Schema 13306 invariato: 34 tabelle, 68 trigger, 101 FK, 144 indici**. Tutti i corpi
SHOW CREATE e metadati trigger (DEFINER, SQL_MODE, charset/collation, ACTION_ORDER)
identici prima/dopo; gli altri metadati pure. Zero orfani su tutte le 101 FK composite
incluse. Validator MT4A 7, MT4C 5, MT5B 6+2, IAM 4+2 tutti a zero. GLOBAL sql_mode
immutato su entrambe le istanze. Zero istituzioni/membership/sidecar/eventi IAM di
fixture residui. Le sole variazioni dati sono quelle degli scenari legacy fissi già
documentati in C/D, nelle medesime sette tabelle e senza variazioni dei conteggi.

**3306 invariata**: confronto completo dati/metadati e tutti i 53 trigger, fingerprint
`FC6DF04E520A46B4084D9007A825D40DC354CE0C231CB80A0E20133886D92F22`.

Backup iniziale before-http.sql: SHA256 `353B849A3B496ABD999BA44A68A91114B6BDCA0E9AEA3FF31F9849FD7708BEFE`.

Backup finale after-http.sql (191692 byte): SHA256 `D6899462FDB3724A1FFD1D800F7344F0649398D8C59861E78FAB919DE28223B3`.

Entrambi privati, exit 0, footer/inventario/dati/hash verificati, con metadati e
overlay SQL_MODE archivistico non eseguito; il solo dump MySQL 5.7 non riproduce
esattamente quei contesti. ACL private controllate, spazio disponibile verificato.
Nessun DDL, restore o migrazione eseguiti; nessun cambiamento di account/password.

Evidenze in `migrations/mt4/private/mt5a-mamp-13306/mt5be-20261021/`, escluse da Git:
final-certification.json, snapshot iniziali/finali, inventari route, TRX, build,
backup e bundle. Nessuna credenziale nei sorgenti/log pubblici, niente dump in Git.
Gli inventari Development/Production dimostrano una sola coppia route/metodo nuova,
nessuna scomparsa e tutti gli schemi/policy/ruoli delle route precedenti invariati.

Branch refactor-database-entities, HEAD `7f0fed3897c225af5fdf3861974930a41122582f` invariata. Nessun commit, staging o push.

## Limiti di fase

Flag scrivente chiuso per default: l'abilitazione è configurazione esplicita e non
è stata applicata a un deployment. Nessun frontend, onboarding, invito, primo admin,
nuovo endpoint audit o gestione utenti globali. Nessun blocco residuo nei percorsi
implementati e verificati; i workflow esclusi non sono dichiarati implementati.

**MT-5B-E HTTP IMPLEMENTED AND VERIFIED**
