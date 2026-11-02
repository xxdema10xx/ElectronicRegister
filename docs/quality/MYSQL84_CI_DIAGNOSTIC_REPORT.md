# MySQL 8.4 CI ? report di interruzione

Stato: **INTERROTTO SU RICHIESTA DELL?UTENTE**. La nuova release **non ? certificata**. Base: `cbd3ced95c7d35a4e95ff5dd4b53fb1b893bdcde`; run segnalato: `41480601854`.

## Causa riprodotta

Creato un clone Git separato in `.diagnostics/clean-cbd3ced9`, con checkout detached di `cbd3ced9` e `core.autocrlf=false`. Il verificatore originale fallisce anche in quel checkout. La chiamata mirata sull?artefatto indicato riproduce `ValueError: Historical bytes not found in Git history: tests/Integration/TestResults/phase5-file-readers.trx`; un test automatico esegue questa riproduzione in un clone temporaneo.

Nel lancio completo del clone locale il primo errore riguarda invece `ElectronicRegisterContext.Multitenancy.cs`: il primo file incontrato dipende dalla rappresentazione del checkout e dalla storia visibile al verificatore. Non ? stata acquisita una nuova esecuzione GitHub Actions.

Per `phase5-file-readers.trx`:

- SHA-256 storico atteso: `D1040C89DECFE870777D3EE4F24A0139FC99EB61395B6D716A6070F0DB014822`.
- SHA-256 del blob Git: `474E2AF92338E06CC8B3618D263E8F225F2A32AF2205768B83CC7946C38F3816`.
- Il file locale originario coincide con l?hash atteso: BOM UTF-8, 364 terminatori CRLF e 5 LF.
- Il blob Git mantiene il BOM ma contiene 369 terminatori LF.
- I due contenuti coincidono esattamente dopo sostituzione CRLF?LF. Per questo file non emerge una differenza di generazione dei risultati; sono perse nella storia le posizioni dei terminatori misti.
- Le conversioni uniformi LF/CRLF e le varianti con/senza BOM non recuperano l?hash storico. Non sono stati ricostruiti artificialmente quei byte.

Lo stesso difetto ? verificato nei seguenti quattro TRX. Tutti i file locali coincidono con gli hash attestati e tutti coincidono con i blob Git dopo normalizzazione CRLF?LF:

| File | CRLF locali | LF locali | BOM | Prova storica in checkout pulito |
|---|---:|---:|---|---|
| `tests/Integration/TestResults/phase5-file-readers.trx` | 364 | 5 | UTF-8 | NON CERTIFICABILE |
| `tests/Unit/TestResults/phase5-baseline.trx` | 2488 | 5 | UTF-8 | NON CERTIFICABILE |
| `tests/Unit/TestResults/phase5-domain.trx` | 2968 | 5 | UTF-8 | NON CERTIFICABILE |
| `tests/Unit/TestResults/phase5-unit.trx` | 3334 | 5 | UTF-8 | NON CERTIFICABILE |

L?attestazione storica registra byte del working tree e dichiara `workingTreeClean: false`; non garantisce che quei byte siano stati conservati come blob Git. Il verificatore precedente poteva inoltre restituire subito successo quando il file locale coincideva con l?hash, senza controllarne la provenienza Git. Questi due comportamenti spiegano la differenza tra workspace originale e checkout pulito.

## Inventario aggregato

Prima della correzione del verificatore storico ? stato introdotto ed eseguito un audit aggregato. L?inventario consolidato sul clone immutato del commit base comprende **1.903 dichiarazioni di hash**, relative a **1.709 percorsi distinti**, e **212 riferimenti Markdown**. Rileva **2.502 anomalie**, non 2.502 file distinti; un file pu? avere pi? dichiarazioni e pi? anomalie.

| Categoria | Conteggio |
|---|---:|
| Hash storico senza blob esatto | 729 |
| Indisponibilit? nel checkout pulito | 652 |
| Hash differente | 391 |
| File mancante | 361 |
| Omissione dalla chiusura nuova / inventario ritirati | 292 |
| Riferimento Markdown mancante | 30 |
| Differenza di rappresentazione nel manifest nuovo, che usa hash LF | 30 |
| Riferimento storico fuori repository, non letto | 16 |
| Manifest/report JSON non decodificabile come UTF-8 | 1 |

Tra le dichiarazioni, 667 non hanno n? un blob esatto n? una variante LF/CRLF/BOM compatibile: sono relative a 634 percorsi distinti. Il conteggio include evidenze private non versionate, dati storici superati e possibili fixture intenzionalmente negative: non va interpretato come un uguale numero di regressioni della release corrente. Ogni dichiarazione conserva sorgenti, hash, varianti, stato e anomalie nel JSON.

