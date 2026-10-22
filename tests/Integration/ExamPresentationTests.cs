using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class Mt3dHttpTests
{
    [Fact]
    public async Task ExamPresentation_NameComesFromSubjectOnCreateUpdateAndRename()
    {
        var teacher = await Access("teacher");
        var input = InputExam();
        // Anonymous payload bypasses JsonIgnore on the client DTO to exercise hostile raw JSON.
        var raw = new { input.ClassSubjectId, Name = "Arbitrary hostile title", input.Description,
            input.AvailableFrom, input.AvailableUntil, input.DurationMinutes, input.PassingPercent,
            input.AssignedStudents, input.Questions };
        var response = await Request(Exams, teacher, raw, "POST");
        await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        var update = new { input.ClassSubjectId, Name = "Another hostile title", Description = "Teacher description",
            input.AvailableFrom, input.AvailableUntil, input.DurationMinutes, input.PassingPercent, input.Questions };
        await Expect(await Request(Exams + "/" + id, teacher, update, "PUT"), HttpStatusCode.NoContent);
        await Db(async db => Assert.Equal("Matematica", (await db.Exams.AsNoTracking().SingleAsync(e => e.Id == id)).Name));
        await Expect(await Request("/api/institution/subjects/" + Id("subject"), await Access(), new { Name = "Analisi" }, "PUT"), HttpStatusCode.NoContent);
        var detail = (await (await Request(Exams + "/" + id, teacher)).Content.ReadFromJsonAsync<ExamDetailDto>())!;
        Assert.Equal("Analisi", detail.Name);
        Assert.Equal("Teacher description", detail.Description);
        await Db(async db =>
        {
            Assert.Equal("Analisi", (await db.Exams.AsNoTracking().SingleAsync(e => e.Id == id)).Name);
            Assert.Equal("Matematica", (await db.Subjects.AsNoTracking().SingleAsync(s => s.Id == Id("subject", true))).Name);
        });
    }

    [Theory]
    [InlineData(QuestionType.Single)]
    [InlineData(QuestionType.Multiple)]
    public async Task ExamPresentation_StartSessionHostRestartAndCanonicalTemplateAgree(QuestionType type)
    {
        var input = InputExam(type: type);
        var second = InputExam(type: type).Questions[0]; second.Position = 1; second.Text = "Second";
        input.Questions.Add(second);
        var teacher = await Access("teacher");
        var created = await Request(Exams, teacher, input, "POST"); await Expect(created, HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        var detail = (await (await Request(Exams + "/" + id, teacher)).Content.ReadFromJsonAsync<ExamDetailDto>())!;
        var canonical = JsonSerializer.Serialize(detail.Questions);
        var start = await Start(detail);
        Assert.Equal("Matematica", start.Exam.Name);
        string? persisted = null;
        await Db(async db => persisted = (await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId)).DisplayOrderJson);
        Assert.NotNull(persisted);
        var student = await Access("student");
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, exam: true);
        var response = await Request(Submission(start.SubmissionId, "session"), student, host: host); await Expect(response, HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<SubmissionSessionDto>())!;
        Assert.Equal(JsonSerializer.Serialize(start.Exam), JsonSerializer.Serialize(session.Exam));
        Assert.DoesNotContain("isCorrect", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var after = (await (await Request(Exams + "/" + id, teacher)).Content.ReadFromJsonAsync<ExamDetailDto>())!;
        Assert.Equal(canonical, JsonSerializer.Serialize(after.Questions));
        var submitted = await Request(Submission(start.SubmissionId, "submit"), student,
            new SubmitExamDto { ClientRevision = 1, Answers = detail.Questions.Select(q => new AnswerInputDto
            { QuestionId = q.Id, SelectedOptionIds = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList() }).ToList() }, "POST");
        await Expect(submitted, HttpStatusCode.OK);
        Assert.True((await submitted.Content.ReadFromJsonAsync<SubmissionResultDto>())!.Passed);
    }

    [Fact]
    public async Task ExamPresentation_ConcurrentStartPersistsOneOrderAndTenantCannotReadIt()
    {
        var exam = await Create(); var teacher = await Access("teacher"); var student = await Access("student");
        await Expect(await Request(Exams + "/" + exam.Id + "/publish", teacher, method: "POST"), HttpStatusCode.NoContent);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Request(Exams + "/" + exam.Id + "/start", student, method: "POST")));
        Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var start = (await responses.Single(r => r.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<StartExamDto>())!;
        await Db(async db =>
        {
            var row = Assert.Single(await db.ExamSubmissions.AsNoTracking().Where(s => s.ExamId == exam.Id).ToListAsync());
            Assert.NotNull(row.DisplayOrderJson);
            Assert.Equal(start.SubmissionId, row.Id);
        });
        await Expect(await Request(Submission(start.SubmissionId, "session"), await Access("student", true)), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExamPresentation_HistoricalNullReturnsCanonicalOrder()
    {
        var exam = await Create(); var start = await Start(exam);
        await Db(async db => { (await db.ExamSubmissions.SingleAsync(s => s.Id == start.SubmissionId)).DisplayOrderJson = null; await db.SaveChangesAsync(); });
        var response = await Request(Submission(start.SubmissionId, "session"), await Access("student"));
        await Expect(response, HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<SubmissionSessionDto>())!;
        Assert.Equal(exam.Questions.Select(q => (q.Id, q.Position)), session.Exam.Questions.Select(q => (q.Id, q.Position)));
        Assert.Equal(exam.Questions[0].Options.Select(o => (o.Id, o.Position)), session.Exam.Questions[0].Options.Select(o => (o.Id, o.Position)));
    }

    [Fact]
    public async Task ExamPresentation_TwoStudentsAndRetakeHaveSeparatePersistedOrders()
    {
        await Db(async db => { (await db.Students.SingleAsync(s => s.Id == Id("student-other"))).ClassId = Id("class"); await db.SaveChangesAsync(); });
        var other = await OtherStudent();
        var input = InputExam();
        input.AssignedStudents.Add(new() { StudentId = Id("student-other") });
        var teacher = await Access("teacher");
        var response = await Request(Exams, teacher, input, "POST"); await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        var exam = (await (await Request(Exams + "/" + id, teacher)).Content.ReadFromJsonAsync<ExamDetailDto>())!;
        var first = await Start(exam);
        response = await Request(Exams + "/" + id + "/start", other, method: "POST"); await Expect(response, HttpStatusCode.OK);
        var second = (await response.Content.ReadFromJsonAsync<StartExamDto>())!;
        Assert.NotEqual(first.SubmissionId, second.SubmissionId);
        await Expect(await Request(Submission(first.SubmissionId, "session"), other), HttpStatusCode.NotFound);
        var retake = await Create(retake: id); var recovery = await Start(retake);
        Assert.Equal("Matematica", retake.Name); Assert.Equal("Matematica", recovery.Exam.Name);
        await Db(async db =>
        {
            var rows = await db.ExamSubmissions.AsNoTracking().Where(s => s.Id == first.SubmissionId || s.Id == second.SubmissionId || s.Id == recovery.SubmissionId).ToListAsync();
            Assert.Equal(3, rows.Count);
            Assert.All(rows, s => Assert.NotNull(s.DisplayOrderJson));
            // Equal random permutations are legitimate: validate membership rather than inequality.
            foreach (var row in rows)
            {
                var order = JsonSerializer.Deserialize<ElectronicRegisterAPI.Domain.Models.StudentExamPresentation.Order>(row.DisplayOrderJson!)!;
                Assert.Equal((row.ExamId == id ? exam : retake).Questions.Select(q => q.Id).OrderBy(x => x), order.Questions.OrderBy(x => x));
            }
        });
    }
}
