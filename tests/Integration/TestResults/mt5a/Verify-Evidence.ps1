$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
function Read-Run([string]$directory) {
    $rows = @()
    $files = @()
    foreach ($file in Get-ChildItem -LiteralPath $directory -Filter '*.trx') {
        [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
        foreach ($result in $trx.TestRun.Results.UnitTestResult) {
            $rows += [pscustomobject]@{ Name = [string]$result.testName; Outcome = [string]$result.outcome }
        }
        $files += [pscustomobject]@{ Name = $file.Name; SHA256 = (Get-FileHash -LiteralPath $file.FullName).Hash; Counters = $trx.TestRun.ResultSummary.Counters }
    }
    if (!$rows.Count) { throw "No results in $directory" }
    if (@($rows | Group-Object Name | Where-Object Count -gt 1).Count) { throw 'Duplicate test identities' }
    return [pscustomobject]@{ Rows = $rows; Files = $files }
}
$historical = Read-Run (Join-Path $root '../mt4c/final-2')
$baseline = Read-Run (Join-Path $root 'baseline')
$first = Read-Run (Join-Path $root 'final-1')
$second = Read-Run (Join-Path $root 'final-2')
if (@(Compare-Object ($historical.Rows.Name | Sort-Object) ($baseline.Rows.Name | Sort-Object)).Count) { throw 'Baseline differs from MT4C test identities' }
if (@(Compare-Object ($first.Rows.Name | Sort-Object) ($second.Rows.Name | Sort-Object)).Count) { throw 'Final identity sets differ' }
foreach ($run in @($first, $second)) {
    $missing = @($baseline.Rows.Name | Where-Object { $_ -notin $run.Rows.Name })
    if ($missing.Count) { throw 'Previous tests removed' }
    $added = @($run.Rows | Where-Object Name -notin $baseline.Rows.Name)
    if ($added.Count -ne 36 -or @($added | Where-Object Outcome -ne 'Passed').Count) { throw 'New tests not all passed' }
}
$freeze = Get-Content (Join-Path $root 'source-freeze.json') -Raw | ConvertFrom-Json
$changed = @($freeze | Where-Object { (Get-FileHash -LiteralPath $_.Path).Hash -ne $_.Hash })
if ($changed.Count) { throw 'Source freeze changed' }
$summary = foreach ($pair in @(@('historical-mt4c', $historical), @('baseline', $baseline), @('final-1', $first), @('final-2', $second))) {
    $run = $pair[1]
    [pscustomobject]@{ Run = $pair[0]; Total = $run.Rows.Count; Passed = @($run.Rows | Where-Object Outcome -eq 'Passed').Count; Failed = @($run.Rows | Where-Object Outcome -eq 'Failed').Count; Other = @($run.Rows | Where-Object Outcome -notin @('Passed','Failed')).Count; Files = $run.Files }
}
[pscustomobject]@{
    Verdict = 'PARTIAL: schema MT4 missing; no MariaDB certification'
    PreviousIdentitiesPreserved = $baseline.Rows.Count
    NewTestsPassedEachFinal = 36
    SourcesUnchangedAcrossFinalRuns = $true
    Summary = $summary
} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $root 'verification.json')
$summary | Select-Object Run, Total, Passed, Failed, Other | Format-Table
