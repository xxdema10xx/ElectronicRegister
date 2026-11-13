using System;
using System.Threading;
using System.Threading.Tasks;
using ElectronicRegisterAPI.Application.Managers;
using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using Moq;
using Xunit;

namespace ElectronicRegisterAPI.Tests.Unit;

// With platform identity enabled, per-institution email policy and assignment writes are
// refused like the disabled module, before authorization or any repository access.
public sealed class InstitutionEmailRetirementTests
{
    private static InstitutionEmailManager Manager() => new(
        null!,
        new Mock<IInstitutionEmailRepository>(MockBehavior.Strict).Object,
        new Mock<IInstitutionEmailGenerator>(MockBehavior.Strict).Object,
        new InstitutionEmailFeature(true),
        new PlatformIdentitySettings(true, "its.example", "h", new byte[32], "e", new byte[32], 36, TimeSpan.FromDays(1), 200));

    [Fact]
    public async Task EveryWriteIsRefusedWhenPlatformIdentityIsEnabled()
    {
        var manager = Manager();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.WritePolicyAsync(EmailCategory.Student, null!, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.ReserveAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.OverrideAsync(Guid.NewGuid(), null!, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.RetireAsync(Guid.NewGuid(), null!, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => manager.CreatePersonAsync(new EmailCreatePerson(Guid.NewGuid(), EmailCategory.Student, 1, "Mario", "Rossi", null), CancellationToken.None));
    }
}
