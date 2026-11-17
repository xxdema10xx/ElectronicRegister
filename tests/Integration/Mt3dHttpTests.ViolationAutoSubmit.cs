using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

// D-07: Multitenancy:ExamApi:ViolationAutoSubmitThreshold. Exams and attempts are created through
// the default host (threshold 0); violations go to a second host with the threshold configured.
public sealed partial class Mt3dHttpTests
{
    private Task<Mt3cHosting.ProgramHost> ThresholdHost(string threshold, bool worker = false) => Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, exam: true,
        extraArguments: new[] { "--Multitenancy:ExamApi:ViolationAutoSubmitThreshold=" + threshold, "--Multitenancy:InstitutionWorker:Enabled=" + worker });
    private Task<HttpResponseMessage> Violate(Guid submission, string token, Mt3cHosting.ProgramHost? host = null, Guid? eventId = null) =>
        Request(Submission(submission, "violations"), token, new InstitutionViolationDto { EventId = eventId ?? Guid.NewGuid(), Type = "tab_hidden" }, "POST", host);
    private static async Task<ViolationReceiptDto> Receipt(HttpResponseMessage response)
    {
        await Expect(response, HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ViolationReceiptDto>())!;
    }
    // Keeps the 5-events-per-10-seconds limit out of the way of the threshold under test.
    private Task AgeViolations(Guid submission) => Execute("UPDATE exam_violation SET occurred_at=DATE_SUB(occurred_at,INTERVAL 1 MINUTE) WHERE submission_id=@id", ("@id", submission));
    private async Task<(int Grades, int Audits)> SubmitEffects(Guid submission)
    {
        var effects = (0, 0);
        await Db(async db => effects = (await db.Grades.CountAsync(g => g.StudentId == Id("student") && g.ClassSubjectId == Id("assignment")), await db.AuditEvents.CountAsync(a => a.EntityId == submission && a.Action == "submission.submit")));
        return effects;
    }

    [Fact]
    public async Task DefaultThresholdZeroKeepsTenViolationsAsSignalsOnly()
    {
        var exam = await Create(); var start = await Start(exam); var student = await Access("student");
        for (var i = 1; i <= 10; i++)
        {
            if (i % 4 == 0) await AgeViolations(start.SubmissionId);
            var response = await Violate(start.SubmissionId, student);
            Assert.Contains("\"autoSubmitted\":false", await response.Content.ReadAsStringAsync());
            var receipt = await Receipt(response);
            Assert.Equal(i, receipt.ViolationCount); Assert.False(receipt.Duplicate); Assert.False(receipt.AutoSubmitted);
        }
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.Equal(10, s.ViolationCount); Assert.Equal("in_progress", s.Status); Assert.Null(s.SubmittedAt); Assert.Null(s.SubmitReason); Assert.Null(s.GradeId);
        });
        Assert.Equal((0, 0), await SubmitEffects(start.SubmissionId));
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = Answers(exam) }, "POST"), HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThirdViolationSubmitsPersistedAnswersOnceAndClosesTheAttempt(bool worker)
    {
        await using var host = await ThresholdHost("3", worker);
        var exam = await Create(); var start = await Start(exam); var student = await Access("student"); var teacher = await Access("teacher");
        var save = await Request(Submission(start.SubmissionId, "answers"), student, new SaveExamAnswersDto { ClientRevision = 1, Answers = Answers(exam) }, "PUT", host);
        await Expect(save, HttpStatusCode.OK); Assert.True((await save.Content.ReadFromJsonAsync<SaveAnswersResponseDto>())!.Accepted);
        for (var i = 1; i <= 2; i++)
        {
            var receipt = await Receipt(await Violate(start.SubmissionId, student, host));
            Assert.Equal(i, receipt.ViolationCount); Assert.False(receipt.AutoSubmitted);
        }
        var third = Guid.NewGuid();
        var submitted = await Receipt(await Violate(start.SubmissionId, student, host, third));
        Assert.Equal(new ViolationReceiptDto(third, 3, false, true), submitted);
        var replay = await Receipt(await Violate(start.SubmissionId, student, host, third));
        Assert.Equal(new ViolationReceiptDto(third, 3, true, true), replay);
        Assert.Equal((1, 1), await SubmitEffects(start.SubmissionId));
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.Equal("graded", s.Status); Assert.Equal("violations", s.SubmitReason); Assert.NotNull(s.SubmittedAt); Assert.NotNull(s.GradeId); Assert.Equal(3, s.ViolationCount);
            Assert.Equal(10m, (await db.Grades.AsNoTracking().SingleAsync(g => g.Id == s.GradeId)).Value);
            var answer = await db.SubmissionAnswers.AsNoTracking().Include(a => a.Options).SingleAsync(a => a.SubmissionId == s.Id);
            Assert.Equal(Answers(exam)[0].SelectedOptionIds, answer.Options.Select(o => o.Id).ToList());
            var audit = await db.AuditEvents.AsNoTracking().SingleAsync(a => a.EntityId == s.Id && a.Action == "submission.submit");
            Assert.Contains("\"source\":\"violation-threshold\"", audit.AfterJson); Assert.Contains("\"reason\":\"Violations\"", audit.AfterJson); Assert.Equal(_studentUser, audit.ActorUserId);
        });
        await Expect(await Violate(start.SubmissionId, student, host), HttpStatusCode.Conflict);
        await Expect(await Request(Submission(start.SubmissionId, "answers"), student, new SaveExamAnswersDto { ClientRevision = 2, Answers = Answers(exam, false) }, "PUT", host), HttpStatusCode.Conflict);
        var manual = await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 3, Answers = Answers(exam, false) }, "POST", host);
        await Expect(manual, HttpStatusCode.OK);
        var persisted = (await manual.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        Assert.Equal(SubmissionStatus.Graded, persisted.Status); Assert.Equal(SubmitReason.Violations, persisted.SubmitReason); Assert.Equal(10m, persisted.Grade);
        var result = await Request(Submission(start.SubmissionId, "result"), student, host: host); await Expect(result, HttpStatusCode.OK);
        var read = (await result.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        Assert.Equal(SubmitReason.Violations, read.SubmitReason); Assert.Equal(10m, read.Grade); Assert.Equal(persisted.SubmittedAt, read.SubmittedAt);
        Assert.Equal(3, (await ArrayResponse(Submission(start.SubmissionId, "violations"), teacher)).Count);
        await Expect(await Request(Exams + "/" + exam.Id + "/close", teacher, method: "POST", host: host), HttpStatusCode.NoContent);
        Assert.Equal((1, 1), await SubmitEffects(start.SubmissionId));
    }

    [Theory]
    [InlineData(QuestionType.Single)]
    [InlineData(QuestionType.Open)]
    public async Task ThresholdSubmitIgnoresEmptyRequiredQuestionsAndDeadline(QuestionType type)
    {
        await using var host = await ThresholdHost("3");
        var exam = await Create(type: type, required: true); var start = await Start(exam); var student = await Access("student");
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = new() }, "POST", host), HttpStatusCode.BadRequest);
        await Expect(await Request(Submission(start.SubmissionId, "submit-expired"), student, method: "POST", host: host), HttpStatusCode.Conflict);
        for (var i = 0; i < 2; i++) Assert.False((await Receipt(await Violate(start.SubmissionId, student, host))).AutoSubmitted);
        Assert.True((await Receipt(await Violate(start.SubmissionId, student, host))).AutoSubmitted);
        var response = await Request(Submission(start.SubmissionId, "result"), student, host: host); await Expect(response, HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        Assert.Equal(SubmitReason.Violations, result.SubmitReason);
        if (type == QuestionType.Open)
        {
            Assert.Equal(SubmissionStatus.PendingReview, result.Status); Assert.Null(result.Grade);
            Assert.Equal((0, 1), await SubmitEffects(start.SubmissionId));
        }
        else
        {
            Assert.Equal(SubmissionStatus.Graded, result.Status); Assert.Equal(1m, result.Grade); Assert.False(result.Passed);
            Assert.Equal((1, 1), await SubmitEffects(start.SubmissionId));
        }
        await Db(async db => Assert.True((await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId)).DeadlineAt > DateTime.UtcNow));
    }

    [Fact]
    public async Task ConcurrentThresholdCrossingsProduceASingleSubmitGradeAndAudit()
    {
        await using var host = await ThresholdHost("3");
        var exam = await Create(); var start = await Start(exam); var student = await Access("student");
        for (var i = 0; i < 2; i++) await Receipt(await Violate(start.SubmissionId, student, host));
        await AgeViolations(start.SubmissionId);
        var responses = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Violate(start.SubmissionId, student, host)));
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var receipts = await Task.WhenAll(responses.Where(r => r.StatusCode == HttpStatusCode.OK).Select(r => r.Content.ReadFromJsonAsync<ViolationReceiptDto>()));
        Assert.Contains(receipts, r => r!.AutoSubmitted && r.ViolationCount == 3);
        Assert.Equal((1, 1), await SubmitEffects(start.SubmissionId));
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.Equal("graded", s.Status); Assert.Equal("violations", s.SubmitReason);
            Assert.Equal(2 + receipts.Length, s.ViolationCount);
            Assert.Equal(s.ViolationCount, await db.ExamViolations.CountAsync(v => v.SubmissionId == s.Id));
        });
    }

    [Fact]
    public async Task OtherStudentsAndInstitutionsCannotTriggerOrObserveTheThresholdSubmit()
    {
        await using var host = await ThresholdHost("3");
        var exam = await Create(); var start = await Start(exam); var other = await OtherStudent(); var foreign = await Access("student", true); var teacher = await Access("teacher");
        var before = await Digest(false);
        for (var i = 0; i < 3; i++)
        {
            await Expect(await Violate(start.SubmissionId, other, host), HttpStatusCode.NotFound);
            await Expect(await Violate(start.SubmissionId, foreign, host), HttpStatusCode.NotFound);
        }
        await Expect(await Violate(start.SubmissionId, teacher, host), HttpStatusCode.Forbidden);
        Assert.Equal(before, await Digest(false));
        var student = await Access("student");
        for (var i = 0; i < 3; i++) await Receipt(await Violate(start.SubmissionId, student, host));
        foreach (var token in new[] { other, foreign })
        {
            await Expect(await Request(Submission(start.SubmissionId, "result"), token, host: host), HttpStatusCode.NotFound);
            await Expect(await Request(Submission(start.SubmissionId, "session"), token, host: host), HttpStatusCode.NotFound);
        }
        await Expect(await Request(Submission(start.SubmissionId, "violations"), await Access("teacher", true), host: host), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("129")]
    [InlineData("three")]
    public async Task InvalidViolationThresholdRefusesStartup(string threshold)
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(async () => { await using var host = await ThresholdHost(threshold); });
        Assert.Matches("Invalid exam violation configuration|ViolationAutoSubmitThreshold", error.ToString());
    }
}
