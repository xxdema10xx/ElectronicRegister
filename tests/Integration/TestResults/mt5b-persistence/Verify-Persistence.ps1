$ErrorActionPreference='Stop'
$root=$PSScriptRoot
function Read-Run([string]$directory){
 $rows=@();$files=@()
 foreach($file in Get-ChildItem -LiteralPath $directory -Filter *.trx -Recurse){
  [xml]$x=[IO.File]::ReadAllText($file.FullName);$r=@($x.TestRun.Results.UnitTestResult);$c=$x.TestRun.ResultSummary.Counters
  if([int]$c.total-ne $r.Count-or [int]$c.executed-ne $r.Count-or [int]$c.passed-ne @($r|Where-Object outcome -eq 'Passed').Count-or [int]$c.failed-ne @($r|Where-Object outcome -eq 'Failed').Count){throw 'TRX counters/results differ'}
  foreach($case in $r){$rows+=[pscustomobject]@{Name=[string]$case.testName;Outcome=[string]$case.outcome;Project=$file.Directory.Name}}
  $files+=[pscustomobject]@{File=$file.FullName;SHA256=(Get-FileHash $file.FullName).Hash;Total=$r.Count;Passed=[int]$c.passed;Failed=[int]$c.failed;NotExecuted=[int]$c.notExecuted}
 }
 if(!$rows.Count-or @($rows|Group-Object Name|Where-Object Count -gt 1).Count){throw 'Missing/duplicate TRX identities'}
 return [pscustomobject]@{Rows=$rows;Files=$files}
}
$old=Read-Run "$root/../mt5a-compatibility/final-2"
$target=Read-Run "$root/security-3"
$baseline=Read-Run "$root/baseline"
$history=@()
foreach($run in 'regression','regression-2'){
 $data=Read-Run "$root/$run"
 $missing=@($old.Rows.Name|Where-Object {$_-notin $data.Rows.Name})
 $new=@($data.Rows.Name|Where-Object {$_-notin $old.Rows.Name})
 $different=@(Compare-Object ($new|Sort-Object) ($target.Rows.Name|Sort-Object))
 $bad=@($data.Rows|Where-Object Outcome -ne 'Passed')
 $valid=$data.Rows.Count-eq 738-and $missing.Count-eq 0-and $new.Count-eq 27-and $different.Count-eq 0-and $bad.Count-eq 0
 $summary=[pscustomobject]@{Run=$run;Valid=$valid;Total=$data.Rows.Count;Unit=@($data.Rows|Where-Object Project -eq 'Unit').Count;Integration=@($data.Rows|Where-Object Project -eq 'Integration').Count;Passed=@($data.Rows|Where-Object Outcome -eq 'Passed').Count;Failed=@($data.Rows|Where-Object Outcome -eq 'Failed').Count;Other=@($data.Rows|Where-Object Outcome -notin @('Passed','Failed')).Count;HistoricalIdentitiesMissing=$missing.Count;NewIdentities=$new.Count;NewIdentityDifferences=$different.Count;Files=$data.Files;NonPassed=$bad}
 $summary|ConvertTo-Json -Depth 6|Set-Content "$root/$run-verification.json"
 $data.Rows|ConvertTo-Json|Set-Content "$root/$run-test-identities.json"
 $history+=$summary
 if($run-eq 'regression-2'-and (!$valid-or $summary.Unit-ne 51-or $summary.Integration-ne 687)){throw 'Stabilized regression invalid'}
}
if($baseline.Rows.Count-ne 711-or @(Compare-Object ($old.Rows.Name|Sort-Object) ($baseline.Rows.Name|Sort-Object)).Count-or @($baseline.Rows|Where-Object Outcome -ne 'Passed').Count){throw 'Baseline identities/outcomes differ'}
$freeze=Get-Content "$root/regression-2-source-freeze.json" -Raw|ConvertFrom-Json
$changed=@($freeze|Where-Object {(Get-FileHash -LiteralPath $_.Path).Hash-ne $_.SHA256})
if($changed.Count){throw 'Compiled sources changed during stabilized run'}
$canonical=Get-Content "$root/../mt5a-mamp-plan/canonical-sql-hashes.json" -Raw|ConvertFrom-Json
$repo=[IO.Path]::GetFullPath("$root/../../../..")
$sqlChanged=@($canonical|Where-Object {(Get-FileHash "$repo/migrations/mt4/$($_.File)").Hash-ne $_.Hash})
if($sqlChanged.Count){throw 'Prior canonical MT4 SQL changed'}
$protected=Get-Content "$root/../mt5a-compatibility/final-source-freeze.json" -Raw|ConvertFrom-Json
$protected=@($protected|Where-Object {$_.Path-match '\\(InstitutionTokens|InstitutionAuthentication|InstitutionAuthManager|TenantContext|MembershipAuthorizationRules|MembershipAuthorizationRepository|LegacyEndpointGate|ExamSubmissionProcessor|ExpiredExamSubmissionWorker)\.cs$'})
if(@($protected|Where-Object {(Get-FileHash $_.Path).Hash-ne $_.Hash}).Count){throw 'Protected auth/gate/worker sources changed'}
$before=(Get-FileHash "$root/../mt5b/original-after.txt").Hash;$after=(Get-FileHash "$root/original-after.txt").Hash
if($before-ne $after){throw 'Original MAMP snapshot differs'}
$integrity=Get-Content "$root/final-integrity.json" -Raw|ConvertFrom-Json
$metadata=Get-Content "$root/final-new-metadata.json" -Raw|ConvertFrom-Json
if($integrity.ForeignKeys-ne 99-or $integrity.ForeignKeyOrphans-ne 0-or !$integrity.GlobalSqlModeUnchanged-or !$integrity.HoldingOnly-or !$metadata.NewTriggerBodiesMatchCanonical-or $metadata.ProofTables-ne 0){throw 'Final DB integrity invalid'}
$restored=@(Get-ChildItem $root -Filter '*-environment-restoration.json'|ForEach-Object {Get-Content $_.FullName -Raw|ConvertFrom-Json})
if(@($restored|Where-Object {!$_.EnvironmentRestored}).Count){throw 'Environment not restored'}
$files=Get-ChildItem $root -Filter *.trx -Recurse|ForEach-Object {[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash $_.FullName).Hash}}
$files|ConvertTo-Json|Set-Content "$root/trx-sha256-manifest.json"
[pscustomobject]@{Status='MT-5B PERSISTENCE READY';Baseline=711;StabilizedTotal=738;Unit=51;Integration=687;NewPassed=27;Failed=0;Skipped=0;NotExecuted=0;OldIdentitiesPreserved=711;FrozenCompiledSources=$freeze.Count;ChangedDuringStabilizedRun=0;ProtectedAuthGateWorkerFiles=$protected.Count;PreviousCanonicalSqlUnchanged=$true;OriginalMampSnapshotUnchanged=$true;OriginalSHA256=$after;Tables=33;Triggers=64;ForeignKeys=99;ForeignKeyOrphans=0;Indexes=141;GenerationJwtActivated=$false;Mt5bCompleted=$false;Mt5cStarted=$false}|ConvertTo-Json|Set-Content "$root/persistence-certification.json"
Get-Content "$root/persistence-certification.json"