Gli inventari iniziali meno raffinati riportavano 2.753 anomalie nel workspace e 2.493 nel clone. L?audit ? stato poi corretto per conservare tutti i percorsi di blob identici e ampliato ai riferimenti Markdown; il conteggio consolidato sopra usa il clone originale immutato.

## Modifiche implementate, ancora da finalizzare

- `tools/artifact_diagnostic.py`: audit aggregato di manifest/report JSON e riferimenti Markdown; esistenza, hash, varianti LF/CRLF/BOM, storia Git, tracciamento, chiusura nuova e stato storico/attivo/ritirato. Lettura batch dei blob, inclusi blob identici sotto percorsi diversi. Le anomalie sono raccolte senza fermarsi al primo artefatto.
- `tools/ci_diagnostic.py`: esecuzione di controlli indipendenti, dipendenze esplicite, stati FAIL/BLOCKED, errori completi dei processi, salvataggio incrementale JSON e controllo delle nuove TRX. Le TRX vuote, saltate, abortite o con contatori incoerenti non possono produrre PASS.
- `tools/final_ci_diagnostic.py`: aggregazione dei report, controllo degli esiti dei job e del numero di report obbligatori; errore se mancano risultati o se un gate obbligatorio non passa.
- `tools/historical_artifacts.py`: eliminato il successo anticipato basato soltanto sul file locale; ricerca estesa alle ref e alla storia completa; recupero storico richiesto su byte esatti. Le differenze di rappresentazione rimangono diagnostiche.
- `tools/mysql84_runtime.py`: aggiunta modalit? `--fresh-only` e riuso della procedura di installazione pulita, per renderla eseguibile su un servizio distinto dall?upgrade.
- `.github/workflows/mysql84-remediation-candidate.yml`: predisposti job indipendenti per diagnostica/statica, installazione/upgrade con matrice `fail-fast: false`, sette domini di integrazione e gate finale; caricamento diagnostica con `always()`. Nessun `continue-on-error` introdotto.
- I sette domini predisposti sono calendario, piani formativi, email, importazioni, IAM, onboarding e tenant. Usano i test esistenti e container nuovi per dominio; calendario include test reali e non usa vecchie TRX come certificazione.
- `tests/Integration/IntegrationFixture.cs`: in CI l?assenza di Docker diventa un errore esplicito.
- `tools/test_ci_diagnostic.py`: quattro test per riproduzione in clone pulito, scenario simultaneo multi-errore con exit non zero, report obbligatori assenti e TRX abortita nonostante exit zero.
- Rigenerata soltanto la chiusura della nuova release `migrations/mysql84-remediation/manifest.json`; i manifest storici non sono stati riscritti. Aggiunta `.diagnostics/` al gitignore.

Le anomalie storiche conservano FAIL/NON_CERTIFIABLE e sono separate dai gate della nuova release tramite il campo `required`. Non sono convertite in PASS. La politica di separazione e la completezza della copertura devono ancora ricevere la revisione finale.

## Verifiche realmente eseguite

