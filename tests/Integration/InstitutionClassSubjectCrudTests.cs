using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    private const string Assignments = "/api/institution/class-subjects";
    private object AssignmentInput(Guid? classId = null, Guid? subjectId = null, Guid? teacherId = null) => new
    {
        ClassId = classId ?? Id("class"),
        SubjectId = subjectId ?? Id("subject-other"),
        TeacherId = teacherId ?? Id("teacher")
    };
    private async Task<Guid> CreateAssignment()
    {
        var response = await Request(Assignments, await Access(), AssignmentInput(), "POST");
        await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.EndsWith(Assignments + "/" + id, response.Headers.Location!.OriginalString);
        await Expect(await Request(response.Headers.Location.OriginalString, await Access()), HttpStatusCode.OK);
        return id;
    }

    [Fact]
    public async Task AssignmentCrudKeepsClassAndSubjectImmutableAndRetainsAudit()
    {
        var token = await Access();
        var id = await CreateAssignment();
        var detail = await Request(Assignments + "/" + id, token);
        Assert.Equal("\"1\"", detail.Headers.ETag!.Tag);
        await Expect(await Request(Assignments + "/" + id, token, new { TeacherId = Id("teacher-other") }, "PUT"), (HttpStatusCode)428);
        await Expect(await Request(Assignments + "/" + id, token, new { TeacherId = Id("teacher-other") }, "PUT", etag: "\"1\""), HttpStatusCode.NoContent);
        await Db(async db =>
        {
            var row = await db.ClassSubjects.SingleAsync(r => r.Id == id);
            Assert.Equal(Id("class"), row.ClassId);
            Assert.Equal(Id("subject-other"), row.SubjectId);
            Assert.Equal(Id("teacher-other"), row.TeacherId);
        });
        await Expect(await Request(Assignments + "/" + id, token, new { TeacherId = Id("teacher") }, "PUT", etag: "\"1\""), HttpStatusCode.Conflict);
        await Expect(await Request(Assignments + "/" + id, token, method: "DELETE"), HttpStatusCode.NoContent);
        await Expect(await Request(Assignments + "/" + id, token), HttpStatusCode.NotFound);
        await Db(async db => Assert.Equal(new[] { "created", "updated", "deleted" }, await db.Set<InstitutionAdministrationEvent>().Where(e => e.InstitutionId == _a && e.EntityId == id).OrderBy(e => e.Revision).Select(e => e.Action).ToArrayAsync()));
    }

    [Theory]
    [InlineData("class")]
    [InlineData("subject")]
    [InlineData("teacher")]
    public async Task AssignmentForeignReferencesAreRejectedWithoutWrites(string field)
    {
        var token = await Access();
        var before = await Digest(false);
        await Expect(await Request(Assignments, token, AssignmentInput(field == "class" ? Id("class", true) : null, field == "subject" ? Id("subject", true) : null, field == "teacher" ? Id("teacher", true) : null), "POST"), HttpStatusCode.NotFound);
        Assert.Equal(before, await Digest(false));
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("InstitutionId")]
    [InlineData("ClassId")]
    [InlineData("SubjectId")]
    public async Task AssignmentUpdateRejectsUnknownOrImmutableFields(string field)
    {
        var body = new JsonObject
        {
            ["TeacherId"] = Id("teacher"),
            [field] = Guid.NewGuid()
        };
        await Expect(await Request(Assignments + "/" + Id("assignment"), await Access(), body, "PUT", etag: "\"0\""), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignmentConcurrentCreateAndUpdateHaveOneWinner()
    {
        var token = await Access();
        var creates = await Task.WhenAll(Request(Assignments, token, AssignmentInput(), "POST"), Request(Assignments, token, AssignmentInput(), "POST"));
        Assert.Equal(1, creates.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, creates.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        var id = await creates.Single(r => r.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<Guid>();
        var edits = await Task.WhenAll(Request(Assignments + "/" + id, token, new { TeacherId = Id("teacher-other") }, "PUT", etag: "\"1\""), Request(Assignments + "/" + id, token, new { TeacherId = Id("teacher") }, "PUT", etag: "\"1\""));
        Assert.Equal(1, edits.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.Equal(1, edits.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    [Theory]
    [InlineData("teacher")]
    [InlineData("student")]
    public async Task AssignmentAdministrativeCommandsRejectNonAdmins(string role)
    {
        var token = await Access(role);
        await Expect(await Request(Assignments, token, AssignmentInput(), "POST"), HttpStatusCode.Forbidden);
        await Expect(await Request(Assignments + "/" + Id("assignment"), token, new { TeacherId = Id("teacher-other") }, "PUT", etag: "\"0\""), HttpStatusCode.Forbidden);
        await Expect(await Request(Assignments + "/" + Id("assignment"), token, method: "DELETE"), HttpStatusCode.Forbidden);
        await Expect(await Request(Assignments + "/" + Id("assignment"), token), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignmentOwnershipAndSqlPagingPreserveTeacherChoices()
    {
        var token = await Access();
        var foreign = Id("assignment", true);
        await Expect(await Request(Assignments + "/" + foreign, token), HttpStatusCode.NotFound);
        await Expect(await Request(Assignments + "/" + foreign, token, new { TeacherId = Id("teacher") }, "PUT", etag: "\"0\""), HttpStatusCode.NotFound);
        await Expect(await Request(Assignments + "/" + foreign, token, method: "DELETE"), HttpStatusCode.NotFound);
        var page = await Request(Assignments + "?pageSize=1&classId=" + Id("class"), token);
        await Expect(page, HttpStatusCode.OK);
        Assert.Single((await page.Content.ReadFromJsonAsync<JsonArray>())!);
        Assert.Equal("1", page.Headers.GetValues("X-Total-Count").Single());
        var teacher = (await (await Request(Assignments, await Access("teacher"))).Content.ReadFromJsonAsync<JsonArray>())!;
        Assert.Single(teacher);
        Assert.Equal(Id("assignment"), teacher[0]!["classSubjectId"]!.GetValue<Guid>());
        await Expect(await Request(Assignments + "?InstitutionId=" + _b, token), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignmentWithGradeCannotChangeTeacherOrBeDeleted()
    {
        var id = await CreateAssignment();
        var token = await Access();
        await Expect(await Request("/api/institution/grades", token, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject-other"), Value = 7, Date = DateOnly.FromDateTime(DateTime.UtcNow) }, "POST"), HttpStatusCode.Created);
        var before = await Digest(false);
        await Expect(await Request(Assignments + "/" + id, token, new { TeacherId = Id("teacher-other") }, "PUT", etag: "\"1\""), HttpStatusCode.Conflict);
        await Expect(await Request(Assignments + "/" + id, token, method: "DELETE"), HttpStatusCode.Conflict);
        Assert.Equal(before, await Digest(false));
    }

    [Fact]
    public async Task AssignmentTeacherExamStudentSubmissionAndAutomaticGradeRemainCompatible()
    {
        var id = await CreateAssignment();
        var teacher = await Access("teacher");
        var admin = await Access();
        var created = await Request(Exams, teacher, new CreateExamDto { ClassSubjectId = id, AvailableFrom = DateTime.UtcNow.AddMinutes(-5), AvailableUntil = DateTime.UtcNow.AddHours(1), DurationMinutes = 30, PassingPercent = 60, AssignedStudents = new() { new() { StudentId = Id("student") } }, Questions = new() { new() { Position = 0, Text = "Question", Type = QuestionType.Single, Points = 10, Required = true, Options = new() { new() { Position = 0, Text = "Yes", IsCorrect = true }, new() { Position = 1, Text = "No", IsCorrect = false } } } } }, "POST");
        await Expect(created, HttpStatusCode.Created);
        var examId = await created.Content.ReadFromJsonAsync<Guid>();
        await Expect(await Request(Exams + "/" + examId + "/publish", teacher, method: "POST"), HttpStatusCode.NoContent);
        // Draft/published exams already protect authorship, before the first attempt.
        await Expect(await Request(Assignments + "/" + id, admin, new { TeacherId = Id("teacher-other") }, "PUT", etag: "\"1\""), HttpStatusCode.Conflict);
        await Expect(await Request(Assignments + "/" + id, admin, method: "DELETE"), HttpStatusCode.Conflict);
        var student = await Access("student");
        var startResponse = await Request(Exams + "/" + examId + "/start", student, method: "POST");
        await Expect(startResponse, HttpStatusCode.OK);
        var start = (await startResponse.Content.ReadFromJsonAsync<StartExamDto>())!;
        Assert.Equal("Other subject", start.Exam.Name);
        Assert.DoesNotContain("isCorrect", await startResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Guid correct = default;
        await Db(async db => correct = await db.QuestionOptions.Where(o => o.Question.ExamId == examId && o.IsCorrect).Select(o => o.Id).SingleAsync());
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = new() { new() { QuestionId = start.Exam.Questions[0].Id, SelectedOptionIds = new() { correct } } } }, "POST"), HttpStatusCode.OK);
        await Db(async db =>
        {
            Assert.True(await db.Grades.AnyAsync(g => g.ClassSubjectId == id && g.StudentId == Id("student") && g.Value == 10));
            Assert.NotNull(await db.ExamSubmissions.Where(s => s.Id == start.SubmissionId).Select(s => s.DisplayOrderJson).SingleAsync());
        });
        var before = await Digest(false);
        await Expect(await Request(Assignments + "/" + id, admin, method: "DELETE"), HttpStatusCode.Conflict);
        Assert.Equal(before, await Digest(false));
    }

    [Fact]
    public async Task AssignmentExamCreationAndTeacherReassignmentCannotCrossAuthorship()
    {
        var id = await CreateAssignment();
        var teacher = await Access("teacher");
        var admin = await Access();
        var exam = new CreateExamDto
        {
            ClassSubjectId = id,
            AvailableFrom = DateTime.UtcNow.AddMinutes(10),
            AvailableUntil = DateTime.UtcNow.AddHours(1),
            DurationMinutes = 30,
            PassingPercent = 60,
            AssignedStudents = new()
            {
                new()
                {
                    StudentId = Id("student")
                }
            },
            Questions = new()
            {
                new()
                {
                    Position = 0,
                    Text = "Q",
                    Type = QuestionType.Open,
                    Points = 10,
                    Required = true
                }
            }
        };
        var results = await Task.WhenAll(Request(Exams, teacher, exam, "POST"), Request(Assignments + "/" + id, admin, new { TeacherId = Id("teacher-other") }, "PUT", etag: "\"1\""));
        if (results[0].StatusCode == HttpStatusCode.Created)
        {
            await Expect(results[1], HttpStatusCode.Conflict);
            await Db(async db => Assert.Equal(Id("teacher"), (await db.ClassSubjects.SingleAsync(c => c.Id == id)).TeacherId));
        }
        else
        {
            Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden });
            await Expect(results[1], HttpStatusCode.NoContent);
            await Db(async db => Assert.False(await db.Exams.AnyAsync(e => e.ClassSubjectId == id)));
        }
    }

    [Fact]
    public async Task AssignmentDeletionAndExamInsertKeepEveryCommittedExam()
    {
        var id = await CreateAssignment();
        var teacher = await Access("teacher");
        var admin = await Access();
        var exam = new CreateExamDto
        {
            ClassSubjectId = id,
            AvailableFrom = DateTime.UtcNow.AddMinutes(10),
            AvailableUntil = DateTime.UtcNow.AddHours(1),
            DurationMinutes = 30,
            PassingPercent = 60,
            AssignedStudents = new()
            {
                new()
                {
                    StudentId = Id("student")
                }
            },
            Questions = new()
            {
                new()
                {
                    Position = 0,
                    Text = "Q",
                    Type = QuestionType.Open,
                    Points = 10,
                    Required = true
                }
            }
        };
        var results = await Task.WhenAll(Request(Exams, teacher, exam, "POST"), Request(Assignments + "/" + id, admin, method: "DELETE"));
        if (results[0].StatusCode == HttpStatusCode.Created)
        {
            await Expect(results[1], HttpStatusCode.Conflict);
            await Db(async db =>
            {
                Assert.True(await db.ClassSubjects.AnyAsync(c => c.Id == id));
                Assert.True(await db.Exams.AnyAsync(e => e.ClassSubjectId == id));
            });
        }
        else
        {
            await Expect(results[0], HttpStatusCode.NotFound);
            await Expect(results[1], HttpStatusCode.NoContent);
            await Db(async db =>
            {
                Assert.False(await db.ClassSubjects.AnyAsync(c => c.Id == id));
                Assert.False(await db.Exams.AnyAsync(e => e.ClassSubjectId == id));
            });
        }
    }
}
