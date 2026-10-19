using System;
using System.Linq;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class ExamStudentMapper
{
    public static ExamForStudentDto ToExamForStudentDto(Exam exam)
    {
        if (exam is null) throw new ArgumentNullException(nameof(exam));

        if (exam.Questions is null)
            throw new InvalidOperationException("La struttura dell'esame è incompleta: domande mancanti.");

        return new ExamForStudentDto
        {
            Id = exam.Id,
            Name = exam.Name,
            Description = exam.Description,
            AvailableUntil = exam.AvailableUntil,
            DurationMinutes = exam.DurationMinutes,
            TotalPoints = exam.TotalPoints,
            Questions = exam.Questions
                .OrderBy(q => q.Position)
                .Select(q => new QuestionForStudentDto
                {
                    Id = q.Id,
                    Position = q.Position,
                    Text = q.Text,
                    Type = q.Type,
                    Points = q.Points,
                    Required = q.Required,
                    Options = (q.Options ?? Enumerable.Empty<QuestionOption>())
                        .OrderBy(o => o.Position)
                        .Select(o => new QuestionOptionForStudentDto
                        {
                            Id = o.Id,
                            Position = o.Position,
                            Text = o.Text
                        }).ToList()
                }).ToList()
        };
    }

    public static StartExamDto ToStartExamDto(Exam exam, Guid submissionId, DateTime startedAt, DateTime deadlineAt)
    {
        return new StartExamDto
        {
            SubmissionId = submissionId,
            StartedAt = startedAt,
            DeadlineAt = deadlineAt,
            Exam = ToExamForStudentDto(exam)
        };
    }
}
