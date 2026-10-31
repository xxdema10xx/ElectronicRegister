using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class ExamCreationMapper
{
    public static (List<Question> Questions, List<ExamStudent> AssignedStudents) MapFromCreateDto(CreateExamDto dto, Guid examId, DateTime nowUtc)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));
        if (dto.Questions is null)
            throw new ArgumentException("La lista delle domande non può essere nulla.", nameof(dto));
        if (dto.AssignedStudents is null)
            throw new ArgumentException("La lista degli studenti non può essere nulla.", nameof(dto));
        var questions = dto.Questions.Select(q =>
        {
            if (q is null)
                throw new ArgumentException("Una domanda non può essere nulla.");
            if (q.Options is null)
                throw new ArgumentException("La lista delle opzioni non può essere nulla.");
            var questionId = Guid.NewGuid();
            return new Question
            {
                Id = questionId,
                ExamId = examId,
                Position = q.Position,
                Text = q.Text,
                Type = q.Type,
                Points = q.Points,
                Required = q.Required,
                Options = q.Options.Select(o =>
                {
                    if (o is null)
                        throw new ArgumentException("Un'opzione non può essere nulla.");
                    return new QuestionOption
                    {
                        Id = Guid.NewGuid(),
                        QuestionId = questionId,
                        Position = o.Position,
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    };
                }).ToList()
            };
        }).ToList();
        var assignedStudents = dto.AssignedStudents.Select(s => new ExamStudent { ExamId = examId, StudentId = s.StudentId, ExtraTimeMinutes = s.ExtraTimeMinutes, AssignedAt = nowUtc }).ToList();
        return (questions, assignedStudents);
    }

    public static Exam MapToExam(
        CreateExamDto dto,
        Guid examId,
        List<Question> questions,
        List<ExamStudent> assignedStudents,
        string subjectName
    )
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));
        if (questions is null)
            throw new ArgumentNullException(nameof(questions));
        if (assignedStudents is null)
            throw new ArgumentNullException(nameof(assignedStudents));
        return new Exam
        {
            Id = examId,
            ClassSubjectId = dto.ClassSubjectId,
            RetakeOfExamId = dto.RetakeOfExamId,
            Name = subjectName,
            Description = dto.Description,
            AvailableFrom = dto.AvailableFrom,
            AvailableUntil = dto.AvailableUntil,
            DurationMinutes = dto.DurationMinutes,
            PassingPercent = dto.PassingPercent,
            TotalPoints = questions.Sum(q => q.Points),
            Status = Domain.Enums.ExamStatus.Draft,
            Questions = questions,
            AssignedStudents = assignedStudents
        };
    }
}
