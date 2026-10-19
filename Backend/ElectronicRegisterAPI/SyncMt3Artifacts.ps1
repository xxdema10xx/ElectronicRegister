$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$groups=@{ architecture='docs/architecture'; Integration='tests/Integration'; sql='migrations/mt3' }
foreach ($key in $groups.Keys) {
 $target=[IO.Path]::GetFullPath((Join-Path $repo $groups[$key]))
 if (-not $target.StartsWith($repo+[IO.Path]::DirectorySeparatorChar)) { throw 'Invalid target.' }
 New-Item -ItemType Directory -Force -Path $target | Out-Null
 Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot "mt3-staging/$key") -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $target $_.Name) }
}
$evidence=Join-Path $repo 'tests/Integration/TestResults/mt3'
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
foreach ($name in @('baseline','targeted','final-1','final-2','initial-git-status.txt','schema-before.txt','backup-sha256.txt','verification.json','scaffold-sha256.txt')) {
 $source=Join-Path $PSScriptRoot "mt3-evidence/$name"
 if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $evidence -Recurse -Force }
}
