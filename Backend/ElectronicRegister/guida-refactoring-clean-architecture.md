# Guida passo-passo: refactoring di ElectronicRegisterAPI

> Basata sui principi di `dritte di programmazione.md`, applicata al progetto `ElectronicRegisterAPI` (ASP.NET Core, .NET 10, EF Core + MySQL), con istruzioni specifiche per **Visual Studio Community 2026**.

Il progetto oggi è un unico progetto (`ElectronicRegisterAPI`) dove Controller, DTO, modelli EF e `DbContext` convivono nello stesso assembly. L'obiettivo è arrivare a 4 librerie (Domain, Infrastructure, Business, Application) + un progetto host (Api) + progetti di test, con Controller "sottili" che iniettano solo Manager.

---

## Indice

0. [Prima di iniziare](#0-prima-di-iniziare)
1. [Preparare la struttura di cartelle](#1-preparare-la-struttura-di-cartelle)
2. [Creare i nuovi progetti in Visual Studio 2026](#2-creare-i-nuovi-progetti-in-visual-studio-2026)
3. [Organizzare la Solution Explorer (cartelle src/test)](#3-organizzare-la-solution-explorer-cartelle-srctest)
4. [Impostare i riferimenti tra progetti](#4-impostare-i-riferimenti-tra-progetti)
5. [Spostare i pacchetti NuGet nel progetto giusto](#5-spostare-i-pacchetti-nuget-nel-progetto-giusto)
6. [Livello Domain](#6-livello-domain)
7. [Livello Infrastructure](#7-livello-infrastructure)
8. [Livello Business](#8-livello-business)
9. [Livello Application](#9-livello-application)
10. [Progetto Api: riscrivere i Controller](#10-progetto-api-riscrivere-i-controller)
11. [Program.cs finale](#11-programcs-finale)
12. [Rendere le classi internal](#12-rendere-le-classi-internal)
13. [Progetti di test](#13-progetti-di-test)
14. [Compilare e verificare](#14-compilare-e-verificare)
15. [Checklist finale di conformità](#15-checklist-finale-di-conformità)

---

## 0. Prima di iniziare

1. **Fai un commit/branch dedicato** prima di iniziare: questo è un refactoring strutturale profondo, non un semplice bugfix.
   ```bash
   git checkout -b refactor/clean-architecture
   git add -A && git commit -m "checkpoint prima del refactoring"
   ```
2. Tieni aperto il file `dritte di programmazione.md` in una scheda: ogni step qui sotto richiama esplicitamente la regola a cui risponde.
3. Verifica in Visual Studio (**Strumenti → Opzioni → Progetti e soluzioni → Generale**) che il formato di default sia `.slnx` (il progetto attuale lo usa già: `ElectronicRegisterAPI.slnx`), così i nuovi progetti che aggiungerai finiranno nello stesso file XML senza conversioni.

---

## 1. Preparare la struttura di cartelle

Chiudi Visual Studio (o tienilo aperto ma non toccare la solution per ora) e, da **Esplora File** o dal terminale, crea manualmente questo scheletro accanto alla cartella `Backend` esistente:

```
ElectronicRegister/                      ← nuova cartella root
├── ElectronicRegisterAPI.slnx           ← la sposteremo qui dal progetto attuale
├── src/
│   ├── Domain/
│   ├── Infrastructure/
│   ├── Business/
│   ├── Application/
│   └── Api/                             ← qui finirà il progetto host attuale
└── test/
```

Questo rispecchia esattamente l'indentatura richiesta dal file `.md`:

```
root
    solution
    src
        Application
        Business
        Infrastructure
        Domain
    test
```

> Nota pratica: creare le cartelle **prima** di aprire Visual Studio evita che il wizard "Nuovo progetto" ti proponga percorsi sbagliati.

---

## 2. Creare i nuovi progetti in Visual Studio 2026

Apri `ElectronicRegisterAPI.slnx` in Visual Studio.

### 2.1 Crea le 4 librerie di classi

Ripeti questi passaggi **4 volte**, una per `Domain`, `Infrastructure`, `Business`, `Application`:

1. In **Esplora soluzioni**, tasto destro sulla soluzione → **Aggiungi → Nuovo progetto...**
2. Cerca il template **"Libreria di classi"** (C#) → **Avanti**.
3. Nome progetto: `ElectronicRegisterAPI.Domain` (poi `.Infrastructure`, `.Business`, `.Application`).
4. Come percorso, seleziona la cartella `src/Domain` (rispettivamente `src/Infrastructure`, ecc.) creata al passo 1. **Deseleziona** "Inserisci soluzione e progetto nella stessa directory" se richiesto, dato che vuoi che il progetto vada dentro `src/Domain`.
5. Framework: **.NET 10.0** (stesso target framework del progetto host, per evitare incompatibilità di riferimento).
6. Crea. Ripeti per gli altri 3.
7. Per ciascun progetto appena creato, elimina il file di default `Class1.cs`.

### 2.2 Sposta il progetto host esistente dentro `src/Api`

Il progetto `ElectronicRegisterAPI` (quello con Controller/Program.cs) va spostato fisicamente in `src/Api`:

1. Chiudi la soluzione in Visual Studio.
2. Da Esplora File, sposta la cartella `Backend/ElectronicRegisterAPI` dentro `ElectronicRegister/src/Api/`.
3. Sposta anche `ElectronicRegisterAPI.slnx` dentro la nuova cartella root `ElectronicRegister/`.
4. Riapri `ElectronicRegisterAPI.slnx` con un editor di testo e correggi il path del progetto host, che ora sarà relativo:
   ```xml
   <Project Path="src/Api/ElectronicRegisterAPI.csproj" />
   ```
5. Riapri la solution in Visual Studio: dovrebbe ricaricare correttamente tutti e 5 i progetti.

> In alternativa, se preferisci non maneggiare l'XML a mano: in Visual Studio tasto destro sul progetto host → **Rimuovi** (solo dalla solution, non elimina i file) → sposta manualmente la cartella da Esplora File → tasto destro sulla solution → **Aggiungi → Progetto esistente...** e ripunta al nuovo percorso del `.csproj`.

---

## 3. Organizzare la Solution Explorer (cartelle src/test)

Per rispecchiare visivamente la struttura anche dentro Visual Studio (oltre che su disco):

1. Tasto destro sulla soluzione → **Aggiungi → Nuova cartella di soluzione** → chiamala `src`.
2. Trascina (drag & drop) i 5 progetti (`Domain`, `Infrastructure`, `Business`, `Application`, `Api`) dentro la cartella di soluzione `src`.
3. Crea un'altra cartella di soluzione `test` (la useremo allo step 13).

> Le "cartelle di soluzione" sono virtuali (vivono solo nel `.slnx`); dato che hai già creato le cartelle reali su disco allo step 1-2, qui stai solo allineando la vista in Visual Studio.

---

## 4. Impostare i riferimenti tra progetti

Regola generale (dalla direzione delle frecce dipende chi può vedere le interfacce di chi):

```
Domain  ←  Infrastructure
Domain  ←  Business
Domain, Business  ←  Application
Domain, Infrastructure, Business, Application  ←  Api (composition root)
```

`Domain` **non deve avere alcun riferimento** in uscita. `Business` **non deve mai referenziare `Infrastructure`**.

Per ciascun progetto, in Visual Studio:

1. Tasto destro sul progetto (es. `Infrastructure`) → **Aggiungi → Riferimento progetto...**
2. Spunta i progetti da referenziare (es. per `Infrastructure` → spunta solo `Domain`).
3. OK.

Applica questa tabella:

| Progetto | Riferimenti da aggiungere |
|---|---|
| `Domain` | nessuno |
| `Infrastructure` | `Domain` |
| `Business` | `Domain` |
| `Application` | `Domain`, `Business` |
| `Api` | `Domain`, `Infrastructure`, `Business`, `Application` |

---

## 5. Spostare i pacchetti NuGet nel progetto giusto

Oggi tutti i pacchetti sono nel `.csproj` dell'unico progetto host. Vanno redistribuiti secondo chi li usa davvero:

| Pacchetto NuGet | Dove va installato | Perché |
|---|---|---|
| `BCrypt.Net-Next` | `Infrastructure` | Usato solo dall'implementazione concreta `IPasswordHasher` |
| `Microsoft.EntityFrameworkCore` | `Infrastructure` | `DbContext` ed entità |
| `Microsoft.EntityFrameworkCore.Tools` | `Infrastructure` | Migrazioni EF Core |
| `Pomelo.EntityFrameworkCore.MySql` | `Infrastructure` | Provider MySQL |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `Infrastructure` **e** `Api` | In `Infrastructure` serve per generare/validare i token (`SecurityTokenHandler`, ecc.); in `Api` serve per `AddAuthentication().AddJwtBearer(...)` nel `Program.cs` |
| `Microsoft.IdentityModel.Protocols` / `Protocols.OpenIdConnect` | `Infrastructure` | Usati dal validatore del token Microsoft |
| `Microsoft.Extensions.Options` | `Infrastructure` (di solito già transitivo con `Microsoft.Extensions.DependencyInjection.Abstractions`) | Pattern `IOptions<T>` |
| `Microsoft.Identity.Web` | Verifica se è ancora usato | Nel codice attuale l'autenticazione Microsoft è validata "a mano" con `ConfigurationManager<OpenIdConnectConfiguration>`, non con questa libreria: se non ci sono using a `Microsoft.Identity.Web` altrove, **rimuovilo** per non lasciare dipendenze morte |
| `Scalar.AspNetCore` | resta in `Api` | Serve solo per la UI di documentazione, è un dettaglio dell'host |
| `Microsoft.AspNetCore.OpenApi` | resta in `Api` | Idem |

Come fare in Visual Studio 2026:

1. Tasto destro sul progetto host `Api` → **Gestisci pacchetti NuGet...**
2. Tab **Installati**: seleziona il pacchetto da spostare (es. `BCrypt.Net-Next`) → **Disinstalla**.
3. Tasto destro sul progetto `Infrastructure` → **Gestisci pacchetti NuGet...** → tab **Sfoglia** → cerca lo stesso pacchetto, stessa versione → **Installa**.
4. Ripeti per ogni riga della tabella.

---

## 6. Livello Domain

*Risponde a: "Vanno creati almeno 4 livelli, uno di dominio con modelli DTO e interfacce dei servizi" + "i modelli devono avere ognuno la sua classe, lo stesso vale per gli enum".*

### 6.1 Sposta i DTO

1. In `ElectronicRegisterAPI.Domain`, crea la cartella `DTOs`.
2. Da Esplora soluzioni, **trascina** (drag & drop) tutti i file da `Api/DTOs/*.cs` dentro `Domain/DTOs/` (Visual Studio chiede se vuoi spostare i file fisicamente: conferma).
3. Apri **Trova e sostituisci nei file** (`Ctrl+Shift+H`) e sostituisci, in tutta la soluzione:
   - `namespace ElectronicRegisterAPI.DTOs` → `namespace ElectronicRegisterAPI.Domain.DTOs`
   - `using ElectronicRegisterAPI.DTOs;` → `using ElectronicRegisterAPI.Domain.DTOs;`

I DTO restano `public` (devono essere visibili da `Business`, `Application` e `Api`).

### 6.2 Crea l'enum dei ruoli

Oggi il ruolo utente è una `string` libera (`"admin"`, `"teacher"`, `"student"`) sparsa in tutti i controller. Crea `Domain/Enums/UserRole.cs`:

```csharp
namespace ElectronicRegisterAPI.Domain.Enums;

public enum UserRole
{
    Admin,
    Teacher,
    Student
}
```

> Nota: negli attributi `[Authorize(Roles = "...")]` di ASP.NET Core resterai comunque legato alla stringa (è un vincolo del framework, gli attributi non accettano enum), ma **dentro** Manager e Service userai sempre `UserRole`, mai la stringa nuda.

### 6.3 Crea le interfacce dei Repository

Cartella `Domain/Interfaces/Repositories/`, un file per interfaccia:

```csharp
// IGradeRepository.cs
namespace ElectronicRegisterAPI.Domain.Interfaces.Repositories;

public interface IGradeRepository
{
    Task<Grade?> GetByIdAsync(Guid id);
    Task<List<Grade>> GetAllAsync();
    Task<int> CountAsync(Guid? teacherId = null);
    Task AddAsync(Grade grade);
    Task UpdateAsync(Grade grade);
    Task DeleteAsync(Grade grade);
    // ... altri metodi di query usati oggi nel GradeController
}
```

Ripeti lo stesso pattern per `IStudentRepository`, `ISubjectRepository`, `ITeacherRepository`, `IUserRepository`.

> `Grade`, `Student`, ecc. qui sono i **modelli di dominio** (POCO semplici, senza attributi EF Core), da non confondere con le entità EF che vivranno in `Infrastructure` (vedi step 7.2). Se preferisci evitare una doppia mappatura modello-dominio/entità-EF per un progetto di queste dimensioni, un compromesso pragmatico accettabile è far esporre alle interfacce dei Repository direttamente i DTO di `Domain/DTOs` invece di un modello di dominio separato — ma **mai** l'entità EF concreta.

### 6.4 Crea le interfacce dei servizi di sicurezza

Cartella `Domain/Interfaces/Security/`:

```csharp
// IPasswordHasher.cs
namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
```

```csharp
// IJwtTokenGenerator.cs
namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IJwtTokenGenerator
{
    string Generate(Guid userId, string email, Enums.UserRole role, Guid? studentId, Guid? teacherId);
}
```

```csharp
// IMicrosoftTokenValidator.cs
using System.Security.Claims;

namespace ElectronicRegisterAPI.Domain.Interfaces.Security;

public interface IMicrosoftTokenValidator
{
    Task<ClaimsPrincipal> ValidateAsync(string accessToken);
}
```

### 6.5 Crea le interfacce dei servizi di Business

Cartella `Domain/Interfaces/Services/`: `IGradeService`, `IStudentService`, `ISubjectService`, `ITeacherService`, `IUserService`, `IAuthService`. Esempio:

```csharp
// IGradeService.cs
namespace ElectronicRegisterAPI.Domain.Interfaces.Services;

public interface IGradeService
{
    void EnsureValidGradeValue(decimal value);
    Task EnsureTeacherTeachesSubjectAsync(Guid teacherId, Guid subjectId);
}
```

### 6.6 Crea le interfacce dei Manager

Cartella `Domain/Interfaces/Managers/`: `IAuthManager`, `IGradeManager`, `IStudentManager`, `ISubjectManager`, `ITeacherManager`, `IUserManager`. Esempio:

```csharp
// IGradeManager.cs
using ElectronicRegisterAPI.Domain.DTOs;

namespace ElectronicRegisterAPI.Domain.Interfaces.Managers;

public interface IGradeManager
{
    Task<List<GradeDto>> GetAllAsync(ClaimsContext caller);
    Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller);
    Task<GradePageDto> GetPagedAsync(int pageNumber, int pageSize, Guid? subjectId, Guid? studentId, DateOnly? date, ClaimsContext caller);
    Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller);
    Task<GradeFiltersDto> GetFiltersAsync(ClaimsContext caller);
    Task<Guid?> AddAsync(AddGradeDto dto, ClaimsContext caller);
    Task<bool> UpdateAsync(Guid id, UpdateGradeDto dto, ClaimsContext caller);
    Task<bool> DeleteAsync(Guid id);
}
```

Dove `ClaimsContext` è un piccolo record che il Controller costruisce leggendo `User.Claims` (ruolo, `studentId`, `teacherId`) e passa al Manager — così il Manager non dipende da `HttpContext`/`ClaimsPrincipal` (dettaglio HTTP), ma riceve solo dati semplici:

```csharp
// ClaimsContext.cs (in Domain/DTOs o Domain/Models)
namespace ElectronicRegisterAPI.Domain.DTOs;

public record ClaimsContext(Domain.Enums.UserRole Role, Guid? StudentId, Guid? TeacherId);
```

Ripeti la stessa logica di interfaccia per gli altri Manager, rispecchiando **una a una** le action già presenti nei rispettivi controller attuali.

---

## 7. Livello Infrastructure

*Risponde a: "tutto quello che è specifico e strettamente legato all'implementazione dell'infrastruttura va messo dentro il livello infrastrutturale" + "un dominio di infrastruttura con modelli legati all'infrastruttura (es. modelli di entità del DB)".*

### 7.1 Sposta le entità EF e il DbContext

1. Crea `Infrastructure/Persistence/Entities/`.
2. Trascina `Grade.cs`, `Student.cs`, `Subject.cs`, `Teacher.cs`, `User.cs` da `Api/Models/` dentro questa cartella.
3. Trascina `ElectronicRegisterContext.cs` in `Infrastructure/Persistence/`.
4. Aggiorna i namespace (`Ctrl+Shift+H`):
   - `namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities` → `namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities` (per le 5 entità)
   - Il `DbContext` va nel namespace `ElectronicRegisterAPI.Infrastructure.Persistence`
5. Rendi tutte e 5 le classi entità e la classe `ElectronicRegisterContext` **`internal`** invece di `public partial`:
   ```csharp
   internal partial class Grade { ... }
   internal partial class ElectronicRegisterContext : DbContext { ... }
   ```

### 7.2 Crea le classi di opzioni (per `IOptions<T>`)

Cartella `Infrastructure/Options/` (queste restano **`public`**: `Program.cs` nell'host deve poterle vedere per chiamare `services.Configure<JwtOptions>(...)`):

```csharp
// JwtOptions.cs
namespace ElectronicRegisterAPI.Infrastructure.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public double ExpiresInMinutes { get; set; }
}
```

```csharp
// AzureAdOptions.cs
namespace ElectronicRegisterAPI.Infrastructure.Options;

public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    public string Instance { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}
```

### 7.3 Crea i servizi tecnici (Security)

Cartella `Infrastructure/Security/`, tutte classi **`internal`**:

```csharp
// BcryptPasswordHasher.cs
using ElectronicRegisterAPI.Domain.Interfaces.Security;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
```

```csharp
// JwtTokenGenerator.cs
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ElectronicRegisterAPI.Domain.Enums;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string Generate(Guid userId, string email, UserRole role, Guid? studentId, Guid? teacherId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role.ToString().ToLowerInvariant()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (studentId.HasValue) claims.Add(new Claim("studentId", studentId.Value.ToString()));
        if (teacherId.HasValue) claims.Add(new Claim("teacherId", teacherId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiresInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

```csharp
// MicrosoftTokenValidator.cs
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;

namespace ElectronicRegisterAPI.Infrastructure.Security;

internal class MicrosoftTokenValidator : IMicrosoftTokenValidator
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly AzureAdOptions _options;

    public MicrosoftTokenValidator(
        ConfigurationManager<OpenIdConnectConfiguration> configManager,
        IOptions<AzureAdOptions> options)
    {
        _configManager = configManager;
        _options = options.Value;
    }

    public async Task<ClaimsPrincipal> ValidateAsync(string accessToken)
    {
        var config = await _configManager.GetConfigurationAsync();

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            IssuerSigningKeys = config.SigningKeys
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(accessToken, validationParameters, out _);
    }
}
```

### 7.4 Crea i Repository

Cartella `Infrastructure/Repositories/`, tutte classi **`internal`**, ognuna implementa la corrispondente interfaccia di `Domain.Interfaces.Repositories` usando `ElectronicRegisterContext`. Sposta qui **tutte** le query LINQ oggi scritte dentro i controller (`_context.Grades...`, `_context.Students...`, ecc.), incluse le `Include` per evitare le query N+1 che oggi i controller fanno con `FirstOrDefault` ripetuti.

### 7.5 Registrazione DI: `AddInfrastructure`

Cartella `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Security;
using ElectronicRegisterAPI.Infrastructure.Options;
using ElectronicRegisterAPI.Infrastructure.Persistence;
using ElectronicRegisterAPI.Infrastructure.Repositories;
using ElectronicRegisterAPI.Infrastructure.Security;

namespace ElectronicRegisterAPI.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ElectronicRegisterContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AzureAdOptions>(configuration.GetSection(AzureAdOptions.SectionName));

        var azureTenantId = configuration["AzureAd:TenantId"];
        var stsDiscoveryEndpoint = $"https://login.microsoftonline.com/{azureTenantId}/v2.0/.well-known/openid-configuration";
        services.AddSingleton(new ConfigurationManager<OpenIdConnectConfiguration>(
            stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever()));

        services.AddScoped<IGradeRepository, GradeRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ISubjectRepository, SubjectRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IMicrosoftTokenValidator, MicrosoftTokenValidator>();

        return services;
    }
}
```

> Questo è l'**unico punto**, oltre a `Program.cs`, in cui compare `IConfiguration`: qui viene "distillata" subito in `JwtOptions`/`AzureAdOptions` tramite `Configure<T>`. Nessun Repository, Service o Manager vedrà mai `IConfiguration`.

---

## 8. Livello Business

*Risponde a: la logica applicativa/di business va isolata dal controller e non deve dipendere dai dettagli infrastrutturali.*

### 8.1 Crea i Service

Cartella `Business/Services/`, classi **`internal`**. Qui sposti tutte le regole "di dominio" oggi scritte nei controller. Esempio per i voti:

```csharp
// GradeService.cs
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;

namespace ElectronicRegisterAPI.Business.Services;

internal class GradeService : IGradeService
{
    private readonly ISubjectRepository _subjectRepository;

    public GradeService(ISubjectRepository subjectRepository)
    {
        _subjectRepository = subjectRepository;
    }

    public void EnsureValidGradeValue(decimal value)
    {
        if (value < 1 || value > 10)
            throw new ArgumentOutOfRangeException(nameof(value), "Il valore del voto deve essere compreso tra 1 e 10.");
    }

    public async Task EnsureTeacherTeachesSubjectAsync(Guid teacherId, Guid subjectId)
    {
        var subject = await _subjectRepository.GetByIdAsync(subjectId);
        if (subject is null || subject.TeacherId != teacherId)
            throw new UnauthorizedAccessException("Il docente non insegna questa materia.");
    }
}
```

Ripeti per `AuthService` (validazione email `allievo_...@itsumbria.it`, regole password con `HasSpecialChar`/lunghezza, regole nome/cognome), `StudentService`/`SubjectService`/`TeacherService` (regole di cancellazione: "non eliminare se esistono voti/materie collegate"), `UserService` (regole di update/password).

> Nota importante: in queste eccezioni usa tipi standard (`ArgumentOutOfRangeException`,z `UnauthorizedAccessException`, oppure una tua eccezione custom `BusinessRuleException`) — sarà compito del Manager (Application) tradurle in `BadRequest`/`Forbid`/`NotFound` HTTP, perché il livello Business non deve conoscere ASP.NET Core.

### 8.2 Registrazione DI: `AddBusinessServices`

```csharp
using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Business.Services;

namespace ElectronicRegisterAPI.Business.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
```

---

## 9. Livello Application

*Risponde a: "togliere la logica dai controller, nei controller ci va una riga di codice, il manager che chiama tutto il flusso".*

### 9.1 Crea i Manager

Cartella `Application/Managers/`, classi **`internal`**. Ogni Manager orchestra Repository (via interfacce) + Service, e restituisce DTO pronti. Esempio (`GradeManager`, versione ridotta del metodo `Add`):

```csharp
// GradeManager.cs (estratto)
internal class GradeManager : IGradeManager
{
    private readonly IGradeRepository _gradeRepository;
    private readonly ISubjectRepository _subjectRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IGradeService _gradeService;

    public GradeManager(
        IGradeRepository gradeRepository,
        ISubjectRepository subjectRepository,
        ITeacherRepository teacherRepository,
        IGradeService gradeService)
    {
        _gradeRepository = gradeRepository;
        _subjectRepository = subjectRepository;
        _teacherRepository = teacherRepository;
        _gradeService = gradeService;
    }

    public async Task<Guid?> AddAsync(AddGradeDto dto, ClaimsContext caller)
    {
        _gradeService.EnsureValidGradeValue(dto.Value);

        if (caller.Role == UserRole.Teacher)
            await _gradeService.EnsureTeacherTeachesSubjectAsync(caller.TeacherId!.Value, dto.SubjectId);

        var subject = await _subjectRepository.GetByIdAsync(dto.SubjectId);
        if (subject is null) return null;

        var grade = new Grade { /* ... */ };
        await _gradeRepository.AddAsync(grade);
        return grade.Id;
    }
}
```

Ripeti lo stesso schema per ogni metodo delle interfacce `I*Manager` definite allo step 6.6, replicando **una a una** le regole già presenti nei controller attuali (le hai già mappate nell'analisi precedente: `Count`, `GetStatistics`, `GetFilters`, `GetPaged`, `GetAll`, `GetById`, `GetGradesByStudentId`, `GetGradesBySubjectName`, `GetGradesByDate`, `Update`, `Add`, `Delete` per `GradeManager`; analoghe per gli altri).

### 9.2 Registrazione DI: `AddApplicationManagers`

```csharp
using Microsoft.Extensions.DependencyInjection;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Application.Managers;

namespace ElectronicRegisterAPI.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationManagers(this IServiceCollection services)
    {
        services.AddScoped<IAuthManager, AuthManager>();
        services.AddScoped<IGradeManager, GradeManager>();
        services.AddScoped<IStudentManager, StudentManager>();
        services.AddScoped<ISubjectManager, SubjectManager>();
        services.AddScoped<ITeacherManager, TeacherManager>();
        services.AddScoped<IUserManager, UserManager>();

        return services;
    }
}
```

---

## 10. Progetto Api: riscrivere i Controller

*Risponde a: "nei controller vengono iniettati i manager e basta".*

Per ciascun controller esistente:

1. Sostituisci l'iniezione di `ElectronicRegisterContext` (e, per `AuthController`, anche `IConfiguration` e `ConfigurationManager<OpenIdConnectConfiguration>`) con **solo** l'interfaccia del Manager corrispondente.
2. Ogni action diventa: costruisci il `ClaimsContext` dai claims dell'utente (se serve), chiama il Manager, traduci l'esito in `ActionResult`.

Esempio completo per un endpoint di `GradeController`:

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GradeController : ControllerBase
{
    private readonly IGradeManager _gradeManager;

    public GradeController(IGradeManager gradeManager)
    {
        _gradeManager = gradeManager;
    }

    private ClaimsContext CurrentCaller() => new(
        Role: Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value, ignoreCase: true),
        StudentId: Guid.TryParse(User.FindFirst("studentId")?.Value, out var sId) ? sId : null,
        TeacherId: Guid.TryParse(User.FindFirst("teacherId")?.Value, out var tId) ? tId : null);

    [HttpPost]
    [Authorize(Roles = "teacher,admin")]
    public async Task<ActionResult> Add(AddGradeDto dto)
    {
        var id = await _gradeManager.AddAsync(dto, CurrentCaller());
        return id is null ? NotFound() : CreatedAtAction(nameof(GetById), new { id }, id);
    }
}
```

Nota: `CurrentCaller()` è l'unica logica "residua" nel controller, ma è un dettaglio di **estrazione claims HTTP**, non business logic — resta legittimo che stia qui perché non fa nessuna decisione, solo mapping dati.

Ripeti per `AuthController`, `StudentController`, `SubjectController`, `TeacherController`, `UsersController`.

> Mentre riscrivi, elimina anche il codice morto individuato nell'analisi: il campo `private static List<Grade> _grades` in `GradeController` e `private static List<Subject> _subjects` in `SubjectController` non sono mai usati.

---

## 11. Program.cs finale

```csharp
using Scalar.AspNetCore;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Business.DependencyInjection;
using ElectronicRegisterAPI.Application.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBusinessServices();
builder.Services.AddApplicationManagers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run("http://0.0.0.0:5000");
```

> `builder.Configuration["Jwt:..."]` resta qui **solo** perché serve a configurare il middleware `AddJwtBearer` di ASP.NET Core stesso (che richiede questi valori direttamente, non tramite DI) — è l'unico altro punto legittimo, oltre ad `AddInfrastructure`, dove tocchi `IConfiguration` grezza.

---

## 12. Rendere le classi internal

*Risponde a: "le classi devono essere tutte internal".*

Per ogni classe **concreta** (Repository, Service, Manager, `ElectronicRegisterContext`, entità EF, classi di Security) aggiungi/verifica il modificatore `internal`. Restano `public`:
- le interfacce in `Domain`
- i DTO e l'enum in `Domain`
- le classi `Options` in `Infrastructure` (devono essere viste da `Program.cs`)
- i metodi di estensione `Add...` in ogni `ServiceCollectionExtensions`

Per poter comunque testare le classi `internal` dai progetti di test (step 13), apri il `.csproj` di `Infrastructure`, `Business` e `Application` (doppio click sul nome del progetto in Esplora soluzioni apre direttamente l'XML in Visual Studio 2026) e aggiungi:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="ElectronicRegisterAPI.Infrastructure.Tests" />
</ItemGroup>
```

(sostituendo il nome del progetto di test corrispondente per `Business` e `Application`).

---

## 13. Progetti di test

1. Tasto destro sulla cartella di soluzione `test` (creata allo step 3) → **Aggiungi → Nuovo progetto...**
2. Cerca template **"Progetto di test xUnit"** (C#).
3. Nome: `ElectronicRegisterAPI.Business.Tests`, percorso: cartella `test/`.
4. Ripeti per `ElectronicRegisterAPI.Application.Tests` e, se vuoi testare i repository con un DB in-memory, `ElectronicRegisterAPI.Infrastructure.Tests`.
5. Per ciascun progetto di test, aggiungi il riferimento al progetto che testa (**Aggiungi → Riferimento progetto**).
6. Installa `NSubstitute` o `Moq` via **Gestisci pacchetti NuGet** per mockare le interfacce `I*Repository`/`I*Service` quando testi Manager e Service in isolamento.

---

## 14. Compilare e verificare

1. `Ctrl+Shift+B` per compilare l'intera soluzione: risolvi gli eventuali `using` mancanti o namespace non aggiornati (l'operazione **Trova e sostituisci nei file** allo step 6.1 va ripetuta con attenzione per ogni namespace spostato: `Models` → `Infrastructure.Persistence.Entities`, `DTOs` → `Domain.DTOs`).
2. Verifica che **Entity Framework Tools** funzioni ancora dal progetto giusto: se generi nuove migrazioni, il comando va lanciato specificando il progetto di startup e il progetto contenente il `DbContext`:
   ```
   dotnet ef migrations add NomeMigrazione --project src/Infrastructure --startup-project src/Api
   ```
3. Avvia con `F5` e testa gli endpoint via Scalar (`/scalar` in ambiente Development) per verificare che login, generazione JWT e le CRUD rispondano come prima del refactoring.

---

## 15. Checklist finale di conformità

Spunta ogni riga confrontandola col file `dritte di programmazione.md`:

- [ ] I Controller contengono al massimo una riga di orchestrazione HTTP + chiamata al Manager
- [ ] Nei Controller è iniettato solo il Manager (nessun `DbContext`, nessun `IConfiguration`)
- [ ] `IConfiguration` compare **solo** in `Program.cs` e in `AddInfrastructure`
- [ ] La configurazione esce verso Manager/Service/Repository **solo** tramite `IOptions<JwtOptions>` / `IOptions<AzureAdOptions>`
- [ ] Tutto ciò che è specifico dell'infrastruttura (EF Core, BCrypt, JWT, validazione token Microsoft) è dentro il progetto `Infrastructure`
- [ ] Esistono i 4 livelli: `Domain` (DTO + interfacce), `Infrastructure` (entità DB + implementazioni), `Business` (logica), `Application` (Manager)
- [ ] Ogni registrazione DI usa interfaccia → concreta (`AddScoped<IX, X>()`), mai la concreta da sola
- [ ] La DI di ogni libreria è nel proprio metodo di estensione `IServiceCollection` (`AddInfrastructure`, `AddBusinessServices`, `AddApplicationManagers`)
- [ ] Tutte le classi concrete sono `internal` (tranne interfacce, DTO, enum, Options, metodi di estensione DI)
- [ ] Ogni modello ed enum ha il proprio file
- [ ] Il lifecycle di ogni servizio registrato è `Scoped` (tranne il `ConfigurationManager<OpenIdConnectConfiguration>`, `Singleton` per motivi di caching interno della libreria Microsoft)
- [ ] La struttura di cartelle rispecchia `root/solution, src/{Application,Business,Infrastructure,Domain}, test`

---

### Riferimento rapido: mapping file vecchi → nuovi

| File/logica oggi | Destinazione |
|---|---|
| `DTOs/*.cs` | `Domain/DTOs/*.cs` |
| `Models/Grade.cs`, `Student.cs`, `Subject.cs`, `Teacher.cs`, `User.cs` | `Infrastructure/Persistence/Entities/*.cs` |
| `Models/ElectronicRegisterContext.cs` | `Infrastructure/Persistence/ElectronicRegisterContext.cs` |
| Query LINQ dentro i Controller | `Infrastructure/Repositories/*Repository.cs` |
| `GenerateJwtToken` in `AuthController` | `Infrastructure/Security/JwtTokenGenerator.cs` |
| `ValidateMicrosoftToken` in `AuthController` | `Infrastructure/Security/MicrosoftTokenValidator.cs` |
| `Bcrypt.HashPassword`/`Verify` | `Infrastructure/Security/BcryptPasswordHasher.cs` |
| Validazioni email/password/nome in `AuthController`/`UsersController` | `Business/Services/AuthService.cs`, `UserService.cs` |
| Regola voto 1-10, "docente insegna la materia" | `Business/Services/GradeService.cs` |
| Regole di blocco cancellazione (studente/materia/docente con dati collegati) | `Business/Services/StudentService.cs`, `SubjectService.cs`, `TeacherService.cs` |
| Corpo di ogni action dei Controller | `Application/Managers/*Manager.cs` |
| `_configuration["Jwt:..."]`, `["AzureAd:..."]` | `Infrastructure/Options/JwtOptions.cs`, `AzureAdOptions.cs` |
