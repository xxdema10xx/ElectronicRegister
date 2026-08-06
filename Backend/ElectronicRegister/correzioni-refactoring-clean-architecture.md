# Correzioni da apportare — refactoring ElectronicRegisterAPI (fino al punto 9.1)

> Analisi eseguita confrontando il codice in `Backend/ElectronicRegister/src` con `dritte-di-programmazione.md` e `guida-refactoring-clean-architecture.md`.
>
> **Verdetto generale: il lavoro è impostato bene.** Struttura cartelle, riferimenti tra progetti, pacchetti NuGet, DI per interfaccia, modificatori `internal`/`public`, mapping Domain↔Entity nel repository: tutto coerente con la guida. Ci sono però **2 problemi bloccanti** che oggi impediscono la compilazione della soluzione, più alcuni difetti minori da sistemare prima di proseguire con gli altri Manager.

---

## 🔴 Problema 1 (bloccante): `GradeManager.cs` senza `namespace`

**File:** `src/Application/ElectronicRegisterAPI.Application/Managers/GradeManager.cs`

Il file inizia così:

```csharp
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;

internal class GradeManager : IGradeManager
{
    ...
```

Manca la riga `namespace ElectronicRegisterAPI.Application.Managers;`: la classe finisce nel namespace globale. Al passo 9.2, quando scriverai `AddApplicationManagers` con `using ElectronicRegisterAPI.Application.Managers;` e `services.AddScoped<IGradeManager, GradeManager>();`, il compilatore **non troverà** `GradeManager` in quel namespace (dovrai scrivere `global::GradeManager`, cosa da evitare) e la build fallirà.

### Come correggere

Aggiungi il namespace subito dopo gli `using`, coerentemente con tutti gli altri livelli già creati:

```csharp
using ElectronicRegisterAPI.Domain.DTOs;
using ElectronicRegisterAPI.Domain.Interfaces.Managers;
using ElectronicRegisterAPI.Domain.Interfaces.Repositories;
using ElectronicRegisterAPI.Domain.Interfaces.Services;
using ElectronicRegisterAPI.Domain.Models;
using ElectronicRegisterAPI.Domain.Enums;

namespace ElectronicRegisterAPI.Application.Managers;

internal class GradeManager : IGradeManager
{
    ...
```

> Controlla questo dettaglio anche in tutti i prossimi Manager che scriverai (`StudentManager`, `SubjectManager`, `TeacherManager`, `UserManager`, `AuthManager`): è facile dimenticarlo se parti copiando l'`using` block da un altro file.

---

## 🔴 Problema 2 (bloccante): entità duplicate tra `Api/Models` e `Infrastructure`

**File coinvolti:**
```
src/Api/ElectronicRegisterAPI/Models/Grade.cs
src/Api/ElectronicRegisterAPI/Models/Student.cs
src/Api/ElectronicRegisterAPI/Models/Subject.cs
src/Api/ElectronicRegisterAPI/Models/Teacher.cs
src/Api/ElectronicRegisterAPI/Models/User.cs
src/Api/ElectronicRegisterAPI/Models/ElectronicRegisterContext.cs
```

Al passo 7.1 della guida, questi file andavano **spostati** (drag & drop, cioè rimossi dalla posizione originale) dentro `Infrastructure/Persistence/` e `Infrastructure/Persistence/Entities/`, aggiornando il namespace da `ElectronicRegisterAPI.Models` a `ElectronicRegisterAPI.Infrastructure.Persistence.Entities`.

Quello che è successo invece: i file sono stati **copiati** (i nuovi esistono correttamente in `Infrastructure`) ma i **vecchi non sono stati cancellati** — e per di più il loro namespace è stato modificato anch'esso in `ElectronicRegisterAPI.Infrastructure.Persistence.Entities`, identico a quello delle nuove entità.

Conseguenze concrete, verificabili subito:

1. **Tipi duplicati con lo stesso nome completo** (`ElectronicRegisterAPI.Infrastructure.Persistence.Entities.Grade`, ecc.) definiti sia nell'assembly `Api` sia nell'assembly `Infrastructure`, che `Api` referenzia — qualunque codice che scriva `using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;` e usi `Grade` genera un errore di ambiguità (CS0104).
2. I **Controller attuali, non ancora riscritti**, contengono ancora `using ElectronicRegisterAPI.Models;` (es. `GradeController.cs`, riga 2) — ma quel namespace **non esiste più da nessuna parte**, perché è stato rinominato anche nella copia vecchia. Il progetto `Api` **non compila, oggi**, indipendentemente dal fatto che tu non abbia ancora affrontato lo step 10.

