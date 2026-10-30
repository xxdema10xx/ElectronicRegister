using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    private async Task<string> StaffToken(string extra = "", bool noProfile = true)
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Staff')", ("@m", Id("teacher-membership")));
        if (!extra.Contains("Teacher")) await Execute("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='Teacher'", ("@m", Id("teacher-membership")));
        if (extra.Contains("Student")) await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Student')", ("@m", Id("teacher-membership")));
        if (extra.Contains("InstitutionAdmin")) await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'InstitutionAdmin')", ("@m", Id("teacher-membership")));
        if (noProfile) await Execute("DELETE FROM institution_teacher_profile WHERE institution_id=@i AND membership_id=@m", ("@i", _a), ("@m", Id("teacher-membership")));
        return await Access("teacher");
    }

    [Theory]
    [InlineData("")][InlineData("Teacher")][InlineData("Student")][InlineData("Teacher,Student")][InlineData("InstitutionAdmin")]
    public async Task StaffRole_AnagraphicCruIsTenantScopedWithoutCreatingGlobalAccounts(string extra)
    {
        var token = await StaffToken(extra);
        await Expect(await Request(Route("teacher") + "?classId=" + Id("class"), token), HttpStatusCode.OK);
        await Expect(await Request(Route("student") + "?classId=" + Id("class"), token), HttpStatusCode.OK);
        foreach (var family in new[] { "student", "teacher" })
        {
            var route = Route(family);
            await Expect(await Request(route, token), HttpStatusCode.OK);
            await Expect(await Request(route + "/" + Id(family, true), token), HttpStatusCode.NotFound);
            var foreignBefore = await Digest(true);
            var created = await Request(route, token, Input(family, "Staff"), "POST");
            await Expect(created, HttpStatusCode.Created);
            var id = await created.Content.ReadFromJsonAsync<Guid>();
            await Expect(await Request(route + "/" + id, token, Input(family, "Renamed"), "PUT"), HttpStatusCode.NoContent);
            await Expect(await Request(route + "/" + id, token, method: "DELETE"), extra == "InstitutionAdmin" ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden);
            await Expect(await Request(route + "/" + Id(family, true), token, Input(family), "PUT"), HttpStatusCode.NotFound);
            Assert.Equal(foreignBefore, await Digest(true));
            await Db(async db =>
            {
                Assert.False(await db.Users.AnyAsync(u => u.StudentId == id || u.TeacherId == id));
                Assert.Equal("student", (await db.Users.AsNoTracking().SingleAsync(u => u.Id == _teacherUser)).Role);
            });
        }
    }

    [Theory]
    [InlineData("role")][InlineData("institutionId")][InlineData("classId")][InlineData("userId")]
    public async Task StaffRole_ForbiddenStudentFieldsProduceNoWrite(string field)
    {
        var token = await StaffToken(); var before = await Digest(false);
        var body = new JsonObject { ["firstName"] = "Forged", ["lastName"] = "Input", [field] = Guid.NewGuid().ToString() };
        await Expect(await Request(Route("student"), token, body, "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(Route("student") + "/" + Id("student"), token, body, "PUT"), HttpStatusCode.BadRequest);
        Assert.Equal(before, await Digest(false));
    }

    [Theory]
    [InlineData("Teacher")][InlineData("Student")][InlineData("Teacher,Student")][InlineData("")]
    public async Task StaffRole_MixedRolesCannotDeleteOrCancelExams(string extra)
    {
        var token = await StaffToken(extra);
        await Expect(await Request(Exams + "/" + Guid.NewGuid(), token, method: "DELETE"), HttpStatusCode.Forbidden);
        await Expect(await Request(Exams + "/" + Guid.NewGuid() + "/cancel", token, method: "POST"), HttpStatusCode.Forbidden);
        await Expect(await Request("/api/institution/grades/" + Guid.NewGuid(), token, method: "DELETE"), HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/institution/admin/members")]
    [InlineData("/api/User")]
    [InlineData("/api/institution/subjects")]
    [InlineData("/api/institution/classes")]
    public async Task StaffRole_CannotReachGovernanceLegacyOrStructuralWrites(string route)
    {
        var token = await StaffToken();
        var response = await Request(route, token, new { Name = "Forbidden" }, "POST");
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
    }

    [Theory]
    [InlineData("remove-role")][InlineData("suspend")]
    public async Task StaffRole_PreviousJwtFailsAfterMembershipChange(string change)
    {
        var token = await StaffToken();
        if (change == "remove-role") await Execute("DELETE FROM institution_membership_role WHERE membership_id=@m AND role='Staff'", ("@m", Id("teacher-membership")));
        else await Execute("UPDATE institution_membership SET status='suspended',suspended_at=UTC_TIMESTAMP(6) WHERE id=@m", ("@m", Id("teacher-membership")));
        await Expect(await Request(Route("student"), token), HttpStatusCode.Forbidden);
        await Expect(await Request(Route("teacher"), token, Input("teacher"), "POST"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffRole_GradesCruKeepIdentityAndTenantAndRejectDelete()
    {
        var token = await StaffToken();
        var route = "/api/institution/grades";
        var created = await Request(route, token, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 7m, Date = new DateOnly(2026,10,27) }, "POST");
        await Expect(created, HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        await Expect(await Request(route, token), HttpStatusCode.OK);
        await Expect(await Request(route + "/" + id, token, new UpdateGradeDto { SubjectId = Id("subject"), Value = 8m, Date = new DateOnly(2026,10,27) }, "PUT"), HttpStatusCode.NoContent);
        await Expect(await Request(route + "/" + id, token, method: "DELETE"), HttpStatusCode.Forbidden);
        await Expect(await Request(route + "/" + id, await Access(b: true)), HttpStatusCode.NotFound);
        await Expect(await Request(route + "/" + id, token, new UpdateGradeDto { SubjectId = Id("subject", true), Value = 9m, Date = new DateOnly(2026,10,27) }, "PUT"), HttpStatusCode.Conflict);
        Guid eventId = Guid.Empty;
        await Db(async db =>
        {
            Assert.Equal(8m, (await db.Grades.AsNoTracking().SingleAsync(g => g.Id == id)).Value);
            var audit = await db.AuditEvents.AsNoTracking().Where(e => e.EntityId == id).OrderBy(e => e.CreatedAt).ToListAsync();
            Assert.Equal(2, audit.Count); eventId = audit[1].Id;
            Assert.Equal("staff.grade.create", audit[0].Action); Assert.Equal("staff.grade.update", audit[1].Action);
            Assert.Equal(7m, System.Text.Json.JsonDocument.Parse(audit[1].BeforeJson!).RootElement.GetProperty("Value").GetDecimal());
            Assert.All(audit, e =>
            {
                Assert.Equal(_teacherUser, e.ActorUserId);
                Assert.Equal(Id("teacher-membership"), System.Text.Json.JsonDocument.Parse(e.AfterJson!).RootElement.GetProperty("ActorMembershipId").GetGuid());
            });
        });
        await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => Execute("UPDATE audit_event SET after_json=JSON_OBJECT('forged',true) WHERE id=@id", ("@id", eventId)));
        await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => Execute("DELETE FROM audit_event WHERE id=@id", ("@id", eventId)));
    }

    private CreateExamDto StaffExamInput(bool foreign = false) => new()
    {
        ClassSubjectId = Id("assignment", foreign), AvailableFrom = DateTime.UtcNow.AddMinutes(-5), AvailableUntil = DateTime.UtcNow.AddHours(2),
        DurationMinutes = 30, PassingPercent = 60,
        AssignedStudents = new() { new() { StudentId = Id("student", foreign) } },
        Questions = new() { new() { Position = 0, Text = "Question", Type = QuestionType.Single, Points = 10, Required = true,
            Options = new() { new() { Position = 0, Text = "Correct", IsCorrect = true }, new() { Position = 1, Text = "Other", IsCorrect = false } } } }
    };

    [Fact]
    public async Task StaffRole_ExamLifecycleUsesRealActorWithoutTeacherProfile()
    {
        var token = await StaffToken();
        await Expect(await Request(Exams, token), HttpStatusCode.OK);
        var created = await Request(Exams, token, StaffExamInput(), "POST"); await Expect(created, HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        await Expect(await Request(Exams + "/" + id, token), HttpStatusCode.OK);
        await Expect(await Request(Exams + "/" + id + "/eligible-students", token), HttpStatusCode.OK);
        var input = StaffExamInput();
        await Expect(await Request(Exams + "/" + id, token, new UpdateExamDto { ClassSubjectId = input.ClassSubjectId, Description = "Edited by Staff", AvailableFrom = input.AvailableFrom, AvailableUntil = input.AvailableUntil, DurationMinutes = 30, PassingPercent = 60, Questions = input.Questions }, "PUT"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + id + "/students", token, new UpdateExamStudentsDto { Students = input.AssignedStudents }, "PUT"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + id + "/publish", token, method: "POST"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + id + "/submissions", token), HttpStatusCode.OK);
        await Expect(await Request(Exams + "/" + id + "/close", token, method: "POST"), HttpStatusCode.NoContent);
        await Expect(await Request(Exams + "/" + id, token, method: "DELETE"), HttpStatusCode.Forbidden);
        await Expect(await Request(Exams, token, StaffExamInput(true), "POST"), HttpStatusCode.NotFound);
        await Db(async db => Assert.All(await db.AuditEvents.AsNoTracking().Where(e => e.EntityId == id).ToListAsync(), e => Assert.Equal(_teacherUser, e.ActorUserId)));
    }

    [Theory]
    [InlineData("review", "PUT")][InlineData("finalize", "POST")][InlineData("answers", "PUT")][InlineData("submit", "POST")]
    public async Task StaffRole_DoesNotGrantTeachingOrStudentSubmissionMutations(string operation, string method)
    {
        var token = await StaffToken();
        await Expect(await Request(Submission(Guid.NewGuid(), operation), token, new { }, method), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffRole_IamAssignsAndRevokesStaffWithAuditAndGeneration()
    {
        await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(), enabled: false,
            school: true, exam: true, membershipAdmin: true, membershipWrite: true);
        var admin = await Access(); var membership = Id("student-membership");
        long revision = -1;
        await Db(async db => revision = (await db.InstitutionMembershipSecurityStates.AsNoTracking().SingleAsync(s => s.MembershipId == membership)).SecurityGeneration);
        var route = "/api/institution/admin/members/" + membership + "/mutations";
        var added = Guid.NewGuid();
        await Expect(await Request(route, admin, new { EventId = added, ExpectedRevision = revision, Action = "AddRole", Role = "Staff", Reason = "Staff integration test" }, "POST", host), HttpStatusCode.OK);
        var staff = await Access("student");
        await Expect(await Request(Route("teacher"), staff), HttpStatusCode.OK);
        await Expect(await Request(route, staff, new { EventId = Guid.NewGuid(), ExpectedRevision = revision + 1, Action = "AddRole", Role = "InstitutionAdmin", Reason = "Denied escalation" }, "POST", host), HttpStatusCode.Forbidden);
        await Expect(await Request(route, admin, new { EventId = Guid.NewGuid(), ExpectedRevision = revision + 1, Action = "RemoveRole", Role = "Staff", Reason = "Staff revoked" }, "POST", host), HttpStatusCode.OK);
        await Expect(await Request(Route("teacher"), staff), HttpStatusCode.Forbidden);
        await Db(async db =>
        {
            var events = await db.InstitutionMembershipEvents.AsNoTracking().Where(e => e.TargetMembershipId == membership).ToListAsync();
            Assert.Equal(2, events.Count);
            Assert.All(events, e => { Assert.Equal(_adminUser, e.ActorUserId); Assert.Equal(Id("admin-membership"), e.ActorMembershipId); });
            Assert.Equal(revision + 2, (await db.InstitutionMembershipSecurityStates.AsNoTracking().SingleAsync(s => s.MembershipId == membership)).SecurityGeneration);
        });
    }

    [Fact]
    public async Task StaffRole_SameUserTeacherInOtherFoundationKeepsIndependentScope()
    {
        var staff = await StaffToken(); var teacherB = await Access("teacher", true);
        await Expect(await Request(Exams, staff), HttpStatusCode.OK);
        await Expect(await Request(Exams, teacherB), HttpStatusCode.OK);
        await Expect(await Request(Route("student") + "/" + Id("student", true), staff), HttpStatusCode.NotFound);
        await Expect(await Request(Route("student") + "/" + Id("student"), teacherB), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("teacherId")][InlineData("institutionId")][InlineData("status")]
    public async Task StaffRole_ExamForgedFieldsCannotCreateOrAttributeData(string field)
    {
        var token = await StaffToken(); var before = await Digest(false);
        var input = JsonSerializerNode(StaffExamInput()); input[field] = Guid.NewGuid();
        await Expect(await Request(Exams, token, input, "POST"), HttpStatusCode.BadRequest);
        Assert.Equal(before, await Digest(false));
    }

    private static JsonObject JsonSerializerNode(object value) => System.Text.Json.JsonSerializer.SerializeToNode(value,
        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!.AsObject();

    [Fact]
    public async Task StaffRole_AllowlistedLegacyTeacherCannotBypassStaffDeleteRestriction()
    {
        var user = Guid.NewGuid(); var teacher = Guid.NewGuid(); var membership = Guid.NewGuid();
        await Db(async db =>
        {
            var profile = new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Teacher { Id = teacher, FirstName = "Legacy", LastName = "Staff" };
            db.Add(profile); db.Entry(profile).Property<Guid>("InstitutionId").CurrentValue = _a;
            db.Users.Add(new() { Id = user, Email = user.ToString("N") + "@staff.example.test", PasswordHash = Hash, Role = "teacher", TeacherId = teacher });
            db.InstitutionMemberships.Add(new() { Id = membership, InstitutionId = _a, UserId = user, Status = "active",
                InstitutionMembershipRoles = new List<ElectronicRegisterAPI.Infrastructure.Persistence.Entities.InstitutionMembershipRole> { new() { MembershipId = membership, Role = "Staff" }, new() { MembershipId = membership, Role = "Teacher" } } });
            db.InstitutionTeacherProfiles.Add(new() { InstitutionId = _a, MembershipId = membership, TeacherId = teacher });
            await db.SaveChangesAsync();
        });
        try
        {
            await using var host = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, new[] { user }, enabled: true, school: true, exam: true);
            var login = await Request("/api/Auth/login", body: new LoginDto { Email = user.ToString("N") + "@staff.example.test", Password = Password }, method: "POST", host: host);
            Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
        }
        finally
        {
            // Keep global legacy backfill fixtures independent of this institutional identity.
            await Execute("UPDATE user SET role='student',teacher_id=NULL WHERE id=@u", ("@u", user));
        }
    }

    [Fact]
    public async Task StaffRole_SignedStaffClaimCannotGrantRoleMissingFromMembership()
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(await Access("teacher"));
        token.Payload[ElectronicRegisterAPI.Infrastructure.Security.InstitutionTokenContract.Role] = "Staff";
        var header = new System.IdentityModel.Tokens.Jwt.JwtHeader(new Microsoft.IdentityModel.Tokens.SigningCredentials(
            new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Mt3cHosting.Key)),
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256));
        header["typ"] = token.Header["typ"];
        var forged = handler.WriteToken(new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(header, token.Payload));
        await Expect(await Request(Route("student"), forged), HttpStatusCode.Forbidden);
        await Expect(await Request(Route("teacher"), forged, Input("teacher"), "POST"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StaffRole_RolloutAndSelectionTicketCannotAuthorizeStaffWrites()
    {
        var staff = await StaffToken();
        await using var disabled = await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString, _a, Array.Empty<Guid>(),
            enabled: false, school: false, exam: false);
        await Expect(await Request(Route("student"), staff, Input("student"), "POST", host: disabled), HttpStatusCode.Forbidden);
        await Expect(await Request(Exams, staff, StaffExamInput(), "POST", host: disabled), HttpStatusCode.Forbidden);
        await Expect(await Request(Route("student"), await Ticket("teacher"), Input("student"), "POST"), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StaffRole_AdminPlusStaffKeepsGradeAndDraftExamDelete()
    {
        var token = await StaffToken("InstitutionAdmin");
        var grade = await Request("/api/institution/grades", token, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 7, Date = new DateOnly(2026,10,27) }, "POST");
        await Expect(grade, HttpStatusCode.Created);
        await Expect(await Request("/api/institution/grades/" + await grade.Content.ReadFromJsonAsync<Guid>(), token, method: "DELETE"), HttpStatusCode.NoContent);
        var exam = await Request(Exams, token, StaffExamInput(), "POST"); await Expect(exam, HttpStatusCode.Created);
        await Expect(await Request(Exams + "/" + await exam.Content.ReadFromJsonAsync<Guid>(), token, method: "DELETE"), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task StaffRole_StudentRoleUsesRealProfileAndExamGradeStillRequiresFinalizationWorkflow()
    {
        await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES(@m,'Staff')", ("@m", Id("student-membership")));
        var token = await Access("student");
        var created = await Request(Exams, token, StaffExamInput(), "POST"); await Expect(created, HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<Guid>();
        var detail = (await (await Request(Exams + "/" + id, token)).Content.ReadFromJsonAsync<ExamDetailDto>())!;
        await Expect(await Request(Exams + "/" + id + "/publish", token, method: "POST"), HttpStatusCode.NoContent);
        var started = await Request(Exams + "/" + id + "/start", token, method: "POST"); await Expect(started, HttpStatusCode.OK);
        var session = (await started.Content.ReadFromJsonAsync<StartExamDto>())!;
        await Expect(await Request(Submission(session.SubmissionId, "violations"), token, new { EventId = Guid.NewGuid(), Type = "tab_hidden" }, "POST"), HttpStatusCode.OK);
        var submitted = await Request(Submission(session.SubmissionId, "submit"), token, new SubmitExamDto
        {
            ClientRevision = 1,
            Answers = detail.Questions.Select(q => new AnswerInputDto { QuestionId = q.Id, SelectedOptionIds = q.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList() }).ToList()
        }, "POST");
        await Expect(submitted, HttpStatusCode.OK);
        Guid? grade = null;
        await Db(async db => grade = (await db.ExamSubmissions.AsNoTracking().SingleAsync(s => s.Id == session.SubmissionId)).GradeId);
        Assert.NotNull(grade);
        await Expect(await Request("/api/institution/grades/" + grade, token, new UpdateGradeDto { SubjectId = Id("subject"), Value = 9, Date = new DateOnly(2026,10,27) }, "PUT"), HttpStatusCode.Conflict);
        await Expect(await Request(Submission(session.SubmissionId, "answers"), await StaffToken(), new SaveExamAnswersDto { ClientRevision = 2 }, "PUT"), HttpStatusCode.Forbidden);
    }
}
