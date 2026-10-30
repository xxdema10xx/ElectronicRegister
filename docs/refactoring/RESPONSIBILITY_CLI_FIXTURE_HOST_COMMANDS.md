# Verifica host delle fixture CLI

> Aggiornamento: regressione corrente già superata e verificata nelle TRX host: 587/587 unitari, 1420/1420 integrazione, 0 Failed/Skipped, inclusi tre CLI e due filesystem. Vedere [evidenza](validation/responsibility-host-regression-verification.json). Le istruzioni seguenti restano una procedura storica per eventuali run futuri autorizzati; non richiedono una nuova esecuzione per chiudere la verifica corrente. Nessuna istanza esistente va alterata in questo aggiornamento. Il confronto SQL con il commit precedente non è disponibile e non è una condizione ancora pendente per attestare il verde del codice attuale.


La correzione riguarda solo i test. Build, 587 unitari e due prove Windows filesystem/ACL sono stati eseguiti in Codex. I tre workflow CLI/MySQL **non sono stati eseguiti in Codex**, perché `TEST_MYSQL_CONNECTION` non è configurata. I dati 1418/1415/3/0 del run precedente sono stati comunicati dal proprietario dell'host; non costituiscono un esito successivo alla correzione.

## Tre test sulla sessione isolata già configurata dall'host

Usare la stessa sessione PowerShell con connessione, porta e datadir esplicite del run autorizzato 13429. Non leggere credenziali di istanze storiche o stampare la connessione. I test mantengono le verifiche SQL dell'identità esatta dell'istanza. Questi comandi non inizializzano o migrano un database; eseguono soltanto i workflow delle fixture e il relativo teardown sintetico.

```powershell
Set-Location 'C:\Users\Lorenzo\ElectronicRegister'
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:TEST_MYSQL_CONNECTION)) { throw 'Configurare la connessione della sola istanza test autorizzata nella sessione host' }
$fixturePort = 0
if (![int]::TryParse($env:ER_TEST_ISOLATED_PORT, [ref]$fixturePort) -or
    $fixturePort -lt 1024 -or $fixturePort -gt 65535 -or $fixturePort -in @(3306,13306)) {
    throw 'Porta test isolata esplicita richiesta'
}
if ([string]::IsNullOrWhiteSpace($env:ER_TEST_ISOLATED_DATADIR)) { throw 'Datadir test esplicita richiesta' }
$fixtureRepo = (Get-Location).Path
$fixtureData = [IO.Path]::GetFullPath($env:ER_TEST_ISOLATED_DATADIR)
if (!$fixtureData.StartsWith($fixtureRepo + '\.staff-private\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Datadir esterna al perimetro test' }

# Nuova directory per log/TRX, separata dalle datadir; ACL solo sulla directory appena creata.
$fixtureEvidence = Join-Path $fixtureRepo ('.staff-private/cli-verification-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureEvidence | Out-Null
$fixtureAcl = New-Object Security.AccessControl.DirectorySecurity
$fixtureAcl.SetAccessRuleProtection($true, $false)
foreach ($sid in @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value,'S-1-5-18','S-1-5-32-544')) {
    $fixtureAcl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
        (New-Object Security.Principal.SecurityIdentifier($sid)), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
}
Set-Acl -LiteralPath $fixtureEvidence -AclObject $fixtureAcl

dotnet build Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx --no-restore -v minimal > (Join-Path $fixtureEvidence 'build.log') 2>&1
if ($LASTEXITCODE -ne 0) { throw "Build fallita: $fixtureEvidence" }
dotnet test tests/Unit/Unit.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=unit.trx' --results-directory $fixtureEvidence -v minimal > (Join-Path $fixtureEvidence 'unit.log') 2>&1
if ($LASTEXITCODE -ne 0) { throw "Unitari falliti: $fixtureEvidence" }

$fixtureCliFilter = @(
    'FullyQualifiedName=ElectronicRegisterAPI.Tests.Integration.Mt5cOnboardingTests.Governance_ActualCliUsesTheDedicatedManagerWithoutRequiringNotificationDelivery',
    'FullyQualifiedName=ElectronicRegisterAPI.Tests.Integration.Mt5cOnboardingTests.ActualAdministrativeCliVerifiesSignedIntentAndUsesOnlyOwnerInstalledChannel',
    'FullyQualifiedName=ElectronicRegisterAPI.Tests.Integration.Mt5cOnboardingTests.PlatformCliRefusesAuthorityConfigurationReadableByEveryoneBeforeMutation'
) -join '|'
dotnet test tests/Integration/Integration.Tests.csproj --no-build --no-restore --filter $fixtureCliFilter `
    --logger 'trx;LogFileName=cli-three.trx' --results-directory $fixtureEvidence -v minimal > (Join-Path $fixtureEvidence 'cli-three.log') 2>&1
