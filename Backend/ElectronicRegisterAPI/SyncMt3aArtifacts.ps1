$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach ($pair in @(@('sql','migrations/mt3'),@('mt1-tooling','migrations/mt1'),@('architecture','docs/architecture'),@('Integration','tests/Integration'))) {
    $source=Join-Path $PSScriptRoot ('mt3a-staging/'+$pair[0])
    $target=[IO.Path]::GetFullPath((Join-Path $repo $pair[1]))
    if (-not $target.StartsWith($repo+[IO.Path]::DirectorySeparatorChar)) { throw 'Invalid destination.' }
    foreach ($file in Get-ChildItem -LiteralPath $source -File) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name) -Force }
}
$evidence=[IO.Path]::GetFullPath((Join-Path $repo 'tests/Integration/TestResults/mt3a'))
if (-not $evidence.StartsWith($repo+[IO.Path]::DirectorySeparatorChar)) { throw 'Invalid evidence destination.' }
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
foreach ($name in @('baseline','final-1','final-2','targeted-4','initial-git-status.txt','schema-columns.tsv','schema-indexes.tsv','schema-fks.tsv','schema-triggers.tsv','legacy-before.json','legacy-after-backfill.json','legacy-preservation.json','verification.json','backup-verification.json','build-final.txt','scaffold-guards.json','scaffold-sha256.json','final-state.json','git-diff-check.txt','sql-preservation.json')) {
 $source=Join-Path $PSScriptRoot ('mt3a-evidence/'+$name)
 if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $evidence -Recurse -Force }
}
