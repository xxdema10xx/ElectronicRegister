# Certificazione locale MySQL 8.4.13 — er-quality-2-mysql84

**READY per la verifica locale richiesta.** Tutti i gate obbligatori riportati sotto sono PASS. Nessun commit, push o deployment. Report machine-readable: [mysql84-certification.json](mysql84-certification.json).

## Correzione hash dimostrata

Il difetto originario è riprodotto: `CHAR(64)` perde il newline prima del trigger. Anche `VARCHAR(64)` da solo non risolve troncamento/coercizione. La candidata usa **VARCHAR(65) ASCII**, mantiene collation e nullabilità originali e aggiunge 16 CHECK ENFORCED su 13 tabelle: lunghezza byte 64, UNHEX valido, confronto binario con HEX(UNHEX). Si accettano esclusivamente 64 caratteri esadecimali maiuscoli, secondo il formato preesistente; i trigger restano attivi. Lo spazio aggiuntivo fa sì che un input lungo troncato in modalità non strict resti lungo 65 e venga rifiutato.

Coperti token_hash, channel_hash, intent_hash, nonce, request_hash, file_sha256 e preview_digest. Mapping EF aggiornati; formato applicativo invariato. Dati storici legittimi, anche hash indicizzati, conservati durante upgrade con confronto di tutte le celle.

**2.772 rifiuti DML reali PASS**: 1.332 su upgrade, 1.332 su fresh, altri 54 UPDATE su invito pending per ciascun percorso. Prima/dopo ogni rifiuto è confrontato il digest dei dati di tutte le tabelle; nessuna modifica persistente. INSERT validi su tutte le 13 tabelle e quattro rotazioni hash valide persistite su inviti pending. Provati SQL mode certificato, non strict e NO_BACKSLASH_ESCAPES; newline finale/interno/iniziale, CRLF, tab, spazi, lunghezze errate, minuscole, non esadecimali, Unicode accentato, NBSP, zero-width, fullwidth, emoji e NUL. Le prove CHAR/VARCHAR e le collation reali sono conservate in `hash-storage-design.json` e `final-artifact-attestation.json`.

## Tenant e capability della fixture

I due casi membership-deleted/user-deleted sono PASS. La riesecuzione finale dei 12 casi di modifica dell’autorità legge tramite un nuovo contesto che membership/utente siano effettivamente assenti e che altre autorità siano preservate, poi verifica 403/401 con token già emesso e rifiuto della selezione successiva. Le aspettative HTTP non sono state cambiate.

Il purge 8.4 è disponibile soltanto per container creati e attestati dallo stesso processo fixture. UUID, hostname, porta interna, datadir, database, root, lista owner e capability casuale devono corrispondere. Il binding conserva le guardie storiche e i prefissi sintetici. Quattro regressioni reali verificano target originale/EC2/arbitrario rifiutati prima della connessione, capability errata, owner errato e account API rifiutati, poi cleanup root autorizzato e ripristino dello scope. I bundle ordinari non ricevono questo binding. Nessun trigger disabilitato e nessun FOREIGN_KEY_CHECKS=0.

## Analisi completa dei 39 fallimenti MT3A

TRX originale: `.diagnostics/mysql84-local-real/dotnet/targeted-before.trx`. Tutti i 39 stack contengono il blocco cleanup storico 5.7/13306. Gruppi esclusivi: **35 solo cleanup; 3 setup legacy esplicito mancante più cleanup; 1 metadato FK esatto NO ACTION contro RESTRICT più cleanup**. Nessun difetto di migrazione o regressione applicativa riprodotto dopo aver reso eseguibili i casi.

Riesecuzione originale completa: 38 PASS e un blocco nel backup per credenziale non passata dalla fixture. Corretto il passaggio esplicito, mantenendo corrispondenza esatta del target, ACL e dump completo reale; il caso restante è PASS. Il JSON associa ognuno dei 39 nomi originali al proprio risultato attuale e al TRX. Non sono considerati risolti per deduzione dalle suite principali.

## Gate reali

| Gate | Esito |
|---|---|
| protected-original-and-volumes | PASS |
| hash-storage-design | PASS |
| upgrade-data-journal-recovery | PASS |
| upgrade-trigger-matrix | PASS |
| fresh-trigger-matrix | PASS |
| routines-least-privilege-call | PASS |
| fresh-after-upgrade | PASS |
| release-http | PASS |
| tenant-persistent-authority | PASS |
| fixture-purge-negative-positive-controls | PASS |
| original-39-mt3a-failures | PASS |
| windows-admin-cli-acls | PASS |
| python | PASS |
| node-artifacts-and-negative-controls | PASS |
| csharp-unit | PASS |
| candidate-manifest-sql-equivalence | PASS |
| immutable-predecessor | PASS |

