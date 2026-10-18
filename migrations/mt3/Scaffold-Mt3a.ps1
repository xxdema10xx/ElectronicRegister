param([string]$Candidate)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$backend=Join-Path $repo 'Backend/ElectronicRegisterAPI'
$b=[System.Data.Common.DbConnectionStringBuilder]::new()
$b.set_ConnectionString($env:TEST_MYSQL_CONNECTION)
if ($b.get_Item('Database') -cne 'electronic_register_test' -or $b.get_Item('Server') -ne '127.0.0.1') { throw 'Explicit loopback test database required.' }
if (-not $Candidate) { $Candidate=Join-Path $backend ('mt3a-evidence/scaffold-'+(Get-Date -Format 'yyyyMMdd-HHmmss')) }
$Candidate=[IO.Path]::GetFullPath($Candidate)
if (Test-Path -LiteralPath $Candidate) { throw 'Candidate exists; never overwrite generated sources blindly.' }
& dotnet ef dbcontext scaffold $env:TEST_MYSQL_CONNECTION Pomelo.EntityFrameworkCore.MySql --project (Join-Path $backend 'src/Infrastructure/ElectronicRegisterAPI.Infrastructure') --startup-project (Join-Path $backend 'src/Api/ElectronicRegisterAPI') --output-dir (Join-Path $Candidate 'Entities') --context-dir $Candidate --context Mt3aCandidateContext --namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities --context-namespace ElectronicRegisterAPI.Infrastructure.Persistence --no-onconfiguring
if ($LASTEXITCODE -ne 0) { throw 'Scaffold failed.' }
$mapping=[IO.File]::ReadAllText((Join-Path $Candidate 'Mt3aCandidateContext.cs'))
foreach ($table in @('Biennium','StudyArea','StudyPath','Subject','Teacher','Student','BienniumStudyArea','BienniumStudyPath','Class','ClassSubject','Exam','ExamStudent','Grade','ExamSubmission','Question','QuestionOption','SubmissionAnswer','SubmissionAnswerOption','ExamViolation','AuditEvent')) {
 if (-not (Test-Path -LiteralPath (Join-Path $Candidate ('Entities/'+$table+'.cs')))) { throw ('Missing candidate '+$table) }
 if (-not ([IO.File]::ReadAllText((Join-Path $Candidate ('Entities/'+$table+'.cs')))).Contains('InstitutionId')) { throw ('Missing owner '+$table) }
}
foreach ($constraint in @('fk_mt3a_student_profile_owner','fk_mt3a_teacher_profile_owner','fk_mt3a_submission_answer_question','fk_mt3a_submission_answer_option_option')) {
 if (-not $mapping.Contains($constraint)) { throw ('Missing generated relationship '+$constraint) }
}
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $Candidate 'Mt3aCandidateContext.cs')
Write-Output 'Candidate verified. Do not copy over generated legacy sources/MT1/MT3 sidecars.'
Write-Output 'Production extension maps ownership as shadow properties and changes catalog indexes only. Composite constraints remain SQL-only, avoiding duplicate EF navigation relationships.'
