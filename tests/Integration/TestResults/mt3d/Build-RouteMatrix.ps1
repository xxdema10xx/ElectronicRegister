$ErrorActionPreference = 'Stop'
$rows = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'routes-Production.json') -Raw | ConvertFrom-Json
$matrix = foreach ($row in $rows) {
    if ($row.controller -notin @('Exam','Grade','InstitutionExam','InstitutionGrade','InstitutionSubmission','InstitutionClassSubject') -and
        -not ($row.controller -eq 'InstitutionStudent' -and $row.action -eq 'GetAll')) { continue }
    $bound = $row.institutionExam -or $row.school
    $repository = ''; $tables = ''; $test = ''
    switch -Regex ($row.controller) {
        'ClassSubject$' { $repository='Bound ClassSubject projection'; $tables='class_subject, class, subject, teacher'; $test='DropdownAndStudentClassFilterAreScopedAndAssignmentSpecific'; break }
        'Student$' { $repository='InstitutionStudentRepository'; $tables='student, class, class_subject'; $test='DropdownAndStudentClassFilterAreScopedAndAssignmentSpecific'; break }
        'Grade$' {
            $repository='GradeRepository + Student/ClassSubject/Subject repositories (same bound scope)'
            $tables='grade, class_subject, student, subject; exam_submission reference on generic writes'
            $test='GradeCrudPagingFiltersStatisticsAndTeacherOwnershipRemainScoped'; break
        }
        'Submission$' {
            $repository='ExamSubmissionRepository + Exam/Grade/Audit repositories; explicit scope factory for session/violations'
            $tables='exam_submission, submission_answer, submission_answer_option, exam, question, question_option, grade, audit_event'
            $test='AssignedSubmissionIdResumeAndRevisionSurviveNewLoginWithoutNewAttempt'
            if ($row.action -match 'Violation') { $tables='exam_submission, exam_violation'; $test='ViolationsPersistIdempotentlyWithoutPenaltiesAndRejectInvalidEvents' }
            elseif ($row.action -match 'Review|Finalize') { $test='OpenReviewFinalizeOverrideAndRetakeKeepOfficialPassedAndOriginalGrade' }
            elseif ($row.action -match 'Submit') { $test='ManualRequiredUsesContentButExpiryUsesOnlyPersistedPartialAnswers' }
            break
        }
        'Exam$' {
            $repository='ExamRepository + ClassSubject/Student/Submission/Audit repositories (same bound scope)'
            $tables='exam, exam_student, question, question_option, class_subject, student, exam_submission'
            $test='DraftUpdateDeleteAndUnstartedCancellationUseInstitutionalLocationsAndAudit'
            if ($row.action -match 'Publish|Close|Cancel') { $tables+='; audit_event write' }
            if ($row.action -match 'Start|Assigned') { $test='AssignedSubmissionIdResumeAndRevisionSurviveNewLoginWithoutNewAttempt' }
            if ($row.action -match 'Eligible|Assign|UpdateStudents') { $test='AssignmentsRejectForeignWrongClassDuplicatesAndInvalidExtraTimeAtomically' }
            break
        }
    }
    [PSCustomObject]@{
        Method=$row.method; Route=$row.route; Roles=($row.roles -join ';'); Input=($row.input | ConvertTo-Json -Compress)
        Output=$row.output; Manager=($row.managers -join ';'); Repository=$repository; Tables=$tables
        InstitutionCheck=if($bound){'Verified MT2 context; SQL owner predicate; tracked writes owner concurrency token; INSERT explicit owner'}else{'Legacy global repositories; scoped/selection JWT blocked by LegacyEndpointGate'}
        ResourceOwnership=if($bound){'Admin within tenant; Teacher assigned ClassSubject; Student own assignment/submission/grade'}else{'Legacy manager rules preserved; no institutional access granted'}
        ProfileCheck=if($bound){'Membership+User+Institution profile resolver; personal endpoints force Student view; no User profile fallback'}else{'Legacy ClaimsContext only behind gate'}
        Migration=if($row.institutionExam){'MT3D explicit InstitutionExamAccess; both flags required'}elseif($row.school){'MT3C extended optional classId'}else{'Preserved legacy, closed to institutional JWT'}
        HttpTests=if($bound){'Mt3dHttpTests.'+$test+'; EveryExamActionRejectsInvalidJwtBeforeModelBindingOrWrites; EveryResourceActionScopesForeignIdsBeforeReadingLockingOrWriting (ID routes)'}else{'Mt2IdentityTests.EveryLegacyEndpointRejectsBothScopedAndSelectionTokensBeforeReadingOrWriting; AuditBootstrapTests; original phase suites'}
    }
}
$matrix | Export-Csv -LiteralPath (Join-Path $PSScriptRoot 'route-matrix.csv') -NoTypeInformation -Encoding UTF8
Write-Output "Generated $($matrix.Count) route rows from the real runtime inventory."
