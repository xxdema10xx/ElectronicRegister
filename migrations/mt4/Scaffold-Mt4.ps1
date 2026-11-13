param([string]$Candidate)
$ErrorActionPreference='Stop'
$b=[System.Data.Common.DbConnectionStringBuilder]::new();$b.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if($b['Database'] -cne 'electronic_register_test' -or $b['Server'] -ne '127.0.0.1'){throw 'Explicit loopback test database required.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$backend=Join-Path $repo 'Backend/ElectronicRegisterAPI'
if(-not $Candidate){$Candidate=Join-Path $PSScriptRoot ('candidate/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))}
$Candidate=[IO.Path]::GetFullPath($Candidate)
if(Test-Path -LiteralPath $Candidate){throw 'Never overwrite a scaffold candidate.'}
& dotnet ef dbcontext scaffold $env:TEST_MYSQL_CONNECTION Pomelo.EntityFrameworkCore.MySql --no-build --project (Join-Path $backend 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure') --startup-project (Join-Path $backend 'src/Api/ElectronicRegisterAPI') --output-dir (Join-Path $Candidate 'Entities') --context-dir $Candidate --context Mt4CandidateContext --namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities --context-namespace ElectronicRegisterAPI.Infrastructure.Persistence --no-onconfiguring
if($LASTEXITCODE -ne 0){throw 'Scaffold failed.'}
foreach($name in @('CourseEdition','CourseEditionCampus','StudentEnrollment')){
 $file=Join-Path $Candidate ('Entities/'+$name+'.cs')
 if(-not [IO.File]::ReadAllText($file).Contains('InstitutionId')){throw ('Missing owner: '+$name)}
 Get-FileHash -LiteralPath $file -Algorithm SHA256
}
Write-Output 'Review candidate. Copy only three new entities; reconcile MT4 sidecar and hook. Preserve all MT1/MT3/MT3A sidecars and legacy generated files.'
