# 📊 Report di revisione finale — ElectronicRegisterAPI (Clean Architecture)

> Analisi eseguita su `Backend/ElectronicRegister/src`, confrontando il codice con `dritte-di-programmazione.md`, `guida-refactoring-clean-architecture.md` e i due report/verifiche precedenti (`REPORT_VERIFICA_PUNTI_2-7.3.md`, `correzioni-refactoring-clean-architecture.md`).
>
> **Nota metodologica:** in questo sandbox non è disponibile l'SDK .NET, quindi non ho potuto lanciare `dotnet build`. Ho comunque installato un compilatore C# (Mono `mcs`) per verificare puntualmente un paio di dubbi (vedi punto sui *namespace mancanti*), e per il resto ho fatto una revisione statica riga per riga di tutti i file `.cs` dei 5 progetti. Ti consiglio comunque di lanciare `Ctrl+Shift+B` in Visual Studio come primo passo appena riprendi in mano il progetto, per avere la conferma definitiva.

---

## 🟢 Sommario esecutivo

Il refactoring è **sostanzialmente completo e ben fatto**: la struttura a 4 livelli + Api esiste, le dipendenze tra progetti sono corrette, la DI è quasi ovunque per interfaccia, la configurazione esce solo tramite `IOptions<T>`, le classi concrete sono `internal`. Rispetto all'ultima verifica (fino al punto 7.3), hai portato a termine anche Business (8), Application (9), Controller (10), `Program.cs` (11) e la "internalizzazione" (12), **più un Global Exception Handler non richiesto esplicitamente dalla guida ma coerente con essa e ben implementato**.

Ho però trovato **4 difetti funzionali reali** (uno di questi è una regressione di sicurezza/validazione rispetto al progetto originale) e **alcune inconsistenze di stile/namespace** che vale la pena sistemare prima di scrivere i test.

---

## 1️⃣ Punti completati correttamente

| Step guida | Cosa verifica | Esito |
|---|---|---|
| 0-1 | Branch/checkpoint, struttura cartelle `root/src/{Domain,Infrastructure,Business,Application,Api}` + `test/` | ✅ |
| 2 | 5 progetti creati, tutti `net10.0`, `Nullable`/`ImplicitUsings` enable | ✅ |
| 3 | Cartelle di soluzione `src`/`test` nello `.slnx` | ✅ |
| 4 | Riferimenti tra progetti: `Domain` isolato; `Infrastructure→Domain`; `Business→Domain`; `Application→Business,Domain`; `Api→tutti` | ✅ verificato nei `.csproj`, nessun riferimento circolare, `Business` non referenzia `Infrastructure` |
| 5 | Pacchetti NuGet nel progetto giusto (BCrypt/EF/Pomelo/IdentityModel in `Infrastructure`, Scalar/OpenApi in `Api`) | ✅ `Microsoft.Identity.Web` correttamente **non presente** (dipendenza morta rimossa come suggerito) |
| 6 | Domain: 23 DTO + `ClaimsContext`, enum `UserRole`, interfacce Repository/Security/Service/Manager | ✅ presente anche il compromesso pragmatico "Domain/Models" (POCO puri) suggerito dalla guida al §6.3, usato coerentemente dai Repository per il mapping |
| 7.1–7.5 | Entità EF `internal` in `Infrastructure/Persistence`, `Options` `public`, servizi di sicurezza (`BcryptPasswordHasher`, `JwtTokenGenerator`, `MicrosoftTokenValidator`), Repository, `AddInfrastructure` | ✅ tutto internal dove richiesto, DI per interfaccia, lifecycle `Scoped` |
| 8 | Business: `GradeService`, `StudentService`, `SubjectService`, `TeacherService`, `UserService` con eccezioni di dominio (`ArgumentException`, `UnauthorizedAccessException`, `BusinessRuleException`) invece di riferimenti ASP.NET | ✅ concettualmente corretto (vedi però bug §3.1) |
| 9 | Application: 6 Manager, uno per aggregato, che orchestrano Repository + Service e restituiscono DTO | ✅ (vedi bug §3.2 e §3.3) |
| 10 | Controller "sottili": iniettano solo il Manager, costruiscono `ClaimsContext` con `CurrentCaller()`, nessun `DbContext`/`IConfiguration` iniettato | ✅ pattern rispettato in tutti e 6 i controller; il codice morto (`_grades`, `_subjects` statici) segnalato nella guida risulta correttamente rimosso |
| 11 | `Program.cs` finale: `AddInfrastructure/AddBusinessServices/AddApplicationManagers(+AddApiServices)`, CORS, JWT Bearer, Auth, OpenApi/Scalar | ✅ identico allo schema della guida, con l'aggiunta di `app.UseExceptionHandler()` (vedi §4) |
| 12 | Classi concrete `internal` (Repository, Service, Manager, entità EF, `ElectronicRegisterContext`, servizi di sicurezza) | ✅ verificato su tutti i file; restano `public` solo interfacce/DTO/enum/Options/estensioni DI, come da guida |
| Checklist "correzioni" 1-3, 5 | Cartella `Api/Models` duplicata eliminata; `namespace` aggiunto a `GradeManager`; `GetByIdAsync` senza query ridondanti; `GradeStatisticsDto` in PascalCase | ✅ tutti confermati nel codice attuale |