| ID | Categoria | File / riga | Test | Esito / errore | Causa / limite | Priorit? | Stato | Dipendenti non eseguiti |
|---|---|---|---|---|---|---|---|---|
| HIST-TRX | Provenienza | quattro TRX sopra, riga 1 | clone pulito + audit | Historical bytes not found in Git history | terminatori misti non conservati nei blob | P1 | FAIL | nessuna nuova regressione usa queste TRX |
| PY | Regressione | `tools/test_ci_diagnostic.py:1` e suite tools | unittest discovery | 81 test superati nell?ultima esecuzione completa | include 4 test diagnostici | P1 | PASS | ? |
| UNIT | Regressione | `tests/Unit/Unit.Tests.csproj:1` | dotnet test Release | 598 superati, 0 ignorati | eseguiti senza database | P1 | PASS | ? |
| OFFLINE-IT | Integrazione | `tests/Integration/Integration.Tests.csproj:1` | StagingCalendarFeature + lettori calendario/CSV/XLSX | 59 superati, 0 ignorati | non certificano i percorsi SQL/HTTP MySQL | P1 | PASS | ? |
| BUILD | Compilazione | soluzione backend, riga 1 | restore locked + build Release | 0 errori; presenti warning | output completo nei JSON locali | P1 | PASS | ? |
| NODE | Statica | `tools/mysql84-release.cjs:1` e altri 5 controlli | release, manifest, MySQL e remediation | tutti passati nelle esecuzioni aggregate | ultima chiusura verificata anche separatamente dopo aggiornamenti | P1 | PASS | ? |
| ORG | Statica | `tools/CodeOrganization:1` | check organizzazione | superato | ha rigenerato un inventario con tre differenze CRLF da ripulire | P2 | PASS | ? |
| WS | Statica | diff repository | git diff --check | superato nelle esecuzioni aggregate | non rilanciato dopo la creazione di questo report | P2 | PASS | ? |
| OLD-VERIFY | Storico | cinque `verify-institution-*.py`, riga 1 | email/import/formative/calendar/staff | fallimenti preservati nel JSON | verifica di vecchie evidenze, non certificazione della nuova release | P1 | FAIL | nessun gate nuovo dipende dai loro PASS |
| DOCKER | Prerequisito | ambiente locale | docker info | permission denied sul named pipe docker_engine; accesso negato al config Docker | Docker locale non accessibile | P1 | FAIL | tutti i percorsi MySQL |
| MYSQL | Integrazione | workflow MySQL, riga 1 | clean install, upgrade, trigger/routine, IAM/onboarding, email, import, formative, calendario, tenant, recupero DDL | non eseguiti localmente | dipendenza DOCKER non soddisfatta | P1 | BLOCKED | tutti i relativi test SQL/HTTP |
| ACTIONLINT | Statica CI | workflow, riga 1 | actionlint 1.7.7 | binario assente; download fallito: impossibile connettersi al server remoto | ambiente locale privo dello strumento e rete inaccessibile | P1 | BLOCKED | validazione completa del workflow |
| CI | Pipeline | workflow, riga 1 | nuova esecuzione GitHub Actions | non avviata | nessun commit o push autorizzato/eseguito | P1 | BLOCKED | verifica reale end-to-end della nuova pipeline |

I dettagli completi, inclusi stdout/stderr dei controlli registrati, si trovano nel report JSON locale `.diagnostics/MYSQL84_CI_DIAGNOSTIC_REPORT.json`. La configurazione CI prepara un artefatto GitHub Actions `mysql84-ci-diagnostic-report` con JSON e Markdown: non ? stato ancora prodotto da una run remota.

## Da fare prima di considerare il lavoro concluso

1. Revisionare il codice e il workflow nell?insieme, inclusi il conteggio dei report obbligatori, la separazione tra provenienza storica e gate nuovi, la completezza dei filtri e la diagnostica delle singole fasi runtime.
2. Validare il workflow con actionlint; la validazione locale non ? stata possibile. Valutare anche il pin immutabile della nuova azione `download-artifact@v4`, attualmente referenziata per versione.
3. Eseguire davvero i percorsi MySQL 8.4.13 su servizi/container nuovi: installazione, upgrade, trigger/routine, IAM/onboarding, email, importazioni, piani formativi, calendario, isolamento/autorizzazioni e recupero DDL. Risolvere gli errori che emergeranno mantenendo tutti gli esiti e i BLOCKED.
4. Verificare che l?isolamento per dominio sia sufficiente anche in caso di test che lascino il database compromesso; introdurre ulteriore separazione dove necessario.
5. Completare la diagnostica delle sottofasi runtime e verificare che ogni problema riporti errore completo, dipendenze e causa sufficientemente specifica. Attualmente alcuni esiti di step runtime rimandano ancora ai log Actions.
6. Ripulire le sole modifiche generate dal controllo organizzazione in `docs/refactoring/CODE_ORGANIZATION_INVENTORY.json`: tre stringhe `visibility` sono passate da `\n` a `\r\n`. Non sono modifiche funzionali intenzionali; non sono state ripristinate dopo l?ordine di interruzione.
7. Ricontrollare chiusura nuova, diff e test necessari dopo eventuali correzioni finali. Alcune ultime modifiche hanno avuto test mirati; non ? stata svolta una revisione finale completa dello stato corrente.
8. I nuovi file e i byte modificati non sono ancora nella storia Git. L?audit del workspace segnala correttamente tale indisponibilit? nel checkout pulito: non eliminarne artificialmente gli errori. L?eventuale versionamento e la run remota restano attivit? successive, fuori da questa sessione interrotta.

## Vincoli rispettati

Nessun commit o push nel repository di lavoro. Nessuna modifica alla release storica `er-quality-1` o ai manifest storici. Nessun accesso alla EC2 o a database esistenti. I test del diagnostico creano esclusivamente storia Git sintetica in repository temporanei. Nessuna vecchia TRX ? stata riscritta o promossa artificialmente a certificazione della nuova release.
