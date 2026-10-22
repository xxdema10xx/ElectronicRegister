using System;
using System.Linq;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Application.Mappers;

internal static class ExamStudentMapper
{
    public static ExamForStudentDto ToExamForStudentDto(Exam exam, string? displayOrderJson = null)
    {
        return StudentExamPresentation.Build(exam, displayOrderJson);
    }

    public static StartExamDto ToStartExamDto(Exam exam, ExamSubmission submission)
    {
        return new StartExamDto
        {
            SubmissionId = submission.Id,
            StartedAt = submission.StartedAt,
            DeadlineAt = submission.DeadlineAt,
            Exam = ToExamForStudentDto(exam, submission.DisplayOrderJson)
        };
    }
}
