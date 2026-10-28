namespace ElectronicRegisterAPI.Infrastructure.Persistence.Migrations;

public sealed record TableDigest(string[] Columns, long Count, string Sha256);
