using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

[Collection(IntegrationCollection.Name)]
public sealed class Mt3aOwnershipTests(IntegrationFixture fixture) : IAsyncLifetime
{
    private readonly Guid _a=Guid.NewGuid(), _b=Guid.NewGuid(), _user=Guid.NewGuid();
    private readonly Dictionary<string,Guid[]> _ids=new();
    private Guid Id(string table,bool b=false)
    { if(!_ids.TryGetValue(table,out var ids)) _ids[table]=ids=new[]{Guid.NewGuid(),Guid.NewGuid()}; return ids[b?1:0]; }
    private async Task<MySqlConnection> Open() => await Mt3aOwnershipMigration.OpenAsync(fixture.ConnectionString);
    private async Task<int> Sql(string sql,params (string,object?)[] parameters)
    { await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);foreach(var (name,value) in parameters) cmd.Parameters.AddWithValue(name,value);return await cmd.ExecuteNonQueryAsync(); }
    private async Task<object?> Scalar(string sql,params (string,object?)[] parameters)
    { await using var c=await Open();await using var cmd=new MySqlCommand(sql,c);foreach(var (name,value) in parameters) cmd.Parameters.AddWithValue(name,value);return await cmd.ExecuteScalarAsync(); }

    public async Task InitializeAsync()
    {
        Assert.True(fixture.IsAvailable,fixture.UnavailableReason);
        await using var c=await Open();await using var tx=await c.BeginTransactionAsync();
        async Task Execute(string sql,params (string,object?)[] ps)
        { await using var cmd=new MySqlCommand(sql,c,tx);foreach(var (name,value) in ps) cmd.Parameters.AddWithValue(name,value);await cmd.ExecuteNonQueryAsync(); }
        await Execute("INSERT INTO user(id,email,password_hash,role) VALUES (@u,@email,'preserved-test-hash','admin')",("@u",_user),("@email",_user.ToString("N")+"@mt3a.example.test"));
        foreach(var isB in new[]{false,true})
        {
            var institution=isB?_b:_a;
            async Task Insert(string table,string columns,string values,params (string,object?)[] ps)
            {
                var parameters=new List<(string,object?)>{("@i",institution)};
                if(table is not ("exam_student" or "submission_answer_option")) parameters.Add(("@id",Id(table,isB)));
                parameters.AddRange(ps);
                await Execute($"INSERT INTO `{table}` (institution_id,{columns}) VALUES (@i,{values})",parameters.ToArray());
            }
            await Execute("INSERT INTO institution(id,code,name,status) VALUES (@i,@code,'Same ITS name','active')",("@i",institution),("@code","MT3A_"+institution.ToString("N")));
            await Insert("biennium","id,start_year,end_year","@id,'2450-09-01','2452-08-31'");
            await Insert("study_area","id,name,description","@id,'Same configurable area','Description'");
            await Insert("study_path","id,name","@id,'Same configurable path'");
            await Insert("subject","id,name","@id,'Same configurable subject'");
            await Insert("teacher","id,first_name,last_name","@id,'Same','Teacher'");
            await Insert("biennium_study_area","id,biennium_id,study_area_id","@id,@biennium,@area",("@biennium",Id("biennium",isB)),("@area",Id("study_area",isB)));
            await Insert("biennium_study_path","id,biennium_study_area_id,study_path_id","@id,@bsa,@path",("@bsa",Id("biennium_study_area",isB)),("@path",Id("study_path",isB)));
            await Insert("class","id,biennium_study_path_id,name","@id,@path,'Same class'",("@path",Id("biennium_study_path",isB)));
            await Insert("student","id,first_name,last_name,class_id","@id,'Same','Student',@class",("@class",Id("class",isB)));
            await Insert("class_subject","id,class_id,subject_id,teacher_id","@id,@class,@subject,@teacher",("@class",Id("class",isB)),("@subject",Id("subject",isB)),("@teacher",Id("teacher",isB)));
            await Insert("exam","id,class_subject_id,name,available_from,available_until,duration_minutes,total_points","@id,@cs,'Exam','2450-10-01','2450-10-02',60,1",("@cs",Id("class_subject",isB)));
            await Insert("exam_student","exam_id,student_id,extra_time_minutes","@exam,@student,0",("@exam",Id("exam",isB)),("@student",Id("student",isB)));
            await Insert("grade","id,student_id,class_subject_id,value,date","@id,@student,@cs,7.5,'2450-10-01'",("@student",Id("student",isB)),("@cs",Id("class_subject",isB)));
            await Insert("exam_submission","id,exam_id,student_id,started_at,deadline_at,grade_id,violation_count,last_client_revision","@id,@exam,@student,'2450-10-01','2450-10-02',@grade,0,0",("@exam",Id("exam",isB)),("@student",Id("student",isB)),("@grade",Id("grade",isB)));
            await Insert("question","id,exam_id,position,text,type,points,required","@id,@exam,1,'Question','single',1,1",("@exam",Id("exam",isB)));
            await Insert("question_option","id,question_id,position,text,is_correct","@id,@q,1,'Option',1",("@q",Id("question",isB)));
            await Insert("submission_answer","id,submission_id,question_id","@id,@s,@q",("@s",Id("exam_submission",isB)),("@q",Id("question",isB)));
            await Insert("submission_answer_option","submission_answer_id,option_id","@answer,@option",("@answer",Id("submission_answer",isB)),("@option",Id("question_option",isB)));
            await Insert("exam_violation","id,submission_id,type,occurred_at","@id,@s,'test','2450-10-01'",("@s",Id("exam_submission",isB)));
            await Insert("audit_event","id,entity_type,entity_id,action,before_json,after_json","@id,'exam_submission',@s,'mt3a.test','{\"before\":true}','{\"after\":true}'",("@s",Id("exam_submission",isB)));
            await Execute("INSERT INTO institution_membership(id,institution_id,user_id,status) VALUES (@m,@i,@u,'active')",("@m",Id("membership",isB)),("@i",institution),("@u",_user));
            foreach(var role in new[]{"Teacher","Student"}) await Execute("INSERT INTO institution_membership_role(membership_id,role) VALUES (@m,@r)",("@m",Id("membership",isB)),("@r",role));
            foreach(var kind in new[]{"teacher","student"}) await Execute($"INSERT INTO institution_{kind}_profile(institution_id,membership_id,{kind}_id) VALUES (@i,@m,@p)",("@i",institution),("@m",Id("membership",isB)),("@p",Id(kind,isB)));
        }
        await tx.CommitAsync();
    }

    [Theory]
    [InlineData("biennium_study_area","biennium_id","biennium")]
    [InlineData("biennium_study_area","study_area_id","study_area")]
    [InlineData("biennium_study_path","biennium_study_area_id","biennium_study_area")]
    [InlineData("biennium_study_path","study_path_id","study_path")]
    [InlineData("class","biennium_study_path_id","biennium_study_path")]
    [InlineData("student","class_id","class")]
    [InlineData("class_subject","class_id","class")]
    [InlineData("class_subject","teacher_id","teacher")]
    [InlineData("class_subject","subject_id","subject")]
    [InlineData("exam","class_subject_id","class_subject")]
    [InlineData("exam","retake_of_exam_id","exam")]
    [InlineData("exam_student","student_id","student")]
    [InlineData("exam_submission","student_id","student")]
    [InlineData("exam_submission","exam_id","exam")]
    [InlineData("exam_submission","grade_id","grade")]
    [InlineData("grade","student_id","student")]
    [InlineData("grade","class_subject_id","class_subject")]
    [InlineData("question","exam_id","exam")]
    [InlineData("question_option","question_id","question")]
    [InlineData("submission_answer","question_id","question")]
    [InlineData("submission_answer","submission_id","exam_submission")]
    [InlineData("submission_answer_option","option_id","question_option")]
    [InlineData("exam_violation","submission_id","exam_submission")]
    public async Task RealCompositeForeignKeysRejectCrossTenantUpdate(string child,string column,string parent)
    {
        // Identifiers are theory literals, never request input. Each failed command
        // is checked from a fresh connection, including the untouched B graph.
        var key=child=="exam_student"?"exam_id":child=="submission_answer_option"?"submission_answer_id":"id";
        var childId=child=="exam_student"?Id("exam"):child=="submission_answer_option"?Id("submission_answer"):Id(child);
        var old=await Scalar($"SELECT `{column}` FROM `{child}` WHERE `{key}`=@id",("@id",childId));
        // Remove unrelated blockers so the error proves the intended child FK,
        // not a UNIQUE collision or an incoming parent FK on the mutated key.
        if(child=="class") await Sql("UPDATE class SET name='Distinct A class' WHERE id=@id",("@id",childId));
        if(child=="question") await Sql("UPDATE question SET position=17 WHERE id=@id",("@id",childId));
        if(child=="question_option") await Sql("UPDATE question_option SET position=17 WHERE id=@id",("@id",childId));
        if(child=="exam_submission" && column=="grade_id") await Sql("UPDATE exam_submission SET grade_id=NULL WHERE id=@id",("@id",Id("exam_submission",true)));
        if(child=="submission_answer") await Sql("DELETE FROM submission_answer_option WHERE submission_answer_id=@id",("@id",childId));
        if(child is "question" or "question_option")
        {
            await Sql("DELETE FROM submission_answer_option WHERE submission_answer_id=@id",("@id",Id("submission_answer")));
            if(child=="question") await Sql("DELETE FROM submission_answer WHERE id=@id",("@id",Id("submission_answer")));
        }
        if(child=="exam_submission" && column=="exam_id")
        {
            await Sql("DELETE FROM submission_answer_option WHERE submission_answer_id=@id",("@id",Id("submission_answer")));
            await Sql("DELETE FROM submission_answer WHERE submission_id=@id",("@id",childId));
        }
        var error=await Assert.ThrowsAsync<MySqlException>(()=>Sql($"UPDATE `{child}` SET `{column}`=@other WHERE `{key}`=@id",("@other",Id(parent,true)),("@id",childId)));
        Assert.Equal(1452,error.Number);
        Assert.Equal(old,await Scalar($"SELECT `{column}` FROM `{child}` WHERE `{key}`=@id",("@id",childId)));
        Assert.Equal(_b.ToString(),(await Scalar($"SELECT institution_id FROM `{parent}` WHERE id=@id",("@id",Id(parent,true))))?.ToString());
    }

    [Fact]
    public async Task CatalogNamesAndBienniumDatesAreIndependentAndProfilesHaveNoInventedUsers()
    {
        foreach(var table in new[]{"subject","study_area","study_path","biennium"})
            Assert.Equal(2L,Convert.ToInt64(await Scalar($"SELECT COUNT(*) FROM `{table}` WHERE institution_id IN (@a,@b)",("@a",_a),("@b",_b))));
        Assert.Equal(2L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM institution_membership WHERE user_id=@u",("@u",_user))));
        Assert.Equal(1L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM user WHERE id=@u",("@u",_user))));
        Assert.Equal(0L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM user WHERE student_id IN (@a,@b) OR teacher_id IN (@ta,@tb)",("@a",Id("student")),("@b",Id("student",true)),("@ta",Id("teacher")),("@tb",Id("teacher",true)))));
        using var scope=fixture.ScopeFactory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElectronicRegisterContext>();
        Assert.Equal(_a,await db.Students.AsNoTracking().Where(s=>s.Id==Id("student")).Select(s=>EF.Property<Guid>(s,"InstitutionId")).SingleAsync());
    }

    [Theory][InlineData("student")][InlineData("teacher")]
    public async Task ProfileOwnershipCannotBeMovedOrRelinked(string kind)
    {
        var error=await Assert.ThrowsAsync<MySqlException>(()=>Sql($"UPDATE `{kind}` SET institution_id=@b WHERE id=@id",("@b",_b),("@id",Id(kind))));
        Assert.Equal(1644,error.Number);
        await Sql($"DELETE FROM institution_{kind}_profile WHERE institution_id=@b",("@b",_b));
        var link=await Assert.ThrowsAsync<MySqlException>(()=>Sql($"INSERT INTO institution_{kind}_profile(institution_id,membership_id,{kind}_id) VALUES (@b,@m,@p)",("@b",_b),("@m",Id("membership",true)),("@p",Id(kind))));
        Assert.Contains(link.Number,new[]{1062,1452});
        Assert.Equal(_a.ToString(),(await Scalar($"SELECT institution_id FROM `{kind}` WHERE id=@id",("@id",Id(kind))))?.ToString());
    }

    [Fact]
    public async Task AnswersCannotUseAnotherExamOrAnOptionOfAnotherQuestionInTheSameTenant()
    {
        var otherExam=Guid.NewGuid();var otherQuestion=Guid.NewGuid();var otherOption=Guid.NewGuid();
        await Sql("INSERT INTO exam(id,institution_id,class_subject_id,name,available_from,available_until,duration_minutes,total_points) VALUES (@e,@a,@cs,'Other exam','2450-10-01','2450-10-02',60,1)",("@e",otherExam),("@a",_a),("@cs",Id("class_subject")));
        await Sql("INSERT INTO question(id,institution_id,exam_id,position,text,type,points,required) VALUES (@q,@a,@e,1,'Other question','single',1,1)",("@q",otherQuestion),("@a",_a),("@e",otherExam));
        await Sql("INSERT INTO question_option(id,institution_id,question_id,position,text,is_correct) VALUES (@o,@a,@q,1,'Other option',1)",("@o",otherOption),("@a",_a),("@q",otherQuestion));
        await Sql("DELETE FROM submission_answer_option WHERE submission_answer_id=@id",("@id",Id("submission_answer")));
        var question=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE submission_answer SET question_id=@q WHERE id=@id",("@q",otherQuestion),("@id",Id("submission_answer"))));Assert.Equal(1452,question.Number);
        var option=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO submission_answer_option(submission_answer_id,option_id) VALUES (@answer,@option)",("@answer",Id("submission_answer")),("@option",otherOption)));Assert.Equal(1452,option.Number);
    }

    [Theory][InlineData("class")][InlineData("grade")][InlineData("retake")]
    public async Task HistoricalSetNullKeepsOwnerAndOtherTenantData(string kind)
    {
        var extra=Guid.NewGuid();var table=kind=="retake"?"exam":kind;
        if(kind=="class")
        {
            await Sql("INSERT INTO class(id,institution_id,biennium_study_path_id,name) VALUES (@id,@a,@path,'Delete only this')",("@id",extra),("@a",_a),("@path",Id("biennium_study_path")));
            await Sql("UPDATE student SET class_id=@class WHERE id=@s",("@class",extra),("@s",Id("student")));
        }
        else if(kind=="retake")
        {
            await Sql("INSERT INTO exam(id,institution_id,class_subject_id,name,available_from,available_until,duration_minutes,total_points) VALUES (@id,@a,@cs,'Retake parent','2450-10-01','2450-10-02',60,1)",("@id",extra),("@a",_a),("@cs",Id("class_subject")));
            await Sql("UPDATE exam SET retake_of_exam_id=@id WHERE id=@e",("@id",extra),("@e",Id("exam")));
        }
        else extra=Id("grade");
        await Sql($"DELETE FROM `{table}` WHERE id=@id",("@id",extra));
        var child=kind=="class"?"student":kind=="grade"?"exam_submission":"exam";
        var reference=kind=="class"?"class_id":kind=="grade"?"grade_id":"retake_of_exam_id";
        Assert.Equal(DBNull.Value,await Scalar($"SELECT {reference} FROM `{child}` WHERE id=@id",("@id",Id(child))));
        Assert.Equal(_a.ToString(),(await Scalar($"SELECT institution_id FROM `{child}` WHERE id=@id",("@id",Id(child))))?.ToString());
        Assert.Equal(_b.ToString(),(await Scalar($"SELECT institution_id FROM `{child}` WHERE id=@id",("@id",Id(child,true))))?.ToString());
    }

    [Fact]
    public async Task AuditIsAttributedFromEntityAndCannotClaimAnotherInstitution()
    {
        var audit=Guid.NewGuid();
        await Sql("INSERT INTO audit_event(id,entity_type,entity_id,action,before_json) VALUES (@id,'exam',@e,'mt3a.verify','{\"retained\":true}')",("@id",audit),("@e",Id("exam")));
        Assert.Equal(_a.ToString(),(await Scalar("SELECT institution_id FROM audit_event WHERE id=@id",("@id",audit)))?.ToString());
        var mismatch=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO audit_event(id,institution_id,entity_type,entity_id,action) VALUES (@id,@b,'exam',@e,'mt3a.reject')",("@id",Guid.NewGuid()),("@b",_b),("@e",Id("exam"))));Assert.Equal(1644,mismatch.Number);
        var unresolved=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO audit_event(id,entity_type,entity_id,action) VALUES (@id,'unknown',@e,'mt3a.reject')",("@id",Guid.NewGuid()),("@e",Guid.NewGuid())));Assert.Equal(1644,unresolved.Number);
    }

    [Fact]
    public async Task PlatformAuditRequiresExplicitMetadataOnlyShapeAndNeverClaimsSchoolData()
    {
        var id=Guid.NewGuid();
        try
        {
            await Sql("INSERT INTO audit_event(id,entity_type,action) VALUES (@id,'platform','platform.bootstrap')",("@id",id));
            Assert.Equal(DBNull.Value,await Scalar("SELECT institution_id FROM audit_event WHERE id=@id",("@id",id)));
            var error=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO audit_event(id,entity_type,action,after_json) VALUES (@id,'platform','platform.bootstrap','{\"school\":true}')",("@id",Guid.NewGuid())));Assert.Equal(1644,error.Number);
            var reclassify=await Assert.ThrowsAsync<MySqlException>(()=>Sql("UPDATE audit_event SET institution_id=@a,entity_type='exam',entity_id=@e WHERE id=@id",("@a",_a),("@e",Id("exam")),("@id",id)));Assert.Equal(1644,reclassify.Number);
        }
        finally { await Sql("DELETE FROM audit_event WHERE id=@id",("@id",id)); }
    }

    [Fact]
    public async Task OwnershipBackfillIsIdempotentAndPreservesAllLegacyCellsAndTenantRows()
    {
        var legacy=Guid.Parse((await Scalar("SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'"))!.ToString()!);
        await using var beforeConnection=await Open();var before=JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(beforeConnection));
        await Mt3aOwnershipMigration.BackfillAsync(fixture.ConnectionString,legacy);
        await Mt3aOwnershipMigration.BackfillAsync(fixture.ConnectionString,legacy);
        await using var afterConnection=await Open();Assert.Equal(before,JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(afterConnection)));
        Assert.Equal(_b.ToString(),(await Scalar("SELECT institution_id FROM exam_submission WHERE id=@id",("@id",Id("exam_submission",true))))?.ToString());
    }

    [Fact]
    public async Task AmbiguousLegacyAuditAbortsBackfillAndLeavesEveryRealCellUntouched()
    {
        var legacy=Guid.Parse((await Scalar("SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'"))!.ToString()!);
        await using var original=await Open();var before=JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(original));
        await using(var probe=await Open())
        {
            // Session-local clone models the BEFORE-enforcement legacy ambiguity.
            // The temporary table has legacy columns without enforcement. No production table
            // is altered, no foreign_key_checks bypass, no DROP or mock repository.
            var backupDir=Path.Combine(Path.GetTempPath(),"ElectronicRegister-MT3A-private");Directory.CreateDirectory(backupDir);
            await Mt3aOwnershipMigration.BackupBeforeDdlAsync(probe,Path.Combine(backupDir,"probe.sql"));
            await using(var create=new MySqlCommand("CREATE TEMPORARY TABLE audit_event (id CHAR(36) PRIMARY KEY,actor_user_id CHAR(36) NULL,action VARCHAR(100),entity_type VARCHAR(100),entity_id CHAR(36) NULL,before_json LONGTEXT NULL,after_json LONGTEXT NULL,request_id VARCHAR(100) NULL,created_at DATETIME(6) DEFAULT CURRENT_TIMESTAMP(6),institution_id CHAR(36) NULL)",probe)) await create.ExecuteNonQueryAsync();
            await using(var ambiguous=new MySqlCommand("INSERT INTO audit_event(id,entity_type,entity_id,action,before_json) VALUES (UUID(),'unclassified',UUID(),'historical','{\"retained\":true}')",probe)) await ambiguous.ExecuteNonQueryAsync();
            var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>Mt3aOwnershipMigration.BackfillConnectionAsync(probe,legacy));
            Assert.Contains("audit_unresolved",error.Message);
        }
        await using var after=await Open();Assert.Equal(before,JsonSerializer.Serialize(await Mt3aOwnershipMigration.SnapshotAsync(after)));
    }

    [Fact]
    public async Task InstitutionDeleteRestrictsAndCannotCascadeSchoolData()
    {
        var error=await Assert.ThrowsAsync<MySqlException>(()=>Sql("DELETE FROM institution WHERE id=@id",("@id",_a)));Assert.Equal(1451,error.Number);
        Assert.Equal(2L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM exam WHERE institution_id IN (@a,@b)",("@a",_a),("@b",_b))));
    }

    [Fact]
    public async Task RootOwnershipRequiresAnExistingInstitutionAndLegacyFallbackIsExplicit()
    {
        var missing=await Assert.ThrowsAsync<MySqlException>(()=>Sql("INSERT INTO subject(id,institution_id,name) VALUES (@id,@missing,'Missing owner')",("@id",Guid.NewGuid()),("@missing",Guid.NewGuid())));Assert.Equal(1452,missing.Number);
        var id=Guid.NewGuid();
        try
        {
            await Sql("INSERT INTO teacher(id,first_name,last_name) VALUES (@id,'Legacy','Without User')",("@id",id));
            var explicitLegacy=await Scalar("SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy'");
            Assert.Equal(explicitLegacy,await Scalar("SELECT institution_id FROM teacher WHERE id=@id",("@id",id)));
            Assert.Equal(0L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM user WHERE teacher_id=@id",("@id",id))));
            Assert.Equal(0L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM institution_teacher_profile WHERE teacher_id=@id",("@id",id))));
        }
        finally { await Sql("DELETE FROM teacher WHERE id=@id",("@id",id)); }
    }

    [Fact]
    public async Task AllCompositeConstraintsAndSupportingIndexesExistWithStrictGuidTypes()
    {
        Assert.Equal(27L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND CONSTRAINT_NAME LIKE 'fk_mt3a_%' AND REFERENCED_TABLE_NAME<>'institution'")));
        Assert.Equal(40L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME LIKE 'mt3a_%'")));
        Assert.Equal(0L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME='institution_id' AND (COLUMN_TYPE<>'char(36)' OR COLLATION_NAME<>'ascii_general_ci')")));
        Assert.Equal(0L,Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE k LEFT JOIN information_schema.STATISTICS s ON s.TABLE_SCHEMA=k.TABLE_SCHEMA AND s.TABLE_NAME=k.TABLE_NAME AND s.COLUMN_NAME=k.COLUMN_NAME AND s.SEQ_IN_INDEX=1 WHERE k.CONSTRAINT_SCHEMA=DATABASE() AND k.CONSTRAINT_NAME LIKE 'fk_mt3a_%' AND k.ORDINAL_POSITION=1 AND s.INDEX_NAME IS NULL")));
        await using var resource=typeof(Mt3aOwnershipMigration).Assembly.GetManifestResourceStream("MT3.006_ownership_enforce.sql")!;
        var ddl=await new StreamReader(resource).ReadToEndAsync();
        await using var c=await Open();
        foreach(Match batch in Regex.Matches(ddl,@"ALTER TABLE `?(\w+)`? ([^;]+);"))
        {
            var table=batch.Groups[1].Value;var text=batch.Groups[2].Value;
            foreach(Match fk in Regex.Matches(text,@"ADD CONSTRAINT (\w+) FOREIGN KEY \(([^)]+)\) REFERENCES `?(\w+)`?\s*\(([^)]+)\) ON DELETE (RESTRICT|CASCADE|SET NULL) ON UPDATE (RESTRICT)"))
            {
                await using var cmd=new MySqlCommand("SELECT k.TABLE_NAME,GROUP_CONCAT(k.COLUMN_NAME ORDER BY k.ORDINAL_POSITION),k.REFERENCED_TABLE_NAME,GROUP_CONCAT(k.REFERENCED_COLUMN_NAME ORDER BY k.ORDINAL_POSITION),r.DELETE_RULE,r.UPDATE_RULE FROM information_schema.KEY_COLUMN_USAGE k JOIN information_schema.REFERENTIAL_CONSTRAINTS r ON r.CONSTRAINT_SCHEMA=k.CONSTRAINT_SCHEMA AND r.CONSTRAINT_NAME=k.CONSTRAINT_NAME WHERE k.CONSTRAINT_SCHEMA=DATABASE() AND k.CONSTRAINT_NAME=@name GROUP BY k.TABLE_NAME,k.REFERENCED_TABLE_NAME,r.DELETE_RULE,r.UPDATE_RULE",c);
                cmd.Parameters.AddWithValue("@name",fk.Groups[1].Value);await using var reader=await cmd.ExecuteReaderAsync();Assert.True(await reader.ReadAsync(),fk.Groups[1].Value);
                Assert.Equal(new[]{table,fk.Groups[2].Value,fk.Groups[3].Value,fk.Groups[4].Value,fk.Groups[5].Value,fk.Groups[6].Value},Enumerable.Range(0,6).Select(reader.GetString).ToArray());
            }
            foreach(Match index in Regex.Matches(text,@"ADD (UNIQUE KEY|KEY) (\w+) \(([^)]+)\)"))
            {
                var name=index.Groups[2].Value;
                if(new[]{"uq_mt3a_question_option_owner","uq_mt3a_submission_answer_owner","uq_mt3a_exam_violation_owner","idx_mt3a_exam_submission_exam","idx_mt3a_question_exam","idx_mt3a_question_option_question"}.Contains(name)) continue;
                await using var cmd=new MySqlCommand("SELECT COLUMN_NAME,NON_UNIQUE FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table AND INDEX_NAME=@name ORDER BY SEQ_IN_INDEX",c);
                cmd.Parameters.AddWithValue("@table",table);cmd.Parameters.AddWithValue("@name",name);
                var actual=new List<string>();await using var reader=await cmd.ExecuteReaderAsync();while(await reader.ReadAsync()){actual.Add(reader.GetString(0));Assert.Equal(index.Groups[1].Value=="UNIQUE KEY"?0:1,reader.GetInt32(1));}
                Assert.Equal(index.Groups[3].Value.Split(','),actual);
            }
        }
    }

    [Theory][InlineData("electronic_register")][InlineData("other_database")]
    public async Task OwnershipToolRejectsUnauthorizedDatabaseBeforeNetwork(string database)
        => await Assert.ThrowsAsync<ArgumentException>(()=>Mt3aOwnershipMigration.OpenAsync("Server=not-a-server;Database="+database+";User=x;"));

    public async Task DisposeAsync()
    {
        await using var c=await Open();
        foreach(var table in new[]{"institution_student_profile","institution_teacher_profile","audit_event","submission_answer_option","submission_answer","exam_violation","exam_submission","grade","exam_student","question_option","question","exam","student","class_subject","class","biennium_study_path","biennium_study_area","subject","teacher","study_path","study_area","biennium"})
        { await using var cmd=new MySqlCommand($"DELETE FROM `{table}` WHERE institution_id IN (@a,@b)",c);cmd.Parameters.AddWithValue("@a",_a);cmd.Parameters.AddWithValue("@b",_b);await cmd.ExecuteNonQueryAsync(); }
        await using var cleanup=new MySqlCommand("DELETE r FROM institution_membership_role r JOIN institution_membership m ON m.id=r.membership_id WHERE m.user_id=@u; DELETE FROM institution_membership WHERE user_id=@u; DELETE FROM user WHERE id=@u; DELETE FROM institution WHERE id IN (@a,@b)",c);
        cleanup.Parameters.AddWithValue("@u",_user);cleanup.Parameters.AddWithValue("@a",_a);cleanup.Parameters.AddWithValue("@b",_b);await cleanup.ExecuteNonQueryAsync();
    }
}
