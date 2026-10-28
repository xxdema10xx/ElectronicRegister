# Calendario istituzionale ITS

Il modulo dispone di schema MySQL, repository, manager, controller, import persistente e viste autorizzate. Lo stato della certificazione e le esecuzioni effettive sono nel [report](../../tests/Integration/INSTITUTION_CALENDAR_REPORT.md): la presenza del codice non equivale a una verifica conclusa.

## Archivio e aggregati

`institution_lesson` è l'unico archivio canonico. Admin, Staff, Teacher e Student leggono gli stessi ID, senza calendari copiati. `institution_calendar_period` identifica Class/mese locale, UNIQUE tenant/classe/primo giorno del mese. Pubblicare una classe non approva bozze delle altre. Un import può contenere fino a 100 classi e più edizioni del tenant, ma sempre un mese dichiarato; conferma AllOrNothing.

Tabelle additive: institution_calendar_settings, institution_calendar_period, institution_calendar_import, institution_lesson, institution_calendar_event e institution_calendar_occupancy. Quest'ultima è un indice di integrità, non un secondo calendario: contiene due occupazioni Class/Teacher per lezione Published non annullata e non alimenta le viste o il monte ore. MySQL 5.7 rifiuta una locking read della tabella lezione dal suo stesso trigger (errore 1442). Il trigger della lezione legge quindi l'indice sotto lock corrente; un AFTER UPDATE lo aggiorna atomicamente. Trigger e ledger impediscono di cancellare o modificare arbitrariamente l'indice. Completed mantiene l'occupazione storica; Cancelled la libera.

Le FK sono composite tenant-aware. La lezione identifica Class, CourseEdition, versione Published, FormativeModule, Subject, ClassSubject e docenti del tenant. I trigger verificano il binding effettivo dell'edizione, il Subject del modulo e la compatibilità ClassSubject. Due nuovi guard su Class/ClassSubject impediscono cambiamenti strutturali che invalidino lezioni esistenti. Le 60 tabelle e i 153 trigger predecessori restano nelle loro definizioni originali; nessun CHECK ignorato è usato come protezione.

Una classe legacy senza CourseEdition/binding resta valida nelle API precedenti ma riceve `calendar.teaching-configuration-required` nel nuovo workflow. Nessun binding, backfill, ClassSubject, enrollment, Exam o Grade viene creato dal calendario. I sidecar EF Database First mappano gli scalari e gli indici; SQL mantiene le FK senza alterare gli owner shadow legacy.

## Tempo

Data locale e TimeOnly di inizio/fine, precisione al minuto e intervalli semichiusi `[start,end)`. Nessuno slot, arrotondamento o numero prestabilito di ore. Fine strettamente successiva all'inizio nello stesso giorno locale; lezioni notturne singole non supportate. TimeZoneId viene configurato esplicitamente per Fondazione; Europe/Rome non è un default universale. Cambiarlo dopo la creazione di lezioni è vietato per evitare reinterpretazioni storiche.

CalendarInterval usa TimeZoneInfo, rifiuta estremi e intervalli attraversanti minuti locali ambigui o inesistenti, senza scegliere offset arbitrari. StartUtc/EndUtc sono persistiti e ordinati come UTC; durata derivata in minuti interi. Il provider del sistema deve conoscere il fuso configurato. Il database controlla valori, precisione e coerenza del ledger; le conversioni IANA/DST sono responsabilità del provider .NET, non delle tabelle timezone MySQL. Aula è testo validato: nessuna prenotazione fisica garantita.

## Stati, pubblicazione e modifiche

Periodo Draft/Published e lezione Draft/Published sono separati: un periodo già Published può avere nuove lezioni Draft in attesa di approvazione. Pubblicazione esplicita di tutte le Draft Scheduled della sola classe/mese, con ExpectedRevision del periodo, nuova validazione dei riferimenti e dei conflitti, publisher e UTC nella stessa transazione. Nessuna pubblicazione automatica al cambio mese.

ApprovalLeadDays è configurabile 0-366. Deadline locale inclusiva = primo giorno del mese meno LeadDays. Il dettaglio espone deadline, overdue, pubblicazione tardiva, publisher e timestamp. Anche pubblicazioni tardive richiedono motivo e audit; non ci sono blocchi irreversibili sulla data.

Creazione manuale produce Draft. PUT modifica una Scheduled Draft o Published; Published mantiene la propria visibilità e cambia atomicamente al commit. Eventi e revisione conservano prima/dopo, motivo, attori e UTC. Riprogrammare può cambiare mese, spostando la lezione al periodo destinazione e approvando esplicitamente la modifica pubblicata. Modulo modificabile soltanto nella stessa versione/edizione/classe. Ownership e docente originario restano immutabili.

