# MULTITENANCY ISOLATION MATRIX - stato corrente MT-3B

**Student/Teacher/Subject tenant-aware nel livello Application/Infrastructure. MT-3 complessiva PARZIALE; HTTP scolastico ancora legacy, gate chiuso.** Dettagli e 426/426 test in due run: [report MT-3B](../../tests/Integration/MULTITENANCY_MT3B_REPORT.md).

| Livello / risorsa | Garanzia corrente | Limite |
| --- | --- | --- |
| Autenticazione MT2 | JWT operativo scoped, membership/Institution Active, ruoli della membership | Nessuna apertura automatica delle route scolastiche |
| Manager istituzionali | ICurrentTenant esistente, rivalidazione corrente e permesso per ogni operazione | Revoca non retroattiva su operazioni gia autorizzate |
| Profili | Resolver e query legano Institution, membership, identita e owner diretto | Nessun fallback User.StudentId/TeacherId; assenza nega |
| Student letture | Admin tenant; Teacher solo classi assegnate in SQL; self-profile separato | Student non accede alla directory |
| Teacher letture | Directory locale per ruoli ammessi; lookup ClassSubject scoped; Count Admin/Teacher | Un User globale puo avere profili A/B distinti |
| Subject letture | Admin catalogo locale; Teacher assegnazioni; Student propria classe | byTeacher mantiene intersezione sulla stessa assegnazione, non solo SubjectId |
| CRUD tre famiglie | Admin, owner shadow esplicito in INSERT, predicato owner+ID in UPDATE/DELETE | Nessun trasferimento owner o creazione automatica membership/ruoli |
| Dipendenze | Class/Teacher/Subject/Student/CS owner coerente; FK MT3A; Delete protetto | Nuovo Student Delete richiede transazione e lock parent per conservare Grade |
| SQL MT3A | 19 tabelle owned, 27 FK composite, cataloghi scoped e 40 trigger invariati | SQL non concede permessi di lettura o ruoli |
| Concorrenza | Scope A/B distinti; due Admin; update cross-tenant negato; UNIQUE scoped concorrente | Nessun benchmark di carico dichiarato |
| HTTP MT2 | Cinque API istituzionali e gate legacy verificati | Nessuna API scolastica scoped esposta |
| Legacy / Exam / Grade / Audit / worker | Percorsi precedenti preservati e protetti dal gate | Migrazione selettiva C/D/E ancora necessaria |
| Holding tecnica | MT3A_LEGACY_TEST provisioning, dati legacy invariati | Manifest SQL distinto da LEGACY_MT1 auth; nessuna riconciliazione implicita |

I repository richiedono TenantContext verificato dal manager e non sono un'autenticazione JWT autonoma. I nuovi test usano servizi e repository reali con MySQL electronic_register_test: 39 casi aggiunti ai 387 precedenti, senza mock di integrita SQL. Baseline e insiemi dei test confrontati tramite TRX; bootstrap reale Development/Production incluso. Non si dichiara isolamento HTTP positivo per controller non migrati.

La tabella e la ricognizione storiche seguenti descrivono lo stato precedente: le sole famiglie Student, Teacher e Subject hanno ora i percorsi dedicati sopra. I metodi globali restano utilizzati dai flussi legacy e non vengono esposti dai nuovi manager.

---

## Snapshot storico MT-3A e fasi precedenti

