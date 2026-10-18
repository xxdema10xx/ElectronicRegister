# MT-2 ricognizione prima delle modifiche

HEAD 970d5d704283daf0c33e106b1ff17149360cfcf4, branch refactor-database-entities.
Preservate modifiche non committate audit/MT1. Build riuscita. Baseline effettiva
39 unit + 218 integration Passed, zero Failed/Skipped, MySQL electronic_register_test.
La prima esecuzione con LogFileName condiviso ha sovrascritto il TRX unit:
una seconda baseline conserva file con nomi generati distinti.

Flusso corrente: AuthController -> AuthManager -> UserRepository/password o
MicrosoftTokenValidator -> JwtTokenGenerator(User.Role/profili) -> JwtBearer ->
ApiControllerBase.CurrentCaller -> ClaimsContext -> manager didattici.
Non esiste AuthService: la validazione account e in UserService.

| Funzionalita | Problema di compatibilita / rischio | MT-2 | MT-3 |
| --- | --- | --- | --- |
| Login/password | JWT assegna ruolo globale User.Role | flusso nuovo distinto; legacy opt-in con identita allowlisted | ritiro token legacy |
| Microsoft | issuer/firma/audience/lifetime validati; email non e identificatore stabile | nuovo flusso solo binding server tid/oid/UserId, nessun provisioning | persist identity link amministrato |
| Auth/me, UserManager MapToDto/Update/UpdatePassword | espone/usa Role, StudentId, TeacherId globali | blocco centralizzato token nuovi | profili tenant e account administration |
| ExamManager tutti i metodi caller; ExamSubmissionManager list/review/finalize | ruolo singolo, ownership TeacherId/StudentId | nessun adattamento o token scoped ammesso | ownership persistita e ruoli multipli |
| GradeManager count/list/get/paged/statistics/filters/create/update | filtri caller.Role e profili globali | invariati dietro gate legacy | filtro SQL InstitutionId + profili |
| SubjectManager count/list/get/byname/byteacher/GetVisibleSubjects | visibilita classe da profili globali | invariato dietro gate legacy | membership/profili tenant |
| StudentManager letture docente | TeacherId globale | invariato dietro gate legacy | ownership tenant |
| SubmissionVisibility/ExamSubmissionMapper | visibilita per ruolo singolo | invariati, non riutilizzati nel nuovo contesto | policy visibilita tenant |
| Teacher/Biennium/Users/Subject/Student/Grade/Exam controller | Authorize admin/teacher/student non scoped | filtro globale MVC fail-closed, inclusi admin e login vecchi | migrare ogni route prima di rimuovere gate |
| InstitutionMembershipRepository | lookup non autorizza identita corrente; no join stato istituto | repository auth read-only dedicato, DB ad ogni richiesta | mantenere guardie oltre query filter |
| Program/DI | una sola audience; nessuna revoca membership | due schemi tipizzati, policy DB, TenantContext scoped | isolamento persistenza/worker |
| Worker/raw SQL | nessun tenant | non modificati/non esposti da nuovo auth | tenant da relazioni persistite |

Rischi iniziali: accettare nuovo InstitutionAdmin come admin globale;
combinare ruoli A/B; usare profili legacy in B; confondere ticket/access;
fidarsi dei claim dopo revoca; consentire login legacy a qualunque User B.
Nessuno risolto dalla sola presenza di InstitutionId nel token.
Nessun backfill permanente presente: niente membership inventate, nessun fallback
alla prima Institution. Nuovo login richiede istituti/membership predisposti.

Scelta: token istituzionali non accedono a NESSUNA API legacy, neppure A.
Legacy richiede Enabled, InstitutionId esplicito, UserIds allowlist server-side.
Identita con membership di altra fondazione non sono ammesse al bridge legacy;
se esiste membership legacy deve essere attiva con istituto attivo e ruolo coerente.
Senza backfill, allowlist e identificativo holding espliciti sono il solo bridge
single-tenant, non un'autorizzazione istituzionale.