**In sintesi: gli step 0→12 della guida sono implementati correttamente nella sostanza.** Non ho trovato violazioni architetturali (nessuna dipendenza vietata, nessun `IConfiguration` fuori da `Program.cs`/`AddInfrastructure`, nessuna entità EF esposta fuori da `Infrastructure`).

---

## 2️⃣ Punti da finire

Come indicato da te, **i test (Step 13) sono esclusi da questa verifica** — non li ho toccati. Oltre a quelli, restano aperti:

- **Step 14 (compilare e verificare)**: da fare tu in Visual Studio con `Ctrl+Shift+B`, perché qui non ho l'SDK .NET per una build reale. Ho comunque verificato "a mano" i punti più a rischio (vedi §5).
- **`InternalsVisibleTo`** (menzionato allo step 12 della guida, propedeutico allo step 13): nessun `.csproj` di `Infrastructure`/`Business`/`Application` ha ancora l'`<ItemGroup><InternalsVisibleTo Include="..."/></ItemGroup>`. Non è un errore adesso (i progetti di test non esistono ancora), ma quando li creerai dovrai aggiungerlo, altrimenti i test non potranno referenziare le classi `internal` (Repository, Service, Manager).

---

## 3️⃣ Punti completati in modo scorretto (bug reali)

### 3.1 🔴 `TeacherManager.UpdateAsync` non applica le modifiche — endpoint "Update Teacher" è un no-op

**File:** `src/Application/ElectronicRegisterAPI.Application/Managers/TeacherManager.cs`

```csharp
public async Task<bool> UpdateAsync(Guid id, UpdateTeacherDto dto)
{
    var teacher = await _teacherRepository.GetByIdAsync(id);
    if (teacher == null) return false;

    await _teacherRepository.UpdateAsync(teacher);   // ← teacher non è mai stato modificato!
    return true;
}
```

Il metodo legge il docente dal database, **non copia mai i valori di `dto.FirstName`/`dto.LastName` sull'oggetto**, e lo ri-salva così com'era. Il Controller riceve `true` (quindi risponde `204 No Content`, "operazione riuscita"), ma nel database non cambia nulla. Confronta con `StudentManager.UpdateAsync`, che fa correttamente `student.FirstName = dto.FirstName; student.LastName = dto.LastName;` prima di salvare — `TeacherManager` è l'unico manager con questo problema.

**Correzione:**
```csharp
public async Task<bool> UpdateAsync(Guid id, UpdateTeacherDto dto)
{
    var teacher = await _teacherRepository.GetByIdAsync(id);
    if (teacher == null) return false;

    teacher.FirstName = dto.FirstName;
    teacher.LastName = dto.LastName;

    await _teacherRepository.UpdateAsync(teacher);
    return true;
}
```

---

### 3.2 🔴 `AuthManager.RegisterAsync` ha perso la validazione del formato email — regressione rispetto all'originale

**File:** `src/Application/ElectronicRegisterAPI.Application/Managers/AuthManager.cs`

Il progetto monolitico originale (`Backend/ElectronicRegisterAPI/Controllers/AuthController.cs`, endpoint `register`) validava esplicitamente che l'email fosse nel formato `allievo_...@itsumbria.it`:

