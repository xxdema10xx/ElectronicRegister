using ElectronicRegisterAPI.Domain.DTOs.FormativePlans;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Policies;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

// HTTP-independent conversion. Existing IDs are accepted only under their original parent.
public sealed class FormativeDraftBuilder : IFormativeDraftBuilder
{
    public FormativePlanSnapshot Build(FormativePlanSnapshot prior, ReplaceFormativeDraft request)
    {
        FormativePlanRules.RequireEditable(prior, request.ExpectedRevision);
        if (request.Units is null || request.Units.Count > FormativePlanRules.MaxUnits)
            throw new ArgumentException();
        Guid Identity(Guid? id, IEnumerable<Guid> allowed)
        {
            if (id is null)
                return Guid.NewGuid();
            if (id == Guid.Empty || !allowed.Contains(id.Value))
                throw new ArgumentException("Identificativo o appartenenza non validi.");
            return id.Value;
        }

        var units = request.Units.Select(u =>
        {
            if (u is null || u.Modules is null || u.Modules.Count > FormativePlanRules.MaxModulesPerUnit)
                throw new ArgumentException();
            var oldUnit = prior.Units.SingleOrDefault(x => x.Id == u.Id);
            var unitId = Identity(u.Id, prior.Units.Select(x => x.Id));
            var modules = u.Modules.Select(m =>
            {
                if (m is null)
                    throw new ArgumentException();
                var oldModule = oldUnit?.Modules.SingleOrDefault(x => x.Id == m.Id);
                var moduleId = Identity(m.Id, oldUnit?.Modules.Select(x => x.Id) ?? []);
                IReadOnlyList<FormativeTopic> Topics(IReadOnlyList<FormativeTopicInput> items, IReadOnlyList<FormativeTopic>? old)
                {
                    if (items is null || items.Count > FormativePlanRules.MaxTopicsPerModule)
                        throw new ArgumentException();
                    return items.Select(t => t is null ? throw new ArgumentException() : new FormativeTopic(Identity(t.Id, old?.Select(x => x.Id) ?? []), FormativePlanRules.Topic(t.Description), t.DisplayOrder)).ToArray();
                }

                return new FormativeModuleSnapshot(moduleId, FormativePlanRules.Code(m.Code), FormativePlanRules.Name(m.Name), FormativePlanRules.Description(m.Description), FormativePlanRules.Minutes(m.PlannedMinutes, false), m.DisplayOrder, m.SubjectId, Topics(m.Competencies, oldModule?.Competencies), Topics(m.Contents, oldModule?.Contents));
            }).ToArray();
            return new FormativeUnitSnapshot(unitId, FormativePlanRules.Code(u.Code), FormativePlanRules.Name(u.Name), FormativePlanRules.Description(u.Description), u.DisplayOrder, modules);
        }).ToArray();
        return prior with
        {
            Name = FormativePlanRules.Name(request.Name),
            TheoryPlannedMinutes = FormativePlanRules.Minutes(request.TheoryPlannedMinutes, false),
            InternshipPlannedMinutes = FormativePlanRules.Minutes(request.InternshipPlannedMinutes, true),
            Units = units
        };
    }
}
