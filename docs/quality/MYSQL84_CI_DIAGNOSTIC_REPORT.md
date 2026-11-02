# MySQL 8.4 CI - rapporto diagnostico

Stato: **correzioni locali completate; certificazione della release BLOCKED**.
Base del guasto: `cbd3ced95c7d35a4e95ff5dd4b53fb1b893bdcde`, run `41480601854`.
La nuova pipeline non e stata eseguita su GitHub e i percorsi SQL MySQL 8.4.13 non sono stati eseguiti in questa sessione: Docker non e disponibile. Nessun risultato storico certifica la nuova release.

## Causa precisa e prove storiche

Il test automatico `test_original_clean_checkout_failure` crea un clone temporaneo, esegue il checkout detached di `cbd3ced9` con `core.autocrlf=false` e riproduce il `ValueError: Historical bytes not found in Git history` del verificatore originale per `tests/Integration/TestResults/phase5-file-readers.trx`.

Hash SHA-256 dichiarato: `D1040C89DECFE870777D3EE4F24A0139FC99EB61395B6D716A6070F0DB014822`.
Hash del blob Git: `474E2AF92338E06CC8B3618D263E8F225F2A32AF2205768B83CC7946C38F3816`.

La precedente indagine ha documentato nel workspace originario BOM UTF-8, 364 CRLF e 5 LF; il blob conserva il BOM e 369 LF. I contenuti coincidono dopo normalizzazione CRLF verso LF. Le conversioni uniformi LF/CRLF e le varianti BOM non ricostruiscono i terminatori misti originali. Non emerge per questo file una diversa generazione dei risultati. L'attestazione registrava i byte del working tree con `workingTreeClean: false`, senza garantirne la conservazione nei blob Git. Il vecchio verificatore poteva inoltre accettare il file locale senza verificarne la provenienza Git.

| Artefatto | CRLF originari | LF originari | Certificazione storica nel clone |
|---|---:|---:|---|
| phase5-file-readers.trx (Integration) | 364 | 5 | NON_CERTIFIABLE / FAIL |
| phase5-baseline.trx (Unit) | 2488 | 5 | NON_CERTIFIABLE / FAIL |
| phase5-domain.trx (Unit) | 2968 | 5 | NON_CERTIFIABLE / FAIL |
| phase5-unit.trx (Unit) | 3334 | 5 | NON_CERTIFIABLE / FAIL |

Il recupero storico richiede byte esatti nella storia Git; LF/CRLF/BOM sono informazioni diagnostiche. I quattro TRX non sono stati riscritti, riattestati o trasformati in PASS.

## Inventario aggregato

L'indagine precedente sul clone immutato della base aveva censito 1.903 dichiarazioni, 1.709 percorsi, 212 riferimenti Markdown e 2.502 anomalie. Fra le dichiarazioni, 667 (634 percorsi distinti) non avevano un blob esatto ne una variante uniforme LF/CRLF/BOM compatibile. Sono comprese evidenze private, dati superati e possibili fixture negative: questi conteggi non equivalgono al numero di regressioni correnti.

La nuova esecuzione locale, successiva alle modifiche, ha censito **1.915 dichiarazioni e 2.670 anomalie di artefatti**. E lo snapshot finale del workspace, distinto dal clone storico. Il JSON conserva ogni dichiarazione, hash atteso/effettivo, varianti, blob, disponibilita nel checkout, stato e inclusione nella chiusura nuova. Le modifiche non committate restano correttamente segnalate come non recuperabili da un checkout pulito; `tools/runtime_diagnostic.py` e anche non tracciato. Nessun hash alternativo viene accettato automaticamente.

L'audit iniziale di questa sessione ha riprodotto anche un `PermissionError` su un riferimento Markdown a una directory privata. Il problema e stato corretto prima di proseguire l'inventario: ora l'errore viene raccolto come `report-reference-unreadable` e gli altri artefatti vengono comunque analizzati. Il test multi-errore verifica anche questo caso.

## Correzioni finalizzate