```csharp
if (string.IsNullOrWhiteSpace(dto.Email) ||
    !dto.Email.StartsWith("allievo_") ||
    !dto.Email.EndsWith("@itsumbria.it"))
{
    return BadRequest("Invalid email format!");
}
```

Questa regola è stata correttamente spostata in `UserService.EnsureSelfRegistrationEmailFormat(string email)` (Business layer, come previsto dalla guida), **ma nel nuovo `AuthManager.RegisterAsync` non viene mai richiamata**:

```csharp
public async Task RegisterAsync(RegisterDto dto)
{
    await _userService.EnsureEmailIsAvailableAsync(dto.Email);
    _userService.EnsureValidPassword(dto.Password);
    _userService.EnsureValidName(dto.FirstName, dto.LastName);
    // ← manca: _userService.EnsureSelfRegistrationEmailFormat(dto.Email);
    ...
}
```

Oggi è quindi possibile registrare uno studente con **qualunque indirizzo email**, bypassando una regola di business esplicita del progetto originale. Va aggiunta la chiamata mancante (subito dopo `EnsureEmailIsAvailableAsync` ha senso logicamente).

**Correzione:**
```csharp
public async Task RegisterAsync(RegisterDto dto)
{
    _userService.EnsureSelfRegistrationEmailFormat(dto.Email);
    await _userService.EnsureEmailIsAvailableAsync(dto.Email);
    _userService.EnsureValidPassword(dto.Password);
    _userService.EnsureValidName(dto.FirstName, dto.LastName);
    ...
}
```

---

### 3.3 🟠 `GradeController.Delete` ignora l'esito e risponde sempre `204`, anche se il voto non esiste

**File:** `src/Api/ElectronicRegisterAPI/Controllers/GradeController.cs`

```csharp
[HttpDelete("{id}")]
[Authorize(Roles = "admin")]
public async Task<ActionResult> Delete(Guid id)
{
    var deleted = await _gradeManager.DeleteAsync(id);
    return NoContent();   // ← "deleted" non viene mai controllato
}
```

Tutti gli altri controller (`Student`, `Subject`, `Teacher`, `User`) seguono coerentemente il pattern `return deleted ? NoContent() : NotFound();`. Qui manca, quindi un `DELETE` su un `id` inesistente risponde `204` invece di `404`, dando un falso senso di successo al chiamante.

**Correzione:**
```csharp
return deleted ? NoContent() : NotFound();
```

---

### 3.4 🟡 `GradeManager.GetGradesByDateAsync` — correzione della checklist precedente non completata

**File:** `src/Application/ElectronicRegisterAPI.Application/Managers/GradeManager.cs`

Il documento `correzioni-refactoring-clean-architecture.md` (punto 4 della checklist) chiedeva di applicare a **entrambi** i metodi `GetGradesBySubjectNameAsync` e `GetGradesByDateAsync` la stessa correzione sulla gestione della lista vuota. È stata applicata solo alla prima:

```csharp
// GetGradesBySubjectNameAsync — CORRETTO
if (grades.Count == 0) return new List<GradeDto>();

// GetGradesByDateAsync — NON corretto, resta il codice fuorviante originale
if (grades.Count == 0) return grades.Select(s => new GradeDto()).ToList();
```

Il comportamento a runtime è identico (una `Select` su una lista vuota produce comunque una lista vuota), quindi **non è un bug funzionale**, ma è un refuso della checklist di correzioni già approvata: il codice resta fuorviante da leggere (sembra generare `GradeDto` vuoti, in realtà no) e non è coerente con il metodo gemello.

**Correzione:**
```csharp
if (grades.Count == 0) return new List<GradeDto>();
```

---

## 4️⃣ Global Exception Handler: serve davvero, o esiste già uno strumento .NET?

**Risposta breve: sì, serve, ed è implementato correttamente — e sì, esiste già uno strumento .NET, che è esattamente quello che hai usato.**

