using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class ExamMapper
{
    public static ExamDetailDto ToDto(Exam exam, IReadOnlyDictionary<Guid, Student> studentsById)
    {
        if (exam is null)
            throw new ArgumentNullException(nameof(exam));
        if (exam.Questions is null)
            throw new InvalidOperationException("La struttura dell'esame è incompleta: domande mancanti.");
        if (exam.AssignedStudents is null)
            throw new InvalidOperationException("La struttura dell'esame è incompleta: studenti assegnati mancanti.");
        return new ExamDetailDto
        {
            Id = exam.Id,
            ClassSubjectId = exam.ClassSubjectId,
            RetakeOfExamId = exam.RetakeOfExamId,
            Name = exam.Name,
            Description = exam.Description,
            AvailableFrom = exam.AvailableFrom,
            AvailableUntil = exam.AvailableUntil,
            DurationMinutes = exam.DurationMinutes,
            PassingPercent = exam.PassingPercent,
            TotalPoints = exam.TotalPoints,
            Status = exam.Status,
            PublishedAt = exam.PublishedAt,
            ClosedAt = exam.ClosedAt,
            CancelledAt = exam.CancelledAt,
            Questions = exam.Questions.OrderBy(q => q.Position).Select(q =>
            {
                if (q.Options is null)
                    throw new InvalidOperationException($"La domanda {q.Id} contiene opzioni nulle.");
                return new QuestionDto
                {
                    Id = q.Id,
                    Position = q.Position,
                    Text = q.Text,
                    Type = q.Type,
                    Points = q.Points,
                    Required = q.Required,
                    Options = q.Options.OrderBy(o => o.Position).Select(o => new QuestionOptionDto { Id = o.Id, Position = o.Position, Text = o.Text, IsCorrect = o.IsCorrect }).ToList()
                };
            }).ToList(),
            AssignedStudents = exam.AssignedStudents.Select(s =>
            {
                if (!studentsById.TryGetValue(s.StudentId, out var student))
                    throw new KeyNotFoundException($"Studente assegnato non trovato: {s.StudentId}");
                return new ExamAssignedStudentDto
                {
                    StudentId = s.StudentId,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    ExtraTimeMinutes = s.ExtraTimeMinutes,
                    AssignedAt = s.AssignedAt
                };
            }).ToList()
        };
    }

    public static AssignedExamDto ToAssignedExamDto(
        Exam exam,
        Guid studentId,
        ExamSubmission? submission,
        DateTime nowUtc
    )
    {
        if (exam is null)
            throw new ArgumentNullException(nameof(exam));
        var assignment = exam.AssignedStudents?.FirstOrDefault(s => s.StudentId == studentId) ?? throw new KeyNotFoundException($"Assegnazione studente non trovata per esame {exam.Id}");
        var canStart = exam.Status == ExamStatus.Published && nowUtc >= exam.AvailableFrom && nowUtc < exam.AvailableUntil && submission is null;
        return new AssignedExamDto
        {
            Id = exam.Id,
            ClassSubjectId = exam.ClassSubjectId,
            Name = exam.Name,
            Description = exam.Description,
            AvailableFrom = exam.AvailableFrom,
            AvailableUntil = exam.AvailableUntil,
            DurationMinutes = exam.DurationMinutes,
            ExtraTimeMinutes = assignment.ExtraTimeMinutes,
            Status = exam.Status,
            SubmissionStatus = submission?.Status,
            SubmissionId = submission?.Id,
            StartedAt = submission?.StartedAt,
            DeadlineAt = submission?.DeadlineAt,
            SubmittedAt = submission?.SubmittedAt,
            CanStart = canStart,
            IsRetake = exam.IsRetake
        };
    }
}
