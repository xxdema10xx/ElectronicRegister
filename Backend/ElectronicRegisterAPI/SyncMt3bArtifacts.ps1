$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Join-Path $PSScriptRoot 'mt3b-staging'
foreach($name in @('Mt3bSchoolTests.cs','MULTITENANCY_MT3B_REPORT.md')) {
 $path=Join-Path $source $name
 if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $repo ('tests/Integration/'+$name)) -Force}
}
foreach($name in @('MULTITENANCY_DESIGN.md','MULTITENANCY_ISOLATION_MATRIX.md')) {
 $path=Join-Path $source $name
 if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination (Join-Path $repo ('docs/architecture/'+$name)) -Force}
}
$target=Join-Path $repo 'tests/Integration/TestResults/mt3b'
New-Item -ItemType Directory -Force -Path $target | Out-Null
foreach($name in @('baseline','targeted','final-1','final-2','verification.json','initial-git-status.txt','database-preflight.txt','legacy-bridge-config.json','generated-subject-query.sql.txt','build-final.txt','git-diff-check.txt','final-state.json','scaffold-guards.json')) {
 $path=Join-Path $PSScriptRoot ('mt3b-evidence/'+$name)
 if(Test-Path -LiteralPath $path){Copy-Item -LiteralPath $path -Destination $target -Recurse -Force}
}
