# Exam Name and Randomization — verifica del 22 ottobre 2026

Branch: `refactor-database-entities`. Nessun commit o push effettuato.

## Comportamento prima e dopo

Prima, Create/Update accettavano `Name` dal client, Start esponeva domande e opzioni
nell'ordine canonico e Session costruiva separatamente il DTO dello studente.

Ora il nome restituito deriva sempre da `Exam.ClassSubject.Subject.Name`. Create e
Update verificano prima classe/materia e autorizzazioni; il mapper riceve un nome
risolto dal database. Il repository ripete la derivazione con una lettura corrente
`FOR UPDATE` della materia, serializzandola con le rinomine anche con REPEATABLE READ.
Le rinomine aggiornano materia ed esami dello stesso proprietario nella stessa
transazione. Description resta modificabile. Anche i recuperi usano lo stesso flusso.
ExamDetailDto, ExamSummaryDto, AssignedExamDto ed ExamForStudentDto conservano Name.

CreateExamDto/UpdateExamDto mantengono soltanto un membro CLR Name opzionale ignorato
con JsonIgnore: i vecchi helper compilano, ma il JSON del client non può impostarlo
e il contratto HTTP di input non lo espone. Il test HTTP usa deliberatamente un
payload anonimo contenente un nome arbitrario, oltrepassando la serializzazione del
DTO client, e verifica il valore realmente persistito.

I frontend presenti non implementano schermate o contratti per gli esami: la ricerca
nei sorgenti non ha individuato contratti di creazione/modifica da aggiornare. Per
un futuro client: usare gli ID delle opzioni per le risposte e Position come rango
visualizzato; non inviare Name e non ricostruire l'ordine dal template amministrativo.

## Persistenza e costruzione della prova

`exam_submission.display_order_json LONGTEXT NULL` contiene gli ID delle domande
nell'ordine estratto e un dizionario question-ID -> array ordinato di option-ID.
Fisher–Yates usa RandomNumberGenerator.GetInt32, con estrazioni indipendenti per
submission e per domanda Single/Multiple. Open non richiede estrazioni per opzioni.
Il generatore è controllabile nei test unitari senza sostituire il generatore sicuro
del percorso HTTP. Nessun test presume che due estrazioni casuali debbano differire.

Start genera il documento prima dell'INSERT, nella transazione esistente. Il lock
sull'esame e il vincolo univoco (exam_id, student_id) mantengono il comportamento
precedente: uno Start concorrente vince, gli altri ricevono Conflict. Documento e
submission vengono salvati atomicamente. L'ordine appartiene alla submission e
quindi eredita tenant/owner e controlli di accesso già applicati dal repository.

`StudentExamPresentation.Build` è l'unica implementazione di ordinamento usata da
ExamStudentMapper/Start e InstitutionExamRepositoryFactory.SessionAsync. GET, login,
nuovo host e nuovo DbContext leggono il documento senza rigenerarlo. Per le nuove
prove Position è il rango visualizzato a partire da 1. Gli ID restano invariati;
IsCorrect non è presente nei DTO studente. Nessuna posizione canonica viene mutata.
Ordini corrotti o con ID non appartenenti al template causano errore, senza rigenerazione.

Grading, finalizzazione, recuperi, regole di assegnazione, scadenza e worker non sono
stati riscritti. Le risposte continuano a usare gli ID persistenti, validati contro
la struttura dell'esame, indipendentemente dalla posizione visualizzata.

## SQL e dati preesistenti

- `migrations/exam-presentation/001_expand.sql`: versione 20261022.1, una sola ADD
  COLUMN nullable LONGTEXT FIRST, compatibile con MySQL 5.7.24; nessun UPDATE storico.
- `002_validate.sql`: forma/nullabilità della colonna, JSON_VALID, inventario dei nomi
  disallineati con tenant, ID e numero di tentativi.