### Come correggere

1. **Elimina fisicamente** l'intera cartella `src/Api/ElectronicRegisterAPI/Models/` (tutti e 6 i file). Le entità e il `DbContext` ora vivono solo in `Infrastructure/Persistence/`.
2. Poiché i Controller esistenti (non ancora migrati) usavano `ElectronicRegisterContext` direttamente, per ora **non compileranno comunque** finché non arrivi allo step 10 (riscrittura Controller) — ed è normale/atteso in questa fase intermedia. L'importante è che l'errore che oggi hai (ambiguità di tipo + namespace introvabile) sparisca, lasciando solo gli errori "attesi" di riferimento a `ElectronicRegisterContext`/`IConfiguration` nei vecchi Controller, che risolverai fisiologicamente quando li riscriverai iniettando i Manager.
3. Da qui in avanti, quando la guida dice "trascina/sposta" un file tra progetti in Visual Studio, verifica sempre con **Esplora File** (o `git status`) che il file sia sparito dalla posizione originale e non semplicemente duplicato.

---

## 🟠 Difetti minori in `GradeManager.cs` (da sistemare prima di proseguire)

### 3. `GetByIdAsync`: chiamate ridondanti al repository

```csharp
public async Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller)
{
    if (caller.Role == UserRole.Student)
    {
        var studentGrade = await _gradeRepository.GetByIdAsync(id);
        if (studentGrade == null || studentGrade.StudentId != caller.StudentId) return null;
    }
    else if (caller.Role == UserRole.Teacher)
    {
        var teacherGrade = await _gradeRepository.GetByIdAsync(id);
        if (teacherGrade == null || teacherGrade.TeacherId != caller.TeacherId) return null;
    }
    var grade = await _gradeRepository.GetByIdAsync(id);   // ← terza chiamata identica
    if (grade == null) return null;
    ...
```

Per uno Studente o un Docente il voto viene interrogato **due volte** dal database per la stessa richiesta (una per il controllo di autorizzazione, una per il mapping). Non è un errore funzionale, ma è uno spreco di query ed è il tipo di cosa che la guida vuole evitare quando dice di spostare le `Include`/query nel Repository per eliminare gli N+1.

**Correzione suggerita:**

```csharp
public async Task<GradeDto?> GetByIdAsync(Guid id, ClaimsContext caller)
{
    var grade = await _gradeRepository.GetByIdAsync(id);
    if (grade is null) return null;

    if (caller.Role == UserRole.Student && grade.StudentId != caller.StudentId) return null;
    if (caller.Role == UserRole.Teacher && grade.TeacherId != caller.TeacherId) return null;

    var student = await _studentRepository.GetByIdAsync(grade.StudentId);
    return new GradeDto
    {
        Id = grade.Id,
        StudentId = grade.StudentId,
        SubjectId = grade.SubjectId,
        TeacherId = grade.TeacherId,
        Value = grade.Value,
        Date = grade.Date,
        Student = student != null ? new StudentDto { Id = student.Id, FirstName = student.FirstName, LastName = student.LastName } : null
    };
}
```

### 4. `GetGradesBySubjectNameAsync` / `GetGradesByDateAsync`: gestione confusa della lista vuota

```csharp
var grades = await _gradeRepository.GetBySubjectNameAsync(subjectName, studentId, teacherId);
if (grades.Count == 0) return grades.Select(g => new GradeDto()).ToList();
```

Se `grades.Count == 0`, allora `grades.Select(...)` produce **comunque** una lista vuota: il codice fa una `Select` su una collezione vuota solo per ottenere... una collezione vuota. È fuorviante da leggere (sembra generare `GradeDto` vuoti, in realtà no) e nasconde un problema più serio: **la firma dell'interfaccia dichiara `Task<List<GradeDto>?>` (nullable)**, presumibilmente perché il Controller doveva poter distinguere "materia inesistente → 404" da "materia esistente ma senza voti → lista vuota". Così com'è scritto, il metodo **non restituisce mai `null`**, quindi quella distinzione si perde.

**Correzione suggerita** (scegli in base a cosa deve fare il Controller):