Da .NET 8 in poi ASP.NET Core espone l'interfaccia **`IExceptionHandler`** (in `Microsoft.AspNetCore.Diagnostics`) proprio come punto di estensione ufficiale per la gestione centralizzata delle eccezioni, agganciato al middleware già esistente `app.UseExceptionHandler()`. Prima di .NET 8 l'unico modo era scrivere middleware custom da zero; da .NET 8 in poi il framework fornisce la "tubatura" (middleware, pipeline, integrazione con `ProblemDetails`/RFC 7807), ma **la logica di mappatura "questa eccezione → questo status HTTP" resta necessariamente compito tuo**, perché è specifica del dominio applicativo.

Il tuo `GlobalExceptionHandler`:
```csharp
services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddProblemDetails();
// ...
app.UseExceptionHandler();
```
segue esattamente il pattern raccomandato da Microsoft: implementa `IExceptionHandler.TryHandleAsync`, è registrato con `AddExceptionHandler<T>()`, usa `AddProblemDetails()` per risposte standard RFC 7807, ed è agganciato con `UseExceptionHandler()` — non hai reinventato nulla, hai correttamente collegato lo strumento nativo di .NET alle tue eccezioni di dominio (`BusinessRuleException`, `ArgumentException`, `UnauthorizedAccessException`).

**Perché è indispensabile in questa architettura, e non solo "carino da avere":** i Manager/Service del livello Business lanciano eccezioni .NET standard proprio perché — per dritta esplicita del progetto — *"il livello Business non deve conoscere ASP.NET Core"* e quindi non può restituire `BadRequest`/`Forbid`/`NotFound` direttamente. Senza un componente come questo, ogni Controller dovrebbe avere `try/catch` propri per tradurre le eccezioni in `ActionResult`, il che violerebbe la dritta "nel controller va una riga di codice, il Manager che chiama tutto il flusso". Il `GlobalExceptionHandler` è quindi il pezzo che *chiude* il pattern eccezioni-come-flusso-di-errore-del-Business, non un extra ridondante.

**Piccola nota per il futuro (net10.0):** da .NET 10 il middleware di exception handling logga sempre a livello `Error` ogni eccezione intercettata, anche quelle già gestite dal tuo `IExceptionHandler` (comportamento diverso da .NET 8/9). Non è un problema di correttezza, ma se in produzione userai un log aggregator, aspettati un log "Error" anche per eccezioni di business volute come `BusinessRuleException` (409) — puoi filtrarle per categoria/status code se vuoi log più puliti.

---

## 5️⃣ Osservazioni minori (non bloccanti, ma da sistemare)

### 5.1 File senza `namespace` esplicito (stile, non blocca la build)

Ho verificato con un compilatore C# (Mono `mcs`) che un tipo dichiarato senza `namespace` finisce nel **namespace globale**, che in C# è comunque visibile senza `using` da qualunque namespace annidato — quindi questi file **compilano lo stesso** (a differenza di quanto indicato nel report di correzioni precedente per `GradeManager`, dove il problema era reale solo perché all'epoca mancava anche altro). Restano però un'incoerenza rispetto alla convenzione seguita ovunque nel resto del progetto, e un rischio latente di collisione di nomi in futuro:

