[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$target = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $target) { throw 'A new release directory is required; existing artifacts are preserved.' }
Push-Location $root
try {
    & python tools/verify-project-artifacts.py
    if ($LASTEXITCODE -ne 0) { throw 'Artifact verification failed' }
    & dotnet publish Backend/ElectronicRegisterAPI/src/Api/ElectronicRegisterAPI/ElectronicRegisterAPI.csproj -c Release -o $target /p:UseAppHost=false /p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed; preserve partial output' }
    # A release must never package the developer's local configuration or keys.
    $sensitive = Get-ChildItem -LiteralPath $target -Recurse -File | Where-Object {
        $_.Name -match '^appsettings(\..+)?\.json$' -or $_.Extension -in @('.pem','.key','.pfx','.p12')
    }
    if ($sensitive) { throw 'Local configuration/key found in publish output. Release refused; do not deploy.' }
    $files = Get-ChildItem -LiteralPath $target -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path=$_.FullName.Substring($target.Length+1).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes=$_.Length }
    }
    $manifest = [ordered]@{ kind='ElectronicRegister application release; no deployment performed'; files=@($files) }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $target 'release-manifest.json') -Encoding utf8
} finally { Pop-Location }
