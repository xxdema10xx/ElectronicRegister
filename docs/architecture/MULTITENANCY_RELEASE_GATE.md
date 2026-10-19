# Aggiornamento del release gate MT-4C

MT-4C completata tecnicamente: due suite 675/675 Passed, zero Failed/Skipped e 644 identita precedenti preservate. **Nessun cutover autorizzato, quattro rollout default false**.
Questa sezione sostituisce soltanto le limitazioni MT4B su lifecycle/audit riportate sotto come
snapshot precedente. [Report MT4C](../../tests/Integration/MULTITENANCY_MT4C_REPORT.md),
[modello lifecycle](ENROLLMENT_LIFECYCLE_AND_AUDIT.md),
[contratti Course](../api/COURSE_FRONTEND_CONTRACTS.md).

Sono implementate le transizioni conservative enrolled -> completed/withdrawn/cancelled e la sola
rettifica motivata CompletionDate terminale. Revision/EventId, lock scoped, audit atomico append-only
e actor/membership verificati non autorizzano il rollout operativo. Le API restano dietro entrambi
SchoolApi e CourseApi e il gate InstitutionAccess; worker ed Esami restano indipendenti.

Prima di qualunque deployment servono migrazione004-006 verificata sull'ambiente autorizzato,
backup/restore certificato, credenziali applicative a privilegio minimo, retention e accessi ai motivi
amministrativi, monitoraggio, gestione conflitti/retry del client e manifest dei dati reali. La prova
locale legge il backup e ne conserva SHA; non certifica restore o sicurezza operativa XAMPP/root.
L'eccezione privilegiata di teardown dei soli tenant sintetici nel database test non e un'API e non
autorizza rimozioni storiche operative. Nessun evento retroattivo viene inventato.

Restano fuori gate: rientri/episodi, rettifiche stato/data iniziale, CourseEdition status workflow,
trasferimenti Class/Campus, unlink storico, amministrazione membership, onboarding e MT5.
Il verde tecnico non equivale a prontezza produzione ne ad autorizzazione all'apertura dei flag.

---

# ElectronicRegister - release gate multi-tenant dopo MT-3E

**Nessun cutover autorizzato. Configurazioni operative non abilitate.** Un risultato verde della suite non autorizza attivazione tenant, migrazione di dati reali o apertura della piattaforma. [Report MT3E](../../tests/Integration/MULTITENANCY_MT3E_REPORT.md).

MT-4B aggiunge `Multitenancy:CourseApi:Enabled`, default false: le sole API del modello formativo
richiedono CourseApi **e** SchoolApi, oltre a JWT/membership/ruolo verificati. Non abilita ExamApi,
InstitutionWorker o API globali. I flussi di stato/rettifica/unlink restano chiusi; l'audit dei nuovi tipi
non e ancora supportato. [Contratti Course](../api/COURSE_FRONTEND_CONTRACTS.md),
[report MT4B](../../tests/Integration/MULTITENANCY_MT4B_REPORT.md). Nessuna apertura operativa.

## Quattro giudizi separati

| Giudizio | Stato e limite |
| --- | --- |
| Checkpoint tecnico MT-3E | Worker istituzionale, audit, rollback/recovery applicativo e verifica dei percorsi amministrativi implementati; prove e contatori nel report |
| Ulteriori integrazioni frontend | Si, per le API istituzionali documentate, in ambiente di test esplicito; nessuna API User/membership globale acquisisce permessi |
| Staging controllato | Candidato tecnico; NON attestato pronto come installazione finche i gate ambientali sotto non sono verificati |
| Produzione | NO: restore certificato, manifest reale, sicurezza di deployment, monitoraggio e governance amministrativa non completati |

## Flag e comportamento

Tutti server-side, assenti/falsi/non validi negano; gli esempi versionati restano false:

```json
{
  "Multitenancy": {
    "SchoolApi": { "Enabled": false },
    "CourseApi": { "Enabled": false },
    "ExamApi": { "Enabled": false },
    "InstitutionWorker": { "Enabled": false }
  }
}
```

SchoolApi abilita soltanto il ramo scolastico MT3C con JWT/membership/owner verificati. ExamApi richiede anche SchoolApi e abilita soltanto le route marcate MT3D. InstitutionWorker abilita soltanto il dispatch interno delle prove gia avviate. Non apre alcun endpoint e non sostituisce l'ownership.

Il flag worker e indipendente dai flag HTTP: chiudere gli accessi interattivi durante un incidente non deve impedire la conservazione/consegna delle risposte gia salvate. La scansione tecnica ammette Institution Active o Suspended, mai Provisioning/holding legacy. Revoca della membership o dei ruoli non cancella la prova e non impedisce l'expiry tecnica; le nuove operazioni interattive restano negate da MT2.

**Non considerare operativo il modulo Esami con i soli flag HTTP.** Prima di un'abilitazione futura occorre osservare il timer del worker reale consegnare A/B, un allarme sul ritardo e sugli errori, recupero dopo riavvio, configurazione DB verificata e approvazione esplicita del rollout. Il client deve continuare a usare deadline UTC e risposte persistite; nessuna promessa di consegna esattamente all'istante della deadline.

## Checklist da approvare nell'ambiente destinazione

