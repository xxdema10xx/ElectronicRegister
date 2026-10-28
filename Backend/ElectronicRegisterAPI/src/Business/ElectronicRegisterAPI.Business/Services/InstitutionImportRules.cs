using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using System.Text.Json;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Business.Services;

/// <summary>Pure mapping and preliminary validation. Never resolves tenant ownership or reserves addresses.</summary>
public sealed class InstitutionImportRules(IInstitutionEmailGenerator emails) : IInstitutionImportRules
{
    private static readonly IReadOnlyDictionary<string, ImportField> Aliases = new Dictionary<string, ImportField>(StringComparer.OrdinalIgnoreCase)
    {
        ["Nome"] = ImportField.FirstName,
        ["FirstName"] = ImportField.FirstName,
        ["First Name"] = ImportField.FirstName,
        ["Cognome"] = ImportField.LastName,
        ["LastName"] = ImportField.LastName,
        ["Last Name"] = ImportField.LastName,
        ["Surname"] = ImportField.LastName,
        ["Classe"] = ImportField.Class,
        ["Class"] = ImportField.Class,
        ["RiferimentoEsterno"] = ImportField.ExternalReference,
        ["Riferimento Esterno"] = ImportField.ExternalReference,
        ["ExternalReference"] = ImportField.ExternalReference,
        ["External Reference"] = ImportField.ExternalReference
    };
    public ImportMappingResult Map(IReadOnlyList<string> headers, ImportCategory category, IReadOnlyList<ImportColumnMapping>? explicitMapping = null)
    {
        if (headers is null || headers.Count is < 1 or > 64 || headers.Any(h => string.IsNullOrWhiteSpace(h) || h.Length > 128 || h.Any(char.IsControl)) || !Enum.IsDefined(category))
            throw new ArgumentException("Intestazioni/categoria non valide.");
        var issues = new List<ImportIssue>();
        var mappings = new List<ImportColumnMapping>();
        if (explicitMapping is not null)
        {
            if (explicitMapping.Count > headers.Count || explicitMapping.Any(m => m is null || m.ColumnIndex < 0 || m.ColumnIndex >= headers.Count || !Enum.IsDefined(m.Field)) || explicitMapping.Select(m => m.ColumnIndex).Distinct().Count() != explicitMapping.Count)
                throw new ImportInputException("InvalidMapping", "Mapping colonne non valido.");
            mappings.AddRange(explicitMapping);
        }
        else
        {
            for (var i = 0; i < headers.Count; i++)
                mappings.Add(new(i, Aliases.TryGetValue(headers[i].Trim(), out var field) ? field : ImportField.Ignore));
        }

        foreach (var group in mappings.Where(m => m.Field != ImportField.Ignore).GroupBy(m => m.Field))
            if (group.Count() > 1)
                issues.Add(new(group.Key.ToString(), "AmbiguousMapping", "Più colonne corrispondono allo stesso campo; specificare il mapping."));
        foreach (var field in new[]
        {
            ImportField.FirstName,
            ImportField.LastName
        }

        )
            if (!mappings.Any(m => m.Field == field))
                issues.Add(new(field.ToString(), "RequiredColumnMissing", "Manca una colonna obbligatoria."));
        if (category == ImportCategory.Teacher && mappings.Any(m => m.Field == ImportField.Class))
            issues.Add(new("Class", "UnsupportedField", "L'importazione docenti non assegna classi o materie."));
        foreach (var m in mappings.Where(m => m.Field == ImportField.Ignore))
            issues.Add(new("Column[" + m.ColumnIndex + "]", "IgnoredColumn", "Colonna ignorata; nessuna proprietà viene assegnata.", true));
        return new(mappings.OrderBy(m => m.ColumnIndex).ToArray(), issues);
    }