- `tools/artifact_diagnostic.py`: raccolta aggregata anche per riferimenti non leggibili, senza interrompere gli altri controlli.
- `tools/ci_diagnostic.py`: eliminazione della TRX precedente prima del comando; rifiuto di risultati vuoti, abortiti, saltati o incoerenti; conservazione dello stdout/stderr parziale in caso di timeout; continuazione dei controlli indipendenti.
- Integrazioni: prerequisito Docker esplicito, discovery dei metodi reali e invocazione separata di ogni metodo con processo e fixture Testcontainers nuovi. Le righe di una stessa Theory restano nella stessa invocazione del metodo. Ogni metodo indipendente successivo riceve un database nuovo anche se il precedente lascia errori DDL. Un filtro vuoto fallisce.
- `tools/runtime_diagnostic.py` e `tools/mysql84_runtime.py`: JSON incrementale delle fasi bootstrap, seed/predecessore, snapshot, rifiuto drift, recupero DDL, accettazione trigger/routine e installazione nuova. Un fallimento conserva traceback, file/riga e blocca le fasi successive che condividono quello stato. Il percorso fresh ha un job e un servizio separati.
- Workflow: log completi delle fasi runtime inclusi nei report e caricati con `always()`; validazione anche della TRX C# nuova; timeout integrazioni portato a 90 minuti per l'isolamento per metodo; `fail-fast: false`; nessun `continue-on-error`.
- `tools/final_ci_diagnostic.py`: verifica identita e contenuto delle dieci suite attese, oltre al conteggio e agli esiti dei job. Report assenti, duplicati, incompleti o sconosciuti falliscono. Le anomalie leggibili di un report incompleto vengono conservate. Il Markdown contiene tutte le colonne richieste e gli errori completi; il JSON mantiene anche i dettagli di provenienza.
- `download-artifact` ora usa il commit immutabile `d3f86a106a0bac45b974a628896c90dbdf5c8093`.
- Rigenerata esclusivamente la chiusura candidata `migrations/mysql84-remediation/manifest.json`; ripulite le sole modifiche generate all'inventario organizzazione.

Le anomalie storiche conservano FAIL e NON_CERTIFIABLE con `required: false`; i gate correnti sono obbligatori. La pipeline calendario esegue test SQL/HTTP reali e nuovi TRX, senza attribuire valore di certificazione ai TRX storici.

## Verifiche della sessione

Gli errori completi, i comandi e le dipendenze sono in `.diagnostics/MYSQL84_CI_DIAGNOSTIC_REPORT.json`. I tentativi precedenti sono conservati separatamente e richiamati dal report JSON; un tentativo fallito non e cancellato dal successivo successo.

