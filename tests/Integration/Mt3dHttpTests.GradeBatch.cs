using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using Microsoft.EntityFrameworkCore;
using Xunit;
using E = ElectronicRegisterAPI.Infrastructure.Persistence.Entities;

namespace ElectronicRegisterAPI.Tests.Integration;

// POST /api/institution/grades/batch: manual grades in thirtieths for one class-subject and date,
// all or nothing, idempotent on eventId. The setup gives the teacher "assignment" (class, subject);
// the same subject is also taught by another teacher in "class-other" ("assignment-shared").
public sealed partial class Mt3dHttpTests
{
    private const string Batch = "/api/institution/grades/batch";
    private static readonly DateOnly BatchDate = new(2026, 11, 17);

    private async Task<List<Guid>> ClassStudents(int count, bool b = false)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();
        await Db(async db =>
        {
            foreach (var id in ids)
            {
                var student = new E.Student { Id = id, FirstName = "Batch", LastName = "Student", ClassId = Id("class", b) };
                db.Add(student);
                db.Entry(student).Property<Guid>("InstitutionId").CurrentValue = b ? _b : _a;
            }
            await db.SaveChangesAsync();
        });
        return ids;
    }

    private CreateGradeBatchDto BatchOf(IEnumerable<Guid> students, Func<int, decimal>? value = null, Guid? eventId = null, string? description = "Verifica scritta in presenza") => new()
    {
        EventId = eventId ?? Guid.NewGuid(),
        SubjectId = Id("subject"),
        ClassId = Id("class"),
        Date = BatchDate,
        Description = description,
        Grades = students.Select((s, i) => new GradeBatchItemDto { StudentId = s, Value = value?.Invoke(i) ?? 18 + i % 13 }).ToList()
    };

    private Task<int> BatchRows(Guid eventId)
    {
        var rows = 0;
        return Db(async db => rows = await db.Grades.CountAsync(g => g.BatchEventId == eventId)).ContinueWith(_ => rows);
    }

    private static async Task<JsonArray> RowErrors(HttpResponseMessage response)
    {
        await Expect(response, HttpStatusCode.BadRequest);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("GradeBatchInvalid", body["code"]!.GetValue<string>());
        return body["errors"]!.AsArray();
    }

    [Fact]
    public async Task BatchOfThirtyGradesIsCreatedOnceWithOneAuditEvent()
    {
        var students = await ClassStudents(30);
        var dto = BatchOf(students);
        var response = await Request(Batch, await Access("teacher"), dto, "POST");
        await Expect(response, HttpStatusCode.Created);
        var result = (await response.Content.ReadFromJsonAsync<GradeBatchResultDto>())!;
        Assert.Equal(dto.EventId, result.EventId);
        Assert.Equal(students, result.Created.Select(c => c.StudentId));
        Assert.Equal(30, await BatchRows(dto.EventId));
        await Db(async db =>
        {
            var rows = await db.Grades.Where(g => g.BatchEventId == dto.EventId).ToListAsync();
            Assert.All(rows, g => { Assert.Equal(Id("assignment"), g.ClassSubjectId); Assert.Equal(BatchDate, g.Date); Assert.Equal("Verifica scritta in presenza", g.Description); });
            Assert.Equal(dto.Grades!.ToDictionary(i => i.StudentId, i => i.Value), rows.ToDictionary(g => g.StudentId, g => g.Value));
            var audits = await db.AuditEvents.Where(a => a.Action == "grade.batch.create" && a.AfterJson!.Contains(dto.EventId.ToString())).ToListAsync();
            var audit = Assert.Single(audits);
            var after = JsonNode.Parse(audit.AfterJson!)!;
            Assert.Equal(30, after["Count"]!.GetValue<int>());
            Assert.Equal(Id("assignment"), after["ClassSubjectId"]!.GetValue<Guid>());
            Assert.Null(after["Value"]); Assert.DoesNotContain("\"Grades\"", audit.AfterJson);
        });
        // The created grades are ordinary manual grades: readable with their description, editable and deletable.
        var admin = await Access();
        var detail = (await (await Request("/api/institution/grades/" + result.Created[0].GradeId, admin)).Content.ReadFromJsonAsync<GradeDto>())!;
        Assert.Equal("Verifica scritta in presenza", detail.Description);
        await Expect(await Request("/api/institution/grades/" + result.Created[0].GradeId, admin, new UpdateGradeDto { SubjectId = Id("subject"), Value = 21, Date = BatchDate }, "PUT"), HttpStatusCode.NoContent);
        await Expect(await Request("/api/institution/grades/" + result.Created[1].GradeId, admin, method: "DELETE"), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ReplayReturnsTheSameResponseAndADifferentPayloadConflicts()
    {
        var students = await ClassStudents(3);
        var teacher = await Access("teacher");
        var dto = BatchOf(students);
        var first = await Request(Batch, teacher, dto, "POST");
        await Expect(first, HttpStatusCode.Created);
        var replay = await Request(Batch, teacher, dto, "POST");
        await Expect(replay, HttpStatusCode.Created);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(3, await BatchRows(dto.EventId));
        var changed = BatchOf(students, i => 25, dto.EventId);
        await Expect(await Request(Batch, teacher, changed, "POST"), HttpStatusCode.Conflict);
        Assert.Equal(3, await BatchRows(dto.EventId));
        await Db(async db => Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "grade.batch.create" && a.AfterJson!.Contains(dto.EventId.ToString()))));
    }

    [Fact]
    public async Task ConcurrentRequestsWithTheSameEventCreateTheGradesOnce()
    {
        var students = await ClassStudents(10);
        var teacher = await Access("teacher");
        var dto = BatchOf(students);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Request(Batch, teacher, dto, "POST")));
        var bodies = new List<string>();
        foreach (var response in responses)
        {
            await Expect(response, HttpStatusCode.Created);
            bodies.Add(await response.Content.ReadAsStringAsync());
        }
        Assert.Single(bodies.Distinct());
        Assert.Equal(10, await BatchRows(dto.EventId));
    }

    [Fact]
    public async Task OneInvalidRowRejectsTheWholeBatchWithPerRowErrors()
    {
        var students = await ClassStudents(5);
        var dto = BatchOf(students, i => i == 2 ? 31 : i == 3 ? 17.5m : 24);
        var errors = await RowErrors(await Request(Batch, await Access("teacher"), dto, "POST"));
        Assert.Equal(new[] { 2, 3 }, errors.Select(e => e!["index"]!.GetValue<int>()));
        Assert.All(errors, e => Assert.Equal("invalid_value", e!["code"]!.GetValue<string>()));
        Assert.Equal(students[2], errors[0]!["studentId"]!.GetValue<Guid>());
        Assert.Equal(0, await BatchRows(dto.EventId));
    }

    [Fact]
    public async Task DuplicateForeignAndOtherClassStudentsAreRowErrors()
    {
        var students = await ClassStudents(2);
        var foreign = (await ClassStudents(1, b: true))[0];
        var dto = BatchOf(new[] { students[0], students[1], students[0], foreign, Id("student-other"), Guid.NewGuid() });
        var errors = await RowErrors(await Request(Batch, await Access(), dto, "POST"));
        var codes = errors.ToDictionary(e => e!["index"]!.GetValue<int>(), e => e!["code"]!.GetValue<string>());
        Assert.Equal(new Dictionary<int, string> { [2] = "duplicate_student", [3] = "student_not_found", [4] = "student_not_in_class", [5] = "student_not_found" }, codes);
        // A student of another institution is indistinguishable from a missing one.
        Assert.Equal(errors[1]!["message"]!.GetValue<string>(), errors[3]!["message"]!.GetValue<string>());
        Assert.Equal(0, await BatchRows(dto.EventId));
    }

    [Fact]
    public async Task BatchSizeAndShapeAreValidated()
    {
        var admin = await Access();
        var students = await ClassStudents(1);
        await Expect(await Request(Batch, admin, BatchOf(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid())), "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(Batch, admin, BatchOf(Array.Empty<Guid>()), "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(Batch, admin, BatchOf(students, eventId: Guid.Empty), "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(Batch, admin, BatchOf(students, description: new string('x', 201)), "POST"), HttpStatusCode.BadRequest);
        await Expect(await Request(Batch, admin, BatchOf(students, description: "riga\u0007"), "POST"), HttpStatusCode.BadRequest);
        // The admin sees the subject in two classes: without classId the class-subject is ambiguous.
        await Expect(await Request(Batch, admin, BatchOf(students), "POST"), HttpStatusCode.Created);
        var ambiguous = BatchOf(students); ambiguous.ClassId = null;
        await Expect(await Request(Batch, admin, ambiguous, "POST"), HttpStatusCode.BadRequest);
        // The teacher teaches the subject in one class only: subjectId alone identifies it, as does classSubjectId.
        var teacher = await Access("teacher");
        var bySubject = BatchOf(await ClassStudents(1)); bySubject.ClassId = null;
        await Expect(await Request(Batch, teacher, bySubject, "POST"), HttpStatusCode.Created);
        var byAssignment = BatchOf(await ClassStudents(1)); byAssignment.ClassId = null; byAssignment.SubjectId = null; byAssignment.ClassSubjectId = Id("assignment");
        await Expect(await Request(Batch, teacher, byAssignment, "POST"), HttpStatusCode.Created);
    }

    [Fact]
    public async Task BatchFollowsTheSingleGradeScope()
    {
        var students = await ClassStudents(1);
        // Teacher on a class-subject that is not theirs: the same answer as POST /grades (the scoped read hides it).
        var teacher = await Access("teacher");
        var single = await Request("/api/institution/grades", teacher, new CreateGradeDto { StudentId = Id("student-other"), SubjectId = Id("subject"), Value = 20, Date = BatchDate }, "POST");
        var other = BatchOf(new[] { Id("student-other") }); other.ClassId = Id("class-other");
        await Expect(await Request(Batch, teacher, other, "POST"), single.StatusCode);
        Assert.Contains(single.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        var byAssignment = BatchOf(new[] { Id("student-other") }); byAssignment.ClassId = null; byAssignment.SubjectId = null; byAssignment.ClassSubjectId = Id("assignment-shared");
        await Expect(await Request(Batch, teacher, byAssignment, "POST"), single.StatusCode);
        // Another institution's class-subject does not exist for this tenant.
        var foreign = BatchOf(students); foreign.ClassSubjectId = Id("assignment", b: true); foreign.SubjectId = null; foreign.ClassId = null;
        await Expect(await Request(Batch, await Access(), foreign, "POST"), HttpStatusCode.NotFound);
        await Expect(await Request(Batch, await Access("student"), BatchOf(students), "POST"), HttpStatusCode.Forbidden);
        await Expect(await Request(Batch, null, BatchOf(students), "POST"), HttpStatusCode.Unauthorized);
        Assert.Equal(0, await BatchRows(other.EventId));
    }

    [Fact]
    public async Task SingleGradeAcceptsAndExposesAnOptionalDescription()
    {
        var admin = await Access();
        var response = await Request("/api/institution/grades", admin, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 27, Date = BatchDate, Description = "  Interrogazione orale  " }, "POST");
        await Expect(response, HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.Equal("Interrogazione orale", (await (await Request("/api/institution/grades/" + id, admin)).Content.ReadFromJsonAsync<GradeDto>())!.Description);
        // PUT without description keeps it; a blank description clears it.
        await Expect(await Request("/api/institution/grades/" + id, admin, new UpdateGradeDto { SubjectId = Id("subject"), Value = 28, Date = BatchDate }, "PUT"), HttpStatusCode.NoContent);
        Assert.Equal("Interrogazione orale", (await (await Request("/api/institution/grades/" + id, admin)).Content.ReadFromJsonAsync<GradeDto>())!.Description);
        await Expect(await Request("/api/institution/grades/" + id, admin, new UpdateGradeDto { SubjectId = Id("subject"), Value = 28, Date = BatchDate, Description = " " }, "PUT"), HttpStatusCode.NoContent);
        Assert.Null((await (await Request("/api/institution/grades/" + id, admin)).Content.ReadFromJsonAsync<GradeDto>())!.Description);
        await Expect(await Request("/api/institution/grades", admin, new CreateGradeDto { StudentId = Id("student"), SubjectId = Id("subject"), Value = 18.5m, Date = BatchDate }, "POST"), HttpStatusCode.BadRequest);
    }
}
