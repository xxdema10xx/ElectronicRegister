"""Record reviewed Phase 5 evidence from completed TRX and live-attestation artifacts, never invent counters."""
import argparse,importlib.util,json,subprocess
from datetime import datetime,timezone
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SPEC=importlib.util.spec_from_file_location('calendar_verify',ROOT/'tools/verify-institution-calendar.py')
V=importlib.util.module_from_spec(SPEC);SPEC.loader.exec_module(V)

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--bootstrap-run',required=True);p.add_argument('--migration-run',required=True)
    p.add_argument('--initial-snapshot',required=True);p.add_argument('--integration-name',required=True)
    p.add_argument('--previous-bootstrap-run',help='Preserve failed exploratory runs from the earlier disposable bootstrap')
    p.add_argument('--complete',action='store_true',help='Use only after manual review of all 32 acceptance criteria')
    a=p.parse_args();b=(ROOT/a.bootstrap_run).resolve();m=(ROOT/a.migration_run).resolve();initial=(ROOT/a.initial_snapshot).resolve()
    def rel(path):return path.relative_to(ROOT).as_posix()
    original_files=json.loads((initial/'initial-files.json').read_text())
    for index,name in enumerate(original_files):
        if (name.startswith('tests/') and name.endswith(('.cs','.csproj'))) or (name.startswith('tools/test_') and name.endswith('.py')):
            assert (ROOT/name).read_bytes()==(initial/str(index)).read_bytes(),'Preexisting local test source changed: '+name
    original=initial/str(original_files.index('tests/Integration/INSTITUTION_CALENDAR_VERIFICATION.json'))
    prior=json.loads(original.read_text());old_runs={r['name']:r for r in prior['tests']['runs']}
    paths={
        'baselineUnit':ROOT/old_runs['unit']['path'],'baselineIntegration':ROOT/old_runs['integration']['path'],
        'phase4Unit':ROOT/old_runs['baselineUnit']['path'],'phase4Integration':ROOT/old_runs['baselineIntegration']['path'],
        'unit':b/'results/calendar-unit-current.trx','integration':b/('results/'+a.integration_name+'.trx'),
        'migrationHttp':m/'results/calendar-migration-complete-http.trx',
    }
    runs=[];sets={}
    for name,path in paths.items():
        identities=V.trx(path);sets[name]=identities;runs.append(dict(name=name,path=rel(path),sha256=V.sha(path),passed=len(identities)))
    for name in ('unit','integration'):
        assert V.sha(ROOT/old_runs[name]['path'])==old_runs[name]['sha256'],'Prior TRX changed'
    baseline=sets['baselineUnit']|sets['baselineIntegration'];current=sets['unit']|sets['integration']
    assert len(baseline)==prior['tests']['currentDistinct'] and baseline<=current
    assert sets['phase4Unit']|sets['phase4Integration'] <= current,'Phase 4 identities missing'
    assert sets['migrationHttp']<=sets['integration']
    source_manifest=b/(a.integration_name+'-source-manifest.json')
    tested_sources=json.loads(source_manifest.read_text())['files']
    assert all(V.sha(ROOT/name)==digest for name,digest in tested_sources.items() if name.endswith(('.cs','.csproj'))),'C# source changed after final test run started'
    native=json.loads((b/'calendar-final-database-evidence.json').read_text())
    assert native['independentBootstrapEqualsMigration'] and native['bothPostflightViolations']==0
    assert V.sha(m/'calendar-predecessor-backup.sql')==native['migration']['backupSha256']
    assert 'Ran 18 tests' in (b/'calendar-python-tests.log').read_text() and 'OK' in (b/'calendar-python-tests.log').read_text()
    head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT).decode().strip()
    branch=subprocess.check_output(['git','branch','--show-current'],cwd=ROOT).decode().strip()
    assert head=='b1140b12f82cfff08818c4e302efe3139185b26a'
    tracked=subprocess.check_output(['git','diff','--name-only'],cwd=ROOT).decode().splitlines()
    assert not any(x.startswith('tests/') and x.endswith(('.cs','.csproj')) for x in tracked),'Previous test source modified'
    untracked=subprocess.check_output(['git','ls-files','--others','--exclude-standard'],cwd=ROOT).decode().splitlines()
    report_path=ROOT/'tests/Integration/INSTITUTION_CALENDAR_REPORT.md';verification=ROOT/'tests/Integration/INSTITUTION_CALENDAR_VERIFICATION.json'
    changed=sorted(set(tracked+untracked+[rel(report_path),rel(verification)]))
    manifest=ROOT/'migrations/bootstrap/manifest.json';closure=json.loads(manifest.read_text())
    criteria=[
        'Import CSV e XLSX tramite HTTP e SDK reale', 'Preview per riga con errori/conflitti/colonne e impatto',
        'Binding reale Class/CourseEdition/Published Module e ClassSubject', 'Durate flessibili al minuto senza slot',
        'Fuso tenant esplicito, DST e UTC', 'Conflitti docente su tutte le classi del tenant', 'Conflitti classe',
        'Importazioni concorrenti rivalidate sotto lock SQL', 'Aggregato classe/mese e Draft persistenti', 'Pubblicazione esplicita e atomica',
        'Operazioni Admin/Staff sul calendario', 'Modifiche Published auditate', 'Annullamento, riprogrammazione e recuperi senza DELETE',
        'Sostituzioni senza riscrivere ClassSubject', 'Vista Teacher unificata', 'Vista Student dalla sola classe corrente',
        'Vista istituzionale amministrativa filtrata e paginata', 'Nessuna Draft/audit nelle viste personali',
        'PlannedMinutes dal piano Published', 'ScheduledMinutes da Published attive', 'DeliveredMinutes soltanto da conferme esplicite',
        'Totali SQL per classe/modulo, UF e progressione', 'Nessun doppio conteggio fra viste/occupazioni', 'Ownership e viste tenant-aware',
        'Capabilities calendario senza escalation IAM/Exam/Grade', 'EventId actor-bound e ricevute persistenti per ogni mutazione',
        'Audit append-only, revisioni, rollback e retry', 'Migrazione MySQL attestata con backup SHA256',
        'Bootstrap indipendente uguale alla migrazione', 'Identità e sorgenti dei test precedenti preservate',
        'Nuovi test HTTP/MySQL passati, inclusi race e deadlock', 'Documentazione e OpenAPI coerenti con le funzionalità',
    ]
    evidence={
        1:['Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit','Calendar_InvalidImportIsAllOrNothingAndXlsxImportPersists'],
        2:['Calendar_InvalidImportIsAllOrNothingAndXlsxImportPersists','Calendar_ImportCommitRejectsUntrustedOrStalePreview'],
        3:['Calendar_LegacyClassAndModuleFromAnotherPublishedVersionAreRejected','Calendar_ForeignOrInventedReferencesCannotCreateLessons'],
        4:['FlexibleDurations','Calendar_PublishedAmendmentCancellationRecoveryAndDeliveredCorrectionPreserveHistory'],
        5:['DstIsNeverResolvedSilently','Calendar_ExpiredPreviewCannotCommitAndLatePublicationDoesNotCertifyDelivery'],
        6:['Calendar_ConcurrentOverlappingManualWritesHaveOneWinnerAndAdjacentLessonsAreAllowed'],
        7:['Calendar_SqlRejectsSignedPublishedOverlapAndOccupancyTampering'],
        8:['Calendar_CompetingImportConfirmationsRevalidateTeacherBookings','Calendar_ConcurrentImportAndManualBookingCannotDoubleBookTeacher'],
        9:['Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit'],
        10:['Calendar_ConcurrentPublicationAndConcurrentCompletionAreRevisionProtected','Calendar_PublishingOneClassDoesNotExposeOtherDraftsAndBudgetsStaySeparate'],
        11:['Calendar_StaffPublishesAndCombinedStaffTeacherDoesNotGainIamPrivileges'],
        12:['Calendar_PublishedChangesAppearInBothPersonalViewsAndReasonsRemainPrivate'],
        13:['Calendar_PublishedAmendmentCancellationRecoveryAndDeliveredCorrectionPreserveHistory'],
        14:['Calendar_SubstitutionChangesRealTeacherViewWithoutChangingClassSubject'],
        15:['Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit'],
        16:['Calendar_StudentCalendarFollowsAuthorizedTransferWithoutRewritingLessons'],
        17:['Calendar_CombinedOperationalRolesDoNotWidenPersonalStudentScope'],
        18:['Calendar_PersonalRolesCannotUseOperationalCapabilities','Calendar_PublishingOneClassDoesNotExposeOtherDraftsAndBudgetsStaySeparate'],
        19:['Calendar_PublishingOneClassDoesNotExposeOtherDraftsAndBudgetsStaySeparate'],
        20:['Calendar_PublishedAmendmentCancellationRecoveryAndDeliveredCorrectionPreserveHistory'],
        21:['Calendar_ExpiredPreviewCannotCommitAndLatePublicationDoesNotCertifyDelivery'],
        22:['Calendar_EndToEndCsvThreeClassesPublicationPersonalViewsCompletionHoursAndAudit'],
        23:['Calendar_ConcurrentPublicationAndConcurrentCompletionAreRevisionProtected'],
        24:['Calendar_TeacherInTwoInstitutionsCannotLeakLessonsAcrossSelectedContexts','Calendar_SqlRejectsSignedPublishedOverlapAndOccupancyTampering'],
        25:['Calendar_PersonalRolesCannotUseOperationalCapabilities','Calendar_TeacherStudentCombinationRetainsStudentScopeWithoutInventingTeacherProfile'],
        26:['Calendar_SameEventWithDifferentPayloadConflictsAndCompletedReplayDoesNotDoubleHours','Calendar_AfterCommitResponseFailureReplaysDurableReceiptOnAnotherHost'],
        27:['Calendar_FaultInjectionRollsBackRowsPeriodsAndAudit','Calendar_ImportFaultOnSecondLessonRollsBackEntireBatchAndRetainsPreview','Calendar_NativeDeadlockLeavesOneDurableLessonAndSameIntentCanRetry'],
        28:['database migration identity/predecessor/backup/postflight artifacts'],29:['database independent schema comparison artifacts'],
        30:['baseline distinct identity comparison and generated-file initial-byte snapshots'],
        31:['completed final TRX and native SQL assertions'],32:['API controller/strict DTO/rollout and reviewed documentation manifest'],
    }
    # These are reviewed assertion references, not coverage inferred from method names.
    for number,references in evidence.items():
        if number<28:
            assert all(any(reference in identity for identity in current) for reference in references),references
    acceptance=[dict(number=i,criterion=text,satisfied=bool(a.complete),evidence=evidence[i]) for i,text in enumerate(criteria,1)]
    status='IMPLEMENTED_AND_VERIFIED' if a.complete else 'IMPLEMENTED_PENDING_VERIFICATION'
    remaining=[] if a.complete else ['Manual acceptance review not yet declared complete']
    final_integration=len(sets['integration']);new=len(current-baseline)
    body=f'''# Fase 5 — calendario istituzionale

Stato: **{status}**. La continuazione completa i layer SQL, Infrastructure, Application e Api che mancavano alla consegna precedente. La certificazione è basata sui risultati elencati sotto; i run intermedi falliti non vengono sommati ai risultati finali.

## Stato iniziale e preservazione

HEAD iniziale/finale `{head}`, branch `{branch}`. Working tree iniziale NON pulita: conteneva i componenti e i report della prima parte. Snapshot iniziale `{rel(initial)}`; report/JSON precedenti conservati prima delle modifiche. Parser e regole temporali/monte ore sono riusati. Nessuna sorgente di test precedente modificata, rinominata o disabilitata. Gli artefatti storici rigenerati dalla regressione sono ripristinati dai byte iniziali, con gli output nuovi conservati privatamente. Nessun commit, push, reset, clean o checkout distruttivo.

## Modello e architettura effettivi

Archivio canonico institution_lesson; periodo classe/mese; impostazioni tenant; staging privato; ledger append-only; indice SQL delle occupazioni. Domain conserva invarianti e contratti, Business il mapping, Application autorizzazione/coordinamento, Infrastructure unità di lavoro/query/lock, Api binding rigoroso e HTTP. Sidecar EF Database First scalari e indici senza cambiare gli owner shadow legacy. Nessuna migrazione automatica allo startup.

FK/UNIQUE/trigger verificano tenant, Class/CourseEdition/binding Published/versione/modulo/Subject/ClassSubject, docenti e lifecycle. OriginalTeacherId e ownership immutabili. Indice occupazioni Class/Teacher aggiornato dai trigger, mai usato come calendario alternativo o per sommare ore. Lock corrente su questo indice evita il limite MySQL 5.7 di locking read della propria tabella nel trigger.

Transazioni ReadCommitted con mutex Institution e IAM-first; profili/riferimenti e ruoli/generation rivalidati sotto lock. Revisioni optimistic per lezione e periodo, EventId globale con hash attore/tenant/payload, ricevute durevoli e replay prima dei controlli sulla revisione corrente. Ledger BeforeJson/StateJson/ReceiptJson con motivo, actor User/Membership e UTC. Snapshot ore RepeatableRead. Deadlock/timeout => 409 e retry dello stesso intento.

## Funzionalità HTTP e sicurezza

Admin/Staff: configurazione, liste istituzionali/classi/edizioni/docenti, Draft, pubblicazione classe/mese, modifiche Published, annullamento, spostamento anche fra mesi, sostituzioni, recuperi, minuti effettivi/rettifiche, storia e ore. Lezioni Completed protette dalle normali riprogrammazioni. Teacher vede tutte le proprie classi e sostituzioni del solo tenant selezionato, conferma solo la propria Published e legge il modulo autorizzato dalla lezione. Student deriva dal profilo reale e dalla classe corrente, segue i trasferimenti senza cambiare lezioni storiche. Nessuna inferenza dalla sola Enrollment o da User.TeacherId.

Viste personali senza Draft, attori o motivi amministrativi. Capabilities calendario separate da IAM/email/piani Published/Exam/Grade; nessuna escalation Staff o PlatformOperator. JWT legacy/Selection non autorizzano il percorso InstitutionAccess. Ruoli combinati e due Fondazioni sono verificati con JWT reali. Route nuove assenti a rollout chiuso; flag InstitutionCalendar default false e rivalidato al commit. Gli altri rollout non sono accesi dal modulo.

## Importazione e tempo

Upload CSV/XLSX reale, parser/preflight esistenti, mapping automatico/manuale, GUID e riferimenti non ambigui, risoluzione SQL, preview privata con TTL 30 minuti e limiti, conflitti e budget. AppendDraft e ReplaceDraftScope esatto, senza sostituzione Published o altre classi. Conferma actor-bound ImportId/EventId/Revision/Digest con nuova verifica di IAM/riferimenti/prenotazioni e AllOrNothing. Errori per riga, colonne e mapping effettivo, riferimenti originali normalizzati, preview ID stabili e impatto ore.

Minuti liberi, intervalli semichiusi, stesso giorno locale, fuso tenant esplicito e UTC. DST ambiguo/inesistente rifiutato senza offset arbitrari. Date Excel 1900/1904 rigorose, seriale 60 fittizio rifiutato, stili numerici limitati e testo ISO; formule/macro/DTD/link esterni/ZIP ostili restano esclusi. Aula solo testo, nessuna prenotazione fisica garantita. Import Student/Teacher conservato.

Pubblicazione esplicita per classe/mese, nessun timer automatico. ApprovalLeadDays configurabile, deadline locale inclusiva, ritardi e publisher UTC. Correzioni Published atomiche e auditate, ClassSubject non riscritto dalle sostituzioni. Recuperi collegati alle Cancelled con nuova identità.

## Ore

Planned = modulo Published. Scheduled = durata UTC di Published Scheduled/Completed non annullate. Delivered = minuti effettivi esplicitamente confermati di Published Completed. Draft e Cancelled zero. Recupero attivo una sola volta, origine annullata zero. Budget separato Class/Module anche per stessa edizione. Residui/eccedenze/warning, UF, totali classe e progressione mensile SQL; nessuna modifica al piano per nascondere eccedenze, nessun completamento automatico Enrollment e nessuna certificazione basata sul tempo trascorso.

## Esecuzioni effettive

| Prova | Risultato |
|---|---:|
| Unitari, esecuzione nuova | {len(sets['unit'])} Passed |
| Integration, regressione completa finale | {final_integration} Passed |
| HTTP calendario su schema migrato, stessi casi già inclusi nel totale | {len(sets['migrationHttp'])} Passed |
| Identità .NET distinte correnti | {len(current)} |
| Identità della consegna precedente preservate | {len(baseline)} |
| Nuove identità rispetto alla consegna precedente | {new} |
| Python, esecuzione nuova | 18 Passed |

Zero Failed/Skipped nei run certificati. I conteggi provengono dai TRX con ResultSummary Completed e confronto delle identità comprensive dei parametri, non dalla somma dei run. Le {prior['tests']['currentDistinct']} identità precedenti e le identità Fase 4 sono effettivamente presenti. I test dei lettori file e i run ripetuti su istanze differenti non vengono contati nuovamente.

Percorso reale verificato: upload/risoluzione/preview => conferma SQL Draft => pubblicazione esplicita => Teacher su tre classi e Student sulla propria => modifiche/cancellazione/recupero/sostituzione => conferma Teacher e rettifica Staff => report ore e audit. Prove concorrenti: import/import, import/manuale, pubblicazione/pubblicazione, pubblicazione/sostituzione, cancellazione/completamento, conferma duplicata, revoca IAM, flag al commit, deadlock nativo, rollback dopo audit/lezione/pubblicazione e perdita risposta dopo commit con replay su altro host. SQL diretto auditato non supera i conflitti Published; audit e occupazioni non sono cancellabili arbitrariamente.

## MySQL, migrazione e bootstrap

Predecessore congelato 60 tabelle/153 trigger/171 FK/257 indici. Finale: {native['inventory']['tables']}/{native['inventory']['triggers']}/{native['inventory']['fks']}/{native['inventory']['indexes']}. Migrazione finale su porta {native['migration']['identity']['port']}, bootstrap indipendente su {native['bootstrap']['identity']['port']}; nuovi UUID/PID/datadir e binary path attestati, porte 3306/13306 mai usate. Backup predecessore SHA256 `{native['migration']['backupSha256']}`. Definizioni precedenti preservate e schema migrato uguale al bootstrap dopo la sola normalizzazione dei guard di teardown delle fixture per porta/datadir.

Cinque controlli SQL calendario a zero anomalie su entrambe le istanze, oltre ai controlli del predecessore. Schema canonico, contract, migrazione, sidecar, validatori e manifest aggiornati. Manifest: {len(closure['files'])} artefatti, SHA256 `{V.sha(manifest)}`. DDL non transazionale: nessuna promessa di rollback automatico; recovery e stop-on-error documentati. Nessun database produttivo modificato. I processi sacrificabili sono arrestati dopo le verifiche di identità.

## Difetti trovati e corretti

Il primo schema esplorativo (65 tabelle) ha evidenziato MySQL 1442 usando FOR UPDATE sulla lezione nel suo trigger: corretto con indice occupazioni protetto e nuovo schema finale, senza alterare istanze produttive. Una prima asserzione nuova confrontava collezioni della receipt per riferimento: corretta in confronto strutturale; non era una duplicazione applicativa.

La prima regressione completa ha registrato 7 failure: quattro percorsi di cancellazione classe fermati dal nuovo FK non classificato e tre backfill che scambiavano time_zone_id per GUID. Correzione circoscritta all'inventario calendario completo e ai due campi fuso con tipo/collation esatti. Le classi vuote legacy restano cancellabili; storico calendario e FK sconosciute restano protetti. Riesame mirato dei casi falliti e successiva regressione completa presenti nei log. I run falliti restano conservati e sono esclusi dai totali Passed finali.

Il controllo OpenAPI ha trovato una JsonException dell'exporter .NET sul default opzionale di un Guid della preview. PreviewLessonId è ora un valore esplicito sempre prodotto dal server, senza default opzionale; le risposte sono tipizzate e il documento OpenAPI reale viene richiesto e verificato dal test. Nessuna permissività dei converter JSON è stata introdotta per aggirare il difetto.

Un ulteriore run sulla vecchia istanza riutilizzata ha registrato 1 failure della retention anagrafica: il servizio processa al massimo 20 staging scaduti per chiamata e il database accumulava anteprime dei run precedenti. Dopo il run risultavano ancora 19 anteprime scadute non elaborate. La regressione certificata è stata eseguita su un nuovo datadir/bootstrap isolato, senza modificare il servizio o il test preesistente; anche il run fallito resta conservato ed escluso dai totali finali.

## Limiti e ambiente

Niente scheduling automatico, TeacherAvailability, solver, frontend, presenze/firme, voti, notifiche, tirocini operativi o Fase 6. Limiti espliciti: lezione entro lo stesso giorno locale; insieme rigoroso di stili Excel; mapping corretto tramite nuova preview; TTL invalida il commit ma non esegue purge fisico di batch/audit; pubblicazione atomica della singola classe/mese. Nessun limite rigido del budget, soltanto warning/riconoscimento.

Python installato eseguito direttamente, nessun nuovo interprete o pacchetto. Nessuna modifica a NuGet.Config o execution policy. Warning NU1900 per feed advisory NuGet non raggiungibile: non certifica un audit aggiornato delle vulnerabilità, ma non ha impedito compilazione/test. Nessuna prova manuale host attribuita all'esecuzione corrente. Validatore offline verifica hash/contract/TRX, non pretende di eseguire SQL o HTTP.

## Criteri di accettazione

'''
    body+='\n'.join(f"{c['number']}. {'Verificato' if c['satisfied'] else 'Da approvare'} — {c['criterion']}. Evidenze: "+', '.join(c['evidence'])+'.' for c in acceptance)
    body+='\n\n## File modificati\n\n'+'\n'.join('- `'+x+'`' for x in changed)+'\n'
    report_path.write_text(body,encoding='utf-8')
    extra=[original,source_manifest,b/'calendar-final-database-evidence.json',b/'calendar-python-tests.log',b/'calendar-postflight-final.tsv',m/'calendar-postflight-final.tsv',b/'formative-postflight-final.tsv',m/'formative-postflight-final.tsv',m/'calendar-migration-evidence.json',b/'calendar-normalized-contract.json',m/'calendar-normalized-contract.json',b/'calendar-openapi-document.json']
    artifacts=[dict(path=x,sha256=V.sha(ROOT/x)) for x in changed if x!=rel(verification) and (ROOT/x).is_file()]
    artifacts +=[dict(path=rel(x),sha256=V.sha(x)) for x in extra]
    exploratory=[]
    import xml.etree.ElementTree as ET
    exploratory_root=(ROOT/a.previous_bootstrap_run).resolve() if a.previous_bootstrap_run else b
    for path in [exploratory_root/('results/'+name+'.trx') for name in ['calendar-full-regression','calendar-api-metadata-targeted','calendar-openapi-stack','calendar-final-closure']]:
        if path.exists():
            document=ET.parse(path);counters=document.find('t:ResultSummary/t:Counters',V.NS)
            exploratory.append(dict(path=rel(path),sha256=V.sha(path),counters=counters.attrib,excludedFromFinalTotals=True))
            artifacts.append(dict(path=rel(path),sha256=V.sha(path)))
    retention=exploratory_root/'calendar-retention-exploratory-evidence.json'
    if retention.exists():artifacts.append(dict(path=rel(retention),sha256=V.sha(retention)))
    value=dict(format=2,status=status,phaseCompleted=bool(a.complete),verifiedAtUtc=datetime.now(timezone.utc).isoformat(),
        initialGit=dict(head=head,branch=branch,workingTreeClean=False,initialSnapshot=rel(initial),preexistingPaths=original_files),
        git=dict(head=head,commitPerformed=False,pushPerformed=False,productionDataModified=False,previousTestSourcesModified=False),
        implemented=dict(calendarSql=True,calendarEfMappings=True,calendarRepository=True,applicationManager=True,httpEndpoints=True,
            importPrivateStaging=True,atomicImportConfirmation=True,explicitMonthlyPublication=True,personalViews=True,
            persistentAudit=True,persistentIdempotency=True,transactionalConflictDetection=True,deliveredTimeWorkflow=True,hoursSqlReport=True,defaultClosedRollout=True),
        tests=dict(runs=runs,baselineDistinct=len(baseline),currentDistinct=len(current),newDistinct=new,baselineMissing=len(baseline-current),fullBaselinePreserved=baseline<=current,pythonPassed=18,failedRunsExcluded=True,recordedIdentitySets={k:sorted(v) for k,v in sets.items()}),
        database=native,acceptance=acceptance,remaining=remaining,modifiedFiles=changed,artifacts=artifacts,exploratoryRuns=exploratory,
        manifest=dict(path=rel(manifest),sha256=V.sha(manifest),artifactCount=len(closure['files'])),
        limitations=['Same-local-day lessons','Strict subset of numeric Excel styles; ISO text supported','Mapping revision by new preview','TTL invalidates confirmation; no automatic physical purge','Atomic publication per class/month','NU1900 advisory feed unavailable; no fresh dependency vulnerability audit claimed'])
    verification.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(status=status,unit=len(sets['unit']),integration=final_integration,baselineDistinct=len(baseline),currentDistinct=len(current),newDistinct=new)))
if __name__=='__main__':main()
