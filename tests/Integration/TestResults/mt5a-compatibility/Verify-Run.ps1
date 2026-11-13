param([ValidateSet('smoke','baseline','targeted','final-1','final-2')][string]$Run)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
function Read-Results([string]$directory){
    $rows=@();$files=@()
    foreach($file in Get-ChildItem -LiteralPath $directory -Filter '*.trx' -Recurse){
        [xml]$doc=[IO.File]::ReadAllText($file.FullName)
        $results=@($doc.TestRun.Results.UnitTestResult)
        $c=$doc.TestRun.ResultSummary.Counters
        if([int]$c.total-ne $results.Count-or [int]$c.executed-ne $results.Count){throw 'TRX total/executed mismatch'}
        if([int]$c.passed-ne @($results|Where-Object outcome -eq 'Passed').Count-or [int]$c.failed-ne @($results|Where-Object outcome -eq 'Failed').Count){throw 'TRX summary/outcome mismatch'}
        if([int]$c.notExecuted-ne 0){throw 'TRX contains NotExecuted'}
        foreach($r in $results){$rows+=[pscustomobject]@{Name=[string]$r.testName;Outcome=[string]$r.outcome;Project=$file.Directory.Name}}
        $files+=[pscustomobject]@{File=$file.FullName;SHA256=(Get-FileHash -LiteralPath $file.FullName).Hash;Total=[int]$c.total;Executed=[int]$c.executed;Passed=[int]$c.passed;Failed=[int]$c.failed;NotExecuted=[int]$c.notExecuted}
    }
    if(!$rows.Count){throw 'No TRX results'}
    if(@($rows|Group-Object Name|Where-Object Count -gt 1).Count){throw 'Duplicate test identities'}
    return [pscustomobject]@{Rows=$rows;Files=$files}
}
$current=Read-Results (Join-Path $root $Run)
$total=switch($Run){smoke{4}baseline{675}targeted{36}default{711}}
$expectedUnit=if($Run-in @('baseline','final-1','final-2')){51}else{0}
$bad=@($current.Rows|Where-Object Outcome -ne 'Passed')
$unit=@($current.Rows|Where-Object Project -eq 'Unit').Count
$integration=@($current.Rows|Where-Object Project -eq 'Integration').Count
$valid=$current.Rows.Count-eq $total-and $bad.Count-eq 0-and $unit-eq $expectedUnit-and $integration-eq ($total-$expectedUnit)
$historic=Read-Results (Join-Path $root '../mt4c/final-2')
$missing=@();$new=@()
if($Run-eq 'baseline'){$missing=@(Compare-Object ($historic.Rows.Name|Sort-Object) ($current.Rows.Name|Sort-Object));if($missing.Count){$valid=$false}}
if($Run-in @('final-1','final-2')){
    $missing=@($historic.Rows.Name|Where-Object {$_-notin $current.Rows.Name});$new=@($current.Rows.Name|Where-Object {$_-notin $historic.Rows.Name})
    $target=Read-Results (Join-Path $root 'targeted')
    if($missing.Count-or @($new).Count-ne 36-or @(Compare-Object ($new|Sort-Object) ($target.Rows.Name|Sort-Object)).Count){$valid=$false}
}
if($Run-eq 'final-2'){
    $first=Read-Results (Join-Path $root 'final-1')
    if(@(Compare-Object ($first.Rows.Name|Sort-Object) ($current.Rows.Name|Sort-Object)).Count){$valid=$false}
}
[pscustomobject]@{Run=$Run;Valid=$valid;ExpectedTotal=$total;Total=$current.Rows.Count;Unit=$unit;Integration=$integration;Passed=@($current.Rows|Where-Object Outcome -eq 'Passed').Count;Failed=@($current.Rows|Where-Object Outcome -eq 'Failed').Count;OtherOutcomes=@($current.Rows|Where-Object Outcome -notin @('Passed','Failed')).Count;PreviousIdentitiesMissing=$missing.Count;NewIdentities=$new.Count;Files=$current.Files;NonPassed=$bad}|ConvertTo-Json -Depth 7|Set-Content (Join-Path $root "$Run-verification.json")
$current.Rows|ConvertTo-Json -Depth 4|Set-Content (Join-Path $root "$Run-test-identities.json")
Write-Output "$Run total=$($current.Rows.Count) passed=$($current.Rows.Count-$bad.Count) nonpassed=$($bad.Count) unit=$unit integration=$integration valid=$valid"
if(!$valid){exit 1}
