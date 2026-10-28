using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;

namespace ElectronicRegisterAPI.Domain.Persistence;

public sealed record ExpiryCandidate(Guid SubmissionId, Guid InstitutionId, DateTime DeadlineAt);
