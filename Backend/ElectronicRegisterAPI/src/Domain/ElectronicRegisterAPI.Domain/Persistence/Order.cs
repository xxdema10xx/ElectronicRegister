namespace ElectronicRegisterAPI.Domain.Persistence;

// Persisted display order; property names are part of the stored JSON contract.
public sealed record Order(Guid[] Questions, Dictionary<Guid, Guid[]> Options);