| ID | Categoria | File / riga | Test | Errore o risultato | Causa / limite | Priorita | Stato | Dipendenti non eseguiti |
|---|---|---|---|---|---|---|---|---|
| HIST-TRX | Provenienza | quattro TRX, 1 | clone pulito del commit base | ValueError storico riprodotto | terminatori misti non conservati | P1 | FAIL | nessuna regressione nuova dipende da queste prove |
| PY | Regressione | tools/test_ci_diagnostic.py, 1 | unittest discovery | 87 test superati | include 10 test diagnostici | P1 | PASS | nessuno |
| MULTI | Diagnostico | tools/test_ci_diagnostic.py, 1 | clone sintetico e CLI | tre hash errati, due mancanti, assente dalla storia, modifica fuori manifest, FAIL indipendente e BLOCKED riportati insieme; exit non zero | nessun errore nascosto | P1 | PASS | nessuno |
| GATE | Diagnostico | tools/final_ci_diagnostic.py, 1 | suite sintetiche | report mancanti/duplicati/incompleti respinti; errori leggibili conservati | successo accettato solo con tutte le suite complete | P1 | PASS | nessuno |
| RUNTIME-DIAG | Diagnostico | tools/runtime_diagnostic.py, 1 | guasto bootstrap simulato | FAIL e sei BLOCKED, traceback e close verificati | simulazione del diagnostico, non test MySQL | P1 | PASS | nessuno |
| NODE | Statica | tools/mysql84-release.cjs, 1 | sei verificatori Node | superati; predecessore immutabile verificato | SQL generato offline non certifica il runtime | P1 | PASS | nessuno |
| RESTORE | Prerequisito | soluzione backend, 1 | restore locked | superato con config esplicita nel workspace | primo tentativo FAIL per NuGet.Config utente non leggibile, conservato nei JSON | P1 | PASS | nessuno |
| BUILD | Compilazione | soluzione backend, 1 | Release | zero errori; warning esistenti | nessun database richiesto | P1 | PASS | nessuno |
| UNIT | Regressione | tests/Unit/Unit.Tests.csproj, 1 | dotnet test Release | 598 PASS, zero ignorati | non SQL | P1 | PASS | nessuno |
| OFFLINE | Regressione | tests/Integration/StagingCalendarFeatureTests.HttpContracts.cs, 45 | calendario feature e lettori calendario/CSV/XLSX | 59 PASS, zero ignorati nelle esecuzioni disponibili | un tentativo aggregato ha fallito due test per python non nel PATH; retry con ER_TEST_PYTHON esplicito | P1 | PASS | nessuno |
| ORG | Statica | tools/CodeOrganization, 1 | check | 661 file, 658 tipi, zero violazioni | inventario generato ripristinato | P2 | PASS | nessuno |
| LINT | Statica CI | workflow MySQL, 1 | actionlint 1.7.12 | superato | shellcheck e pyflakes disabilitati; sintassi Python verificata dai test/import | P1 | PASS | nessuno |
| WS | Statica | diff, 1 | git diff --check | superato | nessun commit effettuato | P2 | PASS | nessuno |
| OLD-VERIFY | Storico | cinque verify-institution-*.py, 1 | email/import/formative/calendar/staff | fallimenti conservati | prove storiche non certificanti | P1 | FAIL | nessun gate nuovo |
| DOCKER | Prerequisito | tools/ci_diagnostic.py, 1 | docker info | FileNotFoundError WinError 2 | eseguibile Docker assente | P1 | FAIL | tutti i percorsi SQL |
| MYSQL | Integrazione | workflow MySQL, 1 | installazione, upgrade, trigger/routine, recupero DDL e sette domini | non eseguiti | dipendenza DOCKER fallita | P1 | BLOCKED | IAM, onboarding, email, import, piani, calendario, tenant e autorizzazioni SQL/HTTP |
| CLEAN | Provenienza nuova | sorgenti modificati, 1 | audit Git | byte nuovi assenti dalla storia; modulo nuovo non tracciato | nessun commit consentito | P1 | FAIL | certificazione da checkout della nuova revisione |
| CI | Pipeline | workflow MySQL, 1 | run end-to-end GitHub Actions | non eseguita | nessun commit/push o dispatch effettuato | P1 | BLOCKED | certificazione completa della release |

Il controllo aggregato locale termina correttamente con exit code diverso da zero per Docker e la provenienza Git delle modifiche locali; le prove storiche mantengono i loro FAIL non obbligatori. Questi errori non sono convertiti in PASS. In CI gli upload diagnostici sono incondizionati e il gate finale rifiuta qualunque gate obbligatorio FAIL, BLOCKED o SKIPPED senza giustificazione.

## Residui effettivi

1. Eseguire i percorsi MySQL 8.4.13 su container nuovi e la pipeline end-to-end quando Docker/GitHub Actions saranno disponibili; correggere eventuali difetti emersi dai test reali. L'implementazione e i test del diagnostico non sostituiscono queste esecuzioni.
2. La certificazione da checkout pulito della nuova revisione richiede che i sorgenti siano versionati successivamente, fuori da questa sessione per il divieto di commit/push. Gli errori di provenienza restano visibili.
3. Le quattro prove storiche con terminatori misti restano NON_CERTIFIABLE. Non sono recuperabili mediante la correzione del verificatore e non sono un'attivita da risolvere cambiandone gli hash.

Non restano richieste di approvazione per modifiche locali. Il lavoro non viene dichiarato certificato o interamente concluso finche i punti 1 e 2 restano aperti.

## Vincoli

Nessun commit o push. Nessuna modifica a er-quality-1, ai manifest pubblicati, alla EC2 o a database esistenti. I test Git usano repository temporanei sintetici o cloni in sola lettura della base. Nessuna evidenza storica e stata riscritta.
