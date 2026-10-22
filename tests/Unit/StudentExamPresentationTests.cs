using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public class StudentExamPresentationTests
{
    private static Exam Template() => new()
    {
        Id = Guid.NewGuid(), Name = "Matematica", Questions = Enumerable.Range(1, 3).Select(n => new Question
        {
            Id = Guid.NewGuid(), Position = n * 10, Text = "Q" + n,
            Type = n == 3 ? QuestionType.Open : n == 1 ? QuestionType.Single : QuestionType.Multiple,
            Options = n == 3 ? new() : Enumerable.Range(1, 3).Select(o => new QuestionOption
            { Id = Guid.NewGuid(), Position = o * 10, Text = "O" + o, IsCorrect = o == 1 }).ToList()
        }).ToList()
    };

    [Fact]
    public void ControlledShuffle_PersistsEveryId_WithoutChangingCanonicalPositions()
    {
        var exam = Template();
        var before = JsonSerializer.Serialize(exam);
        var calls = new List<int>();
        var json = StudentExamPresentation.CreateOrder(exam, bound => { calls.Add(bound); return 0; });
        var dto = StudentExamPresentation.Build(exam, json);
        Assert.Equal(new[] { 3, 2, 3, 2, 3, 2 }, calls);
        Assert.Equal(new[] { exam.Questions[1].Id, exam.Questions[2].Id, exam.Questions[0].Id }, dto.Questions.Select(q => q.Id));
        Assert.Equal(new[] { 1, 2, 3 }, dto.Questions.Select(q => q.Position));
        foreach (var q in dto.Questions.Where(q => q.Type != QuestionType.Open))
        {
            var source = exam.Questions.Single(x => x.Id == q.Id);
            Assert.Equal(new[] { source.Options[1].Id, source.Options[2].Id, source.Options[0].Id }, q.Options.Select(o => o.Id));
            Assert.Equal(new[] { 1, 2, 3 }, q.Options.Select(o => o.Position));
        }
        Assert.Empty(dto.Questions.Single(q => q.Type == QuestionType.Open).Options);
        Assert.Equal(before, JsonSerializer.Serialize(exam));
    }

    [Fact]
    public void IndependentAttempts_ConsumeSeparateGeneratorCalls_AndResumeExactly()
    {
        var exam = Template();
        var first = StudentExamPresentation.CreateOrder(exam, _ => 0);
        var second = StudentExamPresentation.CreateOrder(exam, bound => bound - 1);
        Assert.NotEqual(first, second); // Controlled draws, never a probabilistic assertion.
        var recreated = JsonSerializer.Deserialize<Exam>(JsonSerializer.Serialize(exam))!;
        Assert.Equal(JsonSerializer.Serialize(StudentExamPresentation.Build(exam, first)),
            JsonSerializer.Serialize(StudentExamPresentation.Build(recreated, first)));
    }

    [Fact]
    public void HistoricalNull_PreservesCanonicalPositions()
    {
        var exam = Template();
        var dto = StudentExamPresentation.Build(exam, null);
        Assert.Equal(exam.Questions.Select(q => q.Id), dto.Questions.Select(q => q.Id));
        Assert.Equal(new[] { 10, 20, 30 }, dto.Questions.Select(q => q.Position));
        Assert.Equal(new[] { 10, 20, 30 }, dto.Questions[0].Options.Select(o => o.Position));
    }

    [Fact]
    public void StudentPayload_NeverContainsCorrectnessOrCanonicalPositions()
    {
        var exam = Template();
        var json = JsonSerializer.Serialize(StudentExamPresentation.Build(exam, StudentExamPresentation.CreateOrder(exam, _ => 0)));
        Assert.DoesNotContain("IsCorrect", json);
        Assert.DoesNotContain("\"Position\":10", json);
    }

    [Theory]
    [InlineData("{\"Name\":\"arbitrary\"}")]
    [InlineData("{}")]
    public void InputNames_AreIgnoredAndOptional(string payload)
    {
        Assert.Equal(string.Empty, JsonSerializer.Deserialize<CreateExamDto>(payload)!.Name);
        Assert.Equal(string.Empty, JsonSerializer.Deserialize<UpdateExamDto>(payload)!.Name);
    }

    [Fact]
    public void CorruptOrder_FailsClosed()
    {
        var exam = Template();
        var json = JsonSerializer.Serialize(new StudentExamPresentation.Order(new[] { Guid.NewGuid() }, new()));
        Assert.Throws<InvalidOperationException>(() => StudentExamPresentation.Build(exam, json));
    }
}
