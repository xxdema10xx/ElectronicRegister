namespace ElectronicRegisterAPI.Domain.Results;

public sealed record ProfileAcquisitionResult(
    Guid EventId,
    Guid MembershipId,
    Guid ProfileId,
    string Kind,
    string Status
);
