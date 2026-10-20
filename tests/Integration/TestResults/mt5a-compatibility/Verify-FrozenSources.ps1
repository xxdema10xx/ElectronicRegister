param([string]$Label='final')
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot/../../../..").Path
$freeze=Get-Content "$PSScriptRoot/final-source-freeze.json" -Raw|ConvertFrom-Json
$changed=@($freeze|Where-Object {(Get-FileHash -LiteralPath $_.Path).Hash-ne $_.Hash})
$historic=Get-Content "$PSScriptRoot/../mt5a/source-freeze.json" -Raw|ConvertFrom-Json
$historicalChanges=@($historic|Where-Object {(Get-FileHash -LiteralPath $_.Path).Hash-ne $_.Hash})
$allowed=@('tests/Integration/Mt3dHttpTests.cs','tests/Integration/Mt3eWorkerTests.cs')
$outside=@($historicalChanges|Where-Object {$_.Path.Substring($repo.Length+1).Replace('\','/')-notin $allowed})
$canonical=Get-Content "$PSScriptRoot/../mt5a-mamp-plan/canonical-sql-hashes.json" -Raw|ConvertFrom-Json
$sqlChanged=@($canonical|Where-Object {(Get-FileHash -LiteralPath "$repo/migrations/mt4/$($_.File)").Hash-ne $_.Hash})
$example=Get-Content "$repo/Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/appsettings.example.json" -Raw|ConvertFrom-Json
$valid=$freeze.Count-eq 370-and $changed.Count-eq 0-and $outside.Count-eq 0-and $historicalChanges.Count-eq 2-and $canonical.Count-eq 6-and $sqlChanged.Count-eq 0-and $example.Multitenancy.MembershipAdminApi.Enabled-eq $false
[pscustomobject]@{Valid=$valid;FrozenSources=$freeze.Count;ChangedSinceFreeze=$changed.Count;ChangedSinceHistoricalFreeze=$historicalChanges.Count;ChangesOutsideAllowedTests=$outside.Count;CanonicalSql=$canonical.Count;ChangedCanonicalSql=$sqlChanged.Count;MembershipAdminApiDefaultFalse=($example.Multitenancy.MembershipAdminApi.Enabled-eq $false)}|ConvertTo-Json|Set-Content "$PSScriptRoot/$Label-source-verification.json"
if(!$valid){throw 'Source/canonical SQL/rollout verification failed'}
Write-Output '370 frozen sources unchanged; only two authorized tests differ from historical freeze; six canonical scripts unchanged; rollout false'
