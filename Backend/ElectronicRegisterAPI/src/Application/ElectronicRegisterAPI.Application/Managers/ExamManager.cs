using System.Text.Json;
using ElectronicRegisterAPI.Domain.Exceptions;
﻿using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Application.Mappers;


namespace ElectronicRegisterAPI.Application.Managers
{
    internal class ExamManager : IExamManager
    {
        private readonly IExamRepository _examRepository;
        private readonly IClassSubjectRepository _classSubjectRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly IGradeRepository _gradeRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly IAuditRepository _auditRepository;
        private readonly IExamSubmissionRepository _examSubmissionRepository;
    private readonly IExamSubmissionProcessor _examSubmissionProcessor;
        private readonly IExamService _examService;
        private readonly ISubjectService _subjectService;
        private readonly ITimeProvider _timeProvider;

        public ExamManager(
            IExamRepository examRepository, 
            IExamService examService, 
            IClassSubjectRepository classSubjectRepository, 
            IStudentRepository studentRepository, 
            IGradeRepository gradeRepository,
            IExamSubmissionRepository examSubmissionRepository,
            IExamSubmissionProcessor examSubmissionProcessor,
            ISubjectService subjectService,
            ITransactionManager transactionManager,
            IAuditRepository auditRepository,
            ITimeProvider timeProvider = null!
        )
        {
            _examRepository = examRepository;
            _examService = examService;
            _classSubjectRepository = classSubjectRepository;
            _studentRepository = studentRepository;
            _gradeRepository = gradeRepository;
            _examSubmissionRepository = examSubmissionRepository;
            _examSubmissionProcessor = examSubmissionProcessor;
            _subjectService = subjectService;
            _transactionManager = transactionManager;
            _auditRepository = auditRepository;
            // Placeholder until DI wiring is updated.
            _timeProvider = timeProvider;
        }


        public async Task<List<ExamSummaryDto>> GetAllAsync(ExamPrincipal caller)
        {
            ArgumentNullException.ThrowIfNull(caller);
            if (!caller.IsAdmin && !caller.IsTeacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare gli esami.");

            Guid? teacherId = null;

            if (caller.IsTeacher)
            {
                teacherId = caller.TeacherId
                    ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
            }

            return await _examRepository.GetSummariesAsync(teacherId);
        }



        public async Task<ExamDetailDto?> GetByIdAsync(Guid id, ExamPrincipal caller)
        {
            ArgumentNullException.ThrowIfNull(caller);
            if (!caller.IsAdmin && !caller.IsTeacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare gli esami.");

            if (caller.IsTeacher && !caller.TeacherId.HasValue)
                throw new UnauthorizedAccessException("Identificativo docente non presente.");

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: true, includeStudents: true);

            if (exam is null) return null;

            // Validazione esplicita della struttura restituita dal repository
            if (exam.Questions is null)
                throw new InvalidOperationException("La struttura dell'esame è incompleta: domande mancanti.");

            if (exam.AssignedStudents is null)
                throw new InvalidOperationException("La struttura dell'esame è incompleta: studenti assegnati mancanti.");

            if (caller.IsTeacher)
            {
                Guid teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");

                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);

                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questo esame.");
            }

            var studentIds = exam.AssignedStudents.Select(s => s.StudentId).ToList();

            var students = await _studentRepository.GetByIdsAsync(studentIds);

            var studentsById = students.ToDictionary(s => s.Id);

            return ExamMapper.ToDto(exam, studentsById);
        }



        public async Task<List<AssignedExamDto>> GetAssignedAsync(ExamPrincipal caller)
        {
            ArgumentNullException.ThrowIfNull(caller);
            if (!caller.IsStudent)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare gli esami assegnati.");

            var studentId = caller.StudentId ?? throw new UnauthorizedAccessException("Identificativo studente non presente.");

            var exams = await _examRepository.GetAssignedToStudentAsync(studentId);

            var submissions = await _examSubmissionRepository.GetByStudentAsync(studentId);

            var submissionsByExamId = submissions.ToDictionary(s => s.ExamId);

            var nowUtc = DateTime.UtcNow;

            return exams.Select(exam =>
            {
                submissionsByExamId.TryGetValue(exam.Id, out var submission);
                return ExamMapper.ToAssignedExamDto(exam, studentId, submission, nowUtc);
            }).ToList();
        }

