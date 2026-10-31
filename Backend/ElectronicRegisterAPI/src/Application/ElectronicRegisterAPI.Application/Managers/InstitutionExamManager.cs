using ElectronicRegisterAPI.Domain.Authorization;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using System.Text.Json;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionExamManager(
    InstitutionSchoolAuthorization access,
    IInstitutionProfileResolver profiles,
    IInstitutionExamRepositoryFactory factory,
    ITransactionManager transactions,
    ISubmissionGradingService grading,
    ITimeProvider time,
    IInstitutionWriteAuthorization writeAuthorization
) : IInstitutionExamManager
{
    private enum Operation
    {
        Manage,
        Student,
        Read,
        Teaching
    }

    private sealed record Work(
        ExamPrincipal Actor,
        IInstitutionExamRepositories Repositories,
        ExamManager Exams,
        ExamSubmissionManager Reviews,
        GradeManager Grades,
        ExamSubmissionProcessor Processor,
        ITransactionManager Transactions
    );
    private sealed class AuthorizedTransaction(
        ITransactionManager inner,
        IInstitutionWriteAuthorization authorization,
        TenantContext actor,
        CancellationToken requestCt
    ) : ITransactionManager
    {
        public Task ExecuteAsync(Func<Task> action, CancellationToken ct = default) => inner.ExecuteAsync(async () =>
        {
            await authorization.LockExamAsync(actor, requestCt);
            await action();
        }, requestCt);
    }

    private async Task<Work> Open(Operation operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var t = await access.RequireAsync(ct);
        var studentView = operation == Operation.Student || operation == Operation.Read && !t.Roles.Contains(InstitutionRole.InstitutionAdmin) && !t.Roles.Contains(InstitutionRole.Staff) && !t.Roles.Contains(InstitutionRole.Teacher);
        Guid? student = null, teacher = null;
        var combinedRead = operation == Operation.Read && t.Roles.Contains(InstitutionRole.Teacher) && t.Roles.Contains(InstitutionRole.Student) && !t.Roles.Contains(InstitutionRole.InstitutionAdmin) && !t.Roles.Contains(InstitutionRole.Staff);
        if (studentView)
            student = await profiles.RequireStudentAsync(t, ct);
        else if (!t.Roles.Contains(InstitutionRole.InstitutionAdmin) && (operation == Operation.Teaching || !t.Roles.Contains(InstitutionRole.Staff)))
            teacher = await profiles.RequireTeacherAsync(t, ct);
        if (combinedRead)
            student = await profiles.RequireStudentAsync(t);
        var actor = new ExamPrincipal(t, studentView, student, teacher, combinedRead);
        var r = factory.Create(actor, ct);
        ITransactionManager tx = new AuthorizedTransaction(transactions, writeAuthorization, t, ct);
        var gradeRules = new GradeService(r.ClassSubjects);
        var processor = new ExamSubmissionProcessor(tx, r.Submissions, r.Exams, grading, r.Grades, r.Audit, time);
        return new(actor, r, new ExamManager(r.Exams, new ExamService(r.Exams), r.ClassSubjects, r.Students, r.Grades, r.Submissions, new SubjectService(r.ClassSubjects), tx, r.Audit, time), new ExamSubmissionManager(r.Submissions, r.Exams, r.Students, r.Grades, r.Audit, grading, tx, r.ClassSubjects, gradeRules), new GradeManager(r.Grades, r.Subjects, r.Students, r.ClassSubjects, gradeRules), processor, tx);
    }

    private async Task<T> Run<T>(Operation operation, Func<Work, Task<T>> action, CancellationToken ct) => await action(await Open(operation, ct));
    private async Task Run(Operation operation, Func<Work, Task> action, CancellationToken ct) => await action(await Open(operation, ct));
    public Task<List<ClassSubjectChoiceDto>> ClassSubjectsAsync(CancellationToken ct = default) => Run(Operation.Manage, w => w.Repositories.ClassSubjectChoicesAsync(), ct);
    public Task<List<StudentDto>> EligibleStudentsAsync(Guid examId, CancellationToken ct = default) => Run(Operation.Manage, async w =>
    {
        var exam = await w.Repositories.Exams.GetByIdAsync(examId) ?? throw new KeyNotFoundException("La risorsa non esiste.");
        var cs = await w.Repositories.ClassSubjects.GetByIdAsync(exam.ClassSubjectId) ?? throw new KeyNotFoundException("La risorsa non esiste.");
        return await w.Repositories.StudentsForClassAsync(cs.ClassId);
    }, ct);
    public Task<List<ExamSummaryDto>> ExamsAsync(CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.GetAllAsync(w.Actor), ct);
    public Task<ExamDetailDto?> ExamAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.GetByIdAsync(id, w.Actor), ct);
    public Task<Guid> CreateAsync(CreateExamDto dto, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.AddAsync(dto, w.Actor), ct);
    public Task UpdateAsync(Guid id, UpdateExamDto dto, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.UpdateAsync(id, dto, w.Actor), ct);
    public Task AssignAsync(Guid id, UpdateExamStudentsDto dto, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.UpdateStudentsAsync(id, dto, w.Actor), ct);
    public Task PublishAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.PublishAsync(id, w.Actor), ct);
    public Task CloseAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.CloseAsync(id, w.Actor), ct);
    public Task CancelAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.CancelAsync(id, w.Actor), ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, w => w.Exams.DeleteAsync(id, w.Actor), ct);
    public Task<List<AssignedExamDto>> AssignedAsync(CancellationToken ct = default) => Run(Operation.Student, w => w.Exams.GetAssignedAsync(w.Actor), ct);
    public Task<StartExamDto> StartAsync(Guid id, CancellationToken ct = default) => Run(Operation.Student, w => w.Exams.StartAsync(id, w.Actor), ct);
    public Task<SubmissionSessionDto> SessionAsync(Guid id, CancellationToken ct = default) => Run(Operation.Student, w => w.Repositories.SessionAsync(id), ct);
    public Task<SaveAnswersResponseDto> SaveAsync(Guid id, SaveExamAnswersDto dto, CancellationToken ct = default) => Run(Operation.Student, async w =>
    {
        SaveAnswersResponseDto result = null!;
        await w.Transactions.ExecuteAsync(async () => result = await w.Exams.SaveAnswersAsync(id, dto, w.Actor));
        return result;
    }, ct);
    public Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, CancellationToken ct = default) => Run(Operation.Student, w => w.Processor.ProcessInstitutionAsync(id, dto, SubmitReason.Manual, w.Actor), ct);
    // Explicit authenticated owner-triggered expiry. No payload/late answers; worker scan stays deferred.
    public Task<SubmissionResultDto> SubmitExpiredAsync(Guid id, CancellationToken ct = default) => Run(Operation.Student, w => w.Processor.ProcessInstitutionAsync(id, null, SubmitReason.TimeExpired, w.Actor), ct);
    public Task<ViolationReceiptDto> ReportViolationAsync(Guid id, InstitutionViolationDto dto, CancellationToken ct = default) => Run(Operation.Student, async w =>
    {
        ViolationReceiptDto? result = null;
        await w.Transactions.ExecuteAsync(async () => result = await w.Repositories.ReportViolationAsync(id, dto));
        return result!;
    }, ct);
    public Task<List<ViolationEventDto>> ViolationsAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, w => w.Repositories.ViolationsAsync(id), ct);
    public Task<List<SubmissionSummaryDto>> SubmissionsAsync(Guid examId, SubmissionStatus? status, CancellationToken ct = default) => Run(Operation.Manage, w => w.Reviews.GetByExamAsync(examId, status, w.Actor), ct);
    public Task<SubmissionReviewDto> OwnWorkAsync(Guid id, CancellationToken ct = default) => Run(Operation.Student, w => w.Reviews.GetByIdAsync(id, w.Actor), ct);
    public Task<SubmissionReviewDto> ReviewAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, w => w.Reviews.GetByIdAsync(id, w.Actor), ct);
    public Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto, CancellationToken ct = default) => Run(Operation.Teaching, w => w.Reviews.ReviewAsync(id, dto, w.Actor), ct);
    public Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto, CancellationToken ct = default) => Run(Operation.Teaching, w => w.Reviews.FinalizeAsync(id, dto, w.Actor), ct);
    public async Task<SubmissionResultDto> ResultAsync(Guid id, CancellationToken ct = default)
    {
        var w = await Open(Operation.Read, ct);
        try
        {
            return await w.Exams.GetResultAsync(id, w.Actor);
        }
        catch (KeyNotFoundException)when (w.Actor.CombinedRead)
        {
            var own = await Open(Operation.Student, ct);
            return await own.Exams.GetResultAsync(id, own.Actor);
        }
    }

    public Task<List<GradeDto>> GradesAsync(CancellationToken ct = default) => Run(Operation.Read, w => w.Grades.GetAllAsync(w.Actor), ct);
    public Task<GradeDto?> GradeAsync(Guid id, CancellationToken ct = default) => Run(Operation.Read, w => w.Grades.GetByIdAsync(id, w.Actor), ct);
    public Task<int> GradeCountAsync(CancellationToken ct = default) => Run(Operation.Read, w => w.Grades.CountAsync(w.Actor), ct);
    public Task<GradePageDto> GradePageAsync(
        int pageNumber,
        int pageSize,
        Guid? subjectId,
        Guid? studentId,
        DateOnly? date,
        CancellationToken ct = default
    ) => Run(Operation.Read, w => w.Grades.GetPagedAsync(pageNumber, pageSize, subjectId, studentId, date, w.Actor), ct);
    public Task<GradeFiltersDto> GradeFiltersAsync(CancellationToken ct = default) => Run(Operation.Read, w => w.Grades.GetFiltersAsync(w.Actor), ct);
    public Task<GradeStatisticsDto?> GradeStatisticsAsync(CancellationToken ct = default) => Run(Operation.Read, w => w.Grades.GetStatisticsAsync(w.Actor), ct);
    private Task<T> GradeWrite<T>(
        InstitutionPermission permission,
        Func<Work, Task<T>> action,
        Guid? id = null,
        CancellationToken ct = default
    ) => Run(Operation.Manage, async w =>
    {
        InstitutionPermissions.Require(w.Actor.Tenant!, permission);
        T result = default!;
        await w.Transactions.ExecuteAsync(async () =>
        {
            var before = id.HasValue ? await w.Repositories.Grades.GetByIdAsync(id.Value) : null;
            result = await action(w);
            var changedId = id ?? (result is Guid created ? created : (Guid? )null);
            if (w.Actor.Tenant!.Roles.Contains(InstitutionRole.Staff) && changedId.HasValue && result is not false)
            {
                var after = await w.Repositories.Grades.GetByIdAsync(changedId.Value);
                await w.Repositories.Audit.AddAsync(new AuditEvent { Id = Guid.NewGuid(), ActorUserId = w.Actor.UserId, EntityType = "grade", EntityId = changedId.Value, Action = permission == InstitutionPermission.GradeCreate ? "staff.grade.create" : "staff.grade.update", BeforeJson = before is null ? null : JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(new { ActorMembershipId = w.Actor.Tenant.MembershipId, Grade = after }), CreatedAt = time.UtcNow });
            }
        });
        return result;
    }, ct);
    public Task<Guid?> AddGradeAsync(CreateGradeDto dto, CancellationToken ct = default) => GradeWrite(InstitutionPermission.GradeCreate, w => w.Grades.AddAsync(dto, w.Actor), ct: ct);
    public Task<bool> UpdateGradeAsync(Guid id, UpdateGradeDto dto, CancellationToken ct = default) => GradeWrite(InstitutionPermission.GradeUpdate, w => w.Grades.UpdateAsync(id, dto, w.Actor), id, ct: ct);
    public Task<bool> DeleteGradeAsync(Guid id, CancellationToken ct = default) => Run(Operation.Manage, async w =>
    {
        if (!w.Actor.IsAdmin)
            throw new UnauthorizedAccessException();
        var deleted = false;
        await w.Transactions.ExecuteAsync(async () => deleted = await w.Grades.DeleteAsync(id));
        return deleted;
    }, ct);
}
