param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$backend = Join-Path $repository 'Backend/ElectronicRegisterAPI'
if ($Apply -and (Test-Path -LiteralPath (Join-Path $backend 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.Mt3aOwnership.cs'))) {
    throw 'MT3A is installed. MT1-only Apply would lose later hooks: generate/review with migrations/mt3/Scaffold-Mt3a.ps1; no files copied.'
}
if (!$env:TEST_MYSQL_CONNECTION) { throw 'TEST_MYSQL_CONNECTION is required; no production fallback.' }
$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if (!$connection.ContainsKey('Database') -or $connection.get_Item('Database') -cne 'electronic_register_test') {
    throw 'Scaffold MT-1 only from electronic_register_test.'
}
$candidate = Join-Path $backend '.mt1-scaffold-candidate'
# No --force: retain an existing candidate for review instead of deleting it automatically.
& dotnet ef dbcontext scaffold $env:TEST_MYSQL_CONNECTION Pomelo.EntityFrameworkCore.MySql `
    --project (Join-Path $backend 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure') `
    --startup-project (Join-Path $backend 'src/Api/ElectronicRegisterAPI') `
    --output-dir (Join-Path $candidate 'Entities') --context-dir $candidate --context Mt1ScaffoldContext `
    --namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities --context-namespace ElectronicRegisterAPI.Infrastructure.Persistence `
    --table institution --table campus --table institution_membership --table institution_membership_role --table user --no-onconfiguring
if ($LASTEXITCODE -ne 0) { throw 'Scaffolding failed.' }
$generated = [IO.File]::ReadAllText((Join-Path $candidate 'Mt1ScaffoldContext.cs'))
$start = $generated.IndexOf('        modelBuilder.Entity<Campus>')
$finish = $generated.IndexOf('        modelBuilder.Entity<User>')
if ($start -lt 0 -or $finish -le $start) { throw 'Generated layout changed: review mappings manually.' }
$mapping = $generated.Substring($start, $finish-$start).Replace('entity.HasOne(d => d.User).WithMany(p => p.InstitutionMemberships)', 'entity.HasOne(d => d.User).WithMany()')
$header = @'
// MT-1 sidecar: derived from migrations/mt1/001_expand.sql by database-first scaffolding.
// Kept outside generated ElectronicRegisterContext.cs. Do not scaffold over this file.
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ElectronicRegisterAPI.Infrastructure.Persistence;

public partial class ElectronicRegisterContext
{
    public virtual DbSet<Institution> Institutions { get; set; }
    public virtual DbSet<Campus> Campuses { get; set; }
    public virtual DbSet<InstitutionMembership> InstitutionMemberships { get; set; }
    public virtual DbSet<InstitutionMembershipRole> InstitutionMembershipRoles { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
'@
[IO.File]::WriteAllText((Join-Path $candidate 'ElectronicRegisterContext.Multitenancy.cs'), $header + "`n" + $mapping + "    }`n}`n")
if ($Apply) {
    $persistence = Join-Path $backend 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence'
    foreach ($name in 'Institution','Campus','InstitutionMembership','InstitutionMembershipRole') {
        Copy-Item -LiteralPath (Join-Path $candidate "Entities/$name.cs") -Destination (Join-Path $persistence "Entities/$name.cs")
    }
    Copy-Item -LiteralPath (Join-Path $candidate 'ElectronicRegisterContext.Multitenancy.cs') -Destination (Join-Path $persistence 'ElectronicRegisterContext.Multitenancy.cs')
}
Write-Output 'Review candidate and git diff. Generated legacy User/context are never copied. Rebuild and rerun all tests.'
