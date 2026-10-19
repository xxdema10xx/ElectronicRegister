$ErrorActionPreference = 'Stop'
function Read-Trx([string]$relative, [int]$expected) {
    $path = Join-Path $PSScriptRoot $relative
    [xml]$document = Get-Content -LiteralPath $path
    $c = $document.TestRun.ResultSummary.Counters
    $results = @($document.TestRun.Results.UnitTestResult)
    if ([int]$c.total -ne $expected -or [int]$c.passed -ne $expected -or [int]$c.executed -ne $expected -or
        [int]$c.failed -ne 0 -or [int]$c.notExecuted -ne 0 -or $results.Count -ne $expected -or
        @($results | Where-Object outcome -ne 'Passed').Count -ne 0) { throw "Invalid counters/outcomes: $relative" }
    $names = @($results | ForEach-Object testName | Sort-Object)
    if (@($names | Select-Object -Unique).Count -ne $expected) { throw "Duplicate identity: $relative" }
    [PSCustomObject]@{ file=$relative; total=$expected; passed=$expected; failed=0; skipped=0; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; names=$names }
}
$u0=Read-Trx 'baseline/unit.trx' 51
$i0=Read-Trx 'baseline/integration.trx' 489
$u1=Read-Trx 'final-1/unit.trx' 51
$i1=Read-Trx 'final-1/integration.trx' 526
$u2=Read-Trx 'final-2/unit.trx' 51
$i2=Read-Trx 'final-2/integration.trx' 526
foreach($pair in @(@($u1,$u2),@($i1,$i2))) {
    if(@(Compare-Object $pair[0].names $pair[1].names).Count){ throw 'Final test sets differ' }
}
if(@($u0.names | Where-Object { $_ -notin $u1.names -or $_ -notin $u2.names }).Count -or
    @($i0.names | Where-Object { $_ -notin $i1.names -or $_ -notin $i2.names }).Count){ throw 'Missing pre-existing tests' }
$newUnit=@($u1.names | Where-Object { $_ -notin $u0.names })
$newIntegration=@($i1.names | Where-Object { $_ -notin $i0.names })
if($newUnit.Count -ne 0 -or $newIntegration.Count -ne 37){throw 'Unexpected new test count'}
$result=[ordered]@{
    baseline=@{unit=51;integration=489;total=540;allPreserved=$true}
    final1=@{unit=51;integration=526;total=577;failed=0;skipped=0}
    final2=@{unit=51;integration=526;total=577;failed=0;skipped=0}
    identicalFinalSets=$true; newTestCount=37; newUnit=$newUnit; newIntegration=$newIntegration
    files=@($u0,$i0,$u1,$i1,$u2,$i2) | Select-Object file,total,passed,failed,skipped,sha256
}
$result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'verification.json') -Encoding UTF8
Write-Output 'Verified: 540 previous tests preserved; 37 new; identical final sets; 577/577 twice; zero failed/skipped.'
