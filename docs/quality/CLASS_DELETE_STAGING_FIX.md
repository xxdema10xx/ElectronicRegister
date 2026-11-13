# Correzione class-delete staging

Riferimento: [run 41141247734](https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41141247734), commit `d6bb452a1a3d2b3e6cbde1fbfb96524ac652cc34`.

## Causa e limiti delle evidenze

Letti tramite GitHub il job `124355753202` e il relativo log: 53 PASS, 1 FAIL, 0 BLOCKED; provisioning/audit/preflight/TLS superati; `apiGrantsVerified=true`, `apiDdlDenied=true`. Il log applicativo identifica `InvalidOperationException` in `CalendarIntegrity.ValidateIfPresentAsync`, senza pubblicare messaggio o conteggi della query.

Il ramo responsabile nel sorgente del commit e il controllo combinato `count != 6 || triggerCount != 19 || foreignKeyCount != 22`. Il contratto storico contiene sei tabelle, 19 trigger sulle sei tabelle e 22 FK (1+2+2+4+2+11). La DELETE chiamava questo validatore di backfill con er_api. I grant verificati dal probe sono CRUD sullo schema ed EXECUTE sulla sola readiness, senza TRIGGER.

La [documentazione MySQL 8.4](https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/information-schema-triggers-table.html) richiede TRIGGER per vedere i metadati dei trigger. Dunque, con i grant verificati e lo schema attestato, e il confronto sui trigger invisibili a fallire: non un inventario storico errato. Il valore zero e una deduzione dal contratto dei privilegi, non una misurazione pubblicata dalla vecchia run. Il nuovo probe lo misurera esplicitamente nello stesso account API, insieme a 6 tabelle, 22 FK calendario e 8 FK verso class.

## Correzione

- DELETE usa `ClassDeletionIntegrity`: verifica preventiva di SELECT sullo schema, sei tabelle, 22 FK e ownership calendario. Inventario esatto delle dipendenze verso class, blocchi su studenti/assegnazioni/edizione/storico, transazione e audit restano attivi. Tabelle mancanti, FK mancanti/aggiuntive o metadati insufficienti bloccano la cancellazione; resta supportata l'installazione legacy priva dell'intero calendario.
- Attestazione dei trigger resta nel preflight privilegiato e nel validatore di migrazione, che ora distingue privilegi insufficienti, tabelle, trigger, FK e ownership. Nessun privilegio aggiunto a er_api; nessuna eccezione nascosta.
- Aggiunti dieci casi C# con account CRUD: cancellazione vuota e audit, studenti, assegnazioni, edizione/storico, esami/submission/voti, visibilita insufficiente, tabella storica mancante, FK mancante e FK CASCADE sconosciuta, calendario cancellato con periodo/storico conservati. Il provisioning degli account e dei controlli negativi usa la connessione privilegiata del solo fixture disposable.
- Il gate staging esegue i 54 casi HTTP originali invariati, poi 8 richieste separate tramite la vera API. Verifica 204 su classe vuota, 409 su classe protetta, 404 su classe straniera/eliminata; una lettura review verifica i due eventi `class.deleted`, snapshot e assenza delle righe class.

## Verifiche locali

- Restore locked offline con feed/cache del workspace: soluzione, ReleaseTransportProbe, StagingSupport.
- Build Release soluzione e due helper: PASS; dieci warning nullable preesistenti nei test.
- Unitari: 598 PASS; contratti offline MVC/DTO: 2 PASS; Python: 64 PASS.
- Node: release manifest, 14 controlli negativi del manifest e suite MySQL 8.4 PASS. Organizzazione codice: zero violazioni. Actionlint sui due workflow: PASS.
- Aggiornati solo manifest release corrente e inventario del codice; SQL e manifest storici preservati.

## Verifiche ancora necessarie

Docker non e disponibile localmente: i dieci nuovi casi C# sono compilati ma non eseguiti su MySQL. Servono la suite integrazione su istanza disposable autorizzata e la prossima run staging per confermare 54/54 HTTP, gli 8 controlli aggiuntivi, audit persistente e conteggi reali least-privilege. Nessuna nuova run avviata da questo workspace non pubblicato.

Tentato anche audit dipendenze NuGet live: bloccato dal TLS locale verso api.nuget.org; resta da rieseguire nei job CI esistenti. Restore offline completato con NuGetAudit disabilitato, senza attribuirgli una verifica vulnerabilita.

Nessuna modifica a EC2, Nginx, SQL storico o configurazioni Production. Nessun commit o push. File untracked gia presenti all'inizio conservati.
