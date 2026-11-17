namespace ElectronicRegisterAPI.Domain.Exceptions;

/// <summary>409 on an institutional membership. Code, when set, is exposed as ProblemDetails "code".</summary>
public sealed class MembershipConflictException(string? code = null) : Exception("Conflitto sulla membership istituzionale.")
{
    /// <summary>The change would leave the institution without an active InstitutionAdmin.</summary>
    public const string LastAdministrator = "LastAdministrator";
    /// <summary>ExpectedRevision is not the current revision of the membership.</summary>
    public const string StaleRevision = "StaleRevision";
    /// <summary>EventId already used in this institution for a different intent.</summary>
    public const string EventIdConflict = "EventIdConflict";
    /// <summary>The membership is already in the requested state (role present/absent, already suspended/active).</summary>
    public const string NoOp = "NoOp";
    /// <summary>The action is not allowed from the current status (Pending, Left).</summary>
    public const string InvalidTransition = "InvalidTransition";
    /// <summary>A concurrent change was detected while writing; retry after reading the membership again.</summary>
    public const string ConcurrentChange = "ConcurrentChange";

    public string? Code { get; } = code;
}