Sostituzione occasionale conserva OriginalTeacherId, cambia TeacherId programmato, registra motivo e attore e aggiorna le viste; non cambia ClassSubject.TeacherId. Docente del tenant e assenza conflitti sono rivalidati. Il sostituto può leggere il proprio modulo tramite la lezione, anche senza assegnazione ordinaria ClassSubject.

Cancel cambia stato e conserva record/storia. Un recupero è una nuova Draft con RecoveryOfLessonId di una Cancelled dello stesso contesto didattico; si pubblica esplicitamente. Completed non è annullabile, riprogrammabile o sostituibile dal normale workflow. Correct-delivered-time è separato, amministrativo e motivato. Nessun DELETE di lezioni o audit.

## Import CSV/XLSX

Manager, mapping Business e lettore sicuro esistenti sono riusati. CSV UTF-8/BOM, `;` o `,`, colonne riordinate, alias italiani/inglesi, quoting e righe fisiche. Date testo yyyy-MM-dd e ore HH:mm. XLSX via Open XML SDK 3.5.1: scelta foglio, celle vuote e testo, seriali rigorosi con sistema 1900/1904. Numeri data accettati soltanto con formati built-in 14-17, orari 20-21; seriale fittizio 60 del 1900 rifiutato. Formati custom/combinati/AM-PM o secondi effettivi richiedono testo ISO. Il lettore anagrafico mantiene il rifiuto delle celle numeriche.

Preflight ZIP/XML originale: limiti decompressi, nodi, profondità, DTD disabilitati, niente formule nemmeno cached, macro, link esterni o binari. Nessuna esecuzione. SHA256 dei byte originali. Limiti tecnici attuali: 10 MiB file, 2000 righe, 16 colonne, 512 caratteri/cella, JSON staging 2 MiB. Parser, limiti e semantica Student/Teacher non sono duplicati o modificati.

Class: GUID o nome univoco nel tenant. Module: GUID oppure codice/nome non ambiguo nella versione collegata alla classe; nomi/codici ripetuti richiedono GUID. Teacher: GUID oppure reference stabile del registro Teacher della Fase 3; nome/cognome non sono una chiave. Non si creano assegnazioni per far passare una riga.

Upload/settings consentono mapping esplicito zero-based. Un mapping corretto o dati corretti si presentano con una nuova preview e nuovo EventId: non esiste una PUT del mapping sullo stesso batch. Preview privata tenant/User/Membership, scadenza 30 minuti e massimo 20 preview pendenti per attore. Nessuna lezione o periodo definitivo nella preview; staging SQL di righe e risultato. I batch/eventi sono conservati per audit: TTL invalida la conferma, non è un job di cancellazione fisica del contenuto.

Preview include righe/validi/invalidi/warning/duplicati, riferimenti risolti, durata, conflitti e impatto per modulo/classe. PreviewLessonId identifica stabilmente una riga privata e permette di distinguere conflitti fra righe da ID di lezioni esistenti consultabili dall'operatore. AppendDraft aggiunge. ReplaceDraftScope annulla soltanto Draft Scheduled delle classi/mese dichiarate, mostrando aggiunte/sostituite/conservate. Published e altre classi non vengono sostituite.

Confirm richiede ImportId, EventId, ExpectedRevision e Digest. L'attore viene risolto dal contesto. Rilegge lo staging autorevole, rivalida TTL, IAM, fuso/impostazioni, revisioni dei periodi, ClassSubject, edizione/versione, docenti e prenotazioni globali del tenant; ricostruisce fingerprint/digest e rifiuta preview stale. Warning e discrepanze budget richiedono AcknowledgeWarnings. Qualsiasi errore annulla l'intera transazione, incluso l'annullamento delle vecchie Draft.

## Ore

| Quantità | Fonte esatta |
|---|---|
| PlannedMinutes | Budget del modulo nella versione Published |
| ScheduledMinutes | Durata UTC delle Published Scheduled e Published Completed |
| DeliveredMinutes | Minuti effettivi espliciti delle Published Completed |
| Draft | Zero programmato pubblicato, zero svolto |
| Cancelled | Zero programmato attivo, zero svolto |
| Recupero | Nuova lezione attiva una volta; origine annullata zero |

Complete richiede azione autorizzata e minuti 1-1440, anche se diversi dalla durata programmata. Date passate non certificano nulla. Teacher conferma solo la propria assegnazione reale; Admin/Staff possono registrare e rettificare tramite workflow distinti. Attore e conferma UTC sono persistiti e rettifiche conservano gli eventi precedenti.

Report SQL per Institution/CourseEdition/Class/Module: somme su Lesson senza join all'indice occupazioni e senza duplicazioni delle viste. Restituisce residui, eccedenze, warning, totali UF/classe/percorso nel contesto della classe e progressione mensile. Budget di classi diverse non vengono fusi. Lettura coerente RepeatableRead; nessuna modifica al budget Published, blocco rigido per eccedenza o deduzione del completamento Enrollment.