        public async Task<Guid> AddAsync(CreateExamDto dto, ExamPrincipal caller)
        {
            Guid createdId = default;
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsAdmin && !caller.IsTeacher)
                    throw new UnauthorizedAccessException("Non sei autorizzato a creare esami.");

                ArgumentNullException.ThrowIfNull(dto);

                if (dto.AssignedStudents is null)
                    throw new ArgumentException("La lista degli studenti non può essere nulla.");

                if (dto.Questions is null)
                    throw new ArgumentException("La lista delle domande non può essere nulla.");

                var classSubject = await _subjectService.EnsureValidClassSubjectId(dto.ClassSubjectId);

                _examService.EnsureValidSchedule(dto.AvailableFrom, dto.AvailableUntil, dto.DurationMinutes);
                _examService.EnsureValidPassingPercent(dto.PassingPercent);

                _subjectService.EnsureClassSubjectExists(classSubject);

                // Autorizzazione docente: rimane nel Manager
                if (caller.IsTeacher)
                {
                    var teacherId = caller.TeacherId
                        ?? throw new UnauthorizedAccessException(
                            "Identificativo docente non presente.");

                    if (classSubject!.TeacherId != teacherId)
                        throw new UnauthorizedAccessException(
                            "Non sei autorizzato a creare esami per questa classe.");
                }

                await ValidateRetakeAsync(Guid.Empty, dto.RetakeOfExamId, dto.ClassSubjectId);

                if (dto.AssignedStudents.Any(s => s is null))
                    throw new ArgumentException(
                        "La lista degli studenti contiene un elemento nullo.");

                var studentIds = dto.AssignedStudents
                    .Select(s => s.StudentId)
                    .ToList();

                var students = await _studentRepository.GetByIdsAsync(studentIds);

                var examId = Guid.NewGuid();
                var nowUtc = DateTime.UtcNow;

                var (questions, assignedStudents) = ExamCreationMapper.MapFromCreateDto(dto, examId, nowUtc);

                _examService.EnsureValidQuestions(questions);

                // Delegare la validazione delle assegnazioni allo service
                _examService.EnsureValidAssignedStudents(assignedStudents, students, classSubject.ClassId);

                var exam = ExamCreationMapper.MapToExam(dto, examId, questions, assignedStudents, classSubject.SubjectName);

                await _examRepository.AddAsync(exam);

