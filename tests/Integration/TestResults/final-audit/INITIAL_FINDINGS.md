# Ricognizione iniziale read-only - 2026-10-18

Repository pulito, branch refactor-database-entities, HEAD 970d5d7. Build riuscita; baseline rieseguita su MySQL MAMP: 39 unit + 172 integration = 211 Passed, 0 Failed, 0 Skipped. Nessuna modifica produttiva eseguita prima di questa registrazione. NuGet audit completato con feed nuget.org: nessuna vulnerabilita nota riportata nei sette progetti.

Finding da riprodurre prima delle correzioni:

- SEC-01 HIGH candidato: MicrosoftTokenValidator.ValidateAsync disabilita ValidateIssuer; firma/audience non vincolano il token al tenant configurato. Preparare token con firma valida e issuer estraneo contro metadati locali controllati.
- SEC-02 HIGH candidato: StudentController GET/count/bylastname/id permette a qualunque docente di leggere tutti gli studenti; nessun caller e filtro classe nel manager/repository. Riprodurre con docente senza assegnazioni. La directory pubblica TeacherDto contiene solo nomi/ID e richiede decisione di policy, non prova automatica di IDOR.
- INT-01 HIGH candidato: GradeController update/delete muta anche Grade referenziati da submission, aggirando finalizzazione/override/audit; delete ha FK SetNull. Riprodurre su esame completato e confrontare nuovo contesto.
- INT-02 MEDIUM: Register/RegisterForAdmin e UserManager.Update eseguono piu SaveChanges senza transazione; possibile persistenza parziale. Test fault dopo SQL.
- API-01 MEDIUM: Student, Users e Biennium Add restituiscono bool usato come ID nel CreatedAtAction, producendo Location true anziche Guid. Biennium documenta SubjectDto ma restituisce BienniumDto. Riprodurre via HTTP e OpenAPI reale.
- AUTH-01 MEDIUM: BCrypt.Verify con hash vuoto genera eccezione invece di credenziali non valide (utenti SSO/record senza password); riprodurre login HTTP.
- DATA-01 MEDIUM: StudyAreaRepository.HasStudyPaths compara ID BienniumStudyArea con ID StudyArea; FK blocca comunque la cancellazione, ma controllo e status sono errati. Nessun controller pubblico StudyArea.
- CFG-01 MEDIUM rischio di configurazione: CORS wildcard in tutti gli ambienti, binding HTTP fisso 0.0.0.0:5000; nessuna prova di proxy/TLS/AllowedHosts reali. OpenAPI/Scalar limitati a Development. JWT senza validazione opzioni al bootstrap. Nessun rate limit applicativo osservato; deployment ignoto.
- PERF-01 MEDIUM/LOW: listing senza paging; N query opzioni in SaveAnswers (una per risposta chiusa); Include multipli Exam; Grade filters materializza tutti i voti; AutoDetect apre connessione per creazione scope. Misurare prima di ottimizzare.
- REL-01 MEDIUM: worker usa scope corretti e isolamento per elemento, ma CancellationToken non propagato al DB; elementi permanentemente falliti possono monopolizzare primi 50. Rischio condizionale, non starvation dimostrata.
- TEST-01 MEDIUM copertura: HTTP custom non Program reale, JWT Microsoft/anagrafiche/Grade CRUD non coperti dalla baseline; test concurrency storici con Task.WhenAll non forzano interleaving. Nessun test da rimuovere.

Regole Exam, retake e grading: nessun difetto dimostrato nella ricognizione. Close inizia transazione, prende lock Exam prima di qualsiasi lettura consistente; query SQL InProgress successiva vede gli Start committati precedenti. Non cambiare lock o ConvertToGrade.

Questa lista e iniziale: severity e stato definitivo saranno aggiornati solo sulla base di riproduzioni e verifiche nel report finale.