## Sicurezza e concorrenza

CalendarPermissions è separato da InstitutionPermissions. Admin/Staff hanno gestione operativa/audit; Teacher lettura propria e conferma propria; Student lettura classe corrente. Nessun privilegio IAM/email/piani Published/Exam/Grade viene aggiunto. Ruoli combinati mantengono i filtri personali, mentre le route operative richiedono capability Manage. PlatformOperator e token legacy/Selection non conferiscono accesso didattico.

TeacherId viene dal profilo della membership corrente, mai da User.TeacherId o da un parametro di autorizzazione. La vista comprende tutte le classi e sostituzioni del solo tenant selezionato. Student viene dal profilo reale e Student.ClassId, non dalla sola Enrollment; un trasferimento autorizzato cambia la vista corrente senza riscrivere lezioni. Draft, motivi e audit non escono dalle viste personali. DTO pubblici includono nomi didattici ma non attori o motivazioni riservate.

Ogni scrittura usa transazione ReadCommitted, mutex persistente Institution, poi lock IAM secondo il protocollo esistente. Membership, ruoli esatti, SecurityGeneration e flag sono verificati prima e sotto lock e prima del commit. Riferimenti didattici e profilo del confermatore sono bloccati durante la scrittura. Conflitti classe/docente sono rivalidati su tutte le prenotazioni attive del tenant, comprese Draft; questo è volutamente più conservativo della sola esclusione Published.

EventId globale, lookup tenant-scoped, hash SHA256 dell'intento con tenant/User/Membership, receipt SQL durevole. Replay restituisce la stessa ricevuta prima di controllare revisioni ormai avanzate. Payload/attore diversi con stesso EventId producono 409. Revisioni per risorsa; eventi prima della mutazione nella stessa transazione. UNIQUE risorsa/revisione e trigger collegano ogni mutazione al suo evento. Ledger append-only con BeforeJson/StateJson/ReceiptJson, actor, motivo e UTC.

L'indice occupazioni impedisce sovrapposizioni Published anche a SQL auditato sotto isolamento RepeatableRead; niente lock statici. Deadlock/lock timeout producono 409: il client può ripetere lo stesso intento con lo stesso EventId. Fault observer no-op in produzione permette prove dopo IAM/audit/lezione/pubblicazione, prima del commit e dopo commit prima della risposta. Un guasto dopo commit non annulla il risultato: il retry recupera la ricevuta.

## Rollout, migrazione e recovery

Multitenancy:InstitutionCalendar:Enabled default false; SchoolApi deve essere abilitata. Il controller è escluso all'avvio con flag chiuso e manager/repository rivalidano il flag dinamicamente, anche al commit. Le altre API/flag non vengono accese dal calendario. Le lezioni possono usare binding Published già esistenti senza aprire le route di gestione FormativePlans. Nessuna migrazione allo startup.

Migrazione `migrations/institution-calendar/001_expand.sql`, validazione read-only 002_validate.sql e predecessore congelato. Bootstrap canonico incorpora esattamente lo script; schema contract e manifest includono il nuovo modello. Il runner opera solo su nuovi datadir workspace e porte nuove diverse da 3306/13306, attestando versione/PID/binary path/UUID/datadir, assenza client, predecessore completo e backup SHA256 prima del DDL. Verifica definizioni precedenti e confronto con bootstrap indipendente.

DDL non transazionale: su errore fermarsi, conservare datadir/log/backup, inventariare lo stato parziale. Nessun replay automatico o DROP/reset. Recovery soltanto in un'altra istanza sacrificabile, ripristinando il backup e verificando il contratto prima di pianificare un intervento. I runner non sono strumenti di deployment produttivo.

## Frontend e perimetro

React Native deve mantenere EventId per intento, revisioni Int64 senza perdita JS, digest e scope preview; trattare 403/404/409 come decisioni server. Mostrare Draft/Published, deadline, conflitti e tre contatori distinti, senza dichiarare svolta una lezione soltanto programmata. Rileggere dopo mutazione/replay; la ricevuta è storica. Docente: nessuna selezione classe obbligatoria. Studente: sola classe corrente. Audit e motivi solo nelle schermate operative.

Esclusi: generazione automatica, TeacherAvailability, solver, allocazione aule, presenze/firme, voti, notifiche, frontend, stage operativi e integrazioni esterne. Limiti espliciti: stesso giorno locale, stili Excel limitati, modifica mapping tramite nuova preview, nessun purge automatico dello staging/audit e pubblicazione atomica per singola classe/mese (più classi richiedono comandi separati).
