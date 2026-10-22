#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using MySqlConnector;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    private async Task<Guid> TransferExam(bool open=false)
    {
        var token=await Access("teacher");var response=await Request(Exams,token,new CreateExamDto{ClassSubjectId=Id("assignment"),AvailableFrom=DateTime.UtcNow.AddMinutes(-5),AvailableUntil=DateTime.UtcNow.AddHours(1),DurationMinutes=30,PassingPercent=60,AssignedStudents=new(){new(){StudentId=Id("student")}},Questions=new(){new(){Position=0,Text="Transfer question",Type=open?QuestionType.Open:QuestionType.Single,Points=10,Required=true,Options=open?new():new(){new(){Position=0,Text="Yes",IsCorrect=true},new(){Position=1,Text="No",IsCorrect=false}}}}},"POST");await Expect(response,HttpStatusCode.Created);var id=await response.Content.ReadFromJsonAsync<Guid>();await Expect(await Request(Exams+"/"+id+"/publish",token,method:"POST"),HttpStatusCode.NoContent);return id;
    }
    private async Task<StartExamDto> TransferStart(Guid exam)
    {var response=await Request(Exams+"/"+exam+"/start",await Access("student"),method:"POST");await Expect(response,HttpStatusCode.OK);return (await response.Content.ReadFromJsonAsync<StartExamDto>())!;}
    private async Task TransferSubmit(StartExamDto start,bool open=false)
    {
        var answer=new AnswerInputDto{QuestionId=start.Exam.Questions[0].Id};if(open)answer.OpenText="Historical answer";else{Guid correct=default;await Db(async db=>correct=await db.QuestionOptions.Where(o=>o.QuestionId==answer.QuestionId&&o.IsCorrect).Select(o=>o.Id).SingleAsync());answer.SelectedOptionIds=new(){correct};}
        await Expect(await Request(Submission(start.SubmissionId,"submit"),await Access("student"),new SubmitExamDto{ClientRevision=1,Answers=new(){answer}},"POST"),HttpStatusCode.OK);
    }
    private async Task<string> TransferLearningDigest()
    {
        await using var connection=await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);var rows=new List<string>();
        foreach(var table in new[]{"grade","exam","exam_student","exam_submission","question","question_option","submission_answer","audit_event"})
        {await using var command=new MySqlCommand("SELECT * FROM `"+table+"` WHERE institution_id=@i ORDER BY 1,2",connection);command.Parameters.AddWithValue("@i",_a);await using var reader=await command.ExecuteReaderAsync();while(await reader.ReadAsync()){var cells=new object[reader.FieldCount];reader.GetValues(cells);rows.Add(table+JsonSerializer.Serialize(cells));}}
        return string.Join("\n",rows);
    }
    [Fact]
    public async Task TransferBlocksRealInProgressAttemptWithoutTouchingAnswersOrDeadline()
    {await SeedTransfer();var start=await TransferStart(await TransferExam());var before=await TransferLearningDigest();await Expect(await Request(TransferRoute(),await Access(),TransferInput(),"POST"),HttpStatusCode.Conflict);Assert.Equal(before,await TransferLearningDigest());await AssertTransferUnchanged();}
    [Fact]
    public async Task TransferKeepsUnstartedAssignmentsButCannotStartOldClassExam()
    {await SeedTransfer();var exam=await TransferExam();var before=await TransferLearningDigest();await Expect(await Request(TransferRoute(),await Access(),TransferInput(),"POST"),HttpStatusCode.OK);var student=await Access("student");await Expect(await Request(Exams+"/"+exam+"/start",student,method:"POST"),HttpStatusCode.NotFound);var assigned=(await (await Request(Exams+"/assigned",student)).Content.ReadFromJsonAsync<List<AssignedExamDto>>())!;Assert.Empty(assigned);Assert.Equal(before,await TransferLearningDigest());await Db(async db=>Assert.False(await db.ExamSubmissions.AnyAsync(s=>s.StudentId==Id("student"))));}
    [Theory][InlineData(false)][InlineData(true)]
    public async Task TransferPreservesSubmittedAttemptAndHistoricalReviewFinalization(bool open)
    {
        await SeedTransfer(true);var exam=await TransferExam(open);var start=await TransferStart(exam);await TransferSubmit(start,open);var before=await TransferLearningDigest();await Expect(await Request(TransferRoute(),await Access(),TransferInput(),"POST"),HttpStatusCode.OK);Assert.Equal(before,await TransferLearningDigest());
        await Expect(await Request(Submission(start.SubmissionId,"session"),await Access("student")),HttpStatusCode.OK);
        if(open)
        {var teacher=await Access("teacher");var reviewResponse=await Request(Submission(start.SubmissionId,"review"),teacher);await Expect(reviewResponse,HttpStatusCode.OK);var review=(await reviewResponse.Content.ReadFromJsonAsync<SubmissionReviewDto>())!;await Expect(await Request(Submission(start.SubmissionId,"review"),teacher,new ReviewOpenAnswersDto{Answers=new(){new(){AnswerId=review.Answers[0].AnswerId,AwardedPoints=10}}},"PUT"),HttpStatusCode.NoContent);await Expect(await Request(Submission(start.SubmissionId,"finalize"),teacher,new FinalizeSubmissionDto(),"POST"),HttpStatusCode.NoContent);}
        await Db(async db=>{var submission=await db.ExamSubmissions.SingleAsync(s=>s.Id==start.SubmissionId);Assert.NotNull(submission.GradeId);Assert.NotNull(submission.DisplayOrderJson);Assert.Equal(exam,submission.ExamId);Assert.Equal(Id("student"),submission.StudentId);var grade=await db.Grades.SingleAsync(g=>g.Id==submission.GradeId);Assert.Equal(Id("assignment"),grade.ClassSubjectId);Assert.Equal(10,grade.Value);});
        await Expect(await Request("/api/institution/grades",await Access("teacher")),HttpStatusCode.OK);
    }
    [Fact]
    public async Task TransferAndExamStartCannotBothWinTheSameStudentState()
    {await SeedTransfer();var exam=await TransferExam();var student=await Access("student");var admin=await Access();var results=await Task.WhenAll(Request(Exams+"/"+exam+"/start",student,method:"POST"),Request(TransferRoute(),admin,TransferInput(),"POST"));if(results[0].StatusCode==HttpStatusCode.OK)await Expect(results[1],HttpStatusCode.Conflict);else{await Expect(results[0],HttpStatusCode.NotFound);await Expect(results[1],HttpStatusCode.OK);}await Db(async db=>Assert.False(await db.StudentClassTransferEvents.AnyAsync(e=>e.StudentId==Id("student"))&&await db.ExamSubmissions.AnyAsync(s=>s.StudentId==Id("student")&&s.Status=="in_progress")));}
    [Theory][InlineData("none")][InlineData("legacy")][InlineData("platform")][InlineData("generation")]
    public async Task TransferAndHistoryRejectInvalidAuthentication(string kind)
    {await SeedTransfer();var token=kind=="none"?null:ChangeAdminToken(await Access(),kind);await Expect(await Request(TransferRoute(),token,TransferInput(),"POST"),HttpStatusCode.Unauthorized);await Expect(await Request(TransferHistory(),token),HttpStatusCode.Unauthorized);await AssertTransferUnchanged();}
    [Theory][InlineData("suspended")][InlineData("role")]
    public async Task TransferAndHistoryRevalidateMembership(string kind)
    {await SeedTransfer();var token=await Access();if(kind=="suspended")await Execute("UPDATE institution_membership SET status='suspended' WHERE id=@id",("@id",Id("admin-membership")));else await Execute("DELETE FROM institution_membership_role WHERE membership_id=@id",("@id",Id("admin-membership")));await Expect(await Request(TransferRoute(),token,TransferInput(),"POST"),HttpStatusCode.Forbidden);await Expect(await Request(TransferHistory(),token),HttpStatusCode.Forbidden);await AssertTransferUnchanged();}
    [Theory][InlineData("2026-12-09",3,200)][InlineData("2026-12-10",3,200)][InlineData("2026-12-11",3,409)][InlineData("2027-01-10",4,200)][InlineData("2027-01-11",4,409)]
    public async Task TransferHttpUsesInjectedClockAndConfiguredInclusiveCalendarWindow(string date,int months,int status)
    {await SeedTransfer();await Execute("UPDATE course_edition SET start_date='2026-09-10' WHERE institution_id=@o",("@o",_a));var day=DateOnly.Parse(date);await using var host=await Mt3cHosting.ProgramHost.StartAsync(fixture.ConnectionString,_a,Array.Empty<Guid>(),enabled:false,school:true,course:true,configureServices:services=>services.AddSingleton<TimeProvider>(new TransferClock(new DateTimeOffset(day.ToDateTime(new TimeOnly(12,0)),TimeSpan.Zero))),extraArguments:new[]{"--AcademicTransfers:MaxMonthsFromCourseStart="+months});await Expect(await Request(TransferRoute(),await Access(),TransferInput(date:day),"POST",host:host),(HttpStatusCode)status);}
    private sealed class TransferClock(DateTimeOffset instant):TimeProvider{public override DateTimeOffset GetUtcNow()=>instant;}
}
