using ElectronicRegisterAPI.Domain.DTOs;
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
            IExamSubmissionRepository examSubmissionRepository,
            IExamSubmissionProcessor examSubmissionProcessor,
            ISubjectService subjectService,
            ITimeProvider timeProvider = null!
        )
        {
            _examRepository = examRepository;
            _examService = examService;
            _classSubjectRepository = classSubjectRepository;
            _studentRepository = studentRepository;
            _examSubmissionRepository = examSubmissionRepository;
            _examSubmissionProcessor = examSubmissionProcessor;
            _subjectService = subjectService;
            // Placeholder until DI wiring is updated.
            _timeProvider = timeProvider;
        }


        public async Task<List<ExamSummaryDto>> GetAllAsync(ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);
            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare gli esami.");

            Guid? teacherId = null;

            if (caller.Role == UserRole.Teacher)
            {
                teacherId = caller.TeacherId
                    ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
            }

            return await _examRepository.GetSummariesAsync(teacherId);
        }



        public async Task<ExamDetailDto?> GetByIdAsync(Guid id, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);
            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare gli esami.");

            if (caller.Role == UserRole.Teacher && !caller.TeacherId.HasValue)
                throw new UnauthorizedAccessException("Identificativo docente non presente.");

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: true, includeStudents: true);

            if (exam is null) return null;

            // Validazione esplicita della struttura restituita dal repository
            if (exam.Questions is null)
                throw new InvalidOperationException("La struttura dell'esame è incompleta: domande mancanti.");

            if (exam.AssignedStudents is null)
                throw new InvalidOperationException("La struttura dell'esame è incompleta: studenti assegnati mancanti.");

            if (caller.Role == UserRole.Teacher)
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



        public async Task<List<AssignedExamDto>> GetAssignedAsync(ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);
            if (caller.Role != UserRole.Student)
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

        public async Task<Guid> AddAsync(CreateExamDto dto, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a creare esami.");

            ArgumentNullException.ThrowIfNull(dto);

            if (dto.AssignedStudents is null)
                throw new ArgumentException("La lista degli studenti non può essere nulla.");

            if (dto.Questions is null)
                throw new ArgumentException("La lista delle domande non può essere nulla.");

            var classSubject = await _subjectService.EnsureValidClassSubjectId(dto.ClassSubjectId);
            _examService.EnsureValidName(dto.Name);
            _examService.EnsureValidSchedule(dto.AvailableFrom, dto.AvailableUntil, dto.DurationMinutes);
            _examService.EnsureValidPassingPercent(dto.PassingPercent);

            _subjectService.EnsureClassSubjectExists(classSubject);

            // Autorizzazione docente: rimane nel Manager
            if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId
                    ?? throw new UnauthorizedAccessException(
                        "Identificativo docente non presente.");

                if (classSubject!.TeacherId != teacherId)
                    throw new UnauthorizedAccessException(
                        "Non sei autorizzato a creare esami per questa classe.");
            }

            if (dto.RetakeOfExamId.HasValue)
            {
                var originalExam = await _examRepository
                    .GetByIdAsync(dto.RetakeOfExamId.Value);

                // Delegare la validazione del recupero al service
                _examService.EnsureValidRetakeExam(originalExam, dto.ClassSubjectId);
            }

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

            var exam = ExamCreationMapper.MapToExam(dto, examId, questions, assignedStudents);

            await _examRepository.AddAsync(exam);

            return exam.Id;
        }


        public async Task UpdateAsync(Guid id, UpdateExamDto dto, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a modificare esami.");

            ArgumentNullException.ThrowIfNull(dto);

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: true, includeStudents: true)
                ?? throw new KeyNotFoundException("L'esame non esiste.");

            if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a modificare questo esame.");
            }

            _examService.EnsureEditable(exam);

            _examService.EnsureValidName(dto.Name);
            _examService.EnsureValidSchedule(dto.AvailableFrom, dto.AvailableUntil, dto.DurationMinutes);
            _examService.EnsureValidPassingPercent(dto.PassingPercent);

            // Map questions from DTO
            var (questions, _) = ExamCreationMapper.MapFromCreateDto(new CreateExamDto
            {
                ClassSubjectId = dto.ClassSubjectId,
                RetakeOfExamId = dto.RetakeOfExamId,
                Name = dto.Name,
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
            exam.Name = dto.Name;
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
        }

        public async Task UpdateStudentsAsync(Guid id, UpdateExamStudentsDto dto, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a modificare gli studenti assegnati.");

            ArgumentNullException.ThrowIfNull(dto);

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: false, includeStudents: true)
                ?? throw new KeyNotFoundException("L'esame non esiste.");

            if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a modificare questo esame.");
            }

            _examService.EnsureEditable(exam);

            if (dto.Students is null)
                throw new ArgumentException("La lista degli studenti non può essere nulla.");

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

            _examService.EnsureValidAssignedStudents(assignedStudents, students, exam.ClassSubjectId);

            await _examRepository.ReplaceAssignedStudentsAsync(id, assignedStudents);
        }

        public async Task PublishAsync(Guid id, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a pubblicare esami.");

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: true, includeStudents: true)
                ?? throw new KeyNotFoundException("L'esame non esiste.");

            if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a pubblicare questo esame.");
            }

            var nowUtc = DateTime.UtcNow;

            _examService.EnsurePublishable(exam, nowUtc);

            exam.Status = ExamStatus.Published;
            exam.PublishedAt = nowUtc;

            await _examRepository.UpdateAsync(exam);
        }

        public async Task CloseAsync(Guid id, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a chiudere esami.");

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: false, includeStudents: true)
                ?? throw new KeyNotFoundException("L'esame non esiste.");

            if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a chiudere questo esame.");
            }

            // Closing: set status and ClosedAt
            exam.Status = ExamStatus.Closed;
            exam.ClosedAt = DateTime.UtcNow;

            await _examRepository.UpdateAsync(exam);
        }

        public async Task CancelAsync(Guid id, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato ad annullare esami.");

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: false, includeStudents: true)
                ?? throw new KeyNotFoundException("L'esame non esiste.");

            if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato ad annullare questo esame.");
            }

            var hasSubmissions = await _examRepository.HasSubmissionsAsync(id);

            _examService.EnsureCanCancel(exam, hasSubmissions);

            exam.Status = ExamStatus.Cancelled;
            exam.CancelledAt = DateTime.UtcNow;

            await _examRepository.UpdateAsync(exam);
        }

        public async Task DeleteAsync(Guid id, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Admin && caller.Role != UserRole.Teacher)
                throw new UnauthorizedAccessException("Non sei autorizzato a eliminare esami.");

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: false, includeStudents: false)
                ?? throw new KeyNotFoundException("L'esame non esiste.");

            if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a eliminare questo esame.");
            }

            var hasSubmissions = await _examRepository.HasSubmissionsAsync(id);

            _examService.EnsureDeletable(exam, hasSubmissions);

            await _examRepository.DeleteDraftAsync(id);
        }

        public async Task<StartExamDto> StartAsync(Guid id, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Student)
                throw new UnauthorizedAccessException("Non sei autorizzato ad iniziare esami.");

            var studentId = caller.StudentId ?? throw new UnauthorizedAccessException("Identificativo studente non presente.");

            var exam = await _examRepository.GetByIdAsync(id, includeStructure: true, includeStudents: true)
                ?? throw new KeyNotFoundException("L'esame non esiste.");

            // check assignment
            var assignment = exam.AssignedStudents?.FirstOrDefault(s => s.StudentId == studentId)
                ?? throw new KeyNotFoundException("Non sei assegnato a questo esame.");

            var nowUtc = DateTime.UtcNow;

            var existing = await _examSubmissionRepository.GetByExamAndStudentAsync(id, studentId, withAnswers: false);
            _examService.EnsureStartAllowed(exam, existing, studentId, nowUtc);

            var submissionId = Guid.NewGuid();
            var startedAt = nowUtc;
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
                Answers = new List<SubmissionAnswer>()
            };

            await _examSubmissionRepository.AddAsync(submission);

            return ExamStudentMapper.ToStartExamDto(exam, submissionId, startedAt, deadlineAt);
        }

        public async Task<SaveAnswersResponseDto> SaveAnswersAsync(Guid id, SaveExamAnswersDto dto, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Student)
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

        public async Task<SubmissionResultDto> SubmitAsync(Guid id, SubmitExamDto dto, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            if (caller.Role != UserRole.Student)
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
            var processorResult = await _examSubmissionProcessor.ProcessSubmitAsync(id, dto, SubmitReason.Manual, caller);

            return processorResult;
        }

        public async Task<SubmissionResultDto> GetResultAsync(Guid id, ClaimsContext caller)
        {
            ArgumentNullException.ThrowIfNull(caller);

            var submission = await _examSubmissionRepository.GetByIdAsync(id, withAnswers: false)
                ?? throw new KeyNotFoundException("La submission non esiste.");

            if (caller.Role == UserRole.Student)
            {
                var studentId = caller.StudentId ?? throw new UnauthorizedAccessException("Identificativo studente non presente.");
                if (submission.StudentId != studentId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");
            }
            else if (caller.Role == UserRole.Teacher)
            {
                var teacherId = caller.TeacherId ?? throw new UnauthorizedAccessException("Identificativo docente non presente.");
                var exam = await _examService.EnsureExamExistsAsync(submission.ExamId);
                var classSubject = await _classSubjectRepository.GetByIdAsync(exam.ClassSubjectId);
                if (classSubject is null || classSubject.TeacherId != teacherId)
                    throw new UnauthorizedAccessException("Non sei autorizzato a visualizzare questa submission.");
            }

            return ExamSubmissionMapper.ToSubmissionResultDto(submission);
        }
    }
}
