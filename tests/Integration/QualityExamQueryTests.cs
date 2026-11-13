using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task Quality_ExamFanoutMeasurementPreservesShapeAndSnapshot()
    {
        var studentIds = Enumerable.Range(0, 39).Select(_ => Guid.NewGuid()).Append(Id("student")).ToArray();
        await Db(async db =>
        {
            foreach (var id in studentIds.Where(id => id != Id("student")))
            {
                var student = new ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Student { Id = id, FirstName = "Synthetic", LastName = "Benchmark", ClassId = Id("class") };
                db.Students.Add(student);
                db.Entry(student).Property<Guid>("InstitutionId").CurrentValue = _a;
            }
            await db.SaveChangesAsync();
        });
        var input = StaffExamInput();
        input.AssignedStudents = studentIds.Select(id => new ExamStudentInputDto { StudentId = id }).ToList();
        input.Questions = Enumerable.Range(0, 20).Select(n => { var q = StaffExamInput().Questions[0]; q.Position = n; return q; }).ToList();
        var created = await Request("/api/institution/exams", await Access(), input, "POST");
        await Expect(created, HttpStatusCode.Created);
        var examId = await created.Content.ReadFromJsonAsync<Guid>();
        using var scope = fixture.ScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var query = db.Exams.AsNoTracking().Where(e => e.Id == examId).Include(e => e.ClassSubject).ThenInclude(cs => cs.Subject).Include(e => e.Questions).ThenInclude(q => q.QuestionOptions).Include(e => e.ExamStudents);
        var clock = Stopwatch.StartNew();
        var single = Assert.Single(await query.AsSingleQuery().ToListAsync());
        var singleMs = clock.Elapsed.TotalMilliseconds;
        clock.Restart();
        var split = Assert.Single(await query.AsSplitQuery().ToListAsync());
        var splitMs = clock.Elapsed.TotalMilliseconds;
        Assert.Equal(20, single.Questions.Count);
        Assert.Equal(40, single.ExamStudents.Count);
        Assert.Equal(single.Questions.Select(q => q.Id).OrderBy(x => x), split.Questions.Select(q => q.Id).OrderBy(x => x));
        Assert.Equal(single.ExamStudents.Select(s => s.StudentId).OrderBy(x => x), split.ExamStudents.Select(s => s.StudentId).OrderBy(x => x));
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var rows = new MySqlCommand("SELECT COUNT(*) FROM exam e JOIN question q ON q.exam_id=e.id JOIN question_option o ON o.question_id=q.id JOIN exam_student s ON s.exam_id=e.id WHERE e.id=@exam", connection);
        rows.Parameters.AddWithValue("@exam", examId);
        var fanoutRows = Convert.ToInt64(await rows.ExecuteScalarAsync());
        Assert.Equal(1600, fanoutRows);
        var canonical = (await (await Request("/api/institution/exams/" + examId, await Access())).Content.ReadFromJsonAsync<ExamDetailDto>())!;
        Assert.Equal(20, canonical.Questions.Count);
        Assert.Equal(40, canonical.AssignedStudents.Count);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Shape = "20 questions x 2 options x 40 students", FanoutRows = fanoutRows, SingleMilliseconds = singleMs, SplitMilliseconds = splitMs, Repetitions = 1, Note = "Illustrative cold-run observation, not a production benchmark. Split equivalence requires a transaction/snapshot." }));
        await snapshot.RollbackAsync();
    }
}
