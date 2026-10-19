$ErrorActionPreference = 'Stop'
foreach ($environment in @('Development', 'Production')) {
    $path = Join-Path $PSScriptRoot ('routes-' + $environment + '.json')
    $rows = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    foreach ($row in $rows | Where-Object { !$_.controller }) {
        $row.scheme = 'anonymous Development metadata'
        $row.dataAccess = 'OpenAPI/Scalar schema metadata only; no school records'
        $row.tenantScope = 'no tenant data access'
        $row.rollout = 'Development only'
        $row.authorizationTest = 'Mt3eFinalInventory: OpenAPI Development 200 / Production 404'
    }
    $rows | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
}
$production = Get-Content (Join-Path $PSScriptRoot 'routes-Production.json') -Raw | ConvertFrom-Json
$production | Select-Object method,route,scheme,
    @{n='policy';e={$_.policy -join ','}}, @{n='roles';e={$_.roles -join ','}},
    @{n='manager';e={$_.managers -join ','}}, @{n='repositories';e={$_.repositories -join ','}},
    dataAccess,tenantScope,rollout,authorizationTest |
    Export-Csv (Join-Path $PSScriptRoot 'route-matrix.csv') -NoTypeInformation -Encoding UTF8
Write-Output ('Generated matrix for ' + $production.Count + ' actual Production routes.')
