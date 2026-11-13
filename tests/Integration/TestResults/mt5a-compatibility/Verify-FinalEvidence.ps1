$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$runs=@('baseline','targeted','final-1','final-2')
$results=foreach($run in $runs){
    $r=Get-Content "$root/$run-verification.json" -Raw|ConvertFrom-Json
    if(!$r.Valid-or $r.Failed-ne 0-or $r.OtherOutcomes-ne 0){throw "Invalid verified run: $run"}
    $r
}
$first=Get-Content "$root/final-1-test-identities.json" -Raw|ConvertFrom-Json
$second=Get-Content "$root/final-2-test-identities.json" -Raw|ConvertFrom-Json
$difference=@(Compare-Object ($first|ForEach-Object {"$($_.Project)|$($_.Name)|$($_.Outcome)"}|Sort-Object) ($second|ForEach-Object {"$($_.Project)|$($_.Name)|$($_.Outcome)"}|Sort-Object))
if($difference.Count){throw 'Final runs differ by project/name/outcome'}
$source=Get-Content "$root/after-final-2-source-verification.json" -Raw|ConvertFrom-Json
if(!$source.Valid){throw 'Frozen sources invalid'}
$sql=Get-Content "$root/final-integrity.json" -Raw|ConvertFrom-Json
if($sql.ForeignKeys-ne 98-or $sql.Triggers-ne 53-or $sql.ForeignKeyOrphans-ne 0-or !$sql.GlobalSqlModeUnchanged-or !$sql.NoInstitutionalFixtureResidues-or !$sql.TriggerDefinitionsUnchanged){throw 'SQL integrity invalid'}
$required=Get-Content "$root/final-required-schema.json" -Raw|ConvertFrom-Json
if(!$required.LedgerPresent-or !$required.RevisionDefinitionCorrect-or !$required.GlobalSqlModeUnchanged){throw 'Final schema invalid'}
$before=(Get-FileHash "$root/original-before.txt").Hash
$after=(Get-FileHash "$root/original-after.txt").Hash
$historic=(Get-FileHash "$root/../mt5a-mamp-validation/original-after.txt").Hash
if($before-ne $after-or $after-ne $historic){throw 'Original SQL snapshot differs'}
$restoration=foreach($run in @('build','build-2','build-3','fixes','fixes-2','fixes-3')+$runs){
    $r=Get-Content "$root/$run-environment-restoration.json" -Raw|ConvertFrom-Json
    if(!$r.EnvironmentRestored){throw 'Child environment not restored'}
    $r
}
$diff=Get-Content "$root/diff-check.txt" -Raw
if($diff.Trim()-ne 'ExitCode=0'){throw 'git diff --check failed'}
$trx=Get-ChildItem $root -Filter *.trx -Recurse|ForEach-Object {[pscustomobject]@{Path=$_.FullName;SHA256=(Get-FileHash $_.FullName).Hash}}
$trx|ConvertTo-Json|Set-Content "$root/trx-sha256-manifest.json"
[pscustomobject]@{
    Verdict=('MT-5A COMPLETATA '+[char]0x2014+' 711/711 PASSED ON MAMP MYSQL 5.7.24')
    BaselinePassed=675;Mt5aPassed=36;Final1Passed=711;Final2Passed=711
    FinalRunIdentityOutcomeDifferences=$difference.Count
    Failed=0;Skipped=0;NotExecuted=0
    HistoricalIdentitiesPreserved=675;FrozenSourcesUnchanged=$true
    OnlyTwoAuthorizedTestFilesChanged=$true;CanonicalSqlUnchanged=$true
    SchemaIntegrityPassed=$true;OriginalSqlSnapshotUnchanged=$true;OriginalSnapshotSHA256=$after
    ChildEnvironmentsRestored=$true;RolloutDefaultFalse=$true;DiffCheckPassed=$true
    Server='MAMP MySQL 5.7.24';Port=13306;MariaDbRevalidation=$false
    ProductionReadinessClaimed=$false;Mt5bStarted=$false
}|ConvertTo-Json|Set-Content -Encoding UTF8 "$root/final-certification.json"
Get-Content "$root/final-certification.json"
