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

internal sealed class InstitutionExamManager(InstitutionSchoolAuthorization access, IInstitutionProfileResolver profiles,
    IInstitutionExamRepositoryFactory factory, ITransactionManager transactions, ISubmissionGradingService grading, ITimeProvider time,
    IInstitutionWriteAuthorization writeAuthorization) : IInstitutionExamManager
{
    private enum Operation { Manage, Student, Read, Teaching }
    private sealed record Work(ExamPrincipal Actor, IInstitutionExamRepositories Repositories, ExamManager Exams,
        ExamSubmissionManager Reviews, GradeManager Grades, ExamSubmissionProcessor Processor, ITransactionManager Transactions);
    private sealed class StaffTransaction(ITransactionManager inner, IInstitutionWriteAuthorization authorization, TenantContext actor) : ITransactionManager
    {
        public Task ExecuteAsync(Func<Task> action, CancellationToken ct = default) => inner.ExecuteAsync(async () =>
        {
            await authorization.LockAsync(actor, ct);
            await action();
        }, ct);
    }
    private async Task<Work> Open(Operation operation)
    {
        var t = await access.RequireAsync(CancellationToken.None);
        var studentView = operation == Operation.Student || operation == Operation.Read &&
            !t.Roles.Contains(InstitutionRole.InstitutionAdmin) && !t.Roles.Contains(InstitutionRole.Staff) && !t.Roles.Contains(InstitutionRole.Teacher);
        Guid? student = null, teacher = null;
        var combinedRead = operation == Operation.Read && t.Roles.Contains(InstitutionRole.Teacher) && t.Roles.Contains(InstitutionRole.Student) && !t.Roles.Contains(InstitutionRole.InstitutionAdmin) && !t.Roles.Contains(InstitutionRole.Staff);
        if (studentView) student = await profiles.RequireStudentAsync(t);
        else if (!t.Roles.Contains(InstitutionRole.InstitutionAdmin) && (operation == Operation.Teaching || !t.Roles.Contains(InstitutionRole.Staff))) teacher = await profiles.RequireTeacherAsync(t);
        if (combinedRead) student = await profiles.RequireStudentAsync(t);
        var actor = new ExamPrincipal(t, studentView, student, teacher, combinedRead);
        var r = factory.Create(actor);
        ITransactionManager tx = t.Roles.Contains(InstitutionRole.Staff) ? new StaffTransaction(transactions, writeAuthorization, t) : transactions;
        var gradeRules = new GradeService(r.Grades, r.ClassSubjects);
        var processor = new ExamSubmissionProcessor(tx, r.Submissions, r.Exams, grading, r.Grades, r.Audit, time);
        return new(actor, r, new ExamManager(r.Exams, new ExamService(r.Exams), r.ClassSubjects, r.Students, r.Grades,
            r.Submissions, processor, new SubjectService(r.Subjects, r.ClassSubjects), tx, r.Audit, time),
            new ExamSubmissionManager(r.Submissions, r.Exams, r.Students, r.Grades, r.Audit, grading, tx, r.ClassSubjects, gradeRules),
            new GradeManager(r.Grades, r.Subjects, r.Students, r.ClassSubjects, gradeRules), processor, tx);
    }
    private async Task<T> Run<T>(Operation operation, Func<Work, Task<T>> action) => await action(await Open(operation));
    private async Task Run(Operation operation, Func<Work, Task> action) => await action(await Open(operation));
    public Task<List<ClassSubjectChoiceDto>> ClassSubjectsAsync() => Run(Operation.Manage, w => w.Repositories.ClassSubjectChoicesAsync());
    public Task<List<StudentDto>> EligibleStudentsAsync(Guid examId) => Run(Operation.Manage, async w =>
    {
        var exam = await w.Repositories.Exams.GetByIdAsync(examId) ?? throw new KeyNotFoundException("La risorsa non esiste.");
        var cs = await w.Repositories.ClassSubjects.GetByIdAsync(exam.ClassSubjectId) ?? throw new KeyNotFoundException("La risorsa non esiste.");
        return await w.Repositories.StudentsForClassAsync(cs.ClassId);
    });
    public Task<List<ExamSummaryDto>> ExamsAsync() => Run(Operation.Manage, w => w.Exams.GetAllAsync(w.Actor));
    public Task<ExamDetailDto?> ExamAsync(Guid id) => Run(Operation.Manage, w => w.Exams.GetByIdAsync(id, w.Actor));
    public Task<Guid> CreateAsync(CreateExamDto dto) => Run(Operation.Manage, w => w.Exams.AddAsync(dto, w.Actor));
    public Task UpdateAsync(Guid id, UpdateExamDto dto) => Run(Operation.Manage, w => w.Exams.UpdateAsync(id, dto, w.Actor));
    public Task AssignAsync(Guid id, UpdateExamStudentsDto dto) => Run(Operation.Manage, w => w.Exams.UpdateStudentsAsync(id, dto, w.Actor));
    public Task PublishAsync(Guid id) => Run(Operation.Manage, w => w.Exams.PublishAsync(id, w.Actor));
    public Task CloseAsync(Guid id) => Run(Operation.Manage, w => w.Exams.CloseAsync(id, w.Actor));
    public Task CancelAsync(Guid id) => Run(Operation.Manage, w => w.Exams.CancelAsync(id, w.Actor));
    public Task DeleteAsync(Guid id) => Run(Operation.Manage, w => w.Exams.DeleteAsync(id, w.Actor));
    public Task<List<AssignedExamDto>> AssignedAsync() => Run(Operation.Student, w => w.Exams.GetAssignedAsync(w.Actor));
    public Task<StartExamDto> StartAsync(Guid id) => Run(Operation.Student, w => w.Exams.StartAsync(id, w.Actor));
    public Task<SubmissionSessionDto> SessionAsync(Guid id) => Run(Operation.Student, w => w.Repositories.SessionAsync(id));
    public Task<SaveAnswersResponseDto> SaveAsync(Guid id, SaveExamAnswersDto dto) => Run(Operation.Student, w => w.Exams.SaveAnswersAsync(id, dto, w.Actor));
    public Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto) => Run(Operation.Student, w => w.Processor.ProcessInstitutionAsync(id, dto, SubmitReason.Manual, w.Actor));
    // Explicit authenticated owner-triggered expiry. No payload/late answers; worker scan stays deferred.
    public Task<SubmissionResultDto> SubmitExpiredAsync(Guid id) => Run(Operation.Student, w => w.Processor.ProcessInstitutionAsync(id, null, SubmitReason.TimeExpired, w.Actor));
    public Task<ViolationReceiptDto> ReportViolationAsync(Guid id, InstitutionViolationDto dto) => Run(Operation.Student, async w =>
    {
        ViolationReceiptDto? result = null;
        await w.Transactions.ExecuteAsync(async () => result = await w.Repositories.ReportViolationAsync(id, dto));
        return result!;
    });
    public Task<List<ViolationEventDto>> ViolationsAsync(Guid id) => Run(Operation.Manage, w => w.Repositories.ViolationsAsync(id));
    public Task<List<SubmissionSummaryDto>> SubmissionsAsync(Guid examId, SubmissionStatus? status) => Run(Operation.Manage, w => w.Reviews.GetByExamAsync(examId, status, w.Actor));
    public Task<SubmissionReviewDto> ReviewAsync(Guid id) => Run(Operation.Manage, w => w.Reviews.GetByIdAsync(id, w.Actor));
    public Task ReviewAsync(Guid id, ReviewOpenAnswersDto dto) => Run(Operation.Teaching, w => w.Reviews.ReviewAsync(id, dto, w.Actor));
    public Task FinalizeAsync(Guid id, FinalizeSubmissionDto dto) => Run(Operation.Teaching, w => w.Reviews.FinalizeAsync(id, dto, w.Actor));
    public async Task<SubmissionResultDto> ResultAsync(Guid id)
    {
        var w = await Open(Operation.Read);
        try { return await w.Exams.GetResultAsync(id, w.Actor); }
        catch (KeyNotFoundException) when (w.Actor.CombinedRead)
        { var own = await Open(Operation.Student); return await own.Exams.GetResultAsync(id, own.Actor); }
    }
    public Task<List<GradeDto>> GradesAsync() => Run(Operation.Read, w => w.Grades.GetAllAsync(w.Actor));
    public Task<GradeDto?> GradeAsync(Guid id) => Run(Operation.Read, w => w.Grades.GetByIdAsync(id, w.Actor));
    public Task<int> GradeCountAsync() => Run(Operation.Read, w => w.Grades.CountAsync(w.Actor));
    public Task<GradePageDto> GradePageAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date) => Run(Operation.Read, w => w.Grades.GetPagedAsync(pageNumber, pageSize, subjectId, studentId, date, w.Actor));
    public Task<GradeFiltersDto> GradeFiltersAsync() => Run(Operation.Read, w => w.Grades.GetFiltersAsync(w.Actor));
    public Task<GradeStatisticsDto?> GradeStatisticsAsync() => Run(Operation.Read, w => w.Grades.GetStatisticsAsync(w.Actor));
    private Task<T> GradeWrite<T>(InstitutionPermission permission, Func<Work, Task<T>> action, Guid? id = null) => Run(Operation.Manage, async w =>
    {
        InstitutionPermissions.Require(w.Actor.Tenant!, permission);
        T result = default!;
        await w.Transactions.ExecuteAsync(async () =>
        {
            var before = id.HasValue ? await w.Repositories.Grades.GetByIdAsync(id.Value) : null;
            result = await action(w);
            var changedId = id ?? (result is Guid created ? created : (Guid?)null);
            if (w.Actor.Tenant!.Roles.Contains(InstitutionRole.Staff) && changedId.HasValue && result is not false)
            {
                var after = await w.Repositories.Grades.GetByIdAsync(changedId.Value);
                await w.Repositories.Audit.AddAsync(new AuditEvent
                {
                    Id = Guid.NewGuid(), ActorUserId = w.Actor.UserId, EntityType = "grade", EntityId = changedId.Value,
                    Action = permission == InstitutionPermission.GradeCreate ? "staff.grade.create" : "staff.grade.update",
                    BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
                    AfterJson = JsonSerializer.Serialize(new { ActorMembershipId = w.Actor.Tenant.MembershipId, Grade = after }), CreatedAt = time.UtcNow
                });
            }
        });
        return result;
    });
    public Task<Guid?> AddGradeAsync(CreateGradeDto dto) => GradeWrite(InstitutionPermission.GradeCreate, w => w.Grades.AddAsync(dto, w.Actor));
    public Task<bool> UpdateGradeAsync(Guid id, UpdateGradeDto dto) => GradeWrite(InstitutionPermission.GradeUpdate, w => w.Grades.UpdateAsync(id, dto, w.Actor), id);
    public Task<bool> DeleteGradeAsync(Guid id) => Run(Operation.Manage, async w =>
    {
        if (!w.Actor.IsAdmin) throw new UnauthorizedAccessException();
        var deleted = false;
        await w.Transactions.ExecuteAsync(async () => deleted = await w.Grades.DeleteAsync(id));
        return deleted;
    });
}
