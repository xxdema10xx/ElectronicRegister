using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Policies;
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ElectronicRegisterAPI.Domain.Models;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

public sealed class FormativePlanRulesTests
{
    private static readonly Guid Subject = Guid.Parse("da600000-0000-4000-8000-000000000001");
    private static readonly IReadOnlySet<Guid> Subjects = new HashSet<Guid> { Subject };
    // Synthetic fixture: no curriculum or unspecified PDF hours inferred.
    private static FormativeModuleSnapshot Module(int order = 0, int minutes = 72000) => new(
        Guid.NewGuid(), null, "Synthetic module", null, minutes, order, Subject,
        new[] { new FormativeTopic(Guid.NewGuid(), "Design a synthetic model", 0) },
        new[] { new FormativeTopic(Guid.NewGuid(), "Synthetic topic", 0) });
    private static FormativePlanSnapshot Plan() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        1, "Synthetic curriculum", FormativeVersionStatus.Draft, 0, 72000, 48000,
        new[] { new FormativeUnitSnapshot(Guid.NewGuid(), null, "Synthetic unit", null, 0, new[] { Module() }) });
    private static FormativePlanSnapshot WithModule(FormativePlanSnapshot plan, FormativeModuleSnapshot module) =>
        plan with { Units = new[] { plan.Units[0] with { Modules = new[] { module } } } };
    private static bool Has(FormativePlanSnapshot plan, string code) => FormativePlanRules.Validate(plan, Subjects).Issues.Any(i => i.Code == code);

    [Theory]
    [InlineData(1200, 800)]
    [InlineData(1300, 700)]
    [InlineData(1100, 900)]
    [InlineData(20, 0)]
    public void ConfigurableComponentsPublishWithExactModuleSum(int theoryHours, int internshipHours)
    {
        var p = Plan() with { TheoryPlannedMinutes = theoryHours * 60, InternshipPlannedMinutes = internshipHours * 60 };
        p = WithModule(p, Module(minutes: theoryHours * 60));
        var result = FormativePlanRules.Validate(p, Subjects);
        Assert.True(result.CanPublish);
        Assert.Equal((theoryHours + internshipHours) * 60L, result.TotalPlannedMinutes);
        Assert.Equal(FormativeVersionStatus.Published, FormativePlanRules.Publish(p, 0, Subjects).Status);
    }
    [Theory]
    [InlineData(45)] [InlineData(75)] [InlineData(90)] [InlineData(135)]
    public void NonWholeHourModuleDurationsAreExact(int minutes)
    {
        var p = WithModule(Plan() with { TheoryPlannedMinutes = minutes }, Module(minutes: minutes));
        Assert.True(FormativePlanRules.Validate(p, Subjects).CanPublish);
        Assert.Equal(minutes, FormativePlanRules.UnitMinutes(p.Units[0]));
    }
    [Theory]
    [InlineData(-1)] [InlineData(10000001)] [InlineData(long.MaxValue)] [InlineData(2147483648L)]
    public void InvalidMinuteValuesCannotNarrowOrOverflow(long minutes) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FormativePlanRules.Minutes(minutes, true));
    [Theory]
    [InlineData(-1)] [InlineData(long.MaxValue)]
    public void InvalidWholeHoursAreRejectedBeforeMultiplication(long hours) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FormativePlanRules.WholeHoursToMinutes(hours));
    [Fact] public void WholeHourConversionIsExact() => Assert.Equal(72000, FormativePlanRules.WholeHoursToMinutes(1200));
    [Fact] public void ZeroIsAllowedOnlyWhenRequested()
    {
        Assert.Equal(0, FormativePlanRules.Minutes(0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => FormativePlanRules.Minutes(0, false));
    }
    [Theory]
    [InlineData(70200, 1800, 0)] [InlineData(73800, 0, 1800)]
    public void DraftReportsDeficitAndExcessWithoutChangingBudget(int assigned, long missing, long excess)
    {
        var p = WithModule(Plan(), Module(minutes: assigned));
        var summary = FormativePlanRules.Validate(p, Subjects);
        Assert.Equal(missing, summary.MissingMinutes);
        Assert.Equal(excess, summary.ExcessMinutes);
        Assert.Equal(72000, p.TheoryPlannedMinutes);
        Assert.False(summary.CanPublish);
        Assert.Throws<InvalidOperationException>(() => FormativePlanRules.Publish(p, 0, Subjects));
    }
    [Fact] public void EmptyDraftCanBeInspected()
    {
        var p = Plan() with { Units = Array.Empty<FormativeUnitSnapshot>() };
        var summary = FormativePlanRules.Validate(p, Subjects);
        Assert.Equal(72000, summary.MissingMinutes);
        Assert.Contains(summary.Issues, i => i.Code == "units.count");
    }
    [Fact] public void DraftAllowsEditingAtCurrentRevision() => FormativePlanRules.RequireEditable(Plan(), 0);
    [Theory]
    [InlineData(-1)] [InlineData(1)] [InlineData(long.MaxValue)]
    public void StaleOrInvalidRevisionIsRejected(long revision) =>
        Assert.Throws<InvalidOperationException>(() => FormativePlanRules.RequireEditable(Plan(), revision));
    [Fact] public void ExhaustedRevisionIsRejected()
    {
        var p = Plan() with { Revision = long.MaxValue };
        Assert.Throws<InvalidOperationException>(() => FormativePlanRules.RequireEditable(p, long.MaxValue));
    }
    [Fact] public void PublishedSnapshotCannotPassEditingOrPublishGuard()
    {
        var p = FormativePlanRules.Publish(Plan(), 0, Subjects);
        Assert.Equal(1L, p.Revision);
        Assert.Throws<InvalidOperationException>(() => FormativePlanRules.RequireEditable(p, 1));
        Assert.Throws<InvalidOperationException>(() => FormativePlanRules.Publish(p, 1, Subjects));
    }
    [Fact] public void PublicationDoesNotChangeSourceDraft()
    {
        var p = Plan();
        var published = FormativePlanRules.Publish(p, 0, Subjects);
        Assert.Equal(FormativeVersionStatus.Draft, p.Status);
        Assert.Equal(0L, p.Revision);
        Assert.Equal(p.Id, published.Id);
    }
    [Fact] public void PublishedCollectionsDoNotAliasMutableDraftLists()
    {
        var modules = new List<FormativeModuleSnapshot> { Module() };
        var p = Plan();
        p = p with { Units = new[] { p.Units[0] with { Modules = modules } } };
        var published = FormativePlanRules.Publish(p, 0, Subjects);
        modules.Clear();
        Assert.Single(published.Units[0].Modules);
        Assert.Throws<NotSupportedException>(() => ((IList<FormativeUnitSnapshot>)published.Units).Clear());
    }
    [Fact] public void CloneCreatesIndependentIdsAndPreservesCatalogReferences()
    {
        var p = FormativePlanRules.Publish(Plan(), 0, Subjects);
        var clone = FormativePlanRules.CloneDraft(p, 2);
        Assert.NotEqual(p.Id, clone.Id);
        Assert.NotEqual(p.Units[0].Id, clone.Units[0].Id);
        Assert.NotEqual(p.Units[0].Modules[0].Id, clone.Units[0].Modules[0].Id);
        Assert.NotEqual(p.Units[0].Modules[0].Competencies[0].Id, clone.Units[0].Modules[0].Competencies[0].Id);
        Assert.NotEqual(p.Units[0].Modules[0].Contents[0].Id, clone.Units[0].Modules[0].Contents[0].Id);
        Assert.Equal(p.PlanId, clone.PlanId);
        Assert.Equal(p.InstitutionId, clone.InstitutionId);
        Assert.Equal(p.StudyPathId, clone.StudyPathId);
        Assert.Equal(Subject, clone.Units[0].Modules[0].SubjectId);
        Assert.Equal(FormativeVersionStatus.Draft, clone.Status);
        Assert.Equal(0L, clone.Revision);
        Assert.Equal(2, clone.VersionNumber);
        Assert.True(FormativePlanRules.Validate(clone, Subjects).CanPublish);
        Assert.Equal(FormativeVersionStatus.Published, p.Status);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public void CloneRequiresLaterNumber(int number) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => FormativePlanRules.CloneDraft(Plan(), number));
    [Fact] public void CloneIntentsHaveIndependentIds()
    {
        var p = Plan();
        Assert.NotEqual(FormativePlanRules.CloneDraft(p, 2).Id, FormativePlanRules.CloneDraft(p, 2).Id);
        // Idempotency is a persistence responsibility, not guaranteed by this pure factory.
    }
    [Fact] public void RepeatedModuleNamesAndSubjectsAcrossUnitsAreValid()
    {
        var p = Plan();
        var u = p.Units[0] with { Modules = new[] { Module(minutes: 36000) } };
        p = p with { Units = new[] { u, u with { Id = Guid.NewGuid(), DisplayOrder = 1, Modules = new[] { Module(minutes: 36000) } } } };
        Assert.True(FormativePlanRules.Validate(p, Subjects).CanPublish);
        Assert.Equal(36000, FormativePlanRules.UnitMinutes(p.Units[1]));
    }
    [Fact] public void MissingAndUnresolvedSubjectsBlockPublication()
    {
        var p = Plan();
        Assert.True(Has(WithModule(p, Module() with { SubjectId = null }), "subject.required-or-unavailable"));
        Assert.True(Has(WithModule(p, Module() with { SubjectId = Guid.NewGuid() }), "subject.required-or-unavailable"));
        Assert.False(FormativePlanRules.Validate(p, new HashSet<Guid>()).CanPublish);
    }
    [Fact] public void MultipleCompetenciesAndContentsArePreservedAndSorted()
    {
        var p = Plan();
        p = WithModule(p, Module() with
        {
            Competencies = new[] { new FormativeTopic(Guid.NewGuid(), "Second result", 2), new FormativeTopic(Guid.NewGuid(), "First result", 0) },
            Contents = new[] { new FormativeTopic(Guid.NewGuid(), "Second topic", 1), new FormativeTopic(Guid.NewGuid(), "First topic", 0) }
        });
        var frozen = FormativePlanRules.Publish(p, 0, Subjects);
        Assert.Equal("First result", frozen.Units[0].Modules[0].Competencies[0].Description);
        Assert.Equal("First topic", frozen.Units[0].Modules[0].Contents[0].Description);
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  ")]
    public void RequiredTextRejectsEmptyValues(string? value)
    {
        Assert.Throws<ArgumentException>(() => FormativePlanRules.Name(value));
        Assert.Throws<ArgumentException>(() => FormativePlanRules.Topic(value));
        Assert.Throws<ArgumentException>(() => FormativePlanRules.PlanCode(value));
    }
    [Fact] public void TextLimitsAreExplicit()
    {
        Assert.Equal(200, FormativePlanRules.Name(new string('a', 200)).Length);
        Assert.Throws<ArgumentException>(() => FormativePlanRules.Name(new string('a', 201)));
        Assert.Equal(4000, FormativePlanRules.Topic(new string('a', 4000)).Length);
        Assert.Throws<ArgumentException>(() => FormativePlanRules.Topic(new string('a', 4001)));
        Assert.Equal(64, FormativePlanRules.PlanCode(new string('a', 64)).Length);
        Assert.Throws<ArgumentException>(() => FormativePlanRules.PlanCode(new string('a', 65)));
        Assert.Throws<ArgumentException>(() => FormativePlanRules.Description(new string('\u20ac', 21846)));
    }
    [Fact] public void InvalidModuleTextIsReportedByValidation() =>
        Assert.True(Has(WithModule(Plan(), Module() with { Name = " " }), "name.invalid"));
    [Fact] public void EmptyCompetenciesBlockPublication() =>
        Assert.True(Has(WithModule(Plan(), Module() with { Competencies = Array.Empty<FormativeTopic>() }), "competencies.count"));
    [Fact] public void EmptyContentsBlockPublication() =>
        Assert.True(Has(WithModule(Plan(), Module() with { Contents = Array.Empty<FormativeTopic>() }), "contents.count"));
    [Fact] public void DuplicateIdsAreDetected() =>
        Assert.True(Has(WithModule(Plan(), Module() with { Id = Guid.Empty }), "identity.invalid-or-duplicate"));
    [Fact] public void DuplicateSiblingOrdersAreDetected()
    {
        var p = Plan();
        p = p with { Units = new[] { p.Units[0], p.Units[0] with { Id = Guid.NewGuid() } } };
        Assert.True(Has(p, "order.invalid-or-duplicate"));
    }
    [Fact] public void DuplicateSiblingCodesIgnoreCaseAndWhitespace()
    {
        var p = Plan();
        var unit = p.Units[0];
        p = p with { Units = new[] { unit with { Code = "UF1" }, unit with { Id = Guid.NewGuid(), DisplayOrder = 1, Code = " uf1 " } } };
        Assert.True(Has(p, "code.duplicate"));
    }
    [Fact] public void NegativeModuleMinutesAreRejected() =>
        Assert.True(Has(WithModule(Plan(), Module(minutes: -1)), "minutes.invalid"));
    [Fact] public void UnitAndModuleOrderingIsExplicit()
    {
        var p = Plan();
        var u = p.Units[0] with { DisplayOrder = 9, Modules = new[] { Module(8, 18000), Module(2, 18000) } };
        p = p with { Units = new[] { u, u with { Id = Guid.NewGuid(), DisplayOrder = 1, Modules = new[] { Module(0, 36000) } } } };
        var sorted = FormativePlanRules.Freeze(p);
        Assert.Equal(1, sorted.Units[0].DisplayOrder);
        Assert.Equal(2, sorted.Units[1].Modules[0].DisplayOrder);
        Assert.True(FormativePlanRules.Validate(p, Subjects).CanPublish);
    }
}
