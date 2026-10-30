# Verifica sull'host Windows: due istanze nuove, nessun riuso

> Aggiornamento: regressione corrente già superata e verificata nelle TRX host: 587/587 unitari, 1420/1420 integrazione, 0 Failed/Skipped, inclusi tre CLI e due filesystem. Vedere [evidenza](validation/responsibility-host-regression-verification.json). Le istruzioni seguenti restano una procedura storica per eventuali run futuri autorizzati; non richiedono una nuova esecuzione per chiudere la verifica corrente. Nessuna istanza esistente va alterata in questo aggiornamento. Il confronto SQL con il commit precedente non è disponibile e non è una condizione ancora pendente per attestare il verde del codice attuale.


Questa procedura **non è stata eseguita** nella sessione: la ExecutionPolicy effettiva è `Restricted` e il runner viene respinto con `PSSecurityException` prima di entrare nel corpo dello script. Non cambiare policy, non usare `-ExecutionPolicy Bypass`, dot-sourcing di testo, `Invoke-Expression` o altri aggiramenti. Eseguire solo su un host che autorizza normalmente questi runner; altrimenti fermarsi e far gestire il blocco al proprietario dell'ambiente.

Runner nativo già presente: `tools/New-InstitutionStaffTestInstance.ps1`. Non richiede Python. Verifica MySQL 5.7.24, rifiuta 3306/13306 e porte occupate, genera una directory `instance-<guid>` con ACL private in `.staff-private`, inizializza solo quella directory e importa il bootstrap corrente. Non usare `-Baseline`: quel parametro seleziona uno schema storico differente. Il binder successivo modifica soltanto i guard di teardown delle fixture nell'istanza nuova e attestata, non i file SQL canonici né istanze preesistenti. Si tratta di DDL senza rollback globale: in caso di errore conservare istanza e log e fermarsi, senza replay.

Occorrono i binari MAMP già installati e il codice/cache NuGet disponibili sull'host. Nessuna configurazione NuGet viene modificata; se restore o build sono negati, fermarsi. Non eseguire `tools/test-local.ps1`: quel wrapper usa la porta 13306.

## Funzione per una nuova esecuzione completa

In una normale PowerShell dell'host, dal repository:

