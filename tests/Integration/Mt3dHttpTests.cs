using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Application.DependencyInjection;
using ElectronicRegisterAPI.Business.DependencyInjection;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;



using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using ElectronicRegisterAPI.Api.Security;
using ElectronicRegisterAPI.Api.Controllers;
using ElectronicRegisterAPI.Infrastructure.Security;
namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed partial class Mt3dHttpTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private const string Password = "Mt3cTestPassword!";
    private static readonly string Hash = BCrypt.Net.BCrypt.HashPassword(Password);
    private readonly Guid _a=Guid.NewGuid(),_b=Guid.NewGuid(),_teacherUser=Guid.NewGuid(),_studentUser=Guid.NewGuid(),_adminUser=Guid.NewGuid(),_admin2=Guid.NewGuid();
    private readonly Dictionary<string,Guid[]> _ids=new();
    private Mt3cHosting.ProgramHost _host=null!;
    private readonly Guid _otherStudentUser = Guid.NewGuid();
    private Guid Id(string kind,bool b=false) { if(!_ids.TryGetValue(kind,out var ids))_ids[kind]=ids=new[]{Guid.NewGuid(),Guid.NewGuid()};return ids[b?1:0]; }
    private async Task Db(Func<ElectronicRegisterContext,Task> action)
    { using var scope=fixture.ScopeFactory.CreateScope();await action(scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>()); }
    private async Task Execute(string sql,params (string,object)[] values)
    { await using var c=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);await using var cmd=new MySqlCommand(sql,c);foreach(var (key,value) in values)cmd.Parameters.AddWithValue(key,value);await cmd.ExecuteNonQueryAsync(); }
    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        await Db(async db=>
        {
            foreach(var institution in new[]{_a,_b})db.Institutions.Add(new(){Id=institution,Code="MT3D_"+institution.ToString("N"),Name="Temporary ITS",Status="active"});
            await db.SaveChangesAsync();
            foreach(var isB in new[]{false,true})
            {
                var owner=isB?_b:_a;
                void Add<T>(T entity) where T:class {db.Add(entity);db.Entry(entity).Property<Guid>("InstitutionId").CurrentValue=owner;}
                Add(new E.Biennium{Id=Id("biennium",isB),StartYear=new DateOnly(2500,9,1),EndYear=new DateOnly(2502,8,31)});
                Add(new E.StudyArea{Id=Id("area",isB),Name="Same area",Description="Test"});
                Add(new E.StudyPath{Id=Id("path",isB),Name="Same path"});
                Add(new E.BienniumStudyArea{Id=Id("bsa",isB),BienniumId=Id("biennium",isB),StudyAreaId=Id("area",isB)});
                Add(new E.BienniumStudyPath{Id=Id("bsp",isB),BienniumStudyAreaId=Id("bsa",isB),StudyPathId=Id("path",isB)});
                foreach(var suffix in new[]{"","-other"})
                {
                    Add(new E.Class{Id=Id("class"+suffix,isB),BienniumStudyPathId=Id("bsp",isB),Name="Class"+suffix});
                    Add(new E.Teacher{Id=Id("teacher"+suffix,isB),FirstName="Teacher",LastName="Same"});
                    Add(new E.Student{Id=Id("student"+suffix,isB),FirstName="Student",LastName="Same",ClassId=Id("class"+suffix,isB)});
                    Add(new E.Subject{Id=Id("subject"+suffix,isB),Name=suffix==""?"Matematica":"Other subject"});
                    Add(new E.ClassSubject{Id=Id("assignment"+suffix,isB),ClassId=Id("class"+suffix,isB),TeacherId=Id("teacher"+suffix,isB),SubjectId=Id("subject"+suffix,isB)});
                }
                // Same subject also taught by another teacher in another class:
                // intersecting subject IDs alone would leak a by-teacher association.
                Add(new E.ClassSubject{Id=Id("assignment-shared",isB),ClassId=Id("class-other",isB),TeacherId=Id("teacher-other",isB),SubjectId=Id("subject",isB)});
            }
            db.Users.AddRange(new[]{_teacherUser,_studentUser,_adminUser,_admin2}.Select(id=>new E.User{Id=id,Email=id.ToString("N")+"@mt3d.example.test",PasswordHash=Hash,Role=id==_teacherUser?"admin":"student",TeacherId=id==_teacherUser?Id("teacher"):null}));
            foreach(var isB in new[]{false,true})
            {
                foreach(var role in new[]{"admin","teacher","student"})
                {
                    var user=role=="admin"?_adminUser:role=="teacher"?_teacherUser:_studentUser;
                    var membership=Id(role+"-membership",isB);var owner=isB?_b:_a;
                    db.InstitutionMemberships.Add(new(){Id=membership,InstitutionId=owner,UserId=user,Status="active",InstitutionMembershipRoles=new List<E.InstitutionMembershipRole>{new(){MembershipId=membership,Role=role=="admin"?"InstitutionAdmin":role=="teacher"?"Teacher":"Student"}}});
                    if(role=="teacher")db.InstitutionTeacherProfiles.Add(new(){InstitutionId=owner,MembershipId=membership,TeacherId=Id("teacher",isB)});
                    if(role=="student")db.InstitutionStudentProfiles.Add(new(){InstitutionId=owner,MembershipId=membership,StudentId=Id("student",isB)});
                }
            }
            db.InstitutionMemberships.Add(new(){Id=Id("admin2-membership"),InstitutionId=_a,UserId=_admin2,Status="active",InstitutionMembershipRoles=new List<E.InstitutionMembershipRole>{new(){MembershipId=Id("admin2-membership"),Role="InstitutionAdmin"}}});
            await db.SaveChangesAsync();
        });
        _host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,exam:true);
    }

    private async Task<HttpResponseMessage> Request(string route, string? token = null, object? body = null, string method = "GET", Mt3cHosting.ProgramHost? host = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await (host ?? _host).Client.SendAsync(request);
    }
    private async Task<string> Ticket(string role = "admin")
    {
        var user = role == "teacher" ? _teacherUser : role == "student" ? _studentUser : _adminUser;
        var response = await Request("/api/Auth/identity/login", body: new LoginDto { Email = user.ToString("N") + "@mt3d.example.test", Password = Password }, method: "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
    }
    private async Task<string> Access(string role = "admin", bool b = false)
    {
        var response = await Request("/api/Auth/select-institution", await Ticket(role), new SelectInstitutionDto { InstitutionId = b ? _b : _a, MembershipId = Id(role + "-membership", b) }, "POST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }
    private static string Route(string family) => "/api/institution/" + family + "s";
    private static object Input(string family, string name = "Created") => family == "subject" ? new { Name = name } : (object)new { FirstName = name, LastName = "Http" };
    private async Task<JsonArray> ArrayResponse(string route, string token)
    {
        var response = await Request(route, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonArray>())!;
    }
    private static Guid RowId(JsonNode? row) => row!["id"]!.GetValue<Guid>();
    private async Task<string> Digest(bool b)
    {
        await using var c = await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);
        var rows = new List<string>();
        foreach (var table in new[] { "student", "teacher", "subject", "class_subject", "grade", "exam", "exam_student", "exam_submission", "question", "question_option", "submission_answer", "exam_violation", "audit_event", "institution_student_profile", "institution_teacher_profile" })
        {
            await using var command = new MySqlCommand("SELECT * FROM `" + table + "` WHERE institution_id=@i ORDER BY 1", c);
            command.Parameters.AddWithValue("@i", b ? _b : _a);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { var cells = new object[reader.FieldCount]; reader.GetValues(cells); rows.Add(System.Text.Json.JsonSerializer.Serialize(cells)); }
        }
        return string.Join("\n", rows);
    }

    private static string Exams => "/api/institution/exams";
    private static string Submission(Guid id, string suffix) => "/api/institution/submissions/" + id + "/" + suffix;
    private CreateExamDto InputExam(bool b = false, QuestionType type = QuestionType.Single, bool required = true) => new()
    {
        ClassSubjectId = Id("assignment", b), Name = "Same HTTP exam", AvailableFrom = DateTime.UtcNow.AddMinutes(-5),
        AvailableUntil = DateTime.UtcNow.AddHours(2), DurationMinutes = 30, PassingPercent = 60,
        AssignedStudents = new() { new() { StudentId = Id("student", b), ExtraTimeMinutes = 0 } },
        Questions = new() { new() { Position = 0, Text = "Question", Type = type, Points = 10, Required = required,
            Options = type == QuestionType.Open ? new() : new() { new() { Position = 0, Text = "Correct", IsCorrect = true }, new() { Position = 1, Text = "Other", IsCorrect = type == QuestionType.Multiple } } } }
    };
    private static async Task Expect(HttpResponseMessage response, HttpStatusCode expected) =>
        Assert.True(response.StatusCode == expected, $"Expected {expected}, received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    private async Task<ExamDetailDto> Create(bool b = false, QuestionType type = QuestionType.Single, bool required = true, string role = "teacher", Guid? retake = null)
    {
        var dto = InputExam(b, type, required); dto.RetakeOfExamId = retake;
        var token = await Access(role, b); var response = await Request(Exams, token, dto, "POST");
        await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>(); Assert.NotEqual(Guid.Empty, id);
        Assert.Contains(Exams + "/" + id, response.Headers.Location!.ToString());
        var detail = await Request(response.Headers.Location.ToString(), token); await Expect(detail, HttpStatusCode.OK);
        return (await detail.Content.ReadFromJsonAsync<ExamDetailDto>())!;
    }
    private async Task<StartExamDto> Start(ExamDetailDto exam, bool b = false)
    {
        await Expect(await Request(Exams + "/" + exam.Id + "/publish", await Access("teacher", b), method: "POST"), HttpStatusCode.NoContent);
        var response = await Request(Exams + "/" + exam.Id + "/start", await Access("student", b), method: "POST");
        await Expect(response, HttpStatusCode.OK); return (await response.Content.ReadFromJsonAsync<StartExamDto>())!;
    }
    private static List<AnswerInputDto> Answers(ExamDetailDto exam, bool correct = true, string? open = "Answer") => exam.Questions.Select(q => new AnswerInputDto
    {
        QuestionId = q.Id, OpenText = q.Type == QuestionType.Open ? open : null,
        SelectedOptionIds = q.Type == QuestionType.Open ? new() : q.Options.Where(o => o.IsCorrect == correct).Select(o => o.Id).Take(1).ToList()
    }).ToList();
    private async Task Expire(Guid submission) => await Execute("UPDATE exam_submission SET deadline_at=DATE_SUB(UTC_TIMESTAMP(),INTERVAL 1 SECOND) WHERE id=@id", ("@id", submission));

    [Fact]
    public async Task DropdownAndStudentClassFilterAreScopedAndAssignmentSpecific()
    {
        foreach (var b in new[] { false, true })
        foreach (var role in new[] { "admin", "teacher" })
        {
            var token = await Access(role, b);
            var response = await Request("/api/institution/class-subjects", token); await Expect(response, HttpStatusCode.OK);
            var rows = (await response.Content.ReadFromJsonAsync<List<ClassSubjectChoiceDto>>())!;
            Assert.Equal(role == "admin" ? 3 : 1, rows.Count);
            var own = Assert.Single(rows.Where(r => r.ClassSubjectId == Id("assignment", b)));
            Assert.Equal(Id("class", b), own.ClassId); Assert.Equal("Matematica", own.SubjectName); Assert.Equal("Teacher", own.TeacherFirstName);
            Assert.DoesNotContain(rows, r => r.ClassSubjectId == Id("assignment", !b));
            var students = await ArrayResponse(Route("student") + "?classId=" + Id("class", b), token);
            Assert.Equal(Id("student", b), RowId(Assert.Single(students)));
            await Expect(await Request(Route("student") + "?classId=" + Id("class", !b), token), HttpStatusCode.NotFound);
            if (role == "teacher") await Expect(await Request(Route("student") + "?classId=" + Id("class-other", b), token), HttpStatusCode.NotFound);
        }
        await Expect(await Request("/api/institution/class-subjects", await Access("student")), HttpStatusCode.Forbidden);
        await Expect(await Request(Route("student") + "?classId=" + Id("class"), await Access("student")), HttpStatusCode.Forbidden);
        var foreign = InputExam(); foreign.ClassSubjectId = Id("assignment", true);
        await Expect(await Request(Exams, await Access(), foreign, "POST"), HttpStatusCode.NotFound);
        foreign.ClassSubjectId = Id("assignment-other"); foreign.AssignedStudents = new();
        await Expect(await Request(Exams, await Access("teacher"), foreign, "POST"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignedSubmissionIdResumeAndRevisionSurviveNewLoginWithoutNewAttempt()
    {
        var exam = await Create(); var teacher = await Access("teacher");
        var eligible = await ArrayResponse(Exams + "/" + exam.Id + "/eligible-students", teacher);
        Assert.Equal(Id("student"), RowId(Assert.Single(eligible)));
        await Expect(await Request(Exams + "/" + exam.Id + "/publish", teacher, method: "POST"), HttpStatusCode.NoContent);
        var student = await Access("student");
        var assigned = await (await Request(Exams + "/assigned", student)).Content.ReadFromJsonAsync<List<AssignedExamDto>>();
        Assert.Null(Assert.Single(assigned!).SubmissionId);
        var response = await Request(Exams + "/" + exam.Id + "/start", student, method: "POST"); await Expect(response, HttpStatusCode.OK);
        var start = (await response.Content.ReadFromJsonAsync<StartExamDto>())!;
        await Expect(await Request(Exams + "/" + exam.Id + "/start", student, method: "POST"), HttpStatusCode.Conflict);
        var save = await Request(Submission(start.SubmissionId, "answers"), student, new SaveExamAnswersDto { ClientRevision = 2, Answers = Answers(exam) }, "PUT");
        await Expect(save, HttpStatusCode.OK); Assert.True((await save.Content.ReadFromJsonAsync<SaveAnswersResponseDto>())!.Accepted);
        student = await Access("student");
        var sessionResponse = await Request(Submission(start.SubmissionId, "session"), student); await Expect(sessionResponse, HttpStatusCode.OK);
        var json = await sessionResponse.Content.ReadAsStringAsync();
        foreach (var forbidden in new[] { "isCorrect", "awardedPoints", "teacherComment", "suggestedGrade" }) Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        var session = (await sessionResponse.Content.ReadFromJsonAsync<SubmissionSessionDto>())!;
        Assert.Equal(2, session.CurrentRevision); Assert.Equal(start.StartedAt, session.StartedAt); Assert.Equal(start.DeadlineAt, session.DeadlineAt);
        Assert.Equal(Answers(exam)[0].SelectedOptionIds, Assert.Single(session.Answers).SelectedOptionIds);
        var stale = await Request(Submission(start.SubmissionId, "answers"), student, new SaveExamAnswersDto { ClientRevision = 1, Answers = new() }, "PUT");
        await Expect(stale, HttpStatusCode.OK); Assert.False((await stale.Content.ReadFromJsonAsync<SaveAnswersResponseDto>())!.Accepted);
        var submitted = await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 3, Answers = Answers(exam) }, "POST");
        await Expect(submitted, HttpStatusCode.OK); Assert.True((await submitted.Content.ReadFromJsonAsync<SubmissionResultDto>())!.Passed);
        var again = await (await Request(Exams + "/assigned", await Access("student"))).Content.ReadFromJsonAsync<List<AssignedExamDto>>();
        Assert.Equal(start.SubmissionId, Assert.Single(again!).SubmissionId);
        await Expect(await Request(Submission(start.SubmissionId, "result"), student), HttpStatusCode.OK);
        var after = await Request(Submission(start.SubmissionId, "answers"), student, new SaveExamAnswersDto { ClientRevision = 4, Answers = new() }, "PUT");
        await Expect(after, HttpStatusCode.Conflict);
        await Db(async db => Assert.Equal(1, await db.ExamSubmissions.CountAsync(s => s.ExamId == exam.Id)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OfficialClosedResultPassedAndGenericGradeProtectionArePreserved(bool correct)
    {
        var exam = await Create(); var start = await Start(exam); var student = await Access("student"); var teacher = await Access("teacher");
        var response = await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = Answers(exam, correct) }, "POST");
        await Expect(response, HttpStatusCode.OK); var result = (await response.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        Assert.Equal(SubmissionStatus.Graded, result.Status); Assert.Equal(correct, result.Passed); Assert.Equal(correct ? 10m : 1m, result.Grade);
        Guid grade = default;
        await Db(async db => grade = (await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId)).GradeId!.Value);
        await Expect(await Request("/api/institution/grades/" + grade, teacher, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 9, Date = DateOnly.FromDateTime(DateTime.UtcNow) }, "PUT"), HttpStatusCode.Conflict);
        await Expect(await Request("/api/institution/grades/" + grade, await Access(), method: "DELETE"), HttpStatusCode.Conflict);
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = new() }, "POST"), HttpStatusCode.OK);
        await Expect(await Request(Exams + "/" + exam.Id + "/close", teacher, method: "POST"), HttpStatusCode.NoContent);
        await Db(async db =>
        {
            Assert.Equal(1, await db.Grades.CountAsync(g => g.Id == grade));
            Assert.Equal(_a, await db.Database.SqlQueryRaw<Guid>("SELECT institution_id AS Value FROM grade WHERE id={0}", grade).SingleAsync());
            Assert.True(await db.AuditEvents.AnyAsync(a => a.EntityId == start.SubmissionId && a.Action == "submission.submit"));
        });
    }

    [Theory]
    [InlineData(QuestionType.Single)]
    [InlineData(QuestionType.Multiple)]
    [InlineData(QuestionType.Open)]
    public async Task ManualRequiredUsesContentButExpiryUsesOnlyPersistedPartialAnswers(QuestionType type)
    {
        var exam = await Create(type: type); var start = await Start(exam); var student = await Access("student"); var teacher = await Access("teacher");
        await Expect(await Request(Submission(start.SubmissionId, "submit-expired"), student, method: "POST"), HttpStatusCode.Conflict);
        var partial = new List<AnswerInputDto> { new() { QuestionId = exam.Questions[0].Id, OpenText = type == QuestionType.Open ? "   " : null, SelectedOptionIds = new() } };
        var save = await Request(Submission(start.SubmissionId, "answers"), student, new SaveExamAnswersDto { ClientRevision = 1, Answers = partial }, "PUT");
        await Expect(save, HttpStatusCode.OK); Assert.True((await save.Content.ReadFromJsonAsync<SaveAnswersResponseDto>())!.Accepted);
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 2, Answers = partial }, "POST"), HttpStatusCode.BadRequest);
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.Null(s.SubmittedAt); Assert.Equal(1UL, s.LastClientRevision); Assert.Null(s.GradeId);
        });
        await Expire(start.SubmissionId);
        await Expect(await Request(Exams + "/" + exam.Id + "/close", teacher, method: "POST"), HttpStatusCode.Conflict);
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 3, Answers = Answers(exam) }, "POST"), HttpStatusCode.Conflict);
        await Expect(await Request(Submission(start.SubmissionId, "submit-expired"), student, new SubmitExamDto { ClientRevision = 4, Answers = Answers(exam) }, "POST"), HttpStatusCode.BadRequest);
        var response = await Request(Submission(start.SubmissionId, "submit-expired"), student, method: "POST"); await Expect(response, HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        Assert.Equal(type == QuestionType.Open ? SubmissionStatus.PendingReview : SubmissionStatus.Graded, result.Status);
        Assert.Equal(type == QuestionType.Open ? null : (bool?)false, result.Passed);
        if (type == QuestionType.Open) Assert.Null(result.Grade); else Assert.Equal(1, result.Grade);
        await Expect(await Request(Submission(start.SubmissionId, "submit-expired"), student, method: "POST"), HttpStatusCode.OK);
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.Equal(1UL, s.LastClientRevision);
            Assert.Equal("published", (await db.Exams.AsNoTracking().SingleAsync(e => e.Id == exam.Id)).Status);
            var answer = await db.SubmissionAnswers.AsNoTracking().Include(a => a.Options).SingleAsync(a => a.SubmissionId == s.Id);
            Assert.Empty(answer.Options); Assert.Equal(type == QuestionType.Open ? "   " : null, answer.OpenText);
        });
        await Expect(await Request(Exams + "/" + exam.Id + "/close", teacher, method: "POST"), HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("tab_hidden")]
    [InlineData("window_blur")]
    [InlineData("fullscreen_exit")]
    public async Task ViolationsPersistIdempotentlyWithoutPenaltiesAndRejectInvalidEvents(string type)
    {
        var exam = await Create(); var start = await Start(exam); var student = await Access("student"); var teacher = await Access("teacher");
        var id = Guid.NewGuid(); var dto = new InstitutionViolationDto { EventId = id, Type = type, Metadata = "{\"source\":\"browser\"}" };
        var path = Submission(start.SubmissionId, "violations");
        var duplicates = await Task.WhenAll(Request(path, student, dto, "POST"), Request(path, student, dto, "POST"));
        foreach (var response in duplicates) await Expect(response, HttpStatusCode.OK);
        var receipts = await Task.WhenAll(duplicates.Select(r => r.Content.ReadFromJsonAsync<ViolationReceiptDto>()));
        Assert.Single(receipts.Where(r => r!.Duplicate)); Assert.All(receipts, r => Assert.Equal(1, r!.ViolationCount));
        await Expect(await Request(path, student, new InstitutionViolationDto { EventId = id, Type = type, Metadata = "{}" }, "POST"), HttpStatusCode.Conflict);
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Request(path, student, new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = type }, "POST")));
        foreach (var response in concurrent) await Expect(response, HttpStatusCode.OK);
        await Expect(await Request(path, teacher, dto, "POST"), HttpStatusCode.Forbidden);
        await Expect(await Request(path, student, new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = "invented" }, "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(path, student, new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = type, Metadata = new string('x', 1025) }, "POST"), HttpStatusCode.BadRequest);
        var events = await ArrayResponse(path, teacher); Assert.Equal(3, events.Count);
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.Equal(3, s.ViolationCount); Assert.Equal("in_progress", s.Status); Assert.Null(s.GradeId); Assert.Null(s.SubmittedAt); Assert.Null(s.SubmitReason);
            Assert.Equal(3, await db.ExamViolations.CountAsync(v => v.SubmissionId == s.Id));
            Assert.Equal(_a, await db.Database.SqlQueryRaw<Guid>("SELECT institution_id AS Value FROM exam_violation WHERE id={0}", id).SingleAsync());
        });
        for (var i = 0; i < 2; i++) await Expect(await Request(path, student, new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = type }, "POST"), HttpStatusCode.OK);
        await Expect(await Request(path, student, new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = type }, "POST"), HttpStatusCode.Conflict);
        await Expire(start.SubmissionId);
        await Expect(await Request(path, student, new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = type }, "POST"), HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    public async Task OpenReviewFinalizeOverrideAndRetakeKeepOfficialPassedAndOriginalGrade(int grade)
    {
        var exam = await Create(type: QuestionType.Open); var start = await Start(exam); var student = await Access("student"); var teacher = await Access("teacher");
        var response = await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = Answers(exam) }, "POST");
        await Expect(response, HttpStatusCode.OK); var pending = (await response.Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        Assert.Equal(SubmissionStatus.PendingReview, pending.Status); Assert.Null(pending.Passed); Assert.Null(pending.Grade); Assert.Null(pending.TotalScore);
        var reviewResponse = await Request(Submission(start.SubmissionId, "review"), teacher); await Expect(reviewResponse, HttpStatusCode.OK);
        var review = (await reviewResponse.Content.ReadFromJsonAsync<SubmissionReviewDto>())!;
        var answerId = review.Answers[0].AnswerId;
        await Expect(await Request(Submission(start.SubmissionId, "review"), teacher, new ReviewOpenAnswersDto { Answers = new() { new() { AnswerId = answerId, AwardedPoints = 10, Comment = "Reviewed" } } }, "PUT"), HttpStatusCode.NoContent);
        var finalize = new FinalizeSubmissionDto { FinalGrade = grade, OverrideReason = "Motivated override" };
        var concurrent = await Task.WhenAll(Request(Submission(start.SubmissionId, "finalize"), teacher, finalize, "POST"), Request(Submission(start.SubmissionId, "finalize"), await Access(), finalize, "POST"));
        foreach (var r in concurrent) await Expect(r, HttpStatusCode.NoContent);
        var result = (await (await Request(Submission(start.SubmissionId, "result"), student)).Content.ReadFromJsonAsync<SubmissionResultDto>())!;
        Assert.Equal(grade, result.Grade); Assert.Equal(grade >= 6, result.Passed); Assert.Equal(SubmissionStatus.Finalized, result.Status);
        var assigned = await (await Request(Exams + "/assigned", await Access("student"))).Content.ReadFromJsonAsync<List<AssignedExamDto>>();
        Assert.Equal(start.SubmissionId, Assert.Single(assigned!).SubmissionId);
        var originalGradeId = Guid.Empty;
        await Db(async db => originalGradeId = (await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId)).GradeId!.Value);
        await Expect(await Request(Exams + "/" + exam.Id + "/close", teacher, method: "POST"), HttpStatusCode.NoContent);
        var retake = await Create(retake: exam.Id); var retakeStart = await Start(retake);
        await Expect(await Request(Submission(retakeStart.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = Answers(retake, false) }, "POST"), HttpStatusCode.OK);
        await Db(async db =>
        {
            Assert.Equal(grade, (await db.Grades.AsNoTracking().SingleAsync(g => g.Id == originalGradeId)).Value);
            Assert.Equal(2, await db.Grades.CountAsync(g => g.StudentId == Id("student") && g.ClassSubjectId == Id("assignment")));
            Assert.True(await db.AuditEvents.AnyAsync(a => a.EntityId == start.SubmissionId && a.Action == "submission.override"));
        });
    }

    private IEnumerable<(string Method, string Path, string Role, object? Body)> ResourceActions(Guid exam, Guid submission, Guid grade, string managerRole = "admin")
    {
        var e = Exams + "/" + exam;
        yield return ("GET", e, managerRole, null);
        yield return ("GET", e + "/eligible-students", managerRole, null);
        yield return ("GET", e + "/submissions", managerRole, null);
        yield return ("PUT", e, managerRole, InputExam());
        yield return ("PUT", e + "/students", managerRole, new UpdateExamStudentsDto());
        foreach (var action in new[] { "publish", "close", "cancel" }) yield return ("POST", e + "/" + action, managerRole, null);
        yield return ("DELETE", e, managerRole, null);
        yield return ("POST", e + "/start", "student", null);
        foreach (var action in new[] { "session", "result" }) yield return ("GET", Submission(submission, action), "student", null);
        yield return ("PUT", Submission(submission, "answers"), "student", new SaveExamAnswersDto { ClientRevision = 10 });
        yield return ("POST", Submission(submission, "submit"), "student", new SubmitExamDto { ClientRevision = 10 });
        yield return ("POST", Submission(submission, "submit-expired"), "student", null);
        yield return ("POST", Submission(submission, "violations"), "student", new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = "tab_hidden" });
        foreach (var action in new[] { "review", "result", "violations" }) yield return ("GET", Submission(submission, action), managerRole, null);
        yield return ("PUT", Submission(submission, "review"), managerRole, new ReviewOpenAnswersDto());
        yield return ("POST", Submission(submission, "finalize"), managerRole, new FinalizeSubmissionDto());
        yield return ("GET", "/api/institution/grades/" + grade, managerRole, null);
        yield return ("PUT", "/api/institution/grades/" + grade, managerRole, new UpdateGradeDto { SubjectId = Id("subject"), Value = 8, Date = DateOnly.FromDateTime(DateTime.UtcNow) });
        if (managerRole == "admin") yield return ("DELETE", "/api/institution/grades/" + grade, managerRole, null);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("teacher")]
    public async Task EveryResourceActionScopesForeignIdsBeforeReadingLockingOrWriting(string managerRole)
    {
        var foreign = await Create(true); var start = await Start(foreign, true);
        await Expect(await Request(Submission(start.SubmissionId, "submit"), await Access("student", true), new SubmitExamDto { ClientRevision = 1, Answers = Answers(foreign) }, "POST"), HttpStatusCode.OK);
        Guid grade = default; await Db(async db => grade = (await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId)).GradeId!.Value);
        var a = await Digest(false); var b = await Digest(true);
        var tokens = new Dictionary<string, string>(); foreach (var role in new[] { "admin", "teacher", "student" }) tokens[role] = await Access(role);
        foreach (var (method, path, role, body) in ResourceActions(foreign.Id, start.SubmissionId, grade, managerRole))
        {
            var denied = await Request(path, tokens[role], body, method); await Expect(denied, HttpStatusCode.NotFound);
            var missingPath = path.Replace(foreign.Id.ToString(), Guid.NewGuid().ToString()).Replace(start.SubmissionId.ToString(), Guid.NewGuid().ToString()).Replace(grade.ToString(), Guid.NewGuid().ToString());
            var missing = await Request(missingPath, tokens[role], body, method); await Expect(missing, HttpStatusCode.NotFound);
            var foreignProblem = (await denied.Content.ReadFromJsonAsync<JsonObject>())!;
            var missingProblem = (await missing.Content.ReadFromJsonAsync<JsonObject>())!;
            foreignProblem.Remove("traceId"); missingProblem.Remove("traceId");
            Assert.True(JsonNode.DeepEquals(foreignProblem, missingProblem));
        }
        Assert.Empty(await ArrayResponse(Exams, tokens[managerRole]));
        Assert.Empty(await ArrayResponse(Exams + "/assigned", tokens["student"]));
        Assert.Empty(await ArrayResponse("/api/institution/grades", tokens[managerRole]));
        Assert.Equal(0, await (await Request("/api/institution/grades/count", tokens[managerRole])).Content.ReadFromJsonAsync<int>());
        var invalid = InputExam(); invalid.RetakeOfExamId = foreign.Id;
        await Expect(await Request(Exams, tokens[managerRole], invalid, "POST"), HttpStatusCode.NotFound);
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
    }

    [Fact]
    public async Task AssignmentsRejectForeignWrongClassDuplicatesAndInvalidExtraTimeAtomically()
    {
        var exam = await Create(); var token = await Access("teacher"); var before = await Digest(false);
        foreach (var student in new[] { Id("student", true), Id("student-other") })
        {
            var response = await Request(Exams + "/" + exam.Id + "/students", token,
                new UpdateExamStudentsDto { Students = new() { new() { StudentId = student } } }, "PUT");
            await Expect(response, HttpStatusCode.NotFound);
            Assert.Equal(before, await Digest(false));
        }
        foreach (var rows in new[] {
            new List<ExamStudentInputDto> { new() { StudentId = Id("student") }, new() { StudentId = Id("student") } },
            new List<ExamStudentInputDto> { new() { StudentId = Id("student"), ExtraTimeMinutes = -1 } } })
        {
            await Expect(await Request(Exams + "/" + exam.Id + "/students", token, new UpdateExamStudentsDto { Students = rows }, "PUT"), HttpStatusCode.BadRequest);
            Assert.Equal(before, await Digest(false));
        }
        await Expect(await Request(Exams + "/" + exam.Id + "/students", token, new UpdateExamStudentsDto { Students = new() { new() { StudentId = Id("student"), ExtraTimeMinutes = 5 } } }, "PUT"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + exam.Id + "/publish", token, method: "POST"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + exam.Id + "/students", token, new UpdateExamStudentsDto(), "PUT"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GradeCrudPagingFiltersStatisticsAndTeacherOwnershipRemainScoped()
    {
        var teacher = await Access("teacher"); var admin = await Access(); var student = await Access("student");
        var input = new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 7, Date = DateOnly.FromDateTime(DateTime.UtcNow) };
        var response = await Request("/api/institution/grades", teacher, input, "POST"); await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>(); await Expect(await Request(response.Headers.Location!.ToString(), teacher), HttpStatusCode.OK);
        foreach (var token in new[] { admin, teacher, student })
        {
            Assert.Single(await ArrayResponse("/api/institution/grades", token));
            Assert.Equal(1, await (await Request("/api/institution/grades/count", token)).Content.ReadFromJsonAsync<int>());
            await Expect(await Request("/api/institution/grades/filters", token), HttpStatusCode.OK);
            await Expect(await Request("/api/institution/grades/statistics", token), HttpStatusCode.OK);
            var paged = await Request("/api/institution/grades/paged?subjectId=" + Id("subject") + "&studentId=" + Id("student"), token); await Expect(paged, HttpStatusCode.OK);
            Assert.Equal(1, (await paged.Content.ReadFromJsonAsync<GradePageDto>())!.TotalCount);
        }
        await Expect(await Request("/api/institution/grades/" + id, teacher, new UpdateGradeDto { SubjectId = Id("subject"), Value = 8, Date = input.Date }, "PUT"), HttpStatusCode.NoContent);
        await Expect(await Request("/api/institution/grades/" + id, student, new UpdateGradeDto { SubjectId = Id("subject"), Value = 9, Date = input.Date }, "PUT"), HttpStatusCode.Forbidden);
        input.StudentId = Id("student-other"); input.SubjectId = Id("subject-other");
        await Expect(await Request("/api/institution/grades", teacher, input, "POST"), HttpStatusCode.NotFound);
        await Expect(await Request("/api/institution/grades/" + id, admin, method: "DELETE"), HttpStatusCode.NoContent);
        await Expect(await Request("/api/institution/grades/" + id, admin), HttpStatusCode.NotFound);
    }

    private IEnumerable<(string Method, string Path)> ExamActions()
    {
        var endpoints = _host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<InstitutionExamEndpointAttribute>() is not null).ToArray();
        Assert.Equal(33, endpoints.Length);
        foreach (var endpoint in endpoints)
        foreach (var method in endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods)
            yield return (method, "/" + Regex.Replace(endpoint.RoutePattern.RawText!, "\\{[^}]+\\}", _ => Guid.NewGuid().ToString()));
    }

    [Theory]
    [InlineData("none", 401)]
    [InlineData("legacy", 401)]
    [InlineData("selection", 401)]
    [InlineData("expired", 401)]
    [InlineData("audience", 401)]
    [InlineData("other-user", 403)]
    public async Task EveryExamActionRejectsInvalidJwtBeforeModelBindingOrWrites(string fault, int status)
    {
        var token = fault == "none" ? null : fault == "selection" ? await Ticket() : Alter(await Access(), fault);
        var a = await Digest(false); var b = await Digest(true);
        foreach (var (method, path) in ExamActions())
            await Expect(await Request(path, token, method is "POST" or "PUT" ? new { } : null, method), (HttpStatusCode)status);
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("institution")]
    [InlineData("admin-removed")]
    [InlineData("teacher-removed")]
    [InlineData("roles-added")]
    public async Task CommittedRevocationBlocksEveryExamAction(string fault)
    {
        var token = await Access(fault == "teacher-removed" ? "teacher" : "admin");
        if (fault == "membership") await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id", ("@id", Id("admin-membership")));
        else if (fault == "institution") await Execute("UPDATE institution SET status='suspended' WHERE id=@id", ("@id", _a));
        else if (fault == "roles-added") await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@id,'Teacher')", ("@id", Id("admin-membership")));
        else await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id", ("@id", Id(fault == "teacher-removed" ? "teacher-membership" : "admin-membership")));
        var a = await Digest(false); var b = await Digest(true);
        foreach (var (method, path) in ExamActions()) await Expect(await Request(path, token, method is "POST" or "PUT" ? new { } : null, method), HttpStatusCode.Forbidden);
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
        await Expect(await Request(Exams, await Access(b: true)), HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BothRolloutFlagsAreRequiredForEveryNewAction(bool school, bool exam)
    {
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: school, exam: exam);
        var token = await Access(); var before = await Digest(false);
        foreach (var (method, path) in ExamActions()) await Expect(await Request(path, token, method is "POST" or "PUT" ? new { } : null, method, host), HttpStatusCode.Forbidden);
        Assert.Equal(before, await Digest(false));
    }

    [Fact]
    public async Task LegacyWorkerAndNullCallerCannotProcessInstitutionalSubmissions()
    {
        var exam = await Create(); var start = await Start(exam); await Expire(start.SubmissionId);
        var before = await Digest(false);
        using (var scope = _host.Host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IExamSubmissionRepository>();
            Assert.DoesNotContain(await repository.GetExpiredInProgressAsync(DateTime.UtcNow, 100), s => s.Id == start.SubmissionId);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<Domain.Interfaces.Services.IExamSubmissionProcessor>()
                .ProcessSubmitAsync(start.SubmissionId, null, SubmitReason.TimeExpired, null));
        }
        var worker = Assert.Single(_host.Host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<ElectronicRegisterAPI.Api.BackgroundServices.ExpiredExamSubmissionWorker>());
        await worker.ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(before, await Digest(false));
        await Expect(await Request(Submission(start.SubmissionId, "submit-expired"), await Access("student"), method: "POST"), HttpStatusCode.OK);
    }

    [Fact]
    public async Task MultipleRequiredNeedsOneSelectionNotAllCorrectOptions()
    {
        var exam = await Create(type: QuestionType.Multiple); var start = await Start(exam);
        await Expect(await Request(Submission(start.SubmissionId, "submit"), await Access("student"),
            new SubmitExamDto { ClientRevision = 1, Answers = Answers(exam) }, "POST"), HttpStatusCode.OK);
    }

    private async Task<string> OtherStudent()
    {
        await Db(async db =>
        {
            db.Users.Add(new E.User { Id = _otherStudentUser, Email = _otherStudentUser.ToString("N") + "@mt3d.example.test", PasswordHash = Hash, Role = "admin" });
            db.InstitutionMemberships.Add(new() { Id = Id("other-membership"), UserId = _otherStudentUser, InstitutionId = _a, Status = "active",
                InstitutionMembershipRoles = new List<E.InstitutionMembershipRole> { new() { MembershipId = Id("other-membership"), Role = "Student" } } });
            db.InstitutionStudentProfiles.Add(new() { InstitutionId = _a, MembershipId = Id("other-membership"), StudentId = Id("student-other") });
            await db.SaveChangesAsync();
        });
        var login = await Request("/api/Auth/identity/login", body: new LoginDto { Email = _otherStudentUser.ToString("N") + "@mt3d.example.test", Password = Password }, method: "POST");
        await Expect(login, HttpStatusCode.OK);
        var ticket = (await login.Content.ReadFromJsonAsync<IdentityTicketDto>())!.Ticket;
        var selection = await Request("/api/Auth/select-institution", ticket, new SelectInstitutionDto { InstitutionId = _a, MembershipId = Id("other-membership") }, "POST");
        await Expect(selection, HttpStatusCode.OK); return (await selection.Content.ReadFromJsonAsync<InstitutionAccessDto>())!.Token;
    }

    [Fact]
    public async Task AnotherStudentInSameInstitutionCannotReadResumeSubmitOrReportEvents()
    {
        var exam = await Create(); var start = await Start(exam); var other = await OtherStudent(); var before = await Digest(false);
        foreach (var action in ResourceActions(exam.Id, start.SubmissionId, Guid.NewGuid()).Where(a => a.Role == "student"))
            await Expect(await Request(action.Path, other, action.Body, action.Method), HttpStatusCode.NotFound);
        Assert.Empty(await ArrayResponse(Exams + "/assigned", other));
        Assert.Equal(before, await Digest(false));
    }

    [Theory]
    [InlineData("start")]
    [InlineData("close")]
    [InlineData("cancel")]
    public async Task StartRacesPreserveExamLockAndSingleAttempt(string opponent)
    {
        var exam = await Create(); var teacher = await Access("teacher"); var student = await Access("student");
        await Expect(await Request(Exams + "/" + exam.Id + "/publish", teacher, method: "POST"), HttpStatusCode.NoContent);
        var first = Request(Exams + "/" + exam.Id + "/start", student, method: "POST");
        var second = Request(Exams + "/" + exam.Id + "/" + opponent, opponent == "start" ? student : teacher, method: "POST");
        var results = await Task.WhenAll(first, second);
        Assert.Single(results.Where(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(results.Where(r => r.StatusCode == (opponent == "start" || results[0].StatusCode == HttpStatusCode.OK ? HttpStatusCode.OK : HttpStatusCode.NoContent)));
        await Db(async db =>
        {
            var persisted = await db.Exams.AsNoTracking().SingleAsync(e => e.Id == exam.Id);
            var submissions = await db.ExamSubmissions.CountAsync(s => s.ExamId == exam.Id);
            Assert.Equal(persisted.Status == "published" ? 1 : 0, submissions);
        });
    }

    [Fact]
    public async Task ConcurrentDeviceRevisionsAndAutosaveSubmitNeverLoseAcceptedRevision()
    {
        var exam = await Create(); var start = await Start(exam); var student = await Access("student");
        var first = new SaveExamAnswersDto { ClientRevision = 1, Answers = Answers(exam, false) };
        var second = new SaveExamAnswersDto { ClientRevision = 2, Answers = Answers(exam, true) };
        foreach (var result in await Task.WhenAll(Request(Submission(start.SubmissionId, "answers"), student, first, "PUT"), Request(Submission(start.SubmissionId, "answers"), student, second, "PUT")))
            await Expect(result, HttpStatusCode.OK);
        var session = (await (await Request(Submission(start.SubmissionId, "session"), student)).Content.ReadFromJsonAsync<SubmissionSessionDto>())!;
        Assert.Equal(2, session.CurrentRevision); Assert.Equal(Answers(exam)[0].SelectedOptionIds, session.Answers[0].SelectedOptionIds);
        var save = Request(Submission(start.SubmissionId, "answers"), student, new SaveExamAnswersDto { ClientRevision = 3, Answers = Answers(exam, false) }, "PUT");
        var submit = Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 4, Answers = Answers(exam, true) }, "POST");
        var responses = await Task.WhenAll(save, submit); await Expect(responses[1], HttpStatusCode.OK);
        Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await Db(async db => { var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId); Assert.Equal(4UL, s.LastClientRevision); Assert.NotNull(s.GradeId); });
    }

    [Fact]
    public async Task LateManualAndExpiryRaceCreatesOnlyOneOfficialGrade()
    {
        var exam = await Create(required: false); var start = await Start(exam); var student = await Access("student"); await Expire(start.SubmissionId);
        var manual = Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = Answers(exam) }, "POST");
        var expired = Request(Submission(start.SubmissionId, "submit-expired"), student, method: "POST");
        var responses = await Task.WhenAll(manual, expired); await Expect(responses[1], HttpStatusCode.OK);
        Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.Equal(0UL, s.LastClientRevision); Assert.Equal(1, await db.Grades.CountAsync(g => g.Id == s.GradeId));
            Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.EntityId == s.Id && a.Action == "submission.submit"));
        });
    }

    [Fact]
    public async Task StaffMultiRoleDoesNotNeedImplicitTeacherProfileOrGrantPrivilegesInB()
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@m,'InstitutionAdmin')", ("@m", Id("teacher-membership")));
        await Execute("DELETE FROM institution_teacher_profile WHERE membership_id=@m", ("@m", Id("teacher-membership")));
        var token = await Access("teacher"); var input = InputExam(); input.ClassSubjectId = Id("assignment-other"); input.AssignedStudents = new() { new() { StudentId = Id("student-other") } };
        await Expect(await Request(Exams, token, input, "POST"), HttpStatusCode.Created);
        await Expect(await Request(Exams, await Access("teacher", true), input, "POST"), HttpStatusCode.NotFound);
        Assert.Equal(3, (await ArrayResponse("/api/institution/class-subjects", token)).Count);
    }

    [Fact]
    public async Task TeacherStudentMembershipCombinesReadPermissionsWithoutExposingProvisionalOwnResult()
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@m,'Student')", ("@m", Id("teacher-membership")));
        await Execute("INSERT INTO institution_student_profile(institution_id,membership_id,student_id) VALUES (@i,@m,@s)", ("@i", _a), ("@m", Id("teacher-membership")), ("@s", Id("student-other")));
        var admin = await Access(); var dual = await Access("teacher"); var input = InputExam(type: QuestionType.Open);
        input.ClassSubjectId = Id("assignment-other"); input.AssignedStudents = new() { new() { StudentId = Id("student-other") } };
        var created = await Request(Exams, admin, input, "POST"); await Expect(created, HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        await Expect(await Request(Exams + "/" + id, dual), HttpStatusCode.NotFound);
        await Expect(await Request(Exams + "/" + id + "/publish", admin, method: "POST"), HttpStatusCode.NoContent);
        var started = await Request(Exams + "/" + id + "/start", dual, method: "POST"); await Expect(started, HttpStatusCode.OK);
        var start = (await started.Content.ReadFromJsonAsync<StartExamDto>())!;
        await Expect(await Request(Submission(start.SubmissionId, "submit"), dual, new SubmitExamDto { ClientRevision = 1, Answers = new() { new() { QuestionId = start.Exam.Questions[0].Id, OpenText = "Answer" } } }, "POST"), HttpStatusCode.OK);
        var result = await Request(Submission(start.SubmissionId, "result"), dual); await Expect(result, HttpStatusCode.OK);
        var dto = (await result.Content.ReadFromJsonAsync<SubmissionResultDto>())!; Assert.Null(dto.Passed); Assert.Null(dto.Grade); Assert.Null(dto.TotalScore);
        foreach (var other in new[] { false, true })
            await Expect(await Request("/api/institution/grades", admin, new CreateGradeDto { StudentId = Id(other ? "student-other" : "student"), SubjectId = Id(other ? "subject-other" : "subject"), Value = 8, Date = DateOnly.FromDateTime(DateTime.UtcNow) }, "POST"), HttpStatusCode.Created);
        Assert.Equal(2, (await ArrayResponse("/api/institution/grades", dual)).Count);
        Assert.Equal(2, await (await Request("/api/institution/grades/count", dual)).Content.ReadFromJsonAsync<int>());
        Assert.Single(await ArrayResponse("/api/institution/class-subjects", dual));
    }

    [Fact]
    public async Task CrossTenantEventIdCollisionRollsBackCountAndDoesNotRevealForeignEvent()
    {
        var a = await Create(); var aStart = await Start(a); var b = await Create(true); var bStart = await Start(b, true);
        var eventId = Guid.NewGuid(); var dto = new InstitutionViolationDto { EventId = eventId, Type = "tab_hidden" };
        await Expect(await Request(Submission(bStart.SubmissionId, "violations"), await Access("student", true), dto, "POST"), HttpStatusCode.OK);
        var beforeA = await Digest(false); var beforeB = await Digest(true);
        await Expect(await Request(Submission(aStart.SubmissionId, "violations"), await Access("student"), dto, "POST"), HttpStatusCode.Conflict);
        Assert.Equal(beforeA, await Digest(false)); Assert.Equal(beforeB, await Digest(true));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task RealBootstrapInventoriesAllPoliciesRoutesOpenApiAndRetainsWorker(string environment)
    {
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false, school: true, exam: true, environment: environment);
        var token = await Access(); await Expect(await Request("/api/institution/class-subjects", token, host: host), HttpStatusCode.OK);
        var policy = await host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>().GetPolicyAsync(InstitutionExamEndpointAttribute.PolicyName);
        Assert.Equal(new[] { InstitutionTokenContract.AccessScheme }, policy!.AuthenticationSchemes);
        var endpoints = host.Host.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>().Endpoints.OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is not null).ToArray();
        Assert.Equal(135, endpoints.Count(e => e.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>() is null));
        Assert.Equal(19, endpoints.Count(e => e.Metadata.GetMetadata<InstitutionCourseEndpointAttribute>() is not null));
        Assert.Equal(154, endpoints.Length);
        var rows = endpoints.SelectMany(e => e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods.Select(method =>
        {
            var action = e.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()!;
            var policies = e.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>();
            return new { method, route = e.RoutePattern.RawText, controller = action.ControllerName, action = action.ActionName,
                roles = policies.Select(p => p.Roles).Where(p => !string.IsNullOrEmpty(p)).ToArray(), policy = policies.Select(p => p.Policy).Where(p => !string.IsNullOrEmpty(p)).ToArray(),
                managers = action.ControllerTypeInfo.DeclaredConstructors.SelectMany(c => c.GetParameters()).Select(p => p.ParameterType.Name).ToArray(),
                input = action.MethodInfo.GetParameters().Select(p => new { p.Name, type = p.ParameterType.Name }).ToArray(), output = action.MethodInfo.ReturnType.ToString(),
                institutionExam = e.Metadata.GetMetadata<InstitutionExamEndpointAttribute>() is not null,
                school = e.Metadata.GetMetadata<InstitutionSchoolEndpointAttribute>() is not null };
        })).ToArray();
        Assert.Equal(rows.Length, rows.Select(r => r.method + " " + r.route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(rows.Where(r => r.institutionExam), r =>
        {
            Assert.Equal(new[] { InstitutionExamEndpointAttribute.PolicyName }, r.policy);
            Assert.Equal(new[] { nameof(IInstitutionExamManager) }, r.managers); Assert.Single(r.roles);
            Assert.DoesNotContain("institutionId", r.route!, StringComparison.OrdinalIgnoreCase);
        });
        await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory(), "routes-" + environment + ".json"), System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        var response = await host.Client.GetAsync("/openapi/v1.json"); await Expect(response, environment == "Development" ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        if (environment == "Development")
        {
            var paths = (await response.Content.ReadFromJsonAsync<JsonObject>())!["paths"]!.AsObject();
            foreach (var row in rows.Where(r => r.institutionExam)) Assert.NotNull(paths["/" + row.route!.Replace(":guid", "")]?[row.method.ToLowerInvariant()]);
        }
    }

    private static string EvidenceDirectory()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tests/Integration/TestResults/mt3d"));
        Directory.CreateDirectory(path); return path;
    }
    private sealed record CapturedCommand(string Sql, Guid[] Guids);
    private sealed class SqlObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new();
        public ConcurrentQueue<CapturedCommand> Commands { get; } = new();
        public void OnNext(DiagnosticListener listener) { if (listener.Name == "Microsoft.EntityFrameworkCore") _subscriptions.Add(listener.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> item)
        {
            if (item.Value is CommandExecutedEventData data && !data.Command.CommandText.Contains("JOIN mt3a_legacy_scope"))
                Commands.Enqueue(new(data.Command.CommandText, data.Command.Parameters.Cast<DbParameter>().Select(p => Guid.TryParse(p.Value?.ToString(), out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToArray()));
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => throw error;
        public void Dispose() { foreach (var subscription in _subscriptions) subscription.Dispose(); }
    }

    [Fact]
    public async Task HttpSqlHasTenantParametersConstantListCostAndExplicitOwnedWrites()
    {
        using var observer = new SqlObserver(); using var subscription = DiagnosticListener.AllListeners.Subscribe(observer);
        var exam = await Create(); var start = await Start(exam); var teacher = await Access("teacher"); var student = await Access("student");
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = Answers(exam) }, "POST"), HttpStatusCode.OK);
        await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory(), "write-statements.sql.txt"), string.Join("\n\n", observer.Commands.Where(c => c.Sql.Contains("INSERT") || c.Sql.Contains("UPDATE")).Select(c => c.Sql)));
        Assert.Contains(observer.Commands, c => c.Sql.Contains("INSERT INTO `submission_answer_option`") && c.Sql.Contains("institution_id") && c.Guids.Contains(_a));
        Assert.Contains(observer.Commands, c => c.Sql.Contains("INSERT INTO `grade`") && c.Sql.Contains("institution_id") && c.Guids.Contains(_a));
        Assert.Contains(observer.Commands, c => c.Sql.Contains("INSERT INTO `audit_event`") && c.Sql.Contains("institution_id") && c.Guids.Contains(_a));
        Assert.Contains(observer.Commands, c => Regex.IsMatch(c.Sql, "UPDATE `exam`[\\s\\S]*?WHERE[^;]*institution_id"));
        var observations = new List<object>();
        foreach (var (path, token) in new[] {
            ("/api/institution/class-subjects",teacher), (Route("student")+"?classId="+Id("class"),teacher),
            (Exams+"/"+exam.Id+"/eligible-students",teacher), (Exams,teacher), (Exams+"/assigned",student),
            (Submission(start.SubmissionId,"session"),student), (Submission(start.SubmissionId,"review"),teacher),
            ("/api/institution/grades",student), ("/api/institution/grades/paged",student), (Submission(start.SubmissionId,"result"),student) })
        {
            observer.Commands.Clear(); var sessionSetup = TestDatabaseSession.Initializations; var clock = Stopwatch.StartNew(); var response = await Request(path, token); clock.Stop(); await Expect(response, HttpStatusCode.OK);
            var commands = observer.Commands.ToArray(); Assert.InRange(commands.Length, 3, 15);
            Assert.DoesNotContain(commands, c => c.Guids.Contains(_b));
            Assert.All(commands.Where(c => c.Sql.Contains("FROM `exam`") || c.Sql.Contains("FROM `grade`") || c.Sql.Contains("FROM `exam_submission`")), c => Assert.Contains("institution_id", c.Sql));
            observations.Add(new { path, queries = commands.Length, testSessionSetupCommands = TestDatabaseSession.Initializations - sessionSetup,
                milliseconds = clock.Elapsed.TotalMilliseconds, sql = commands.Select(c => c.Sql).ToArray() });
        }
        observer.Commands.Clear(); await ArrayResponse(Exams, teacher); var baseline = observer.Commands.Count;
        for (var i = 0; i < 3; i++) await Create();
        observer.Commands.Clear(); Assert.Equal(4, (await ArrayResponse(Exams, teacher)).Count); Assert.Equal(baseline, observer.Commands.Count);
        await File.WriteAllTextAsync(Path.Combine(EvidenceDirectory(), "http-query-observations.json"), System.Text.Json.JsonSerializer.Serialize(observations, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        await Db(async db =>
        {
            var session = await db.Database.SqlQueryRaw<string>("SELECT @@SESSION.sql_mode AS Value").SingleAsync();
            var global = await db.Database.SqlQueryRaw<string>("SELECT @@GLOBAL.sql_mode AS Value").SingleAsync();
            Assert.Contains("STRICT_TRANS_TABLES", session); Assert.DoesNotContain("STRICT_", global);
        });
    }

    [Fact]
    public async Task DraftUpdateDeleteAndUnstartedCancellationUseInstitutionalLocationsAndAudit()
    {
        var exam = await Create(role: "admin"); var admin = await Access(); var input = InputExam();
        await Expect(await Request(Exams + "/" + exam.Id, admin, new UpdateExamDto { ClassSubjectId = input.ClassSubjectId, Name = "Updated draft",
            AvailableFrom = input.AvailableFrom, AvailableUntil = input.AvailableUntil, DurationMinutes = input.DurationMinutes, PassingPercent = input.PassingPercent, Questions = input.Questions }, "PUT"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + exam.Id + "/publish", admin, method: "POST"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + exam.Id + "/cancel", admin, method: "POST"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + exam.Id, admin, method: "DELETE"), HttpStatusCode.Conflict);
        var draft = await Create(); await Expect(await Request(Exams + "/" + draft.Id, admin, method: "DELETE"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + draft.Id, admin), HttpStatusCode.NotFound);
        await Db(async db =>
        {
            Assert.Equal("cancelled", (await db.Exams.AsNoTracking().SingleAsync(e => e.Id == exam.Id)).Status);
            Assert.Equal(2, await db.AuditEvents.CountAsync(a => a.EntityId == exam.Id && (a.Action == "exam.publish" || a.Action == "exam.cancel")));
        });
    }

    [Theory]
    [InlineData("foreign-question", 404)]
    [InlineData("foreign-option", 404)]
    [InlineData("duplicate-question", 400)]
    [InlineData("duplicate-option", 400)]
    [InlineData("single-many-options", 400)]
    public async Task AnswerTamperingIsRejectedWithoutChangingEitherInstitution(string fault, int status)
    {
        var exam = await Create(); var foreign = await Create(true); var start = await Start(exam);
        var answers = Answers(exam);
        switch (fault)
        {
            case "foreign-question": answers[0].QuestionId = foreign.Questions[0].Id; break;
            case "foreign-option": answers[0].SelectedOptionIds = new() { foreign.Questions[0].Options[0].Id }; break;
            case "duplicate-question": answers.Add(new() { QuestionId = answers[0].QuestionId }); break;
            case "duplicate-option": answers[0].SelectedOptionIds.Add(answers[0].SelectedOptionIds[0]); break;
            case "single-many-options": answers[0].SelectedOptionIds = exam.Questions[0].Options.Select(o => o.Id).ToList(); break;
        }
        var a = await Digest(false); var b = await Digest(true);
        await Expect(await Request(Submission(start.SubmissionId, "answers"), await Access("student"), new SaveExamAnswersDto { ClientRevision = 1, Answers = answers }, "PUT"), (HttpStatusCode)status);
        Assert.Equal(a, await Digest(false)); Assert.Equal(b, await Digest(true));
    }

    [Fact]
    public async Task ReviewFinalizeRaceLeavesOneOfficialGradeAndAtomicAudit()
    {
        var exam = await Create(type: QuestionType.Open); var start = await Start(exam); var student = await Access("student"); var teacher = await Access("teacher");
        await Expect(await Request(Submission(start.SubmissionId, "submit"), student, new SubmitExamDto { ClientRevision = 1, Answers = Answers(exam) }, "POST"), HttpStatusCode.OK);
        var review = (await (await Request(Submission(start.SubmissionId, "review"), teacher)).Content.ReadFromJsonAsync<SubmissionReviewDto>())!;
        var reviewed = Request(Submission(start.SubmissionId, "review"), teacher, new ReviewOpenAnswersDto { Answers = new() { new() { AnswerId = review.Answers[0].AnswerId, AwardedPoints = 10 } } }, "PUT");
        var finalized = Request(Submission(start.SubmissionId, "finalize"), teacher, new FinalizeSubmissionDto(), "POST");
        var responses = await Task.WhenAll(reviewed, finalized); await Expect(responses[0], HttpStatusCode.NoContent);
        Assert.Contains(responses[1].StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict });
        await Expect(await Request(Submission(start.SubmissionId, "finalize"), teacher, new FinalizeSubmissionDto(), "POST"), HttpStatusCode.NoContent);
        await Db(async db =>
        {
            var s = await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == start.SubmissionId);
            Assert.NotNull(s.GradeId); Assert.Equal(1, await db.Grades.CountAsync(g => g.Id == s.GradeId));
            Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.EntityId == s.Id && a.Action == "submission.finalize"));
            Assert.Equal(_a, await db.Database.SqlQueryRaw<Guid>("SELECT institution_id AS Value FROM audit_event WHERE entity_id={0} AND action='submission.finalize'", s.Id).SingleAsync());
        });
    }

    [Fact]
    public async Task OversizedViolationBodyNeverPersistsAnEvent()
    {
        var exam = await Create(); var start = await Start(exam); var before = await Digest(false);
        var response = await Request(Submission(start.SubmissionId, "violations"), await Access("student"), new InstitutionViolationDto { EventId = Guid.NewGuid(), Type = "tab_hidden", Metadata = new string('x', 5000) }, "POST");
        await Expect(response, HttpStatusCode.RequestEntityTooLarge);
        Assert.Equal(before, await Digest(false));
    }

    [Fact]
    public async Task UninitializedContextAndManuallyConstructedPrincipalNeverAuthorizeARepository()
    {
        var before = await Digest(false);
        using var scope = _host.Host.Services.CreateScope();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<IInstitutionExamManager>().ExamsAsync());
        var tenant = new TenantContext(_adminUser, _a, Id("admin-membership"), new[] { InstitutionRole.InstitutionAdmin });
        Assert.Throws<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<IInstitutionExamRepositoryFactory>().Create(new ExamPrincipal(tenant, false, null, null)));
        Assert.Equal(before, await Digest(false));
    }

    [Fact]
    public async Task SharedGlobalStudentConcurrentSessionsNeverMixAnswersOrInstitution()
    {
        var a = await Create(type: QuestionType.Open); var aStart = await Start(a);
        var b = await Create(true, QuestionType.Open); var bStart = await Start(b, true);
        var aToken = await Access("student"); var bToken = await Access("student", true);
        await Expect(await Request(Submission(aStart.SubmissionId, "answers"), aToken, new SaveExamAnswersDto { ClientRevision = 1, Answers = Answers(a, open: "A") }, "PUT"), HttpStatusCode.OK);
        await Expect(await Request(Submission(bStart.SubmissionId, "answers"), bToken, new SaveExamAnswersDto { ClientRevision = 1, Answers = Answers(b, open: "B") }, "PUT"), HttpStatusCode.OK);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            var isB = i % 2 == 0; var start = isB ? bStart : aStart;
            var response = await Request(Submission(start.SubmissionId, "session"), isB ? bToken : aToken); await Expect(response, HttpStatusCode.OK);
            var session = (await response.Content.ReadFromJsonAsync<SubmissionSessionDto>())!;
            Assert.Equal(isB ? b.Id : a.Id, session.ExamId); Assert.Equal(isB ? "B" : "A", Assert.Single(session.Answers).OpenText); Assert.Equal(1, session.CurrentRevision);
        }));
    }

    private string Alter(string token, string fault)
    {
        var pieces = token.Split('.');
        var payload = JsonNode.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(pieces[1]))!.AsObject();
        switch (fault)
        {
            case "expired": payload["exp"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(); payload["nbf"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(); break;
            case "audience": payload["aud"] = "wrong-audience"; break;
            case "other-user": payload["sub"] = _teacherUser.ToString(); break;
            case "legacy": payload["aud"] = "mt2-client"; payload.Remove("purpose"); payload.Remove("institution_id"); payload.Remove("membership_id"); payload.Remove("institution_role"); payload["role"] = "admin"; break;
        }
        if (fault == "legacy")
        {
            var header = JsonNode.Parse(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Decode(pieces[0]))!.AsObject();
            header["typ"] = "JWT"; pieces[0] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(header.ToJsonString());
        }
        pieces[1] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(payload.ToJsonString());
        var input = pieces[0] + "." + pieces[1];
        return input + "." + Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(System.Security.Cryptography.HMACSHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Mt3cHosting.Key), System.Text.Encoding.UTF8.GetBytes(input)));
    }

    public async Task DisposeAsync()
    {
        if (_host is not null) await _host.DisposeAsync();
        await Db(async db=>
        {
            foreach(var table in new[]{"institution_student_profile","institution_teacher_profile","audit_event","exam_violation","submission_answer_option","submission_answer","exam_submission","exam_student","question_option","question","exam","grade","class_subject","student","teacher","subject","class","biennium_study_path","biennium_study_area","study_path","study_area","biennium"})
            {
                // Closed identifier allowlist; tenant GUIDs remain SQL parameters.
                var sql="DELETE FROM `"+table+"` WHERE institution_id IN ({0},{1})";
                await db.Database.ExecuteSqlRawAsync(sql,_a,_b);
            }
            var users=new[]{_teacherUser,_studentUser,_adminUser,_admin2,_otherStudentUser};
            var memberships=await db.InstitutionMemberships.Where(m=>users.Contains(m.UserId)).Select(m=>m.Id).ToListAsync();
            await db.InstitutionMembershipRoles.Where(r=>memberships.Contains(r.MembershipId)).ExecuteDeleteAsync();
            await db.InstitutionMemberships.Where(m=>users.Contains(m.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(u=>users.Contains(u.Id)).ExecuteDeleteAsync();
            await db.Institutions.Where(i=>i.Id==_a||i.Id==_b).ExecuteDeleteAsync();
        });

    }
}