| Area | Evidenza obbligatoria | Stato locale MT3E |
| --- | --- | --- |
| Schema | Versione effettiva DB, InnoDB, colonne/trigger/FK e indici confrontati con script | Solo test MariaDB 10.4.32: 27 FK MT3A + 2 dei profili, 40 trigger |
| Backfill e ownership | Manifest firmato/revisionato, zero owner mancanti/incoerenti, confronto dati prima/dopo | Holding tecnica preservata; manifest operativo reale da riconciliare |
| Legacy | Identity:Legacy e allowlist espliciti; disabilitare bridge pre-backfill per membership permanenti; bloccare vecchi endpoint | Gate preservato, nessuna estensione ai JWT scoped; CONTRACT non eseguito |
| Istituti e membership | Identita legale verificata, assegnazioni/ruoli approvati, profili corretti e procedure revoca | Solo A/B temporanei rimossi a fine test; nessuna fondazione reale attivata |
| API | SchoolApi/ExamApi espliciti, JWT negativi/revoca/cross-tenant nell'ambiente | Provati con Program reale in Development/Production locale |
| Worker | InstitutionWorker esplicito, scansione A/B/timer/errori/riavvio/lag osservati, istanze dimensionate | Provato sul DB test; default false operativo |
| Audit | Owner da risorsa, actor umano/null tecnico, RequestId/UTC e atomicita; retention/accessi autorizzati | Workflow testati; nessuna API audit globale |
| SQL strict | Verificare sql_mode di OGNI connessione applicativa/worker/utility e comportamento dei pool | ER_TEST_STRICT_SESSION=1 solo loopback/test; non e una soluzione di configurazione produttiva |
| Backup | Export consistente, accesso ristretto, cifratura/retention e verifica degli artefatti | Tool e prove precedenti; nessun nuovo dump di dati personali pubblicato |
| Restore | Ripristino completo provato in ambiente autorizzato, verifica integrita, RPO/RTO misurati | NON certificato; nessun DROP/restore distruttivo effettuato |
| JWT e segreti | Chiavi reali custodite/ruotate, issuer/audience/lifetime corretti, niente chiavi test o segreti Git | Solo chiavi effimere delle fixture |
| HTTPS/proxy | TLS, forwarded headers fidati, timeout/body limit e configurazione del reverse proxy verificati | NON verificato in deployment reale |
| CORS | Allowlist delle origini reali; rivedere AllowAll preesistente in Program | Blocco di release: default attuale permissivo non e certificazione di sicurezza browser |
| Rate limiting | Limiti login/selection/API e proxy, dimensioni payload e protezione abuso | Limiti violazioni verificati; rate limiting generale non completato |
| Monitoraggio | Errori worker per tipo/ID, lag scadenze, backlog, tentativi/fallimenti, DB/pool/lock e alert | Log applicativi presenti; nessun sistema operativo di alerting collaudato |
| Recovery | Runbook, responsabilita e accessi tecnici ristretti, gestione item incoerenti e commit incerto | Rollback/eccezioni/riavvio applicativo provati; crash hardware/failover non simulati |
| Amministrazione | Governance ruoli, inviti/verifica identita, revoca, ultimo admin, account condivisi e storico | API istituzionali di provisioning/membership/credenziali ancora chiuse |
| Frontend | Integrazione completa con policy risultati, revision, UTC, revoca e gestione errori | Contratto aggiornato; frontend non modificato/testato in questo checkpoint |
| Carico | Batch/ritardi/scope/connessioni misurati su volume rappresentativo, deadlock e piu istanze | Solo fixture piccole; nessuna garanzia produttiva |

## Recovery operativo conservativo

Un errore prima del commit rollbacka SubmittedAt, Grade e audit: lo stato resta InProgress e un giro successivo puo riprovare. Il cursore avanza anche dopo l'errore e riparte dopo una scansione vuota; il cutoff e congelato per sweep per non inseguire indefinitamente nuove scadenze. Nessuna modifica automatica a FailedAuto, nessuna sanzione o perdita di risposte. Il processo riavviato riparte dall'inizio e ritrova gli arretrati. Il lock e l'idempotenza rendono sicuri i retry dopo un commit gia riuscito, anche con piu istanze.

In caso di perdita connessione durante commit, non presumere rollback: dopo ripristino connettivita rileggere lo stato persistito e usare il percorso idempotente. Un problema strutturale di ownership/assegnazione viene registrato e richiede diagnosi; non disabilitare FK/trigger o spostare l'owner per farlo passare. Item incoerenti esclusi dalla scansione richiedono anche controlli di integrita/monitoraggio, non solo log di dispatch.

Per indisponibilita DB lasciare chiuso il traffico operativo, ripristinare il servizio, verificare connettivita e backlog, poi osservare il worker. Non lanciare tooling di preparazione su schema migrato. Per danno ai dati serve una procedura di restore separata, autorizzata e collaudata; una normale eccezione .NET non dimostra recovery da crash hardware.

Le API User e registrazione legacy non devono essere usate come pannello di amministrazione istituzionale. Un InstitutionAdmin non puo eliminare un account globale, modificarne credenziali o assegnarsi ruoli in B. Nessuna membership automatica per email, nessun onboarding pubblico. Le decisioni di prodotto amministrative precedono una futura API dedicata.

Il record MT3A_LEGACY_TEST resta Provisioning: non e una fondazione reale e non va attivato per superare il gate. Il rilascio richiede evidenze dell'ambiente destinazione e autorizzazione specifica; questo documento non la concede.
