using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Integration;
public sealed partial class InstitutionClassCrudTests
{
    [Fact]
    public async Task CampusDeletionAndCourseLinkPreserveEditionAndEveryCommittedLink()
    {
        var id = await CreateCampus();
        var edition = await Edition();
        var token = await Access();
        var results = await Task.WhenAll(Request($"/api/institution/course-editions/{edition}/campuses", token, new { CampusId = id }, "POST"), Request(Campuses + "/" + id, token, method: "DELETE"));
        if (results[0].StatusCode == HttpStatusCode.Created)
        {
            await Expect(results[1], HttpStatusCode.Conflict);
            await Db(async db =>
            {
                Assert.True(await db.Campuses.AnyAsync(c => c.Id == id));
                Assert.True(await db.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CampusId == id));
            });
        }
        else
        {
            Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict });
            await Expect(results[1], HttpStatusCode.NoContent);
            await Db(async db => Assert.False(await db.CourseEditionCampuses.AnyAsync(c => c.InstitutionId == _a && c.CampusId == id)));
        }

        await Db(async db => Assert.True(await db.CourseEditions.AnyAsync(e => e.Id == edition && e.InstitutionId == _a)));
    }

    [Fact]
    public async Task AcademicPathDeletionAndClassCreationNeverCascadeACommittedClass()
    {
        var bsa = await AcademicCreate("biennium-study-areas", new { BienniumId = Id("biennium"), StudyAreaId = await AcademicCreate("study-areas", AcademicInput("study-areas")) });
        var path = await AcademicCreate("biennium-study-paths", new { BienniumStudyAreaId = bsa, StudyPathId = Id("path") });
        var token = await Access();
        var results = await Task.WhenAll(Request(Classes, token, new { Name = "Concurrent", BienniumStudyPathId = path }, "POST"), Request(Academic("biennium-study-paths") + "/" + path, token, method: "DELETE"));
        if (results[0].StatusCode == HttpStatusCode.Created)
        {
            await Expect(results[1], HttpStatusCode.Conflict);
            await Db(async db => Assert.True(await db.Classes.AnyAsync(c => c.BienniumStudyPathId == path)));
        }
        else
        {
            Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Conflict });
            await Expect(results[1], HttpStatusCode.NoContent);
            await Db(async db => Assert.False(await db.Classes.AnyAsync(c => c.BienniumStudyPathId == path)));
        }
    }
}
