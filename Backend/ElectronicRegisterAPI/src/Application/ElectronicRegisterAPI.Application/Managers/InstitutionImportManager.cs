using ElectronicRegisterAPI.Domain.Configuration;
using ElectronicRegisterAPI.Domain.DTOs.Email;
using ElectronicRegisterAPI.Domain.DTOs.Import;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Exceptions;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Persistence;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Persistence.Import;
using ElectronicRegisterAPI.Domain.Policies;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ElectronicRegisterAPI.Application.Services;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Models;

namespace ElectronicRegisterAPI.Application.Managers;

internal sealed class InstitutionImportManager(
    InstitutionSchoolAuthorization access,
    IInstitutionImportRepository repository,
    IInstitutionImportFileReader reader,
    IInstitutionImportRules rules,
    IInstitutionEmailRepository emails,
    IInstitutionStudentRepository students,
    IInstitutionTeacherRepository teachers,
    IInstitutionImportFeature feature,
    PlatformIdentitySettings platform,
    IFiscalCodeProtector fiscalCodes,
    IPasswordHasher passwords
) : IInstitutionImportManager
{
    // Headers that carry addresses: with platform accounts the server generates every address.
    private static readonly HashSet<string> EmailHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "E-mail", "E mail", "Mail", "Indirizzo email", "Indirizzo e-mail", "IndirizzoEmail", "Posta elettronica", "Email Address", "EmailAddress"
    };
    private static readonly HashSet<string> FiscalCodeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "CodiceFiscale", "Codice Fiscale", "CF", "FiscalCode", "Fiscal Code"
    };

    private async Task<TenantContext> Actor(CancellationToken ct)
    {
        if (!feature.IsEnabled)
            throw new UnauthorizedAccessException();
        return await access.PermissionAsync(InstitutionPermission.UserCreate, ct);
    }

    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string Intent(TenantContext actor, object request) => Hash(new { actor.InstitutionId, actor.UserId, actor.MembershipId, Request = request });
    private static void EventId(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("EventId obbligatorio.");
    }

    private static void Live(ImportStoredBatch stored)
    {
        if (stored.Batch.State != "Preview" || stored.Batch.ExpiresAtUtc <= DateTime.UtcNow || stored.Payload is null)
            throw new ImportConflictException(stored.Batch.State == "Completed" ? "AlreadyCompleted" : "PreviewExpired");
    }

    private void RequireMode(ImportPreviewSettings settings, IReadOnlyList<string> headers)
    {
        // Per-institution email policies are no longer operative with platform identity.
        if (settings.EmailMode == ImportEmailMode.ReserveInstitutionalAddress && platform.Enabled)
            throw new ImportInputException("EmailPolicyModeRetired", "Le policy email per istituto non sono più operative: usare CreatePlatformAccounts.");
        if (settings.EmailMode != ImportEmailMode.CreatePlatformAccounts)
            return;
        if (!platform.Enabled)
            throw new ImportInputException("PlatformAccountsUnavailable", "La creazione di account di piattaforma non è abilitata.");
        if (settings.Category != ImportCategory.Student)
            throw new ImportInputException("PlatformAccountsStudentOnly", "La creazione di account da file è disponibile solo per gli studenti.");
        if (headers.Any(h => EmailHeaders.Contains(h.Trim())))
            throw new ImportInputException("EmailColumnNotAllowed", "Il file non deve contenere una colonna email: gli indirizzi sono generati dalla piattaforma.");
    }

    // Fiscal-code cells are encrypted before the staging payload is persisted, so the
    // private preview never stores them in clear. The column cannot be moved by a remap.
    private ImportTable Protect(ImportTable table, ImportPreviewSettings settings)
    {
        if (!platform.Enabled)
            return table;
        var columns = table.Headers.Select((header, index) => (header, index))
            .Where(c => FiscalCodeHeaders.Contains(c.header.Trim()) || settings.Mapping?.Any(m => m is not null && m.ColumnIndex == c.index && m.Field == ImportField.FiscalCode) == true)
            .Select(c => c.index).ToHashSet();
        if (columns.Count == 0)
            return table;
        return table with
        {
            Rows = table.Rows.Select(r => r with
            {
                Cells = r.Cells.Select((cell, i) => columns.Contains(i) && cell.Length > 0 && !fiscalCodes.IsSealed(cell) ? fiscalCodes.Seal(cell.Length <= 64 ? cell : cell[..64]) : cell).ToArray()
            }).ToArray()
        };
    }

    private static string Digest(ImportPayload payload) => Hash(new { payload.Table.Sha256, payload.Settings, payload.Mapping, payload.Rows, payload.PolicyId, payload.PolicyRevision, payload.Warnings });
    private static ImportBatch Metadata(
        Guid id,
        ImportPayload payload,
        long revision,
        DateTime created,
        DateTime expires
    )
    {
        var invalid = payload.Rows.Count(r => r.Data.Status is ImportRowStatus.Invalid or ImportRowStatus.AmbiguousReference);
        if (payload.Mapping.Issues.Any(i => !i.Warning))
            invalid = payload.Rows.Count;
        return new(id, payload.Settings.Category, "Preview", revision, payload.Table.Sha256, Digest(payload), payload.Rows.Count, payload.Rows.Count - invalid, invalid, created, expires, payload.PolicyRevision, payload.Warnings, payload.Table.Headers, payload.Mapping.Columns);
    }

    private static void AddIssue(Dictionary<int, List<ImportIssue>> issues, int row, ImportIssue issue)
    {
        if (!issues.TryGetValue(row, out var list))
            issues[row] = list = [];
        list.Add(issue);
    }

    // CreatePlatformAccounts: per-row fiscal-code and address checks. Only this institution's
    // own members are consulted; whether a code exists elsewhere is never disclosed here.
    private async Task<IReadOnlyDictionary<int, string>> FiscalCodesAsync(
        IInstitutionImportSession session,
        ImportTable table,
        ImportMappingResult mapping,
        IReadOnlyList<ImportValidatedRow> preliminary,
        Dictionary<int, List<ImportIssue>> issues
    )
    {
        var codes = new Dictionary<int, string>();
        var column = mapping.Columns.SingleOrDefault(c => c.Field == ImportField.FiscalCode);
        if (column is null || mapping.Issues.Any(i => !i.Warning))
            return codes;
        foreach (var raw in table.Rows)
        {
            var cell = raw.Cells[column.ColumnIndex];
            if (string.IsNullOrWhiteSpace(cell))
            {
                AddIssue(issues, raw.RowNumber, new("FiscalCode", "RequiredFieldMissing", "Codice fiscale obbligatorio."));
                continue;
            }
            if (!fiscalCodes.IsSealed(cell))
                throw new ImportInputException("FiscalCodeColumnNotProtected", "La colonna del codice fiscale non può essere cambiata dopo il caricamento: caricare di nuovo il file.");
            var code = FiscalCodeRules.Normalize(fiscalCodes.Unseal(cell));
            if (!FiscalCodeRules.IsValid(code))
                AddIssue(issues, raw.RowNumber, new("FiscalCode", "InvalidFiscalCode", "Codice fiscale non valido."));
            else
                codes[raw.RowNumber] = code;
        }
        foreach (var duplicate in codes.GroupBy(c => c.Value, StringComparer.Ordinal).Where(g => g.Count() > 1))
            foreach (var row in duplicate)
                AddIssue(issues, row.Key, new("FiscalCode", "DuplicateFiscalCodeWithinFile", "Codice fiscale ripetuto nel file."));
        foreach (var row in preliminary.Where(r => r.FirstName is not null && r.LastName is not null && PlatformEmailRules.Base(r.FirstName, r.LastName) is null))
            AddIssue(issues, row.RowNumber, new("Row", "NameNotRepresentable", "Nome e cognome devono contenere lettere rappresentabili in un indirizzo email."));
        var hmacs = codes.ToDictionary(c => c.Key, c => fiscalCodes.Hmac(c.Value));
        var members = await session.FiscalCodesInInstitutionAsync(hmacs.Values.Distinct(StringComparer.Ordinal).ToArray());
        foreach (var (row, hmac) in hmacs.Where(h => members.Contains(h.Value)))
            AddIssue(issues, row, new("FiscalCode", "FiscalCodeExistsInInstitution", "La persona è già presente nella Fondazione."));
        return codes;
    }

    private async Task<(ImportPayload Payload, IReadOnlyDictionary<int, string> FiscalCodes)> Prepare(
        TenantContext actor,
        IInstitutionImportSession session,
        ImportTable table,
        ImportPreviewSettings settings,
        Guid except,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined(settings.Category) || !Enum.IsDefined(settings.EmailMode) || table.Rows.Count == 0)
            throw new ImportInputException("NoDataRows", "Il file deve contenere righe dati e una categoria valida.");
        RequireMode(settings, table.Headers);
        if (settings.Classes is not null && (settings.Classes.Count > table.Rows.Count || settings.Classes.Any(c => c is null || c.ClassId == Guid.Empty || !table.Rows.Any(r => r.RowNumber == c.RowNumber)) || settings.Classes.Select(c => c.RowNumber).Distinct().Count() != settings.Classes.Count))
            throw new ImportInputException("InvalidClassChoice", "Scelta classe non valida.");
        if (settings.EmailCandidates is not null && (settings.EmailMode != ImportEmailMode.ReserveInstitutionalAddress || settings.EmailCandidates.Count > table.Rows.Count || settings.EmailCandidates.Any(c => c is null || c.CandidateOffset is < 0 or > 99 || !table.Rows.Any(r => r.RowNumber == c.RowNumber)) || settings.EmailCandidates.Select(c => c.RowNumber).Distinct().Count() != settings.EmailCandidates.Count))
            throw new ImportInputException("InvalidEmailCandidate", "Scelta candidato email non valida.");
        var mapping = rules.Map(table.Headers, settings.Category, settings.Mapping);
        var accounts = settings.EmailMode == ImportEmailMode.CreatePlatformAccounts;
        if (accounts && !mapping.Columns.Any(c => c.Field == ImportField.FiscalCode))
            mapping = mapping with { Issues = mapping.Issues.Append(new("FiscalCode", "RequiredColumnMissing", "Manca una colonna obbligatoria.")).ToArray() };
        var warnings = mapping.Issues.ToList();
        if (!accounts && mapping.Columns.FirstOrDefault(c => c.Field == ImportField.FiscalCode) is { } ignored)
            warnings.Add(new("Column[" + ignored.ColumnIndex + "]", "FiscalCodeIgnored", "Il codice fiscale è usato solo dalla modalità CreatePlatformAccounts.", true));
        EmailPolicyVersion? policy = null;
        if (settings.EmailMode == ImportEmailMode.ReserveInstitutionalAddress)
        {
            try
            {
                policy = await emails.PolicyAsync(actor, settings.Category == ImportCategory.Student ? EmailCategory.Student : EmailCategory.Teacher, null, ct);
            }
            catch (KeyNotFoundException)
            { /* Validation returns a stable per-row missing-policy error. */
            }
        }

        var preliminary = mapping.Issues.Any(i => !i.Warning) ? table.Rows.Select(r => new ImportValidatedRow(r.RowNumber, ImportRowStatus.Invalid, null, null, null, null, null, mapping.Issues)).ToArray() : rules.Validate(table, settings.Category, mapping, settings.EmailMode, policy, settings.EmailCandidates?.ToDictionary(c => c.RowNumber, c => c.CandidateOffset));
        var accountIssues = new Dictionary<int, List<ImportIssue>>();
        var codes = accounts ? await FiscalCodesAsync(session, table, mapping, preliminary, accountIssues) : new Dictionary<int, string>();
        var rows = new List<ImportPreparedRow>();
        var classes = new Dictionary<string, IReadOnlyList<ImportClassMatch>>(StringComparer.Ordinal);
        foreach (var row in preliminary)
        {
            ct.ThrowIfCancellationRequested();
            var issues = row.Issues.Where(i => i.Code != "RequiresTenantResolution").ToList();
            if (accountIssues.TryGetValue(row.RowNumber, out var extra))
                issues.AddRange(extra);
            Guid? classId = null;
            string? fingerprint = null;
            IReadOnlyList<Guid>? candidates = null;
            var choice = settings.Classes?.SingleOrDefault(c => c.RowNumber == row.RowNumber)?.ClassId;
            if (row.ClassReference is { } reference)
            {
                var key = reference + "\0" + choice;
                if (!classes.TryGetValue(key, out var matches))
                {
                    matches = await session.ClassesAsync(reference, choice);
                    classes.Add(key, matches);
                }

                if (matches.Count == 0)
                    issues.Add(new("Class", "ClassNotFound", "Classe non disponibile nella Fondazione selezionata."));
                else if (matches.Count > 1)
                {
                    candidates = matches.Select(c => c.Id).ToArray();
                    issues.Add(new("Class", "AmbiguousClass", "Specificare un identificativo classe senza ambiguità."));
                }
                else
                {
                    classId = matches[0].Id;
                    fingerprint = matches[0].Fingerprint;
                }
            }
            else if (choice.HasValue)
                issues.Add(new("Class", "InvalidClassChoice", "Il file non contiene un riferimento classe per la riga."));
            if (row.ExternalReference is { } external && await session.ExternalExistsAsync(settings.Category, external))
                issues.Add(new("ExternalReference", "ExternalReferenceExists", "Riferimento esterno già registrato nella Fondazione."));
            if (row.FirstName is { } first && row.LastName is { } last && await session.HomonymExistsAsync(settings.Category, first, last))
                issues.Add(new("Row", "PossibleExistingPerson", "Possibile omonimia con un'anagrafica esistente; nessun merge o aggiornamento.", true));
            var status = issues.Any(i => !i.Warning) ? issues.Any(i => i.Code == "AmbiguousClass") ? ImportRowStatus.AmbiguousReference : ImportRowStatus.Invalid : issues.Any() ? ImportRowStatus.Warning : ImportRowStatus.Valid;
            rows.Add(new(row with { Status = status, Issues = issues }, classId, fingerprint, candidates));
        }

        if (await session.PreviouslyImportedAsync(table.Sha256, settings.Category, except))
            warnings.Add(new("File", "FilePreviouslyImported", "File identico già importato nel tenant: una ripetizione richiede intenzione esplicita e motivazione.", true));
        return (new(table, settings, mapping, rows, policy?.PolicyId, policy?.Revision, warnings), codes);
    }

    public async Task<ImportBatch> PreviewAsync(
        Stream file,
        string fileName,
        ImportParsingOptions parsing,
        ImportPreviewSettings settings,
        CancellationToken ct
    )
    {
        var actor = await Actor(ct);
        var table = await reader.ReadAsync(file, fileName, parsing, feature.Settings.Limits, ct);
        RequireMode(settings, table.Headers);
        table = Protect(table, settings);
        return await repository.ExecuteAsync(actor, async session =>
        {
            var id = Guid.NewGuid();
            var payload = (await Prepare(actor, session, table, settings, id, ct)).Payload;
            var now = DateTime.UtcNow;
            var b = Metadata(id, payload, 1, now, now.AddMinutes(feature.Settings.TtlMinutes));
            await session.CreateAsync(b, payload, Guid.NewGuid(), Intent(actor, new { Op = "Preview", Id = id, table.Sha256, Settings = settings }));
            return b;
        }, ct);
    }

    public async Task<ImportBatch> GetAsync(Guid id, CancellationToken ct)
    {
        var actor = await Actor(ct);
        return await repository.ExecuteAsync(actor, async s =>
        {
            var b = await s.GetAsync(id);
            return b.Batch with
            {
                Columns = b.Payload?.Table.Headers,
                Mapping = b.Payload?.Mapping.Columns
            };
        }, ct);
    }

    public async Task<ImportRowsPage> RowsAsync(
        Guid id,
        int page,
        int size,
        CancellationToken ct
    )
    {
        if (page < 1 || size is < 1 or > 100 || (long)(page - 1) * size > int.MaxValue)
            throw new ArgumentException();
        var actor = await Actor(ct);
        return await repository.ExecuteAsync(actor, async s =>
        {
            var b = await s.GetAsync(id);
            Live(b);
            return new ImportRowsPage(page, size, b.Payload!.Rows.Count, b.Payload.Rows.Skip((page - 1) * size).Take(size).ToArray());
        }, ct);
    }

    public async Task<ImportBatchPage> ListAsync(int page, int size, CancellationToken ct) => await repository.ExecuteAsync(await Actor(ct), s => s.ListAsync(page, size), ct);
    public async Task<ImportBatch> RemapAsync(Guid id, ImportRemap request, CancellationToken ct)
    {
        EventId(request.EventId);
        var actor = await Actor(ct);
        var hash = Intent(actor, new { Op = "Remap", Id = id, Request = request });
        return await repository.ExecuteAsync(actor, async session =>
        {
            var stored = await session.GetAsync(id);
            var replay = await session.ReplayAsync(request.EventId);
            if (replay is not null)
            {
                if (replay.RequestHash != hash)
                    throw new ImportConflictException("EventIdConflict");
                return JsonSerializer.Deserialize<ImportBatch>(replay.ResultJson)!;
            }

            Live(stored);
            if (stored.Batch.Revision != request.ExpectedRevision || request.Settings.Category != stored.Batch.Category || request.ExpectedRevision == long.MaxValue)
                throw new ImportConflictException("StalePreview");
            var payload = (await Prepare(actor, session, stored.Payload!.Table, request.Settings, id, ct)).Payload;
            var next = Metadata(id, payload, stored.Batch.Revision + 1, stored.Batch.CreatedAtUtc, stored.Batch.ExpiresAtUtc);
            await session.RemapAsync(next, payload, request.EventId, hash);
            return next;
        }, ct);
    }

    // Every invalid row with its blocking issue codes, never cell values.
    private static IReadOnlyList<ImportRowError> InvalidRows(ImportPayload payload) => payload.Rows
        .Where(r => r.Data.Status is ImportRowStatus.Invalid or ImportRowStatus.AmbiguousReference || payload.Mapping.Issues.Any(i => !i.Warning))
        .OrderBy(r => r.Data.RowNumber)
        .Select(r => new ImportRowError(r.Data.RowNumber, r.Data.Issues.Concat(payload.Mapping.Issues).Where(i => !i.Warning).Select(i => i.Code).Distinct(StringComparer.Ordinal).ToArray()))
        .ToArray();

    // Durable results keep identifiers only; current addresses are read back for this tenant.
    private static async Task<ImportResult> WithAddresses(IInstitutionImportSession session, ImportResult result)
    {
        if (result.Accounts is null)
            return result;
        var addresses = await session.EmailsAsync(result.Accounts.Select(a => a.UserId).Distinct().ToArray());
        return result with { Accounts = result.Accounts.Select(a => a with { Email = addresses.GetValueOrDefault(a.UserId), TemporaryPassword = null }).ToArray() };
    }

    public async Task<ImportResult> ConfirmAsync(Guid id, ImportConfirmation request, CancellationToken ct)
    {
        EventId(request.EventId);
        if (request.Reason is { Length: > 500 } || request.Reason?.Any(char.IsControl) == true || request.Reason is not null && string.IsNullOrWhiteSpace(request.Reason) || request.AllowRepeatedFile && string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Motivazione obbligatoria per la ripetizione.");
        var actor = await Actor(ct);
        var hash = Intent(actor, new { Op = "Confirm", Id = id, Request = request });
        // CreatePlatformAccounts: BCrypt hashes for every row are computed before the batch
        // transaction, so neither the tenant lock nor the global email mutex is held meanwhile.
        var peek = platform.Enabled ? await repository.ExecuteAsync(actor, s => s.GetAsync(id), ct) : null;
        var credentials = peek?.Payload?.Settings.EmailMode == ImportEmailMode.CreatePlatformAccounts ? Credentials(peek.Payload.Rows.Count, ct) : null;
        return await repository.ExecuteAsync(actor, async session =>
        {
            var stored = await session.GetAsync(id);
            var replay = await session.ReplayAsync(request.EventId);
            if (replay is not null)
            {
                if (replay.RequestHash != hash)
                    throw new ImportConflictException("EventIdConflict");
                return await WithAddresses(session, JsonSerializer.Deserialize<ImportResult>(replay.ResultJson)!);
            }

            Live(stored);
            if (stored.Batch.Revision != request.ExpectedRevision || stored.Batch.PreviewDigest != request.PreviewDigest || stored.Batch.Revision == long.MaxValue)
                throw new ImportConflictException("StalePreview");
            if (stored.Batch.InvalidRows > 0)
                throw new ImportInputException("RowsInvalid", "Correggere tutte le righe prima di confermare.", InvalidRows(stored.Payload!));
            var (payload, codes) = await Prepare(actor, session, stored.Payload!.Table, stored.Payload.Settings, id, ct);
            if (Digest(payload) != stored.Batch.PreviewDigest)
                throw new ImportConflictException("PreviewContextChanged");
            if ((payload.Warnings.Any(i => i.Warning) || payload.Rows.Any(r => r.Data.Issues.Any(i => i.Warning))) && !request.AcknowledgeWarnings)
                throw new ImportConflictException("WarningsNotAcknowledged");
            if (await session.PreviouslyImportedAsync(payload.Table.Sha256, payload.Settings.Category, id) && !request.AllowRepeatedFile)
                throw new ImportConflictException("RepeatedFileNotAuthorized");
            var created = new List<ImportCreatedRow>();
            IReadOnlyList<ImportAccountRow>? accounts = null;
            if (payload.Settings.EmailMode == ImportEmailMode.CreatePlatformAccounts)
            {
                if (credentials is null || credentials.Length != payload.Rows.Count)
                    throw new ImportConflictException("StalePreview");
                accounts = await CreateAccountsAsync(session, id, payload, codes, credentials, created, fiscalCodes);
            }
            else
                foreach (var row in payload.Rows.OrderBy(r => r.Data.RowNumber))
                {
                    ct.ThrowIfCancellationRequested();
                    Guid? student = null, teacher = null, assignment = null;
                    if (payload.Settings.EmailMode == ImportEmailMode.ReserveInstitutionalAddress)
                    {
                        var a = await emails.CreatePersonAsync(actor, new(Guid.NewGuid(), payload.Settings.Category == ImportCategory.Student ? EmailCategory.Student : EmailCategory.Teacher, payload.PolicyRevision!.Value, row.Data.FirstName!, row.Data.LastName!, row.ClassId), ct);
                        if (a.Email != row.Data.EmailPreview)
                            throw new ImportConflictException("EmailAvailabilityChanged");
                        student = a.StudentId;
                        teacher = a.TeacherId;
                        assignment = a.Id;
                    }
                    else if (payload.Settings.Category == ImportCategory.Student)
                    {
                        student = Guid.NewGuid();
                        await students.AddAsync(actor, new Student { Id = student.Value, FirstName = row.Data.FirstName!, LastName = row.Data.LastName!, ClassId = row.ClassId }, null, ct);
                    }
                    else
                    {
                        teacher = Guid.NewGuid();
                        await teachers.AddAsync(actor, new Teacher { Id = teacher.Value, FirstName = row.Data.FirstName!, LastName = row.Data.LastName! }, null, ct);
                    }

                    var item = new ImportCreatedRow(row.Data.RowNumber, student, teacher, assignment);
                    await session.AddCreatedAsync(id, payload.Settings.Category, item, row.Data.ExternalReference);
                    created.Add(item);
                }

            // Persisted result and audit: identifiers only, never addresses or passwords.
            var result = new ImportResult(id, stored.Batch.Revision + 1, created.Count, created, accounts?.Select(a => a with { Email = null, TemporaryPassword = null }).ToArray());
            await session.CompleteAsync(id, stored.Batch.Revision, request.EventId, hash, result, request.Reason ?? "Importazione CreateOnly AllOrNothing");
            return result with { Accounts = accounts };
        }, ct);
    }

    // E4: one transaction for the whole file. BCrypt hashes for every row are computed in
    // parallel before the global email mutex is taken, so neither the mutex hold time nor the
    // total cost depends on how many people already exist on the platform.
    private (string Temporary, string Hash)[] Credentials(int count, CancellationToken ct)
    {
        var temporaries = Enumerable.Range(0, count).Select(_ => TemporaryPasswords.New()).ToArray();
        var hashes = new string[count];
        Parallel.For(0, count, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Environment.ProcessorCount }, i => hashes[i] = passwords.Hash(temporaries[i]));
        return temporaries.Zip(hashes).ToArray();
    }

    private static async Task<IReadOnlyList<ImportAccountRow>> CreateAccountsAsync(
        IInstitutionImportSession session,
        Guid id,
        ImportPayload payload,
        IReadOnlyDictionary<int, string> codes,
        IReadOnlyList<(string Temporary, string Hash)> credentials,
        List<ImportCreatedRow> created,
        IFiscalCodeProtector fiscalCodes
    )
    {
        var ordered = payload.Rows.OrderBy(r => r.Data.RowNumber).ToArray();
        var people = ordered.Select((r, i) => new ImportAccountRequest(r.Data.RowNumber, r.Data.FirstName!, r.Data.LastName!, PlatformEmailRules.Base(r.Data.FirstName, r.Data.LastName)!, r.ClassId,
            fiscalCodes.Protect(codes[r.Data.RowNumber]), credentials[i].Hash, credentials[i].Temporary)).ToArray();
        var accounts = await session.CreatePlatformAccountsAsync(people);
        var items = accounts.Select((a, i) => new ImportCreatedRow(ordered[i].Data.RowNumber, a.StudentId, null, null)).ToArray();
        await session.AddCreatedManyAsync(id, payload.Settings.Category, items, ordered.Select(r => r.Data.ExternalReference).ToArray());
        created.AddRange(items);
        return accounts.Select(a => a.Account).ToArray();
    }

    public async Task<ImportResult> ResultAsync(Guid id, CancellationToken ct)
    {
        var actor = await Actor(ct);
        return await repository.ExecuteAsync(actor, async s => await WithAddresses(s, (await s.GetAsync(id)).Result ?? throw new ImportConflictException("NotCompleted")), ct);
    }

    public async Task<byte[]> TemplateAsync(ImportCategory category, CancellationToken ct, bool platformAccounts = false)
    {
        _ = await Actor(ct);
        if (!Enum.IsDefined(category) || platformAccounts && category != ImportCategory.Student)
            throw new ArgumentException();
        // Synthetic data only: the fiscal code is the well-known textbook example, not a person.
        var csv = platformAccounts ? "Nome;Cognome;CodiceFiscale;Classe;RiferimentoEsterno\r\nMario;Rossi;RSSMRA80A01H501U;;STU-001\r\n"
            : category == ImportCategory.Student ? "Nome;Cognome;Classe;RiferimentoEsterno\r\nMario;Rossi;;STU-001\r\nGiulia;Bianchi;;STU-002\r\n" : "Nome;Cognome;RiferimentoEsterno\r\nMario;Rossi;DOC-001\r\nAnna Maria;Verdi;DOC-002\r\n";
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }
}
