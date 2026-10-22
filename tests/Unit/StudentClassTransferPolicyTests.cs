#nullable enable
using System;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Exceptions;
using Xunit;
namespace ElectronicRegisterAPI.Tests.Unit;
public sealed class StudentClassTransferPolicyTests
{
    [Theory]
    [InlineData("2026-09-10",3,"2026-12-10")]
    [InlineData("2026-09-10",4,"2027-01-10")]
    [InlineData("2026-01-31",1,"2026-02-28")]
    [InlineData("2024-01-31",1,"2024-02-29")]
    [InlineData("2024-02-29",12,"2025-02-28")]
    [InlineData("2026-11-30",3,"2027-02-28")]
    [InlineData("2026-08-31",3,"2026-11-30")]
    public void DeadlineUsesCalendarMonths(string start,int months,string end)
    {
        var deadline=DateOnly.Parse(end);Assert.Equal(deadline,StudentClassTransferPolicy.Deadline(DateOnly.Parse(start),months));
        StudentClassTransferPolicy.EnsureAllowed(DateOnly.Parse(start),deadline.AddDays(-1),months);
        StudentClassTransferPolicy.EnsureAllowed(DateOnly.Parse(start),deadline,months);
        Assert.Throws<BusinessRuleException>(()=>StudentClassTransferPolicy.EnsureAllowed(DateOnly.Parse(start),deadline.AddDays(1),months));
    }
    [Fact] public void MissingCourseStartNeverGetsASurrogate()=>Assert.Throws<BusinessRuleException>(()=>StudentClassTransferPolicy.Deadline(null,3));
    [Fact] public void OverflowFailsClosed()=>Assert.Throws<BusinessRuleException>(()=>StudentClassTransferPolicy.Deadline(new DateOnly(9999,12,31),3));
    [Fact] public void DefaultsAreThreeMonthsAndExplicitRomeZone(){var o=new AcademicTransferOptions();Assert.Equal(3,o.MaxMonthsFromCourseStart);Assert.Equal("Europe/Rome",o.TimeZoneId);Assert.True(AcademicTransferOptions.Valid(o));}
    [Theory][InlineData(0)][InlineData(-1)][InlineData(121)]
    public void InvalidMonthConfigurationIsRejected(int months)=>Assert.False(AcademicTransferOptions.Valid(new(){MaxMonthsFromCourseStart=months}));
    [Fact] public void UnknownZoneIsRejected()=>Assert.False(AcademicTransferOptions.Valid(new(){TimeZoneId="Not/A/Zone"}));
    [Theory]
    [InlineData("2026-03-28T23:30:00Z","2026-03-29")]
    [InlineData("2026-03-29T01:30:00Z","2026-03-29")]
    [InlineData("2026-10-25T00:30:00Z","2026-10-25")]
    [InlineData("2026-10-25T01:30:00Z","2026-10-25")]
    [InlineData("2026-07-01T22:30:00Z","2026-07-02")]
    public void OperationalDateUsesRomeIncludingDst(string instant,string date)=>Assert.Equal(DateOnly.Parse(date),StudentClassTransferPolicy.OperationalDate(new Clock(DateTimeOffset.Parse(instant)),"Europe/Rome"));
    private sealed class Clock(DateTimeOffset instant):TimeProvider{public override DateTimeOffset GetUtcNow()=>instant;}
    [Theory][InlineData(null)][InlineData("")][InlineData(" \t\r\n ")]
    public void BlankReasonRejected(string? reason)=>Assert.Throws<ArgumentException>(()=>StudentClassTransferPolicy.Reason(reason));
    [Fact] public void ReasonLengthAndTrimAreEnforced(){Assert.Equal("reason",StudentClassTransferPolicy.Reason(" reason "));Assert.Equal(1000,StudentClassTransferPolicy.Reason(new string('x',1000)).Length);Assert.Throws<ArgumentException>(()=>StudentClassTransferPolicy.Reason(new string('x',1001)));}
    [Theory][InlineData("InstitutionId")][InlineData("StudentId")][InlineData("FromClassId")][InlineData("ActorUserId")][InlineData("ActorMembershipId")][InlineData("AppliedLimitMonths")][InlineData("RevisionAfter")]
    public void ServerOwnedFieldsAreNeverAccepted(string field)=>Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<TransferStudentClassDto>("{\"EventId\":\""+Guid.NewGuid()+"\",\"ToClassId\":\""+Guid.NewGuid()+"\",\"EffectiveDate\":\"2026-10-22\",\"Reason\":\"test\",\"ExpectedRevision\":0,\""+field+"\":null}"));
}