    public IReadOnlyList<ImportValidatedRow> Validate(
        ImportTable table,
        ImportCategory category,
        ImportMappingResult mapping,
        ImportEmailMode emailMode = ImportEmailMode.None,
        EmailPolicyVersion? policy = null,
        IReadOnlyDictionary<int, int>? emailCandidateOffsets = null
    )
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(mapping);
        if (!Enum.IsDefined(emailMode) || table.Rows is null || table.Rows.Count > 10000 || table.Rows.Any(r => r is null || r.RowNumber < 1 || r.Cells is null || r.Cells.Count != table.Headers.Count || r.Cells.Any(c => c is null || c.Length > 4096)))
            throw new ArgumentException("Tabella o modalità non valide.");
        var verified = Map(table.Headers, category, mapping.Columns);
        if (verified.Issues.Any(i => !i.Warning) || mapping.Issues.Any(i => !i.Warning))
            throw new ImportInputException("InvalidMapping", "Risolvere le anomalie di mapping prima della validazione.");
        var result = new List<ImportValidatedRow>();
        var references = new Dictionary<string, int>(StringComparer.Ordinal);
        var exactRows = new HashSet<string>(StringComparer.Ordinal);
        var generated = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in table.Rows)
        {
            string? Value(ImportField field)
            {
                var column = verified.Columns.SingleOrDefault(c => c.Field == field);
                return column is null ? null : raw.Cells[column.ColumnIndex].Trim();
            }

            var first = Value(ImportField.FirstName);
            var last = Value(ImportField.LastName);
            var classRef = Value(ImportField.Class);
            var external = Value(ImportField.ExternalReference);
            classRef = string.IsNullOrEmpty(classRef) ? null : classRef;
            external = string.IsNullOrEmpty(external) ? null : external;
            var issues = new List<ImportIssue>();
            foreach (var(field, value, required, max)in new[]
            {
                ("FirstName", first, true, 100),
                ("LastName", last, true, 100),
                ("Class", classRef, false, 128),
                ("ExternalReference", external, false, 100)
            }

            )
            {
                if (required && string.IsNullOrWhiteSpace(value))
                    issues.Add(new(field, "RequiredFieldMissing", "Campo obbligatorio mancante."));
                if (value is null)
                    continue;
                if (value.Length > max)
                    issues.Add(new(field, "ValueTooLong", "Lunghezza massima del campo superata."));
                if (value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) || value.Any(char.IsSurrogate) && !WellFormed(value))
                    issues.Add(new(field, "InvalidCharacter", "Caratteri non validi nel campo."));
                if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
                    issues.Add(new(field, "FormulaLikeValue", "Il valore inizia con un prefisso formula non ammesso."));
            }

            if (external is not null)
            {
                if (references.TryGetValue(external, out var prior))
                {
                    var issue = new ImportIssue("ExternalReference", "DuplicateExternalReference", "Riferimento esterno ripetuto nel file.");
                    issues.Add(issue);
                    var before = result[prior];
                    result[prior] = before with
                    {
                        Status = ImportRowStatus.Invalid,
                        Issues = before.Issues.Append(issue).ToArray()
                    };
                }
                else
                    references.Add(external, result.Count);
            }

            if (!exactRows.Add(JsonSerializer.Serialize(new[] { first, last, classRef, external })))
                issues.Add(new("Row", "DuplicateWithinFile", "Riga identica nel file: possibile omonimia; necessaria conferma intenzionale, nessun merge automatico.", true));
            if (classRef is not null)
                issues.Add(new("Class", "RequiresTenantResolution", "Il riferimento deve essere risolto e autorizzato dal workflow istituzionale."));
            string? preview = null;
            if (emailMode == ImportEmailMode.ReserveInstitutionalAddress)
            {
                if (policy is null || policy.State != "Published")
                    issues.Add(new("Email", "EmailPolicyMissing", "È richiesta una policy pubblicata della Fondazione."));
                else if (policy.Category != (category == ImportCategory.Student ? EmailCategory.Student : EmailCategory.Teacher))
                    issues.Add(new("Email", "EmailPolicyCategoryMismatch", "Categoria della policy non coerente con il file."));
                else if (!issues.Any(i => !i.Warning && i.Field is "FirstName" or "LastName"))
                {
                    try
                    {
                        emails.Validate(policy.Template);
                        var attempts = policy.Template.Collision == EmailCollisionStrategy.Reject ? 1 : policy.Template.MaximumAttempts;
                        var offset = emailCandidateOffsets?.GetValueOrDefault(raw.RowNumber) ?? 0;
                        if (offset < 0 || offset >= attempts)
                            throw new ArgumentException();
                        for (var attempt = offset; attempt < attempts; attempt++)
                        {
                            var candidate = emails.Candidate(policy.Template, new(first, last), attempt);
                            if (generated.Add(candidate.NormalizedEmail))
                            {
                                preview = candidate.Email;
                                break;
                            }
                        }

                        if (preview is null)
                            issues.Add(new("Email", "EmailCollisionWithinFile", "Collisione email interna al file non risolta dalla policy."));
                    }
                    catch (ArgumentException)
                    {
                        issues.Add(new("Email", "InvalidEmail", "La policy o il nome non producono un indirizzo valido."));
                    }
                }
            }

            var status = issues.Any(i => !i.Warning && i.Code != "RequiresTenantResolution") ? ImportRowStatus.Invalid : classRef is not null ? ImportRowStatus.AmbiguousReference : issues.Any() ? ImportRowStatus.Warning : ImportRowStatus.Valid;
            result.Add(new(raw.RowNumber, status, first, last, classRef, external, preview, issues));
        }

        return result;
    }

    private static bool WellFormed(string value)
    {
        for (var i = 0; i < value.Length; i++)
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[++i]))
                    return false;
            }
            else if (char.IsLowSurrogate(value[i]))
                return false;
        return true;
    }
}
