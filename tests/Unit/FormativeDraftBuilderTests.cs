using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Policies;
#nullable enable
using System;
using System.Linq;
using ElectronicRegisterAPI.Business.Services;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class FormativeDraftBuilderTests
{
    private readonly FormativeDraftBuilder builder=new();
    private static FormativePlanSnapshot Empty()=>new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,"Draft",FormativeVersionStatus.Draft,0,72000,48000,Array.Empty<FormativeUnitSnapshot>());
    private static ReplaceFormativeDraft Request()=>new(Guid.NewGuid(),0,"Draft",72000,48000,
        new[]{new FormativeUnitInput(null,null,"UF",null,0,new[]{new FormativeModuleInput(null,null,"Module",null,75,0,null,new[]{new FormativeTopicInput(null,"Outcome",0)},new[]{new FormativeTopicInput(null,"Topic",0)})})},"Synthetic edit");
    private static ReplaceFormativeDraft Existing(FormativePlanSnapshot p)
    {var u=p.Units.Single();var m=u.Modules.Single();return Request() with{Units=new[]{new FormativeUnitInput(u.Id,u.Code,u.Name,u.Description,u.DisplayOrder,new[]{new FormativeModuleInput(m.Id,m.Code,m.Name,m.Description,m.PlannedMinutes,m.DisplayOrder,m.SubjectId,m.Competencies.Select(t=>new FormativeTopicInput(t.Id,t.Description,t.DisplayOrder)).ToArray(),m.Contents.Select(t=>new FormativeTopicInput(t.Id,t.Description,t.DisplayOrder)).ToArray())})}};}
    [Fact]public void NewElementsReceiveIndependentIds()
    {var source=Empty();var result=builder.Build(source,Request());Assert.Empty(source.Units);Assert.NotEqual(Guid.Empty,result.Units[0].Id);Assert.NotEqual(result.Units[0].Id,result.Units[0].Modules[0].Id);Assert.Equal(75,result.Units[0].PlannedMinutes);}
    [Fact]public void ExistingIdsAreStableAcrossEdits()
    {var p=builder.Build(Empty(),Request());var result=builder.Build(p,Existing(p) with{Name="Updated"});Assert.Equal(p.Units[0].Id,result.Units[0].Id);Assert.Equal(p.Units[0].Modules[0].Id,result.Units[0].Modules[0].Id);Assert.Equal(p.Units[0].Modules[0].Competencies[0].Id,result.Units[0].Modules[0].Competencies[0].Id);}
    [Fact]public void ForeignUnitIdIsRejected()
    {var request=Request();Assert.Throws<ArgumentException>(()=>builder.Build(Empty(),request with{Units=new[]{request.Units[0] with{Id=Guid.NewGuid()}}}));}
    [Fact]public void ForeignModuleIdIsRejected()
    {var request=Request();Assert.Throws<ArgumentException>(()=>builder.Build(Empty(),request with{Units=new[]{request.Units[0] with{Modules=new[]{request.Units[0].Modules[0] with{Id=Guid.NewGuid()}}}}}));}
    [Fact]public void TopicIdsCannotBeMovedBetweenCompetenciesAndContents()
    {var p=builder.Build(Empty(),Request());var request=Existing(p);var u=request.Units[0];var m=u.Modules[0];Assert.Throws<ArgumentException>(()=>builder.Build(p,request with{Units=new[]{u with{Modules=new[]{m with{Contents=m.Competencies}}}}}));}
    [Fact]public void PublishedCannotBeChangedThroughBuilder()
    {Assert.Throws<InvalidOperationException>(()=>builder.Build(Empty() with{Status=FormativeVersionStatus.Published},Request()));}
    [Fact]public void ObsoleteRevisionIsRejected()
    {Assert.Throws<InvalidOperationException>(()=>builder.Build(Empty() with{Revision=1},Request()));}
    [Fact]public void RemovalIsExplicitAndDoesNotChangePrior()
    {var p=builder.Build(Empty(),Request());var result=builder.Build(p,Request() with{Units=Array.Empty<FormativeUnitInput>()});Assert.Empty(result.Units);Assert.Single(p.Units);}
    [Theory][InlineData(-1)][InlineData(10000001)]
    public void InvalidBudgetIsRejected(int minutes)=>Assert.Throws<ArgumentOutOfRangeException>(()=>builder.Build(Empty(),Request() with{TheoryPlannedMinutes=minutes}));
    [Fact]public void NullCollectionsAreRejected()
    {Assert.Throws<ArgumentException>(()=>builder.Build(Empty(),Request() with{Units=null!}));}
    [Fact]public void TechnicalTimestampsAreNotTransferredToClonedDraft()
    {var p=Empty() with{CreatedAtUtc=DateTime.UtcNow,PublishedAtUtc=DateTime.UtcNow,Status=FormativeVersionStatus.Published};var clone=FormativePlanRules.CloneDraft(p,2);Assert.Null(clone.PublishedAtUtc);Assert.Null(clone.CreatedAtUtc);}
}