                createdId = exam.Id;
            });
            return createdId;
        }


        public async Task UpdateAsync(Guid id, UpdateExamDto dto, ExamPrincipal caller)
        {
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsAdmin && !caller.IsTeacher)
                    throw new UnauthorizedAccessException("Non sei autorizzato a modificare esami.");

                ArgumentNullException.ThrowIfNull(dto);

                var exam = await _examRepository.GetByIdForUpdateAsync(id)
                    ?? throw new KeyNotFoundException("L'esame non esiste.");

                if (caller.IsTeacher)
                {
                    var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                    var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                    if (classSubject is null || classSubject.TeacherId != teacherId)
                        throw new UnauthorizedAccessException("Non sei autorizzato a modificare questo esame.");
                }

                _examService.EnsureEditable(exam);
                if (dto.ClassSubjectId != exam.ClassSubjectId)
                    throw new ArgumentException("ClassSubjectId cannot be changed for an existing exam.");
                await ValidateRetakeAsync(exam.Id, dto.RetakeOfExamId, exam.ClassSubjectId);


                _examService.EnsureValidSchedule(dto.AvailableFrom, dto.AvailableUntil, dto.DurationMinutes);
                _examService.EnsureValidPassingPercent(dto.PassingPercent);

                // Map questions from DTO
                var (questions, _) = ExamCreationMapper.MapFromCreateDto(new CreateExamDto
                {
                    ClassSubjectId = dto.ClassSubjectId,
                    RetakeOfExamId = dto.RetakeOfExamId,
                    Description = dto.Description,
                    AvailableFrom = dto.AvailableFrom,
                    AvailableUntil = dto.AvailableUntil,
                    DurationMinutes = dto.DurationMinutes,
                    PassingPercent = dto.PassingPercent,
                    Questions = dto.Questions,
                    AssignedStudents = new List<ExamStudentInputDto>()
                }, exam.Id, DateTime.UtcNow);

                _examService.EnsureValidQuestions(questions);

                // Update exam properties
                exam.RetakeOfExamId = dto.RetakeOfExamId;
                var namingSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId)
                    ?? throw new KeyNotFoundException("ClassSubject not found.");
                exam.Name = namingSubject.SubjectName;
                exam.Description = dto.Description;
                exam.AvailableFrom = dto.AvailableFrom;
                exam.AvailableUntil = dto.AvailableUntil;
                exam.DurationMinutes = dto.DurationMinutes;
                exam.PassingPercent = dto.PassingPercent;
                exam.TotalPoints = _examService.CalculateTotalPoints(questions);

                // Replace structure (questions/options)
                exam.Questions = questions;

                await _examRepository.ReplaceStructureAsync(exam);

                // Update main exam properties
                await _examRepository.UpdateAsync(exam);
            });
        }

        public async Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ExamPrincipal caller)
        {
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsAdmin && !caller.IsTeacher)
                    throw new UnauthorizedAccessException("Non sei autorizzato a modificare gli studenti assegnati.");

                ArgumentNullException.ThrowIfNull(dto);

                var exam = await _examRepository.GetByIdForUpdateAsync(id)
                    ?? throw new KeyNotFoundException("L'esame non esiste.");

                if (caller.IsTeacher)
                {
                    var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                    var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                    if (classSubject is null || classSubject.TeacherId != teacherId)
                        throw new UnauthorizedAccessException("Non sei autorizzato a modificare questo esame.");
                }

                _examService.EnsureEditable(exam);

                if (dto.Students is null)
                    throw new ArgumentException("La lista degli studenti non può essere nulla.");

                if (dto.Students.Any(s => s is null))
                    throw new ArgumentException("Student assignments cannot contain null elements.");
                var studentIds = dto.Students.Select(s => s.StudentId).ToList();

                var students = await _studentRepository.GetByIdsAsync(studentIds);

                var nowUtc = DateTime.UtcNow;

                var assignedStudents = dto.Students.Select(s => new ExamStudent
                {
                    ExamId = id,
                    StudentId = s.StudentId,
                    ExtraTimeMinutes = s.ExtraTimeMinutes,
                    AssignedAt = nowUtc
                }).ToList();

                var assignedClassSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId)
                    ?? throw new KeyNotFoundException("ClassSubject not found.");
                _examService.EnsureValidAssignedStudents(assignedStudents, students, assignedClassSubject.ClassId);

                await _examRepository.ReplaceAssignedStudentsAsync(id, assignedStudents);
            });
        }

        public async Task PublishAsync(Guid id, ExamPrincipal caller)
        {
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsAdmin && !caller.IsTeacher)
                    throw new UnauthorizedAccessException("Non sei autorizzato a pubblicare esami.");

                var exam = await _examRepository.GetByIdForUpdateAsync(id)
                    ?? throw new KeyNotFoundException("L'esame non esiste.");

                if (caller.IsTeacher)
                {
                    var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                    var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                    if (classSubject is null || classSubject.TeacherId != teacherId)
                        throw new UnauthorizedAccessException("Non sei autorizzato a pubblicare questo esame.");
                }

                var nowUtc = DateTime.UtcNow;

                _examService.EnsurePublishable(exam, nowUtc);
                await ValidateRetakeAsync(exam.Id, exam.RetakeOfExamId, exam.ClassSubjectId);
                var publishClassSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId)
                    ?? throw new KeyNotFoundException("ClassSubject not found.");
                var publishStudents = await _studentRepository.GetByIdsAsync(exam.AssignedStudents.Select(s => s.StudentId));
                _examService.EnsureValidAssignedStudents(exam.AssignedStudents, publishStudents, publishClassSubject.ClassId);
                var beforeJson = LifecycleJson(exam);

                exam.Status = ExamStatus.Published;
                exam.PublishedAt = nowUtc;

                await _examRepository.UpdateAsync(exam);
                await WriteLifecycleAuditAsync(exam, caller, "exam.publish", beforeJson);
            });
        }

        public async Task CloseAsync(Guid id, ExamPrincipal caller)
        {
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsAdmin && !caller.IsTeacher)
                    throw new UnauthorizedAccessException("Non sei autorizzato a chiudere esami.");

                var exam = await _examRepository.GetByIdForUpdateAsync(id)
                    ?? throw new KeyNotFoundException("L'esame non esiste.");

                if (caller.IsTeacher)
                {
                    var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                    var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                    if (classSubject is null || classSubject.TeacherId != teacherId)
                        throw new UnauthorizedAccessException("Non sei autorizzato a chiudere questo esame.");
                }

                _examService.EnsureCanClose(exam);
                if (await _examRepository.HasInProgressSubmissionsAsync(id))
                    throw new BusinessRuleException("Cannot close an exam while submissions are in progress.");
                var beforeJson = LifecycleJson(exam);
                // Closing: set status and ClosedAt
                exam.Status = ExamStatus.Closed;
                exam.ClosedAt = DateTime.UtcNow;

                await _examRepository.UpdateAsync(exam);
                await WriteLifecycleAuditAsync(exam, caller, "exam.close", beforeJson);
            });
        }

        public async Task CancelAsync(Guid id, ExamPrincipal caller)
        {
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsAdmin && !caller.IsTeacher)
                    throw new UnauthorizedAccessException("Non sei autorizzato ad annullare esami.");

                var exam = await _examRepository.GetByIdForUpdateAsync(id)
                    ?? throw new KeyNotFoundException("L'esame non esiste.");

                if (caller.IsTeacher)
                {
                    var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                    var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                    if (classSubject is null || classSubject.TeacherId != teacherId)
                        throw new UnauthorizedAccessException("Non sei autorizzato ad annullare questo esame.");
                }

                var hasSubmissions = await _examRepository.HasSubmissionsAsync(id);

                _examService.EnsureCanCancel(exam, hasSubmissions);

                var beforeJson = LifecycleJson(exam);
                exam.Status = ExamStatus.Cancelled;
                exam.CancelledAt = DateTime.UtcNow;

                await _examRepository.UpdateAsync(exam);
                await WriteLifecycleAuditAsync(exam, caller, "exam.cancel", beforeJson);
            });
        }

        public async Task DeleteAsync(Guid id, ExamPrincipal caller)
        {
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsAdmin && !caller.IsTeacher)
                    throw new UnauthorizedAccessException("Non sei autorizzato a eliminare esami.");

                var exam = await _examRepository.GetByIdForUpdateAsync(id)
                    ?? throw new KeyNotFoundException("L'esame non esiste.");

                if (caller.IsTeacher)
                {
                    var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                    var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                    if (classSubject is null || classSubject.TeacherId != teacherId)
                        throw new UnauthorizedAccessException("Non sei autorizzato a eliminare questo esame.");
                }

                var hasSubmissions = await _examRepository.HasSubmissionsAsync(id);

                _examService.EnsureDeletable(exam, hasSubmissions);

                if (await _examRepository.HasRetakesAsync(id))
                    throw new BusinessRuleException("An exam referenced by a retake cannot be deleted.");
                await _examRepository.DeleteDraftAsync(id);
            });
        }

        public async Task<StartExamDto> StartAsync(Guid id, ExamPrincipal caller)
        {
            StartExamDto started = null!;
            await _transactionManager.ExecuteAsync(async () =>
            {
                ArgumentNullException.ThrowIfNull(caller);

                if (!caller.IsStudent)
                    throw new UnauthorizedAccessException("Non sei autorizzato ad iniziare esami.");

                var studentId = caller.StudentId ?? throw new UnauthorizedAccessException("Identificativo studente non presente.");

                var exam = await _examRepository.GetByIdForUpdateAsync(id)
                    ?? throw new KeyNotFoundException("L'esame non esiste.");

                // check assignment
                var assignment = exam.AssignedStudents?.FirstOrDefault(s => s.StudentId == studentId)
                    ?? throw new KeyNotFoundException("Non sei assegnato a questo esame.");

                var nowUtc = DateTime.UtcNow;

                var existing = await _examSubmissionRepository.GetByExamAndStudentAsync(id, studentId, withAnswers: false);
                _examService.EnsureStartAllowed(exam, existing, studentId, nowUtc);

                var submissionId = Guid.NewGuid();
                // DATETIME(6) persists microseconds; return the same instant that resume reads.
                var startedAt = caller.Tenant is null ? nowUtc : new DateTime(nowUtc.Ticks / 10 * 10, DateTimeKind.Utc);
                var deadlineAt = startedAt.AddMinutes(exam.DurationMinutes + assignment.ExtraTimeMinutes);

                var submission = new ExamSubmission
                {
                    Id = submissionId,
                    ExamId = id,
                    StudentId = studentId,
                    Status = SubmissionStatus.InProgress,
                    StartedAt = startedAt,
                    DeadlineAt = deadlineAt,
                    Revision = 0,
                    MaxScore = exam.TotalPoints,
                    PassingScore = exam.TotalPoints * exam.PassingPercent / 100m,
                    DisplayOrderJson = StudentExamPresentation.CreateOrder(exam),
                    Answers = new List<SubmissionAnswer>()
                };

                await _examSubmissionRepository.AddAsync(submission);

                started = ExamStudentMapper.ToStartExamDto(exam, submission);
            });
            return started;
        }

        public async Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ExamPrincipal caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (!caller.IsStudent)
                throw new UnauthorizedAccessException("Non sei autorizzato a salvare risposte.");

            ArgumentNullException.ThrowIfNull(dto);

            var studentId = caller.StudentId ?? throw new UnauthorizedAccessException("Identificativo studente non presente.");

            var submission = await _examSubmissionRepository.GetByIdAsync(id, withAnswers: false)
                ?? throw new KeyNotFoundException("La submission non esiste.");

            if (submission.StudentId != studentId)
                throw new UnauthorizedAccessException("Non sei autorizzato a modificare questa submission.");

            _examService.EnsureSubmissionInProgress(submission);

            var answers = dto.Answers?.Select(a => new SubmissionAnswer
            {
                Id = Guid.NewGuid(),
                SubmissionId = id,
                QuestionId = a.QuestionId,
                OpenText = a.OpenText,
                SelectedOptionIds = a.SelectedOptionIds ?? new List<Guid>()
            }).ToList() ?? new List<SubmissionAnswer>();

            var result = await _examSubmissionRepository.SaveAnswersAsync(id, dto.ClientRevision, answers);

            return ExamSubmissionMapper.ToSaveResponseDto(result);
        }

        public async Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, ExamPrincipal caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (!caller.IsStudent)
                throw new UnauthorizedAccessException("Non sei autorizzato a inviare esami.");

            ArgumentNullException.ThrowIfNull(dto);

            var studentId = caller.StudentId ?? throw new UnauthorizedAccessException("Identificativo studente non presente.");

            var submission = await _examSubmissionRepository.GetByIdAsync(id, withAnswers: true)
                ?? throw new KeyNotFoundException("La submission non esiste.");

            if (submission.StudentId != studentId)
                throw new UnauthorizedAccessException("Non sei autorizzato a inviare questa submission.");

            _examService.EnsureSubmissionInProgress(submission);

            // Delegate processing to shared processor. Processor will persist DTO answers and mark the submission as begun
            // only after acquiring the necessary lock and verifying timing constraints.
            var processorResult = await (caller.Tenant is null ? _examSubmissionProcessor.ProcessSubmitAsync(id, dto, SubmitReason.Manual, caller.VisibilityCaller) : _examSubmissionProcessor.ProcessInstitutionAsync(id, dto, SubmitReason.Manual, caller));

            return processorResult;
        }

        public async Task<SubmissionResultDto> GetResultAsync(Guid id, ExamPrincipal caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            var submission = await _examSubmissionRepository.GetByIdAsync(id, withAnswers: false)
                ?? throw new KeyNotFoundException("La submission non esiste.");

            if (caller.IsStudent)
            {
                var studentId = caller.StudentId ?? throw new UnauthorizedAccessException("Identificativo studente non presente.");
                if (submission.StudentId != studentId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");
            }
            else if (caller.IsTeacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var exam = await _examService.EnsureExamExistsAsync(submission.ExamId);
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");
            }

            else if (!caller.IsAdmin)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");

            // When returning result, resolve official grade value if present via grade repository
            decimal? officialGrade = null;
            if (submission.GradeId.HasValue)
            {
                var grade = await _gradeRepository.GetByIdAsync(submission.GradeId.Value);
                if (grade is null)
                {
                    throw new InvalidOperationException("Stato incoerente: GradeId presente ma Grade non trovato.");
                }
                officialGrade = grade.Value;
            }

            return ExamSubmissionMapper.ToSubmissionResultDto(submission, officialGrade, caller.VisibilityCaller);
        }
        private async Task ValidateRetakeAsync(Guid examId, Guid? originalId, Guid classSubjectId)
        {
            var visited = new HashSet<Guid> { examId };
            while (originalId.HasValue)
            {
                if (!visited.Add(originalId.Value))
                    throw new ArgumentException("Retake references cannot be self-referential or cyclic.");
                var original = await _examRepository.GetByIdForUpdateAsync(originalId.Value);
                _examService.EnsureValidRetakeExam(original, classSubjectId);
                originalId = original!.RetakeOfExamId;
            }
        }

        private static string LifecycleJson(Exam exam) => JsonSerializer.Serialize(new
        {
            exam.Status, exam.PublishedAt, exam.ClosedAt, exam.CancelledAt, exam.RetakeOfExamId
        });

        private Task WriteLifecycleAuditAsync(Exam exam, ExamPrincipal caller, string action, string beforeJson) =>
            _auditRepository.AddAsync(new AuditEvent
            {
                Id = Guid.NewGuid(), ActorUserId = caller.UserId, EntityType = "exam", EntityId = exam.Id,
                Action = action, BeforeJson = beforeJson, AfterJson = LifecycleJson(exam), CreatedAt = DateTime.UtcNow
            });

        Task<List<ExamSummaryDto>> IExamManager.GetAllAsync(ClaimsContext caller) => GetAllAsync(ExamPrincipal.Legacy(caller));
        Task<ExamDetailDto?> IExamManager.GetByIdAsync(Guid id, ClaimsContext caller) => GetByIdAsync(id, ExamPrincipal.Legacy(caller));
        Task<List<AssignedExamDto>> IExamManager.GetAssignedAsync(ClaimsContext caller) => GetAssignedAsync(ExamPrincipal.Legacy(caller));
        Task<Guid> IExamManager.AddAsync(CreateExamDto dto, ClaimsContext caller) => AddAsync(dto, ExamPrincipal.Legacy(caller));
        Task IExamManager.UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller) => UpdateAsync(id, dto, ExamPrincipal.Legacy(caller));
        Task IExamManager.UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ClaimsContext caller) => UpdateStudentsAsync(id, dto, ExamPrincipal.Legacy(caller));
        Task IExamManager.PublishAsync(Guid id, ClaimsContext caller) => PublishAsync(id, ExamPrincipal.Legacy(caller));
        Task IExamManager.CloseAsync(Guid id, ClaimsContext caller) => CloseAsync(id, ExamPrincipal.Legacy(caller));
        Task IExamManager.CancelAsync(Guid id, ClaimsContext caller) => CancelAsync(id, ExamPrincipal.Legacy(caller));
        Task IExamManager.DeleteAsync(Guid id, ClaimsContext caller) => DeleteAsync(id, ExamPrincipal.Legacy(caller));
        Task<StartExamDto> IExamManager.StartAsync(Guid id, ClaimsContext caller) => StartAsync(id, ExamPrincipal.Legacy(caller));
        Task<SaveAnswersResponseDto> IExamManager.SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ClaimsContext caller) => SaveAnswersAsync(id, dto, ExamPrincipal.Legacy(caller));
        Task<SubmissionResultDto> IExamManager.SubmitAsync(Guid id, SubmitExamDto dto, ClaimsContext caller) => SubmitAsync(id, dto, ExamPrincipal.Legacy(caller));
        Task<SubmissionResultDto> IExamManager.GetResultAsync(Guid id, ClaimsContext caller) => GetResultAsync(id, ExamPrincipal.Legacy(caller));
    }
}
