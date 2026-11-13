using System;
using System.Collections.Generic;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;
using Moq;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public class ExamLifecycleServiceTests
{
    private readonly ExamService _service = new(new Mock<IExamRepository>().Object);

    [Theory]
    [InlineData(SubmissionStatus.InProgress)]
    [InlineData(SubmissionStatus.PendingReview)]
    [InlineData(SubmissionStatus.Graded)]
    [InlineData(SubmissionStatus.Finalized)]
    [InlineData(SubmissionStatus.FailedAuto)]
    public void ExistingAttempt_AlwaysPreventsAnotherStart(SubmissionStatus status)
    {
        var now = DateTime.UtcNow;
        var studentId = Guid.NewGuid();
        var exam = ValidExam(now);
        exam.Status = ExamStatus.Published;
        exam.AssignedStudents.Add(new ExamStudent { StudentId = studentId });
        Assert.Throws<BusinessRuleException>(() => _service.EnsureStartAllowed(exam, new ExamSubmission { Status = status }, studentId, now));
    }

    [Fact]
    public void Publish_RejectsInconsistentStoredTotal()
    {
        var now = DateTime.UtcNow;
        var exam = ValidExam(now);
        exam.TotalPoints = 99m;
        Assert.Throws<BusinessRuleException>(() => _service.EnsurePublishable(exam, now));
    }

    [Fact]
    public void PassingPercent100_IsRejectedBeforeItCanReachConversion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SubmissionGradingService().ConvertToGrade(10m, 10m, 100m));
        Assert.Throws<ArgumentException>(() => _service.EnsureValidPassingPercent(100m));
    }

    [Theory]
    [InlineData(ExamStatus.Draft)]
    [InlineData(ExamStatus.Closed)]
    [InlineData(ExamStatus.Cancelled)]
    public void Close_RequiresPublished(ExamStatus status) =>
        Assert.Throws<BusinessRuleException>(() => _service.EnsureCanClose(new Exam { Name = "Exam", Status = status }));

    private static Exam ValidExam(DateTime now) => new()
    {
        Name = "Valid draft", Status = ExamStatus.Draft, AvailableFrom = now.AddMinutes(-1),
        AvailableUntil = now.AddHours(1), DurationMinutes = 20, PassingPercent = 60, TotalPoints = 5,
        Questions = new List<Question> { new() { Id = Guid.NewGuid(), Text = "Open", Type = QuestionType.Open, Points = 5 } },
        AssignedStudents = new List<ExamStudent> { new() { StudentId = Guid.NewGuid() } }
    };
}