```csharp
// Se "nessun risultato" deve restituire semplicemente una lista vuota:
var grades = await _gradeRepository.GetBySubjectNameAsync(subjectName, studentId, teacherId);
if (grades.Count == 0) return new List<GradeDto>();
```

oppure, se invece serve distinguere "materia inesistente" (→ `null` → 404 nel Controller) da "materia esistente senza voti" (→ lista vuota → 200 con array vuoto), aggiungi prima una verifica esplicita sull'esistenza della materia (es. tramite `_subjectRepository.GetByNameAsync`) e restituisci `null` solo in quel caso. Applica la stessa correzione a `GetGradesByDateAsync`.

---

## 🟡 Nota di stile: naming delle proprietà in `GradeStatisticsDto`

**File:** `src/Domain/ElectronicRegisterAPI.Domain/DTOs/GradeStatisticsDto.cs`

```csharp
public class GradeStatisticsDto
{
    public decimal yearlyAverage { get; set; }
    public decimal?[] monthlyAverage { get; set; } = new decimal?[12];
}
```

Tutti gli altri DTO del progetto (`GradeDto`, `GradePageDto`, `GradeFiltersDto`, ecc.) usano PascalCase per le proprietà pubbliche, come da convenzione C#. Qui invece sono in camelCase. Non è una violazione delle "dritte", ma è un'incoerenza che vale la pena sistemare ora (prima che venga serializzata verso un frontend che si aspetta un certo casing):

```csharp
public class GradeStatisticsDto
{
    public decimal YearlyAverage { get; set; }
    public decimal?[] MonthlyAverage { get; set; } = new decimal?[12];
}
```

(Ricorda di aggiornare anche l'uso in `GradeManager.GetStatisticsAsync`, dove oggi scrivi `yearlyAverage = statistics.YearlyAverage`.)

---

## 🟡 Nota per quando scriverai `AuthManager` (non bloccante ora)

**File:** `src/Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IAuthManager.cs`

```csharp
Task MicrosoftLoginAsync(MicrosoftLoginDto dto);
Task LoginAsync(LoginDto dto);
```

Questi due metodi restituiscono `Task` (void), ma un login deve tipicamente restituire il JWT generato (o un DTO che lo contiene) al Controller, altrimenti quest'ultimo non avrà nulla da rispondere al client. Quando arriverai allo step Business/Application per l'autenticazione, valuta di cambiare la firma in qualcosa come `Task<string?> LoginAsync(LoginDto dto)` o `Task<LoginResultDto?> LoginAsync(LoginDto dto)`. Non blocca il lavoro fatto finora: lo segnalo solo perché l'interfaccia è già "congelata" in `Domain` e converrebbe corregerla prima di scriverci sopra `AuthManager`.

---

## Checklist delle azioni da fare, in ordine

1. [X] Elimina la cartella `src/Api/ElectronicRegisterAPI/Models/` (tutti i 6 file: entità duplicate + `ElectronicRegisterContext` duplicato).
2. [X] Aggiungi `namespace ElectronicRegisterAPI.Application.Managers;` in `GradeManager.cs`.
3. [X] Semplifica `GetByIdAsync` in `GradeManager` per fare una sola chiamata a `_gradeRepository.GetByIdAsync`.
4. [X] Sistema la gestione della lista vuota in `GetGradesBySubjectNameAsync` e `GetGradesByDateAsync` (decidi se il caso "nessun risultato" deve restituire `null` o lista vuota, e sii coerente con la firma dell'interfaccia).
5. [X] Rinomina in PascalCase le proprietà di `GradeStatisticsDto` e aggiorna `GradeManager.GetStatisticsAsync` di conseguenza.
6. [ ] (facoltativo, da tenere a mente) Rivedi la firma di `LoginAsync`/`MicrosoftLoginAsync` in `IAuthManager` prima di implementare `AuthManager`.
7. [ ] Dopo le correzioni 1-2, prova a compilare solo i progetti `Domain`, `Infrastructure`, `Business`, `Application` (non ancora `Api`, che fallirà comunque finché non riscrivi i Controller — è normale) per verificare che questi quattro livelli siano puliti prima di proseguire con gli altri Manager.

Una volta sistemati questi punti, puoi procedere tranquillamente a scrivere `StudentManager`, `SubjectManager`, `TeacherManager`, `UserManager` e `AuthManager` seguendo lo stesso schema di `GradeManager` (che, a parte i punti sopra, è strutturalmente corretto e ben allineato alla guida).
