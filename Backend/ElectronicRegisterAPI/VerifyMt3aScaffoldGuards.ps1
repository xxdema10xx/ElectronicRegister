$ErrorActionPreference='Stop'
$persistence=Join-Path $PSScriptRoot 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence'
function Get-MappingDigest {
 $values=Get-ChildItem -LiteralPath $persistence -Recurse -Filter '*.cs' | Sort-Object FullName | Get-FileHash -Algorithm SHA256
 return ($values.Hash -join '|')
}
$before=Get-MappingDigest
$results=@()
foreach ($script in @('../../migrations/mt1/Scaffold-Mt1.ps1','../../migrations/mt3/Scaffold-Mt3.ps1')) {
 $path=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $script))
 $oldPreference=$ErrorActionPreference
 $ErrorActionPreference='Continue'
 $message=(& powershell -NoProfile -File $path -Apply 2>&1 | Out-String)
 $exit=$LASTEXITCODE
 $ErrorActionPreference=$oldPreference
 if ($exit -eq 0 -or -not $message.Contains('MT3A is installed')) { throw ('Missing scaffold protection: '+$script) }
 $results+=@{Script=$script;ApplyRejectedBeforeCopy=$true}
}
$after=Get-MappingDigest
if ($before -cne $after) { throw 'Scaffold guard modified production sources.' }
@{Guards=$results;AllPersistenceSourceHashesUnchanged=$true} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mt3a-evidence/scaffold-guards.json')
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $PSScriptRoot 'mt3a-evidence/scaffold-final/Mt3aCandidateContext.cs') | Select-Object Algorithm,Hash | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'mt3a-evidence/scaffold-sha256.json')
Write-Output 'Both unsafe partial Apply paths rejected; all persistence source hashes unchanged.'
