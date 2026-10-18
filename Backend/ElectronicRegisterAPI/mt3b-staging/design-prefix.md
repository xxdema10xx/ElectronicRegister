# ElectronicRegister - stato corrente MT-3B

**Checkpoint B completato nel livello Application/Infrastructure Student, Teacher e Subject. MT-3 complessiva PARZIALE: C/D/E restano da implementare. Nessuna API scolastica multi-tenant operativa; gate legacy chiuso.**

Questa sezione aggiorna gli snapshot storici conservati sotto. Verifiche, file, rischi ed evidenze: [MULTITENANCY_MT3B_REPORT.md](../../tests/Integration/MULTITENANCY_MT3B_REPORT.md). Baseline 387 test preservati; due suite definitive 426/426, zero Failed/Skipped, 39 nuovi casi MySQL scoped.

## Application e repository istituzionali

Nuove interfacce dedicate IInstitutionStudentManager, IInstitutionTeacherManager e IInstitutionSubjectManager utilizzano esclusivamente i rispettivi repository istituzionali. Vecchi manager/repository e controller conservano i percorsi legacy. I nuovi manager ricavano TenantContext dall'ICurrentTenant scoped MT2 e rivalidano membership, Institution Active e insieme dei ruoli tramite IInstitutionAuthManager a ogni operazione. InstitutionSchoolAuthorization centralizza la policy; nessun ruolo o profilo deriva dai campi legacy User. Assenza del contesto fallisce senza fallback, anche negli host non HTTP.

InstitutionAdmin amministra solo il proprio istituto. Teacher risolve il profilo nella membership corrente e vede studenti delle classi assegnate e materie pertinenti. Student risolve il proprio profilo, non ottiene la directory studenti e vede solo materie delle assegnazioni della propria classe. Directory nomi/ID Teacher ammessa ai ruoli preesistenti entro il tenant; Count resta Admin/Teacher. I ruoli multipli non sono ridotti a un UserRole; la richiesta del proprio profilo richiede sempre ruolo e collegamento pertinenti.

## Query, scritture e integrita

InstitutionSchoolQueries applica EF.Property<Guid>(entity, "InstitutionId") prima della materializzazione. ClassSubject, Class, Teacher, Subject e Student sono coerenti per tenant; visibilita tramite EXISTS SQL, Count lato server, AsNoTracking e numero costante di query. Subject/byTeacher interseca la stessa assegnazione visibile nella classe Student, anche se la materia esiste in piu classi. Nessun filtro globale seguito da filtro in memoria.

I writer valorizzano esplicitamente la shadow property InstitutionId prima del salvataggio. Update/Delete includono owner e ID nel predicato SQL; owner non trasferibile. La creazione puo collegare solo membership esistenti Active con ruolo pertinente nello stesso istituto; non crea User, membership o ruoli. TransactionManager rende atomico il link. Delete Student conserva il divieto in presenza di Grade con lock sul solo parent Student nella transazione, senza modificare i lock Exam/Submission. Le 27 FK composite e i 40 trigger MT3A restano invariati, nessun DDL o nuova configurazione EF duplicata.

## Holding e limiti operativi

La holding `4d543341-0000-4000-8000-000000000001` / `MT3A_LEGACY_TEST` resta provisioning. Il manifest SQL legacy non equivale al manifest auth `LEGACY_MT1`: nessuna attivazione, copia o trasferimento. I test usano due istituti Active temporanei espliciti e scope distinti con teardown; il database persistente torna alla sola holding. Nessuna modifica di produzione.

L'autorizzazione corrente e verificata prima dell'operazione; una revoca non annulla retroattivamente operazioni gia in corso. I repository istituzionali richiedono il contesto verificato dal manager. Exam, Submission, Grade, Audit e worker non sono migrati. Le cinque API MT2 e il rifiuto dei token scoped sulle route scolastiche legacy restano verificati; l'isolamento HTTP positivo richiede MT-3C.

---

## Snapshot storico MT-3A e fasi precedenti