Python: **104 PASS**; unitari C#: **598 PASS**. Gate HTTP: **1 PASS**, tramite account API a privilegi minimi, con login/selezione, letture, mutazioni membership, email, import studenti/docenti, piani e rifiuto cross-tenant. Routine CALL reale restituisce 1 su entrambi i database. Le tre CLI Windows con ACL e i 710 casi già PASS nelle suite obbligatorie sono riutilizzati dal report precedente dopo verifica delle evidenze, senza ripetizione indiscriminata. I due FAIL obbligatori ora sono PASS: copertura combinata dei 355 metodi/712 casi; non si dichiara una nuova esecuzione monolitica.

## Upgrade, fresh e recupero

Predecessore ricostruito dal commit immutabile, errore 3995 riprodotto, snapshot reale e dati legittimi preservati. Journal concluso con 13 ALTER, 19 trigger e routine. Provate interruzioni dopo ALTER, DROP/CREATE TRIGGER e DROP/CREATE PROCEDURE, riavvio separatamente approvato del solo server temporaneo, maintenance persistente, riconciliazione senza replay e idempotenza. Drift del predecessore rifiutato prima della manutenzione; CHECK disabilitato rifiutato anche sul journal completato e readiness a 0; CHECK ripristinato e metadati nuovamente verificati.

La prima accettazione ha incontrato un seed incompleto (description obbligatoria) e una ripresa ha rilevato nonce sintetici non univoci. Sono errori della preparazione delle prove, conservati come FAIL nelle evidenze; corretti i seed, la matrice completa sullo schema aggiornato è PASS. La fresh install successiva è PASS, comprese equivalenza schema, bundle manomesso rifiutato, protocolli confermati, journal, marker e idempotenza. La regressione aggiuntiva pending è stata eseguita separatamente su entrambi i percorsi e integrata nel runner permanente.

Il manifest definitivo differisce dalle revisioni durante lo sviluppo dei test. L’attestazione finale confronta tutti gli SQL fresh, i 13 ALTER, i 19 CREATE TRIGGER e il CREATE della routine: **identici per checksum agli artefatti realmente verificati**, con ulteriore preflight completo e CALL API. Non è stato falsificato alcun journal per accettare un manifest diverso.

## Isolamento e risorse

Originale: `2299602d4bce333ef0fb626e9b1fb22758ae15ed62edb637357c7ebd4d8bc1b2`, `mysql84-local`, volume `mysql84_data`, porta 3307. StartedAt `2026-11-02T18:28:50.191606317Z` invariato e RestartCount 0. Nessuna operazione mutativa sull’originale.

Principale: `81497357df0973e293d65a19889476f6eeb6178715d394fc495d891220739829`, nome `er84-quality2-local-20261102-a71c`, UUID `45aff413-d704-11f1-b8d9-5e82fd3aef6c`, hostname `81497357df09`, host 13384/server 3306, datadir `/var/lib/mysql/`, volume `er84-quality2-local-20261102-a71c-data`. Immagine `mysql:8.4.13@sha256:5cf1e1293ea42e077e6a22114e6773b9aae3bb8f9933a2acd5a9721518ff8f5b`.

Inventario finale: 26 container temporanei e 26 volumi dedicati conservati; nessuna condivisione con l’originale. Arresti eseguiti soltanto dopo inventario e approvazioni separate. Nessuna rimozione né cleanup globale. Concorrenza limitata a una nuova fixture .NET alla volta oltre al principale. Percorso locale distinto da GitHub, senza impostare GITHUB_ACTIONS=true. EC2, staging e produzione non coinvolti.

## Artefatti, file e durate

Manifest candidato SHA256: `72d934cd187512303e87aa979f14c89e8bcf2ee8c394c5acd85c29f0320fd8f1`; **1107 file** verificati, 12 controlli negativi del manifest candidato PASS; controlli Node del predecessore e manifest storico PASS. Confrontati 11 artefatti immutabili con `8e7d9ecd42c001f1fa9b30e120535ce1bb0ef6e8`; hash raw e normalizzati nel JSON. Rigenerati soltanto manifest e contratto candidati.

Modifiche: generatore/contratto hash, runner e journal strutturale/routine, matrice DML permanente, mapping EF delle sette aree, capability del purge e sue regressioni, fixture/backup/metadati MT3A, assert di persistenza Mt2, percorso HTTP locale attestato. Elenco esatto dei file e motivazione nel JSON; modifiche preesistenti conservate.

Durata complessiva di questa sessione: circa 69.4 minuti, comprese analisi e approvazioni. Accettazione upgrade: 181.829 s; fresh successiva: 272.093 s; recupero DDL: 327.843 s; attestazione finale: 41.078 s. Durate individuali TRX e fasi nel JSON.

Problemi obbligatori residui: **nessuno**. Limiti dichiarati: nessun nuovo run GitHub/partizione per metodo e nessun deployment; build finale PASS con 15 warning di compilazione, zero errori. Le prove grezze e gli ambienti temporanei restano disponibili per ispezione in `.diagnostics/mysql84-certification/` e `.diagnostics/mysql84-local-real/`.
