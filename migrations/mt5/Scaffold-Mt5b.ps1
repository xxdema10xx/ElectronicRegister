param([Parameter(Mandatory=$true)][string]$Candidate)
$ErrorActionPreference='Stop'
if(!$env:TEST_MYSQL_CONNECTION){throw 'Explicit isolated connection required'}
$builder=[Data.Common.DbConnectionStringBuilder]::new();$builder.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if($builder['Server']-ne '127.0.0.1'-or $builder['Port']-ne '13306'-or $builder['Database']-cne 'electronic_register_test'){throw 'Wrong scaffold target'}
$Candidate=[IO.Path]::GetFullPath($Candidate)
if(Test-Path -LiteralPath $Candidate){throw 'Never overwrite scaffold candidates'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$names=@('ConnectionStrings__Mt5bScaffold','ConnectionStrings__DefaultConnection','Jwt__Key','Jwt__Issuer','Jwt__Audience','ASPNETCORE_ENVIRONMENT')
$old=@{};foreach($n in $names){$old[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
try {
 $env:ConnectionStrings__Mt5bScaffold=$env:TEST_MYSQL_CONNECTION
 $env:ConnectionStrings__DefaultConnection=$env:TEST_MYSQL_CONNECTION
 $env:Jwt__Key='MT5B-scaffold-only-public-test-key-at-least-32-bytes'
 $env:Jwt__Issuer='mt5b-scaffold';$env:Jwt__Audience='mt5b-scaffold';$env:ASPNETCORE_ENVIRONMENT='Development'
 & dotnet ef dbcontext scaffold 'Name=ConnectionStrings:Mt5bScaffold' Pomelo.EntityFrameworkCore.MySql --no-build `
  --project "$repo/Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure" `
  --startup-project "$repo/Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI" `
  --table institution_membership_security_state --output-dir "$Candidate/Entities" --context-dir $Candidate `
  --context Mt5bScaffoldContext --namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities `
  --context-namespace ElectronicRegisterAPI.Infrastructure.Persistence --no-onconfiguring
 if($LASTEXITCODE-ne 0){throw 'MT5B scaffold failed; preserve candidate'}
 Get-FileHash -LiteralPath "$Candidate/Entities/InstitutionMembershipSecurityState.cs"
 Write-Output 'Candidate only: no legacy entities/context overwritten. Review FK sidecar explicitly.'
} finally {foreach($n in $names){[Environment]::SetEnvironmentVariable($n,$old[$n],'Process')}}
