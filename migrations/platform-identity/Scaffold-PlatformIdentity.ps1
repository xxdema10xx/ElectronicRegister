param(
    [Parameter(Mandatory=$true)][string]$Candidate,
    [Parameter(Mandatory=$true)][string]$ContainerName,
    [Parameter(Mandatory=$true)][int]$Port
)
# Database First scaffold CANDIDATE for er-platform-identity-1, against a disposable
# mysql:8.4.13 container created for this purpose (label er.platform-identity.disposable=1).
# Never overwrites committed entities: review the candidate, copy deliberately, then
# update tools/CodeOrganization/generated-sources.json.
$ErrorActionPreference='Stop'
if($Port -in @(3306,3307,13306) -or $Port -lt 1024 -or $Port -gt 65535 -or $ContainerName -eq 'mysql84-local'){throw 'Disposable scaffold target required'}
if(!$env:MYSQL_PWD){throw 'MYSQL_PWD must contain the disposable root credential'}
$info=@(((docker inspect $ContainerName) -join "`n")|ConvertFrom-Json)
if($LASTEXITCODE -ne 0 -or $info.Count -ne 1 -or $info[0].Config.Labels.'er.platform-identity.disposable' -cne '1' -or $info[0].Config.Image -cne 'mysql:8.4.13'){throw 'Not a disposable scaffold container'}
if(@((docker port $ContainerName 3306/tcp)|Where-Object {$_ -eq "127.0.0.1:$Port"}).Count -ne 1){throw 'Port binding differs'}
$Candidate=[IO.Path]::GetFullPath($Candidate)
if(Test-Path -LiteralPath $Candidate){throw 'Never overwrite scaffold candidates'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$connection="Server=127.0.0.1;Port=$Port;Database=electronic_register_test;User ID=root;Password=$($env:MYSQL_PWD);SslMode=Disabled"
$names=@('ConnectionStrings__PlatformIdentityScaffold','ConnectionStrings__DefaultConnection','Jwt__Key','Jwt__Issuer','Jwt__Audience','ASPNETCORE_ENVIRONMENT','Deployment__Mode','Deployment__LocalTestDatabasePort')
$old=@{};foreach($n in $names){$old[$n]=[Environment]::GetEnvironmentVariable($n,'Process')}
try {
 $env:ConnectionStrings__PlatformIdentityScaffold=$connection
 $env:ConnectionStrings__DefaultConnection=$connection
 $env:Jwt__Key='platform-identity-scaffold-only-public-test-key-32b'
 $env:Jwt__Issuer='platform-identity-scaffold';$env:Jwt__Audience='platform-identity-scaffold';$env:ASPNETCORE_ENVIRONMENT='Development'
 $env:Deployment__Mode='LocalTest';$env:Deployment__LocalTestDatabasePort=[string]$Port
 & dotnet ef dbcontext scaffold 'Name=ConnectionStrings:PlatformIdentityScaffold' Pomelo.EntityFrameworkCore.MySql --no-build `
  --project "$repo/Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure" `
  --startup-project "$repo/Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI" `
  --table user --table identity_lifecycle_event --output-dir "$Candidate/Entities" --context-dir $Candidate `
  --context PlatformIdentityScaffoldContext --namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities `
  --context-namespace ElectronicRegisterAPI.Infrastructure.Persistence --no-onconfiguring
 if($LASTEXITCODE -ne 0){throw 'Scaffold failed; preserve candidate'}
 Get-ChildItem -Recurse -File $Candidate|Get-FileHash -Algorithm SHA256
 Write-Output 'Candidate only: no committed entity or context overwritten. Review before copying.'
} finally {foreach($n in $names){[Environment]::SetEnvironmentVariable($n,$old[$n],'Process')}}
