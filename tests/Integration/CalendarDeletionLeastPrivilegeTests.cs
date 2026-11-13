using System;
using System.Net;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Domain.DTOs.Calendar;
using MySqlConnector;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;

public sealed partial class InstitutionFormativePlanTests
{
    [Fact]
    public async Task LeastPrivilegeDeletePreservesCancelledCalendarPeriodAndHistory()
    {
        await using var setup = await CalendarHost();
        var token = await Token();
        var data = await CalendarSetup(setup, token);
        var lesson = await CalendarCreate(setup, token, data.Classes[0], data.Module, data.Teacher);
        await Ok<CalendarReceipt>(await Send($"calendar/lessons/{lesson.Id}/cancel", token, new CalendarCommand(Guid.NewGuid(), lesson.Revision, "Retention probe"), "POST", setup));
        var periods = await Count("institution_calendar_period");
        var events = await Count("institution_calendar_event");
        Assert.True(periods > 0 && events > 0);
        var settings = new MySqlConnectionStringBuilder(fixture.ConnectionString);
        var user = "cal_" + Guid.NewGuid().ToString("N")[..20];
        var password = Guid.NewGuid().ToString("N");
        await Sql($"CREATE USER '{user}'@'%' IDENTIFIED BY '{password}'");
        try
        {
            await Sql($"GRANT SELECT,INSERT,UPDATE,DELETE ON `{settings.Database}`.* TO '{user}'@'%'");
            settings.UserID = user; settings.Password = password; settings.Pooling = false;
            await using var app = await Mt3cHosting.ProgramHost.StartAsync(settings.ConnectionString, tenant, Array.Empty<Guid>(), enabled: false, school: true, course: true,
                extraArguments: new[] { "--Multitenancy:InstitutionCalendar:Enabled=true", "--Multitenancy:FormativePlans:Enabled=true" });
            Assert.Equal(HttpStatusCode.Conflict, (await Send("classes/" + lesson.ClassId, token, method: "DELETE", target: app)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Send("classes/" + lesson.ClassId, token, target: app)).StatusCode);
            Assert.Equal(periods, await Count("institution_calendar_period"));
            Assert.Equal(events, await Count("institution_calendar_event"));
            Assert.Equal(0, await Count("institution_class_event", "AND action='class.deleted'"));
        }
        finally { await Sql($"DROP USER '{user}'@'%'"); }
    }
}
