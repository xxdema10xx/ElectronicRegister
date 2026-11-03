# Run #13: ACL Windows e conservazione dell'errore primario

Run esaminato: <https://github.com/xxdema10xx/ElectronicRegister/actions/runs/41715631290>,
commit `d36f2a1c8933a6109b87e906d8526006a8e8ea77`, artefatto `mysql84-windows-cli` ID `13270470383`.

L'artefatto originale conferma `platform`, `restore`, `build` e `cleanup` PASS,
`provision` FAIL in `private_acl(run)`, nessun TRX. Il messaggio
`Missing or duplicate Windows checks` maschera il fallimento di provisioning.
Lo stderr PowerShell originale non è recuperabile: il subprocess lo catturava
ma lo scartava; non è contenuto nell'artefatto né nei log del job.
Non è possibile dimostrare l'eccezione specifica del run #13 a posteriori.

## Riproduzione e limite della diagnosi

Il job usa Windows Server 2025, immagine `windows-2025-vs2026` versione
`20261030.294.1`. Il target privato è nel workspace
`.staff-private/windows-cli-<uuid>`, non in `RUNNER_TEMP`; quest'ultimo contiene
gli eseguibili MySQL scaricati e gli script temporanei delle Actions.
La protezione e gli owner del target hosted prima del fallimento non erano registrati.

Sul Windows locale, con Windows PowerShell e token senza Administrators abilitato,
la funzione originale estratta dal commit passa su un target nuovo con DACL
ereditata, ma fallisce sullo stesso target già protetto: `Set-Acl`,
`PrivilegeNotHeldException`, privilegio `SeSecurityPrivilege` mancante.
Il risultato si ripete con parent ereditato e parent protetto.
Questa è una condizione di errore dimostrata del codice originale, non la prova
che il target hosted fosse già protetto.

Il [provider FileSystem di PowerShell](https://github.com/PowerShell/PowerShell/blob/master/src/System.Management.Automation/namespaces/FileSystemSecurity.cs)
copia inizialmente tutte le sezioni del descrittore e, dopo un errore di privilegio,
stima le sezioni minime. L'euristica confronta protezione audit della sorgente
con protezione accessi del target; può così mantenere la sezione audit quando
il target ha la DACL protetta. Il sorgente corrente spiega il comportamento
riprodotto; non certifica la build PowerShell specifica del runner storico.

La correzione usa `Directory.SetAccessControl` di .NET Framework, invocato con
`powershell.exe`, per persistere soltanto le sezioni modificate. Conserva il
gruppo esistente, assegna l'owner al SID corrente se necessario, rimuove
ereditarietà e regole precedenti e imposta esattamente tre grant FullControl
ereditabili: utente corrente, SYSTEM e Administrators. La verifica legge
direttamente i SID: nessuna traduzione SID/NTAccount è necessaria. Una prova
separata conferma che un SID non risolvibile fallisce nella traduzione in nome
ma rimane rappresentabile come SID. Non è stata riprodotta una transizione da
owner Administrators su un token hosted elevato: rimane una differenza ambientale.

## Diagnostica e report

Il nuovo helper emette solo metadati ACL: owner, gruppo, SID, diritti,
ereditarietà, protezione e SDDL di parent e target prima/dopo. In caso di errore
conserva operazione, messaggio PowerShell, tipo, HRESULT, FullyQualifiedErrorId
e categoria, più gli ACL ancora leggibili. Non legge file, credenziali,
configurazioni o variabili d'ambiente nel report; sostituisce il percorso target
nel messaggio. Il chiamante conserva anche gli errori di avvio/parse dell'host.
La directory privata viene eliminata soltanto dopo la raccolta della diagnostica.

Tutti i nove check sono presenti dall'inizio: le dipendenze non eseguite restano
BLOCKED. Il validatore e l'aggregatore mostrano prima la riga FAIL originale,
anche per un vecchio report incompleto, mantenendo obbligatori tutti i check,
i tre TRX e le verifiche negative ACL. Nessuno skip può certificare il job.

## Risultati effettivi locali, 3 novembre 2026

- Certificazione Windows finale: 3/3 metodi CLI reali PASS, zero skipped;
  nove check PASS; tre fasi ACL negative; cleanup PASS; report e digest dei
  tre TRX validati indipendentemente con `validate_report`.
- Regressioni Windows ACL reali: 2/2 PASS, includono parent ereditato/protetto,
  owner, tre soli grant, ereditarietà dei figli, applicazione ripetuta e
  conservazione di un errore PowerShell reale per target assente.
- Regressioni validatore Windows: 11/11 PASS; aggregazione diagnostica: 13/13
  PASS; bootstrap/manifest: 6/6 PASS.
- `node tools/mysql84-release.cjs` PASS: chiusura corrente e predecessore
  immutabile verificati. `node tools/test-mysql84-release.cjs` PASS:
  12 controlli negativi e drift, bundle offline. Nessuna nuova prova runtime 8.4.
- Manifest correttivo aggiornato solo per gli hash dei tool modificati e i due
  nuovi file `private_acl.ps1`, `test_windows_private_acl.py`, entrambi inclusi.
  `git diff --check` PASS. Backend, SQL, migration, test Linux, workflow e
  manifest storico invariati.

Evidenze locali non versionate: `.diagnostics/run13-source/windows-cli-diagnostic.json`,
`.diagnostics/run13-acl-probe.py`, `.diagnostics/run13-acl-probe-results.json`,
`.diagnostics/run13-windows-final/windows-cli-diagnostic.json` e `0.trx`, `1.trx`, `2.trx`.
I campi di provenienza GitHub delle prove locali sono vuoti.
Un tentativo iniziale in sandbox è stato bloccato al restore; l'esecuzione
completa fuori sandbox è passata. Le regressioni temporanee negate dalla
sandbox sono state rieseguite fuori sandbox con successo.

Nessun commit, push, deployment o nuovo workflow remoto è stato eseguito.
Il run #13 rimane fallito; la correzione necessita di una futura verifica hosted.