$fixtureCliExit = $LASTEXITCODE
[xml]$fixtureCliTrx = [IO.File]::ReadAllText((Join-Path $fixtureEvidence 'cli-three.trx'))
$fixtureCliCounters = $fixtureCliTrx.TestRun.ResultSummary.Counters
if ($fixtureCliExit -ne 0 -or [int]$fixtureCliCounters.total -ne 3 -or [int]$fixtureCliCounters.passed -ne 3 -or [int]$fixtureCliCounters.executed -ne 3) {
    throw "Tre test non superati integralmente: conservare log/TRX in $fixtureEvidence"
}

dotnet test tests/Integration/Integration.Tests.csproj --no-build --no-restore `
    --filter 'FullyQualifiedName~ElectronicRegisterAPI.Tests.Integration.PrivateTestFilesTests' `
    --logger 'trx;LogFileName=filesystem.trx' --results-directory $fixtureEvidence -v minimal > (Join-Path $fixtureEvidence 'filesystem.log') 2>&1
if ($LASTEXITCODE -ne 0) { throw "Prove filesystem fallite: $fixtureEvidence" }
Get-FileHash (Join-Path $fixtureEvidence 'cli-three.trx'), (Join-Path $fixtureEvidence 'filesystem.trx') -Algorithm SHA256
```

Le fixture creano ora `.staff-private/cli-fixtures-<run-guid>/<tipo>-<test-guid>`. I file di autorità e gli intenti firmati rimangono privati e ignorati da Git. Non modificare ACL di cartelle storiche e non eseguire come Administrator per aggirare un errore. Il test Everyone deve rifiutare il file deliberatamente allargato e accettare lo stesso intento con DACL ripristinata; non basta un exit code negativo per un target configurato male.

## Dopo 3/3: regressione completa su nuova istanza

Il superamento dei tre test è un prerequisito, non la certificazione finale. Richiedere/eseguire una regressione completa su **una nuova istanza**, con datadir nuova e una porta libera diversa da 3306/13306/13429. Seguire la funzione `Invoke-ResponsibilityIsolatedRun` in [RESPONSIBILITY_INTEGRATION_HOST_COMMANDS.md](RESPONSIBILITY_INTEGRATION_HOST_COMMANDS.md): copiarne soltanto la definizione e le variabili iniziali, senza ripetere l'esempio finale che usa 13429. Poi:

```powershell
if (!(Get-Command Invoke-ResponsibilityIsolatedRun -ErrorAction SilentlyContinue)) { throw 'Definire prima la funzione di isolamento documentata' }
$fullFixtureEvidence = Invoke-ResponsibilityIsolatedRun -SourceRoot $responsibilityRepo -Port 13431
```

Se 13431 è occupata, fermarsi e scegliere esplicitamente un'altra porta libera; non arrestare o riutilizzare il suo proprietario. Il runner resta soggetto alla policy dell'host: nessun bypass. La suite integration attesa passa da 1418 a **1420** per i due nuovi casi filesystem; il totale effettivo va verificato nella TRX, con **0 Failed e 0 Skipped**, preservando tutti i 1418 casi precedenti. Nessuno di questi comandi host è stato eseguito da Codex.