- `tools/Invoke-ExamPresentationMigration.ps1`: guard su server 5.7.24, porta 13306,
  database e suffisso datadir isolati; assenza colonna, connessioni e transazioni;
  esportazione privata dei nomi, backup logico completo con SHA256, applicazione unica
  e postflight. Rifiuta replay; un errore SQL interrompe il flusso senza ripristino.
- Bootstrap consolidato, schema contract, manifest SHA256 e validatore del setup
  aggiornati. L'inventario resta 40 tabelle, 88 trigger, 118 FK e 164 indici.

NULL significa esplicitamente tentativo storico: Session conserva ordine e Position
canonici precedenti. Non esiste backfill casuale delle prove avviate.

La migrazione NON riscrive i vecchi nomi. Il README della migrazione descrive la
riconciliazione: backup completo e inventario privati, analisi delle differenze e dei
tentativi già conclusi, approvazione dell'operatore, aggiornamenti distinti per owner
ed ID con confronto del vecchio valore, conteggi e validazione finale. L'export dei
nomi originali deve essere conservato. Nel frattempo i DTO leggono il nome corrente
della materia. Non è stato eseguito alcun UPDATE massivo dei nomi storici.

## Verifiche eseguite

Build della soluzione: riuscita, zero errori; rimangono 11 warning preesistenti.
Test mirati: 7 unitari e 6 integrazioni reali su MySQL, tutti superati.
Due regressioni complete riuscite: ciascuna 76 unitari + 903 integrazioni = 979 test,
zero failure e zero skip. La seconda usa il codice finale, inclusa la lettura corrente
per il nome. Le 966 identità precedenti restano presenti: 69 unitari e 897 integrazioni,
più 13 nuove identità. Confrontate con HEAD le firme e gli attributi dei test in 64
file sorgente esistenti: nessuna identità modificata. Sono cambiate soltanto due
aspettative incompatibili con il nuovo nome automatico; le assertion di sicurezza
sono rimaste invariate.

| Requisito | Evidenza |
| --- | --- |
| 1–4: derivazione, nome ostile, update, rinomina | ExamPresentation_NameComesFromSubjectOnCreateUpdateAndRename; InputNames_AreIgnoredAndOptional; aspettative Phase4 aggiornate |
| 5 e 20: recuperi e tentativi separati | ExamPresentation_TwoStudentsAndRetakeHaveSeparatePersistedOrders; regressione recuperi esistente |
| 6–7: domande, Single e Multiple | ControlledShuffle_PersistsEveryId_WithoutChangingCanonicalPositions; teoria HTTP Single/Multiple |
| 8: posizioni canoniche immutate | snapshot unitario dell'intero modello e confronto del template amministrativo prima/dopo Start |
| 9–10: Start/Session e ricreazione host/DbContext | ExamPresentation_StartSessionHostRestartAndCanonicalTemplateAgree |
| 11: Start concorrenti | quattro richieste HTTP, un successo, tre Conflict, una sola submission con ordine persistito |
| 12: studenti indipendenti | generatore controllato con estrazioni distinte; due studenti HTTP con documenti separati, senza assertion probabilistica di diversità |
| 13–14: ID e correzione automatica | invio tramite gli ID corretti del template dopo shuffle Single/Multiple, esito Passed; regressione grading invariata |
| 15: Open e correzione manuale | OpenReviewFinalizeOverrideAndRetakeKeepOfficialPassedAndOriginalGrade e suite di review esistente |
| 16: risposte corrette non esposte | controllo JSON HTTP e StudentPayload_NeverContainsCorrectnessOrCanonicalPositions |
| 17: scadenza e worker | suite Mt3eWorkerTests, expiry/concurrency e regressione esistente, tutte superate |
| 18: isolamento A/B e owner | lettura Session da tenant B respinta; materia di B invariata dopo rinomina A; suite multi-tenant completa |
| 19: compatibilità storica | HistoricalNull_PreservesCanonicalPositions e ExamPresentation_HistoricalNullReturnsCanonicalOrder |