| File | Namespace atteso |
|---|---|
| `Api/.../Controllers/GradeController.cs` | `ElectronicRegisterAPI.Api.Controllers` (gli altri 5 controller ce l'hanno) |
| `Api/.../Controllers/ApiControllerBase.cs` | `ElectronicRegisterAPI.Api.Controllers` |
| `Business/.../Services/TeacherService.cs` | `ElectronicRegisterAPI.Business.Services` |
| `Domain/.../Interfaces/Services/IUserService.cs` | `ElectronicRegisterAPI.Domain.Interfaces.Services` |
| `Domain/.../Interfaces/Managers/IUserManager.cs` | `ElectronicRegisterAPI.Domain.Interfaces.Managers` |

### 5.2 Segreti in chiaro in `appsettings.json`, non escluso da `.gitignore`

`src/Api/ElectronicRegisterAPI/appsettings.json` contiene una **chiave JWT reale** e i **TenantId/ClientId di Azure AD** reali. Il `.gitignore` esclude solo `appsettings.Development.json` e `appsettings.*.local.json`, **non** `appsettings.json`: se questo file viene committato, quei segreti finiscono nella cronologia Git. Consiglio: spostare i valori sensibili in User Secrets (`dotnet user-secrets`) o variabili d'ambiente e lasciare in `appsettings.json` solo placeholder, come già fai correttamente in `appsettings.example.json`.

### 5.3 Cartella del progetto monolitico originale ancora presente

`Backend/ElectronicRegisterAPI/` (il progetto pre-refactoring, a un solo assembly) esiste ancora accanto a `Backend/ElectronicRegister/` (la nuova solution). Non interferisce con la build della nuova solution, ma è dead weight che può generare confusione (es. aprire per sbaglio lo `.slnx` vecchio). Se il codice vecchio è già recuperabile da Git, valuta di eliminare la cartella fisicamente.

### 5.4 Micro-osservazioni di codice (facoltative)

- `IGradeService.EnsureGradeExists(Guid id)` è implementato in `GradeService` ma **non è mai chiamato** da nessun Manager: o è codice morto da rimuovere, o mancava una `EnsureGradeExists` in `GradeManager.UpdateAsync`/`DeleteAsync` prima di procedere (attualmente entrambi si limitano a un `if (grade is null) return false/null`, funzionalmente equivalente ma senza passare dal Business layer).
- `SubjectRepository.cs` ha uno `using` che aliasa un intero namespace (`using Subject = ElectronicRegisterAPI.Domain.Models;`) mai utilizzato: è innocuo ma va rimosso per pulizia.
- `SubjectManager.GetSubjectsByTeacherIdAsync` recupera prima tutte le materie del docente e **solo dopo** applica il controllo di autorizzazione (`caller.Role == UserRole.Teacher && caller.TeacherId != teacherId`); funzionalmente corretto ma fa una query di troppo quando l'accesso viene comunque negato — converrebbe controllare il permesso prima di interrogare il repository.

---

## 6️⃣ Migliorie consigliate (con step di implementazione)

Queste non sono "errori" rispetto alla guida, ma miglioramenti di robustezza che consiglio prima o dopo aver scritto i test.

### Miglioria 1 — Validare `dto.StudentId` in `GradeManager.AddAsync`

**Problema:** quando si crea un voto, il codice verifica che la materia (`SubjectId`) esista, ma non che lo studente (`StudentId`) esista. Un `POST /api/grade` con uno `StudentId` casuale crea comunque il voto nel database, generando un record "orfano".

**Step:**
1. Apri `src/Application/ElectronicRegisterAPI.Application/Managers/GradeManager.cs`, metodo `AddAsync`.
2. Dopo il controllo `if (subject is null) return null;`, aggiungi:
   ```csharp
   var student = await _studentRepository.GetByIdAsync(dto.StudentId);
   if (student is null) return null;
   ```
3. Verifica che il Controller (`GradeController.Add`) traduca correttamente il `null` restituito in `NotFound()` (lo fa già: `return id is null ? NotFound() : CreatedAtAction(...)`).

### Miglioria 2 — Centralizzare la traduzione ruolo↔stringa oggi duplicata in 3 punti

**Problema:** la conversione `UserRole ↔ "admin"/"teacher"/"student"` è scritta a mano e duplicata identica in `UserRepository.MapToModel`, `UserRepository.MapToEntity` e `UserRepository.UpdateAsync`. Se in futuro aggiungerai un ruolo, rischi di aggiornarne solo 2 istanze su 3.

**Step:**
1. In `src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/`, crea un file `RoleMapper.cs`:
   ```csharp
   using ElectronicRegisterAPI.Domain.Enums;

   namespace ElectronicRegisterAPI.Infrastructure.Repositories;

   internal static class RoleMapper
   {
       public static string ToDbString(UserRole role) => role switch
       {
           UserRole.Admin => "admin",
           UserRole.Teacher => "teacher",
           UserRole.Student => "student",
           _ => throw new ArgumentOutOfRangeException(nameof(role), $"Ruolo non valido: {role}")
       };

       public static UserRole FromDbString(string role) => role.ToLowerInvariant() switch
       {
           "admin" => UserRole.Admin,
           "teacher" => UserRole.Teacher,
           "student" => UserRole.Student,
           _ => throw new ArgumentOutOfRangeException(nameof(role), $"Ruolo non valido: {role}")
       };
   }
   ```
2. Sostituisci le 3 occorrenze in `UserRepository.cs` con `RoleMapper.ToDbString(...)` / `RoleMapper.FromDbString(...)`.

### Miglioria 3 — Estrarre le "magic string" dei claim JWT (`"studentId"`, `"teacherId"`) in costanti condivise

**Problema:** le stringhe `"studentId"` e `"teacherId"` sono scritte a mano sia in `JwtTokenGenerator.Generate` (dove il claim viene creato) sia in `ApiControllerBase.CurrentCaller()` (dove viene letto). Un refuso in uno dei due punti romperebbe silenziosamente l'autenticazione.

**Step:**
1. In `src/Domain/ElectronicRegisterAPI.Domain/`, crea `Constants/CustomClaimTypes.cs`:
   ```csharp
   namespace ElectronicRegisterAPI.Domain.Constants;

   public static class CustomClaimTypes
   {
       public const string StudentId = "studentId";
       public const string TeacherId = "teacherId";
   }
   ```
2. In `Infrastructure/Security/JwtTokenGenerator.cs`, sostituisci le stringhe letterali con `CustomClaimTypes.StudentId` / `CustomClaimTypes.TeacherId`.
3. In `Api/Controllers/ApiControllerBase.cs`, fai lo stesso nei due `User.FindFirst(...)`.

### Miglioria 4 — Rimuovere i segreti reali da `appsettings.json` (vedi §5.2)

**Step:**
1. Da terminale, nella cartella `src/Api/ElectronicRegisterAPI`: `dotnet user-secrets init`.
2. Copia i valori reali di `Jwt:Key`, `AzureAd:TenantId`, `AzureAd:ClientId`, `AzureAd:Audience`, `ConnectionStrings:DefaultConnection` con `dotnet user-secrets set "Jwt:Key" "..."` (uno per valore).
3. Sostituisci in `appsettings.json` i valori reali con placeholder tipo `"CHANGE-ME"` (come già fa `appsettings.example.json`).
4. Aggiungi `appsettings.json` al `.gitignore` **solo se** il repository non deve mai contenerlo neanche come placeholder versionato; altrimenti lascialo con i placeholder e usa User Secrets/variabili d'ambiente in ogni ambiente (dev/staging/prod).

### Miglioria 5 — Preparare `InternalsVisibleTo` prima di creare i progetti di test

**Step (da fare quando affronti lo Step 13):**
1. Apri `Infrastructure.csproj`, `Business.csproj`, `Application.csproj`.
2. In ciascuno aggiungi:
   ```xml
   <ItemGroup>
     <InternalsVisibleTo Include="ElectronicRegisterAPI.Infrastructure.Tests" />
     <InternalsVisibleTo Include="ElectronicRegisterAPI.Business.Tests" />
     <InternalsVisibleTo Include="ElectronicRegisterAPI.Application.Tests" />
   </ItemGroup>
   ```
   (puoi includere anche solo il progetto di test pertinente a ciascuna libreria, se preferisci restare stretti).
3. Crea i progetti xUnit come da §13 della guida.

---

## 7️⃣ Checklist finale in ordine di priorità

1. [X] **Bug 3.1** — `TeacherManager.UpdateAsync`: applicare `dto.FirstName`/`dto.LastName` prima del salvataggio (blocca la funzionalità "modifica docente")
2. [X] **Bug 3.2** — `AuthManager.RegisterAsync`: richiamare `EnsureSelfRegistrationEmailFormat` (regressione di validazione)
3. [X] **Bug 3.3** — `GradeController.Delete`: usare l'esito di `DeleteAsync` per `NotFound()`/`NoContent()`
4. [X] **Bug 3.4** — `GradeManager.GetGradesByDateAsync`: allineare a `GetGradesBySubjectNameAsync` (`return new List<GradeDto>();`)
5. [ ] **§5.1** — Aggiungere i `namespace` mancanti nei 5 file elencati (non blocca la build, ma va sistemato per coerenza prima dei test)
6. [X] **§5.2** — Spostare i segreti reali fuori da `appsettings.json`
7. [X] **§5.3** — Valutare la rimozione della cartella del progetto monolitico originale
8. [X] Migliorie 1-3 (facoltative, robustezza)
9. [ ] Migliorie 5 + Step 13-14 della guida (test, compilazione finale) — quando deciderai di affrontarli
