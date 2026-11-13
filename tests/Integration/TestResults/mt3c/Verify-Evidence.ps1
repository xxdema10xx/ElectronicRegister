$ErrorActionPreference = 'Stop'
function Read-Trx([string]$relative, [int]$expected) {
    $path = Join-Path $PSScriptRoot $relative
    [xml]$document = Get-Content -LiteralPath $path
    $counters = $document.TestRun.ResultSummary.Counters
    $results = @($document.TestRun.Results.UnitTestResult)
    if ([int]$counters.total -ne $expected -or [int]$counters.passed -ne $expected -or [int]$counters.executed -ne $expected -or
        [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or $results.Count -ne $expected -or
        @($results | Where-Object outcome -ne 'Passed').Count -ne 0) { throw "Invalid counters/outcomes: $relative" }
    $names = @($results | ForEach-Object testName | Sort-Object)
    if (@($names | Select-Object -Unique).Count -ne $expected) { throw "Duplicate test identity: $relative" }
    return [PSCustomObject]@{ file=$relative; total=$expected; passed=$expected; failed=0; skipped=0; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; names=$names }
}
$unitBaseline = Read-Trx 'baseline/unit.trx' 48
$integrationBaseline = Read-Trx 'baseline-ready/integration.trx' 378
$u1 = Read-Trx 'final-1/unit.trx' 48
$i1 = Read-Trx 'final-1/integration.trx' 434
$u2 = Read-Trx 'final-2/unit.trx' 48
$i2 = Read-Trx 'final-2/integration.trx' 434
foreach ($pair in @(@($unitBaseline,$u1),@($unitBaseline,$u2),@($i1,$i2))) {
    if (@(Compare-Object $pair[0].names $pair[1].names).Count -ne 0) { throw 'Final identities differ' }
}
$missing = @($integrationBaseline.names | Where-Object { $_ -notin $i1.names -or $_ -notin $i2.names })
if ($missing.Count -ne 0) { throw 'Pre-existing tests missing' }
$new = @($i1.names | Where-Object { $_ -notin $integrationBaseline.names })
if ($new.Count -ne 56 -or @($new | Where-Object { $_ -notmatch 'Mt3c' }).Count -ne 0) { throw 'Unexpected new cases' }
$result = [ordered]@{
    baseline = @{ unit=48; integration=378; total=426; allPreserved=$true }
    final1 = @{ unit=48; integration=434; total=482; failed=0; skipped=0 }
    final2 = @{ unit=48; integration=434; total=482; failed=0; skipped=0 }
    identicalFinalTestSets = $true
    newTests = $new
    newTestCount = $new.Count
    files = @($unitBaseline,$integrationBaseline,$u1,$i1,$u2,$i2) | Select-Object file,total,passed,failed,skipped,sha256
}
$result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'verification.json') -Encoding UTF8
Write-Output 'Verified XML counters and every outcome: 426 baseline preserved; 56 new; two identical final sets, 482/482 each; zero failed/skipped.'