I log grezzi sono ignorati da Git. Il riepilogo pubblico con conteggi, hash dei log e
prove dello schema è `EXAM_NAME_AND_RANDOMIZATION_VERIFICATION.json`.

## Database e riproducibilità

Nessuna connessione di lavoro al database sulla 3306. Usate soltanto nuove istanze
isolate MySQL 5.7.24 sulla 13306, avviate una per volta con datadir privati separati.
Preflight e backup prima del DDL. Nessun reset, restore, disabilitazione di trigger/FK,
GLOBAL sql_mode o replay di migrazioni su uno schema già aggiornato.

Un primo giro è stato interrotto perché i guard del banco di prova richiedevano un
suffisso datadir preciso. I dati di quel giro sono stati preservati; non sono state
indebolite le assertion. Le regressioni complete sono state eseguite su una seconda
istanza nuova, nel percorso richiesto, creata dal bootstrap candidato a Git.

Verificate contro il database tutte le 40 definizioni di tabella, le definizioni e
metadati completi degli 88 trigger, 118 FK e 164 indici: nessuna differenza. Una terza
istanza nuova ha ricevuto lo schema precedente da HEAD, quindi la migrazione additiva
corrente dopo il backup: tutte le 40 definizioni risultanti coincidono con il nuovo
contract. Un secondo tentativo di migrazione è stato rifiutato prima di creare anche
la directory delle evidenze. Nessuna DDL ha avuto esito parziale.

## File modificati e portabilità GitHub

Applicazione: ExamManager, InstitutionSubjectManager, ExamCreationMapper,
ExamStudentMapper. Dominio: CreateExamDto, UpdateExamDto, ClassSubject, ExamSubmission,
nuovo StudentExamPresentation. Infrastruttura: ElectronicRegisterContext, entità EF
ExamSubmission, ClassSubjectRepository, ExamRepository, ExamSubmissionRepository,
InstitutionExamRepositoryFactory, InstitutionSubjectRepository, SubjectRepository.

Database/tooling: bootstrap 001_schema.sql, schema-contract.json, manifest.json,
README; directory migrations/exam-presentation; nuovo Invoke-ExamPresentationMigration.ps1;
local_test_db.py; .gitignore. Test: nuovo StudentExamPresentationTests.cs,
ExamPresentationTests.cs, aspettative in Phase4_ExamValidationAndAtomicityTests.cs,
questo report e il riepilogo JSON.

I nuovi SQL sono esplicitamente ammessi da .gitignore. Gli attributi LF esistenti per
migrations e tools sono sufficienti: nessuna modifica a .gitattributes necessaria.
Il manifest copre 128 file pubblici, inclusi SQL e strumento nuovi, con SHA256 dei
byte previsti dal checkout. Verificati hash, visibilità Git, terminazioni di riga e
assenza di dipendenze private. I file storici generati dai test sono stati ripristinati
ai loro byte originali. Backup, datadir e log grezzi restano sotto percorsi ignorati;
nessuna credenziale o chiave privata è aggiunta ai candidati.

## Limiti e consegna

La riconciliazione dei nomi di un database storico reale richiede l'analisi e la
decisione dell'operatore descritte sopra: non viene applicata automaticamente.
Le rinomine supportate passano dai repository applicativi; operazioni SQL manuali
sulla materia richiedono la stessa disciplina di riconciliazione.

Python non è installato in questo ambiente: il driver Python completo di setup non
è stato avviato. Il bootstrap e le verifiche equivalenti sono stati eseguiti con il
client MySQL nativo; lo script PowerShell di migrazione è stato realmente eseguito.
Gli strumenti e gli SQL restano riproducibili dai file pubblici, senza input privati.

EXAM NAME AND RANDOMIZATION IMPLEMENTED AND VERIFIED
