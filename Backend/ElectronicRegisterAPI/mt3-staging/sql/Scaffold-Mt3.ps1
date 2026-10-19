param([switch]$Apply)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$backend=Join-Path $repo 'Backend/ElectronicRegisterAPI'
$connection=[System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if ($connection.get_Item('Database') -cne 'electronic_register_test') { throw 'Test database required.' }
$candidate=Join-Path $backend 'mt3-evidence/scaffold'
if (-not (Test-Path -LiteralPath (Join-Path $candidate 'Mt3CandidateContext.cs'))) {
 & dotnet ef dbcontext scaffold $env:TEST_MYSQL_CONNECTION Pomelo.EntityFrameworkCore.MySql --project (Join-Path $backend 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure') --startup-project (Join-Path $backend 'src/Api/ElectronicRegisterAPI') --output-dir (Join-Path $candidate 'Entities') --context-dir $candidate --context Mt3CandidateContext --namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities --context-namespace ElectronicRegisterAPI.Infrastructure.Persistence --table institution_student_profile --table institution_teacher_profile --table institution_membership --table student --table teacher --no-onconfiguring
 if ($LASTEXITCODE -ne 0) { throw 'Scaffold failed.' }
}
$generated=[IO.File]::ReadAllText((Join-Path $candidate 'Mt3CandidateContext.cs'))
$start=$generated.IndexOf('        modelBuilder.Entity<InstitutionStudentProfile>')
$finish=$generated.IndexOf('        modelBuilder.Entity<Student>')
if ($start -lt 0 -or $finish -le $start) { throw 'Review generated mapping layout.' }
$mapping=$generated.Substring($start,$finish-$start)
foreach ($type in @('Student','Teacher')) {
 $mapping=$mapping.Replace("WithOne(p => p.Institution${type}Profile)",'WithOne()')
}
$header=@'
// Database-first mapping from migrations/mt3/001_profile_expand.sql. Regenerate with Scaffold-Mt3.ps1.
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;
public partial class ElectronicRegisterContext
{
    public virtual DbSet<InstitutionStudentProfile> InstitutionStudentProfiles { get; set; }
    public virtual DbSet<InstitutionTeacherProfile> InstitutionTeacherProfiles { get; set; }
    private static void ConfigureMt3Profiles(ModelBuilder modelBuilder)
    {
'@
[IO.File]::WriteAllText((Join-Path $candidate 'ElectronicRegisterContext.Mt3Profiles.cs'),$header+"`n"+$mapping+"    }`n}`n")
if ($Apply) {
 $persistence=Join-Path $backend 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence'
 foreach ($name in @('InstitutionStudentProfile','InstitutionTeacherProfile')) { Copy-Item -LiteralPath (Join-Path $candidate "Entities/$name.cs") -Destination (Join-Path $persistence "Entities/$name.cs") }
 Copy-Item -LiteralPath (Join-Path $candidate 'ElectronicRegisterContext.Mt3Profiles.cs') -Destination (Join-Path $persistence 'ElectronicRegisterContext.Mt3Profiles.cs')
 $sidecar=Join-Path $persistence 'ElectronicRegisterContext.Multitenancy.cs'
 $text=[IO.File]::ReadAllText($sidecar)
 if (-not $text.Contains('ConfigureMt3Profiles(modelBuilder);')) {
  $needle="partial void OnModelCreatingPartial(ModelBuilder modelBuilder)`r`n    {"
  if (-not $text.Contains($needle)) { $needle="partial void OnModelCreatingPartial(ModelBuilder modelBuilder)`n    {" }
  if (-not $text.Contains($needle)) { throw 'Review MT1 mapping hook.' }
  [IO.File]::WriteAllText($sidecar,$text.Replace($needle,$needle+"`n        ConfigureMt3Profiles(modelBuilder);"))
 }
}
