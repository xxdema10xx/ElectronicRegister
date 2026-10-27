using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class InstitutionImportRulesTests
{
    private readonly InstitutionImportRules rules=new(new InstitutionEmailGenerator());
    private static ImportTable Table(string[] headers,params string[][] rows)=>new(new string('A',64),";",headers,rows.Select((r,i)=>new ImportRawRow(i+2,r)).ToArray());
    private static EmailPolicyVersion Policy(EmailCollisionStrategy collision=EmailCollisionStrategy.Reject,EmailCategory category=EmailCategory.Student)=>
        new(Guid.NewGuid(),category,1,"Published",new("allievo_{cognome}.{nome:iniziale}","itsumbria.it",new(),collision,"_{n}"),Guid.NewGuid(),Guid.NewGuid(),DateTime.UtcNow);
    [Theory]
    [InlineData("Nome","Cognome")][InlineData("First Name","Surname")][InlineData("FirstName","LastName")][InlineData(" nome ","COGNOME")][InlineData("First Name","Last Name")]
    public void MappingRecognizesOnlyExactDocumentedAliases(string first,string last)
    {var m=rules.Map(new[]{last,first},ImportCategory.Student);Assert.Empty(m.Issues);Assert.Contains(new ImportColumnMapping(0,ImportField.LastName),m.Columns);Assert.Contains(new ImportColumnMapping(1,ImportField.FirstName),m.Columns);}
    [Fact]
    public void AmbiguousMappingRequiresExplicitChoice()
    {
        var headers=new[]{"Nome","First Name","Cognome"};var automatic=rules.Map(headers,ImportCategory.Student);
        Assert.Contains(automatic.Issues,i=>i.Code=="AmbiguousMapping");
        var manual=rules.Map(headers,ImportCategory.Student,new[]{new ImportColumnMapping(1,ImportField.FirstName),new ImportColumnMapping(2,ImportField.LastName)});Assert.Empty(manual.Issues);
    }
    [Theory]
    [InlineData(-1,ImportField.FirstName)][InlineData(2,ImportField.FirstName)][InlineData(0,(ImportField)42)]
    public void ClosedMappingRejectsInvalidIndexOrField(int index,ImportField field)=>Assert.Throws<ImportInputException>(()=>rules.Map(new[]{"Nome","Cognome"},ImportCategory.Student,new[]{new ImportColumnMapping(index,field)}));
    [Fact]
    public void DuplicateColumnAssignmentAndForbiddenDestinationsFailClosed()
    {
        Assert.Throws<ImportInputException>(()=>rules.Map(new[]{"Nome","Cognome"},ImportCategory.Student,new[]{new ImportColumnMapping(0,ImportField.FirstName),new ImportColumnMapping(0,ImportField.LastName)}));
        foreach(var value in new[]{"Role","InstitutionId","UserId","MembershipId","PasswordHash","Revision","ActorUserId"})
            Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ImportColumnMapping>("{\"ColumnIndex\":0,\"Field\":\""+value+"\"}"));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ImportColumnMapping>("{\"ColumnIndex\":0,\"Field\":1}"));
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<ImportColumnMapping>("{\"ColumnIndex\":0,\"Field\":\"FirstName\",\"Role\":\"InstitutionAdmin\"}"));
    }
    [Fact]
    public void UnknownHeadersCannotMassAssignSecurityProperties()
    {var map=rules.Map(new[]{"Nome","Cognome","Role","InstitutionId","PasswordHash"},ImportCategory.Student);Assert.Equal(3,map.Columns.Count(c=>c.Field==ImportField.Ignore));Assert.All(map.Issues,i=>Assert.True(i.Warning));}
    [Fact]
    public void TeacherClassMappingRequiresExplicitIgnore()
    {Assert.Contains(rules.Map(new[]{"Nome","Cognome","Classe"},ImportCategory.Teacher).Issues,i=>i.Code=="UnsupportedField");}
    [Theory]
    [InlineData("","Rossi","RequiredFieldMissing")][InlineData("Mario","","RequiredFieldMissing")]
    [InlineData("=CMD()","Rossi","FormulaLikeValue")][InlineData("+Mario","Rossi","FormulaLikeValue")]
    [InlineData("@SUM(1)","Rossi","FormulaLikeValue")][InlineData("-1","Rossi","FormulaLikeValue")]
    [InlineData("Ma\nrio","Rossi","InvalidCharacter")]
    [InlineData("Ma\u200brio","Rossi","InvalidCharacter")]
    public void InvalidRowsHaveStableFieldErrors(string first,string last,string code)
    {var table=Table(new[]{"Nome","Cognome"},new[]{first,last});var row=Assert.Single(rules.Validate(table,ImportCategory.Student,rules.Map(table.Headers,ImportCategory.Student)));Assert.Equal(ImportRowStatus.Invalid,row.Status);Assert.Contains(row.Issues,i=>i.Code==code);Assert.Equal(2,row.RowNumber);}
    [Fact]
    public void NameLengthMatchesExistingAnagraphicContract()
    {var t=Table(new[]{"Nome","Cognome"},new[]{new string('a',101),"Rossi"});Assert.Contains(Assert.Single(rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student))).Issues,i=>i.Code=="ValueTooLong");}
    [Fact]
    public void HomonymsAreWarningsRatherThanAnIdentityOrAutomaticMerge()
    {var t=Table(new[]{"Nome","Cognome"},new[]{"Mario","Rossi"},new[]{"Mario","Rossi"});var rows=rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student));Assert.Equal(2,rows.Count);Assert.Equal(ImportRowStatus.Valid,rows[0].Status);Assert.Equal(ImportRowStatus.Warning,rows[1].Status);Assert.Contains(rows[1].Issues,i=>i.Code=="DuplicateWithinFile"&&i.Warning);}
    [Fact]
    public void DuplicateStableReferenceInvalidatesBothRowsButIsCaseSensitive()
    {var t=Table(new[]{"Nome","Cognome","RiferimentoEsterno"},new[]{"Mario","Rossi","STU-1"},new[]{"Marco","Rossi","STU-1"},new[]{"Luca","Rossi","stu-1"});var rows=rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student));Assert.All(rows.Take(2),r=>Assert.Equal(ImportRowStatus.Invalid,r.Status));Assert.Equal(ImportRowStatus.Valid,rows[2].Status);}
    [Fact]
    public void ClassReferenceRemainsUnresolvedWithoutTenantAuthorization()
    {var t=Table(new[]{"Nome","Cognome","Classe"},new[]{"Mario","Rossi","A"});var r=Assert.Single(rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student)));Assert.Equal(ImportRowStatus.AmbiguousReference,r.Status);Assert.Contains(r.Issues,i=>i.Code=="RequiresTenantResolution");}
    [Theory]
    [InlineData("Mario","Rossi","allievo_rossi.m@itsumbria.it")][InlineData("Giulia","Bianchi","allievo_bianchi.g@itsumbria.it")][InlineData("Luca","De Luca","allievo_deluca.l@itsumbria.it")]
    public void EmailPreviewReusesPhase2PureGenerator(string first,string last,string expected)
    {var t=Table(new[]{"Nome","Cognome"},new[]{first,last});Assert.Equal(expected,Assert.Single(rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student),ImportEmailMode.ReserveInstitutionalAddress,Policy())).EmailPreview);}
    [Theory]
    [InlineData(EmailCollisionStrategy.Reject)][InlineData(EmailCollisionStrategy.NumericSuffix)]
    public void InFileEmailCollisionUsesPublishedPolicy(EmailCollisionStrategy collision)
    {var t=Table(new[]{"Nome","Cognome"},new[]{"Mario","Rossi"},new[]{"Marco","Rossi"});var rows=rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student),ImportEmailMode.ReserveInstitutionalAddress,Policy(collision));if(collision==EmailCollisionStrategy.Reject)Assert.Contains(rows[1].Issues,i=>i.Code=="EmailCollisionWithinFile");else Assert.Equal("allievo_rossi.m_2@itsumbria.it",rows[1].EmailPreview);}
    [Fact]
    public void ExplicitNumericCandidateRemainsBoundedByPublishedPolicy()
    {
        var t=Table(new[]{"Nome","Cognome"},new[]{"Mario","Rossi"});var mapping=rules.Map(t.Headers,ImportCategory.Student);var offsets=new Dictionary<int,int>{{2,1}};
        var candidate=Assert.Single(rules.Validate(t,ImportCategory.Student,mapping,ImportEmailMode.ReserveInstitutionalAddress,Policy(EmailCollisionStrategy.NumericSuffix),offsets));Assert.Equal("allievo_rossi.m_2@itsumbria.it",candidate.EmailPreview);
        Assert.Equal(ImportRowStatus.Invalid,Assert.Single(rules.Validate(t,ImportCategory.Student,mapping,ImportEmailMode.ReserveInstitutionalAddress,Policy(),offsets)).Status);
    }
    [Fact]
    public void NoneNeverGeneratesEmailEvenWhenPolicyIsAvailable()
    {var t=Table(new[]{"Nome","Cognome"},new[]{"Mario","Rossi"});Assert.Null(Assert.Single(rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student),policy:Policy())).EmailPreview);}
    [Theory]
    [InlineData("missing")][InlineData("draft")][InlineData("category")][InlineData("invalid-template")]
    public void EmailPreconditionsProduceRowErrors(string kind)
    {var t=Table(new[]{"Nome","Cognome"},new[]{"Mario","Rossi"});var p=kind=="missing"?null:kind=="draft"?Policy() with{State="Draft"}:kind=="category"?Policy(category:EmailCategory.Teacher):Policy() with{Template=new("{role}","itsumbria.it",new())};var r=Assert.Single(rules.Validate(t,ImportCategory.Student,rules.Map(t.Headers,ImportCategory.Student),ImportEmailMode.ReserveInstitutionalAddress,p));Assert.Equal(ImportRowStatus.Invalid,r.Status);Assert.Null(r.EmailPreview);}
}