```powershell
Set-Location 'C:\Users\Lorenzo\ElectronicRegister'
$ErrorActionPreference = 'Stop'
$responsibilityRepo = (Get-Location).Path
$responsibilityMySql = 'C:\MAMP\bin\mysql\bin'

function Invoke-ResponsibilityIsolatedRun {
    param([string]$SourceRoot, [int]$Port, [switch]$Restore)

    $SourceRoot = [IO.Path]::GetFullPath($SourceRoot).TrimEnd('\')
    if ($SourceRoot -ne $responsibilityRepo -and
        !$SourceRoot.StartsWith($responsibilityRepo + '\.staff-private\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Source root outside the explicit workspace'
    }
    for ($ancestor = [IO.DirectoryInfo]$SourceRoot; $null -ne $ancestor; $ancestor = $ancestor.Parent) {
        if (($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Reparse point refused' }
    }
    if ($Port -lt 1024 -or $Port -gt 65535 -or $Port -in @(3306,13306)) { throw 'New isolated port required' }
    # Abort also if listener enumeration itself is unavailable.
    $listeners = @(Get-NetTCPConnection -State Listen -ErrorAction Stop)
    if ($listeners.LocalPort -contains $Port) { throw 'Port occupied: choose a different new port; never stop its owner' }
    $privateRoot = Join-Path $SourceRoot '.staff-private'
    if (!(Test-Path -LiteralPath $privateRoot)) { New-Item -ItemType Directory -Path $privateRoot | Out-Null }
    $before = @(Get-ChildItem -LiteralPath $privateRoot -Directory -Filter 'instance-*' | ForEach-Object FullName)

    & (Join-Path $SourceRoot 'tools/New-InstitutionStaffTestInstance.ps1') -MySqlBin $responsibilityMySql -Port $Port | Out-Host
    # The runner throws on failure. Never rerun it against a partial directory.
    $created = @(Get-ChildItem -LiteralPath $privateRoot -Directory -Filter 'instance-*' |
        Where-Object { $_.FullName -notin $before })
    if ($created.Count -ne 1) { throw 'Cannot identify exactly one newly created instance' }
    $run = $created[0].FullName
    $data = Join-Path $run 'data'
    $identity = ([IO.File]::ReadAllText((Join-Path $run 'identity.tsv')).Trim()) -split "`t"
    if ($identity.Count -ne 3 -or $identity[0] -ne '5.7.24' -or [int]$identity[1] -ne $Port -or
        [IO.Path]::GetFullPath($identity[2]).TrimEnd('\','/') -ine $data) { throw 'Recorded instance identity mismatch' }
    $pidText = [IO.File]::ReadAllText((Join-Path $run 'pid.txt')).Trim()
    $listener = @(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction Stop)
    if ($listener.Count -ne 1 -or $listener[0].LocalAddress -ne '127.0.0.1' -or
        $listener[0].OwningProcess -ne [int]$pidText -or
        (Get-Process -Id ([int]$pidText) -ErrorAction Stop).Path -ine (Join-Path $responsibilityMySql 'mysqld.exe')) {
        throw 'Listener/process mismatch'
    }

    $names = @('MYSQL_PWD','TEST_MYSQL_CONNECTION','ER_TEST_ISOLATED_PORT','ER_TEST_ISOLATED_DATADIR',
        'ER_TEST_STRICT_SESSION','MYSQLDUMP_EXE','MYSQLDUMP_FLAVOR','ER_MT5B_SQL_PROOF')
    $previous = @{}
    foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name) }
    try {
        # Read only the password generated by this invocation, never an old instance's credentials.
        $env:MYSQL_PWD = [IO.File]::ReadAllText((Join-Path $run 'password.txt'))
        & (Join-Path $SourceRoot 'tools/Bind-StaffDisposableFixtureRetention.ps1') `
            -MySqlBin $responsibilityMySql -Port $Port -ExpectedDataDirectory $data | Out-Host
        # Binder verifies actual SQL VERSION(), @@port and exact @@datadir before any DDL.
        $connection = New-Object System.Data.Common.DbConnectionStringBuilder
        $connection['Server'] = '127.0.0.1'
        $connection['Port'] = $Port
        $connection['Database'] = 'electronic_register_test'
        $connection['User ID'] = 'root'
        $connection['Password'] = $env:MYSQL_PWD
        $connection['Allow User Variables'] = $true
        $connection['SslMode'] = 'None'
        $env:TEST_MYSQL_CONNECTION = $connection.ConnectionString
        $env:ER_TEST_ISOLATED_PORT = [string]$Port
        $env:ER_TEST_ISOLATED_DATADIR = $data
        $env:ER_TEST_STRICT_SESSION = '1'
        $env:MYSQLDUMP_EXE = Join-Path $responsibilityMySql 'mysqldump.exe'
        $env:MYSQLDUMP_FLAVOR = 'MySQL57'
        [Environment]::SetEnvironmentVariable('ER_MT5B_SQL_PROOF', $null)
        $solution = Join-Path $SourceRoot 'Backend/ElectronicRegisterAPI/ElectronicRegisterAPI.slnx'
        if ($Restore) {
            dotnet restore $solution --locked-mode > (Join-Path $run 'restore.log') 2>&1
            if ($LASTEXITCODE -ne 0) { throw "Restore failed; private log: $run" }
        }
        dotnet build $solution --no-restore -v minimal > (Join-Path $run 'build.log') 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Build failed; private log: $run" }
        foreach ($suite in @('Unit','Integration')) {
            $results = Join-Path $run $suite
            New-Item -ItemType Directory -Path $results | Out-Null
            dotnet test (Join-Path $SourceRoot "tests/$suite/$suite.Tests.csproj") --no-build --no-restore `
                --logger 'trx;LogFileName=result.trx' --results-directory $results -v minimal `
                > (Join-Path $run "$suite.log") 2>&1
            $testExit = $LASTEXITCODE
            [xml]$trx = [IO.File]::ReadAllText((Join-Path $results 'result.trx'))
            $c = $trx.TestRun.ResultSummary.Counters
            if ($testExit -ne 0 -or [int]$c.total -eq 0 -or $c.total -ne $c.passed -or $c.total -ne $c.executed) {
                throw "$suite failed/skipped/incomplete; retain private logs and TRX at $run"
            }
        }
        return $run
    } finally {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name]) }
    }
}

# Current code: stop if 13429 is occupied; choose another unused non-reserved port explicitly.
$currentEvidence = Invoke-ResponsibilityIsolatedRun -SourceRoot $responsibilityRepo -Port 13429
```

Non stampare connessione o password. Ogni failure interrompe il percorso e conserva la nuova istanza per la diagnosi. Questa funzione non arresta, cancella, ripara o riutilizza alcuna istanza.

## Baseline reale precedente al refactoring su un'altra istanza nuova

La baseline TRX storica 82/1279/57 non dimostra l'equivalenza SQL. Per confrontare il comportamento precedente serve una seconda esecuzione sul commit iniziale, senza sovrascrivere il working tree corrente:

```powershell
$comparisonRoot = Join-Path $responsibilityRepo ('.staff-private/responsibility-baseline-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $comparisonRoot | Out-Null
$archive = Join-Path $comparisonRoot 'source.zip'
git -C $responsibilityRepo archive --format=zip --output=$archive f12a0e1ac6b95507bc0ac571600c797fd9ededfc
if ($LASTEXITCODE -ne 0) { throw 'Cannot export baseline' }
$baselineSource = Join-Path $comparisonRoot 'source'
Expand-Archive -LiteralPath $archive -DestinationPath $baselineSource
# Baseline has no restored assets in the new tree: use ordinary authorized host restore, no config changes.
$baselineEvidence = Invoke-ResponsibilityIsolatedRun -SourceRoot $baselineSource -Port 13430 -Restore

[xml]$before = [IO.File]::ReadAllText((Join-Path $baselineEvidence 'Integration/result.trx'))
[xml]$after = [IO.File]::ReadAllText((Join-Path $currentEvidence 'Integration/result.trx'))
$oldOutcomes = @($before.TestRun.Results.UnitTestResult | ForEach-Object { $_.testName + '|' + $_.outcome } | Sort-Object)
$newOutcomes = @($after.TestRun.Results.UnitTestResult | ForEach-Object { $_.testName + '|' + $_.outcome } | Sort-Object)
Compare-Object $oldOutcomes $newOutcomes
Get-FileHash (Join-Path $baselineEvidence 'Integration/result.trx'), (Join-Path $currentEvidence 'Integration/result.trx') -Algorithm SHA256
```

Il confronto deve preservare nomi, molteplicità ed esiti dei test precedenti. Gli unitari attesi sono 559 nel commit iniziale e 587 nel codice rifattorizzato; la suite integration attuale contiene 1418 casi, ma il numero effettivo va letto dalle nuove TRX. Nessuna di queste esecuzioni host è attualmente certificata. Se uno dei due run non termina integralmente verde, diagnosticare i suoi errori prima di procedere con altri refactoring.
