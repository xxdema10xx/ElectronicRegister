using ElectronicRegisterAPI.Domain.Results;
using ElectronicRegisterAPI.Domain.Enums;
using System.Collections.ObjectModel;
using System.Text;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Domain.Policies;

public static class FormativePlanRules
{
    // Technical bounds, not regulatory claims. No universal 1200/800 hour requirement.
    public const int MaxComponentMinutes = 10_000_000;
    public const int MaxUnits = 500;
    public const int MaxModulesPerUnit = 500;
    public const int MaxTopicsPerModule = 200;
    public static string Name(string? value) => RequiredText(value, 200);
    public static string? Code(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var result = value.Trim();
        if (result.Length > 64 || result.Any(char.IsControl))
            throw new ArgumentException("Codice non valido.");
        return result;
    }

    public static string PlanCode(string? value) => Code(value) ?? throw new ArgumentException("Codice richiesto.");
    public static string? Description(string? value)
    {
        if (value is not null && Encoding.UTF8.GetByteCount(value) > 65535)
            throw new ArgumentException("Descrizione troppo lunga.");
        return value;
    }

    public static string Topic(string? value) => RequiredText(value, 4000);
    private static string RequiredText(string? value, int maximum)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > maximum || text.Contains('\0'))
            throw new ArgumentException("Testo richiesto o troppo lungo.");
        return text;
    }

    public static int Minutes(long value, bool allowZero)
    {
        if (value < (allowZero ? 0 : 1) || value > MaxComponentMinutes)
            throw new ArgumentOutOfRangeException(nameof(value));
        return checked((int)value);
    }

    public static int WholeHoursToMinutes(long hours)
    {
        if (hours < 0 || hours > MaxComponentMinutes / 60)
            throw new ArgumentOutOfRangeException(nameof(hours));
        return Minutes(checked(hours * 60), true);
    }

    // validSubjectIds must come from a tenant-scoped repository lookup, never from the client.
    // Drafts may be incomplete; issues are returned without changing the budget or structure.
    public static FormativeHourSummary Validate(FormativePlanSnapshot plan, IReadOnlySet<Guid> validSubjectIds)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(validSubjectIds);
        var issues = new List<FormativeValidationIssue>();
        void Issue(string code, Guid id) => issues.Add(new(code, id));
        void CheckText(Func<object?> check, string code, Guid id)
        {
            try
            {
                check();
            }
            catch (ArgumentException)
            {
                Issue(code, id);
            }
        }

        void CheckMinutes(int minutes, bool zero, Guid id)
        {
            if (minutes < (zero ? 0 : 1) || minutes > MaxComponentMinutes)
                Issue("minutes.invalid", id);
        }

        var ids = new HashSet<Guid>();
        void Identity(Guid id)
        {
            if (id == Guid.Empty || !ids.Add(id))
                Issue("identity.invalid-or-duplicate", id);
        }

        void Siblings(IEnumerable<(int Order, string? Code, Guid Id)> siblings)
        {
            var orders = new HashSet<int>();
            var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in siblings)
            {
                if (item.Order < 0 || !orders.Add(item.Order))
                    Issue("order.invalid-or-duplicate", item.Id);
                CheckText(() => Code(item.Code), "code.invalid", item.Id);
                if (!string.IsNullOrWhiteSpace(item.Code) && !codes.Add(item.Code.Trim()))
                    Issue("code.duplicate", item.Id);
            }
        }

        Identity(plan.Id);
        if (plan.InstitutionId == Guid.Empty || plan.StudyPathId == Guid.Empty || plan.PlanId == Guid.Empty)
            Issue("owner.invalid", plan.Id);
        if (plan.VersionNumber <= 0 || plan.Revision < 0 || !Enum.IsDefined(plan.Status))
            Issue("version.invalid", plan.Id);
        CheckText(() => Name(plan.Name), "name.invalid", plan.Id);
        CheckMinutes(plan.TheoryPlannedMinutes, false, plan.Id);
        CheckMinutes(plan.InternshipPlannedMinutes, true, plan.Id);
        if (plan.Units.Count == 0 || plan.Units.Count > MaxUnits)
            Issue("units.count", plan.Id);
        Siblings(plan.Units.Select(u => (u.DisplayOrder, u.Code, u.Id)));
        long assigned = 0;
        foreach (var unit in plan.Units)
        {
            Identity(unit.Id);
            CheckText(() => Name(unit.Name), "name.invalid", unit.Id);
            CheckText(() => Description(unit.Description), "description.invalid", unit.Id);
            if (unit.Modules.Count == 0 || unit.Modules.Count > MaxModulesPerUnit)
                Issue("modules.count", unit.Id);
            Siblings(unit.Modules.Select(m => (m.DisplayOrder, m.Code, m.Id)));
            foreach (var module in unit.Modules)
            {
                Identity(module.Id);
                CheckText(() => Name(module.Name), "name.invalid", module.Id);
                CheckText(() => Description(module.Description), "description.invalid", module.Id);
                CheckMinutes(module.PlannedMinutes, false, module.Id);
                assigned = checked(assigned + module.PlannedMinutes);
                if (module.SubjectId is not { } subject || !validSubjectIds.Contains(subject))
                    Issue("subject.required-or-unavailable", module.Id);
                Topics(module.Competencies, "competencies", module.Id);
                Topics(module.Contents, "contents", module.Id);
            }
        }

        void Topics(IReadOnlyList<FormativeTopic> topics, string kind, Guid moduleId)
        {
            if (topics.Count == 0 || topics.Count > MaxTopicsPerModule)
                Issue(kind + ".count", moduleId);
            Siblings(topics.Select(t => (t.DisplayOrder, (string? )null, t.Id)));
            foreach (var topic in topics)
            {
                Identity(topic.Id);
                CheckText(() => Topic(topic.Description), kind + ".text.invalid", topic.Id);
            }
        }

        long difference = (long)plan.TheoryPlannedMinutes - assigned;
        if (difference != 0)
            Issue("theory.total-mismatch", plan.Id);
        return new(plan.TheoryPlannedMinutes, plan.InternshipPlannedMinutes, (long)plan.TheoryPlannedMinutes + plan.InternshipPlannedMinutes, assigned, Math.Max(0, difference), Math.Max(0, -difference), issues.AsReadOnly());
    }

    public static void RequireEditable(FormativePlanSnapshot plan, long expectedRevision)
    {
        if (plan.Status != FormativeVersionStatus.Draft || expectedRevision < 0 || plan.Revision != expectedRevision || plan.Revision == long.MaxValue)
            throw new InvalidOperationException("Versione immutabile o revisione non corrente.");
    }

    // No in-place mutation: publication freezes independently owned, ordered collections.
    // Persistence must still lock/revalidate IAM and enforce SQL immutability atomically.
    public static FormativePlanSnapshot Publish(FormativePlanSnapshot plan, long expectedRevision, IReadOnlySet<Guid> validSubjectIds)
    {
        RequireEditable(plan, expectedRevision);
        if (!Validate(plan, validSubjectIds).CanPublish)
            throw new InvalidOperationException("Piano non pubblicabile.");
        return Freeze(plan)with
        {
            Status = FormativeVersionStatus.Published,
            Revision = checked(plan.Revision + 1)
        };
    }

    // nextVersionNumber is allocated under a persisted plan lock by the future repository.
    public static FormativePlanSnapshot CloneDraft(FormativePlanSnapshot source, int nextVersionNumber)
    {
        if (nextVersionNumber <= source.VersionNumber)
            throw new ArgumentOutOfRangeException(nameof(nextVersionNumber));
        var copy = Freeze(source);
        return copy with
        {
            Id = Guid.NewGuid(),
            VersionNumber = nextVersionNumber,
            Status = FormativeVersionStatus.Draft,
            Revision = 0,
            CreatedAtUtc = null,
            PublishedAtUtc = null,
            Units = ReadOnly(copy.Units.Select(u => u with { Id = Guid.NewGuid(), Modules = ReadOnly(u.Modules.Select(m => m with { Id = Guid.NewGuid(), Competencies = ReadOnly(m.Competencies.Select(t => t with { Id = Guid.NewGuid() })), Contents = ReadOnly(m.Contents.Select(t => t with { Id = Guid.NewGuid() })) })) }))
        };
    }

    public static long UnitMinutes(FormativeUnitSnapshot unit) => unit.Modules.Sum(m => (long)m.PlannedMinutes);
    public static FormativePlanSnapshot Freeze(FormativePlanSnapshot plan) => plan with
    {
        Units = ReadOnly(plan.Units.OrderBy(u => u.DisplayOrder).ThenBy(u => u.Id).Select(u => u with { Modules = ReadOnly(u.Modules.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m with { Competencies = ReadOnly(m.Competencies.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id)), Contents = ReadOnly(m.Contents.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id)) })) }))
    };
    private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
}
