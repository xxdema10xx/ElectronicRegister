using System;

namespace ElectronicRegisterAPI.Tests.Integration;

// Identifies an explicitly seeded account. Profile IDs are scenario metadata,
// never claims or authorization input; FixtureTenant resolves persisted IAM.
internal sealed record FixtureActor(Guid UserId, Guid? StudentId, Guid? TeacherId);
