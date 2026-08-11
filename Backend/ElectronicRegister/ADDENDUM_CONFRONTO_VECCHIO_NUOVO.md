# 🔍 Addendum — Confronto sistematico `ElectronicRegisterAPI` (vecchio monolite) vs `ElectronicRegister` (nuova architettura)

> Confronto endpoint-per-endpoint di tutti e 6 i controller (`Auth`, `Grade`, `Student`, `Subject`, `Teacher`, `User`) tra `Backend/ElectronicRegisterAPI` (originale) e `Backend/ElectronicRegister/src` (refactored), fatto **prima di eliminare la cartella vecchia**, come richiesto.

**Verdetto in breve:** oltre alle 2 regressioni già note (§3.1 e §3.2 del report principale), ho trovato **2 nuove regressioni reali** (§1) e **importante: una vulnerabilità di sicurezza presente nell'originale che il refactoring ha corretto** (§2, la trovi per prima perché è la notizia migliore). Ci sono poi diverse **modifiche di comportamento intenzionali/migliorative** (§3) di cui è bene tu sia consapevole prima di eliminare il vecchio codice, perché cambiano lo status HTTP o l'autorizzazione di alcuni endpoint rispetto a prima.

---

## 1️⃣ Nuove regressioni trovate (da correggere)

### 1.1 🔴 `MicrosoftLogin`: un token Microsoft non valido ora risponde `500` invece di `401`

**Vecchio** (`AuthController.MicrosoftLogin`):
```csharp
try
{
    principal = await ValidateMicrosoftToken(dto.AccessToken);
}
catch (Exception ex)
{
    return Unauthorized("Token Microsoft non valido: " + ex.Message);
}
```

**Nuovo** (`AuthManager.MicrosoftLoginAsync`):
```csharp
public async Task<string?> MicrosoftLoginAsync(MicrosoftLoginDto dto)
{
    var principal = await _microsoftTokenValidator.ValidateAsync(dto.AccessToken);
    // nessun try/catch: se il token è scaduto/malformato/firma non valida,
    // ValidateToken lancia SecurityTokenException (o simili) che NON viene intercettata qui
    ...
}
```

Il `GlobalExceptionHandler` non ha un caso specifico per le eccezioni di validazione token (`SecurityTokenException` e derivate non ereditano da `ArgumentException`/`UnauthorizedAccessException`/`BusinessRuleException`), quindi finiscono nel ramo `_ => 500 Internal Server Error`. **Un token Microsoft scaduto o non valido, che prima dava correttamente `401 Unauthorized`, oggi risponde `500`** — un client che gestisce diversamente i due casi (es. redirect al login su 401, messaggio di errore generico su 500) si comporterà in modo sbagliato.

**Correzione consigliata**, in `AuthManager.MicrosoftLoginAsync`:
```csharp
public async Task<string?> MicrosoftLoginAsync(MicrosoftLoginDto dto)
{
    ClaimsPrincipal principal;
    try
    {
        principal = await _microsoftTokenValidator.ValidateAsync(dto.AccessToken);
    }
    catch (Exception)
    {
        return null; // il Controller traduce già "null" in 401 Unauthorized
    }
    // ... resto invariato
}
```
Il `Controller` (`AuthController.MicrosoftLogin`) già traduce un `null` in `Unauthorized(...)`, quindi questa correzione basta a ripristinare il comportamento originale senza toccare il Controller.

---

### 1.2 🟠 `GetStatistics`: nessun voto registrato ora risponde `200` (con medie a zero) invece di `404`

**Vecchio** (`GradeController.GetStatistics`):
```csharp
if (!await query.AnyAsync())
    return NotFound();

var yearlyAverage = await query.AverageAsync(g => g.Value);
```

**Nuovo** (`GradeRepository.GetStatisticsAsync` + `GradeManager.GetStatisticsAsync`):
```csharp
var yearlyAverage = await query.Select(g => (decimal?)g.Value).AverageAsync() ?? 0;
// nessun controllo "query vuota → 404": se non ci sono voti, restituisce sempre
// GradeStatisticsDto { YearlyAverage = 0, MonthlyAverage = [null x12] } con 200 OK
```

Per uno studente o un docente che non ha ancora nessun voto registrato, prima l'endpoint rispondeva `404` (nessun dato); ora risponde `200` con una media di `0`. Un frontend che distingue "nessun dato ancora" da "media effettivamente 0" (es. per non mostrare un grafico fuorviante) si romperebbe silenziosamente.

**Correzione consigliata**, in `GradeManager.GetStatisticsAsync` (esempio per il ramo studente, stesso pattern per teacher/admin):
```csharp
public async Task<GradeStatisticsDto?> GetStatisticsAsync(ClaimsContext caller)
{
    Guid? teacherId = caller.Role == UserRole.Teacher ? caller.TeacherId : null;
    Guid? studentId = caller.Role == UserRole.Student ? caller.StudentId : null;

    var hasAnyGrade = await _gradeRepository.CountAsync(teacherId, studentId) > 0;
    if (!hasAnyGrade) return null;   // il Controller già traduce null in NotFound()

    var statistics = await _gradeRepository.GetStatisticsAsync(teacherId, studentId);
    return new GradeStatisticsDto { YearlyAverage = statistics.YearlyAverage, MonthlyAverage = statistics.MonthlyAverage };
}
```
(Nota: il Controller `GradeController.GetStatistics` oggi fa `return Ok(statistics);` senza controllare il null — andrà aggiornato anche lui in `return statistics is null ? NotFound() : Ok(statistics);`.)

---

## 2️⃣ 🎉 Una vulnerabilità di sicurezza dell'originale è stata corretta (da tenere, non da "ripristinare")

Confrontando `UsersController.UpdatePassword` vecchio e nuovo ho trovato qualcosa di importante, in positivo.

**Vecchio** (`UsersController.UpdatePassword`):
```csharp
[HttpPut("updatepassword/{id}")]
[Authorize(Roles = "student,teacher,admin")]
public async Task<ActionResult> UpdatePassword(Guid id, UpdatePasswordDto dto)
{
    var user = await _context.Users.FindAsync(id);
    if (user == null) return NotFound();
    if (!User.IsInRole("admin") && user.Id != id) return Forbid();   // ← BUG
    ...
}
```

Il controllo `user.Id != id` è **sempre falso**, perché `user` è stato appena recuperato con `FindAsync(id)` — quindi `user.Id` è per costruzione uguale a `id`. Il controllo avrebbe dovuto confrontare l'id del **chiamante** (dal token JWT) con l'id del **bersaglio** (`id` nel path), non l'id del bersaglio con sé stesso. Il risultato pratico: **qualunque studente, docente o admin autenticato poteva cambiare la password di qualsiasi altro utente**, semplicemente chiamando `PUT /api/users/updatepassword/{id-di-un-altro}` — la `[Authorize(Roles = "student,teacher,admin")]` a livello di endpoint non bastava a impedirlo, perché di fatto tutti gli utenti autenticati rientravano in uno di quei 3 ruoli.

**Nuovo** (`UserService.EnsureCallerCanChangePassword`, richiamato da `UserManager.UpdatePasswordAsync`):
```csharp
public void EnsureCallerCanChangePassword(ClaimsContext caller, Guid targetUserId)
{
    if (caller.Role != UserRole.Admin && caller.UserId != targetUserId)
        throw new UnauthorizedAccessException("Non puoi modificare la password di un altro utente.");
}
```
Qui il confronto è corretto: `caller.UserId` (letto dal claim `sub` del JWT, quindi l'utente realmente autenticato) viene confrontato con `targetUserId` (il parametro `id` della richiesta). Questo **chiude la falla** presente nell'originale. Non è chiaro se sia stata una correzione intenzionale o un "effetto collaterale" del pattern `ClaimsContext`, ma il risultato è comunque corretto e va assolutamente mantenuto così com'è — è un ottimo motivo in più per non tornare al codice vecchio.

---

## 3️⃣ Altre differenze di comportamento (probabilmente intenzionali/migliorative, ma da confermare)

Queste non sono "bug" nel senso stretto — sono scelte che sembrano deliberate e in generale più corrette del comportamento originale — ma **cambiano il contratto dell'API** rispetto a prima, quindi è bene che tu le riconosca consapevolmente prima di considerare il refactoring "equivalente al 100%" al vecchio codice:

| Endpoint | Comportamento vecchio | Comportamento nuovo | Valutazione |
|---|---|---|---|
| `DELETE` su Student/Subject/Teacher con record collegati | `400 BadRequest` con messaggio | `409 Conflict` (via `BusinessRuleException` → `GlobalExceptionHandler`) | Più corretto semanticamente (409 = conflitto di stato, non richiesta malformata), ma è un **cambio di contratto HTTP**: se hai un frontend che controlla `status === 400` per questi casi, va aggiornato a `409`. |
| `GET /api/grade/{id}` su un voto non tuo (studente/docente) | `403 Forbid` | `404 NotFound` | Pratica comune per non rivelare l'esistenza di risorse altrui, ma è comunque un cambio di status code osservabile dal client. |
| `GET /api/grade/count` per studenti | Vietato (`403`, ruolo studente escluso) | Permesso (`200`) | Sembra un fix di coerenza voluto (tutti gli altri endpoint "grade" già permettevano `student`), ma è un **ampliamento di autorizzazione** — verifica che sia voluto. |
| `GET /api/subject/byteacher/{id}` per un docente che chiede l'id di un **altro** docente | Nessuna restrizione: vedeva le materie altrui | Restituisce lista vuota → `404` | Fix di un possibile leak di informazioni tra docenti — positivo, ma cambia cosa un docente può vedere. |
| `POST /api/subject` con nome materia già esistente | Permesso (nessun controllo di unicità) | Bloccato (`BusinessRuleException` → `409`) | Se nel vostro dataset esistono già materie con nomi duplicati, questo controllo bloccherà nuove creazioni con lo stesso nome — verifica che sia compatibile con i dati attuali. |
| `PUT /api/grade/update/{id}` (docente) che sposta un voto su una materia che il docente **non** insegna | Permesso (solo l'ownership del voto era controllata, non la nuova materia) | Bloccato (`EnsureTeacherTeachesSubjectAsync` ora chiamato anche in Update, non solo in Add) | Fix di una regola di business mancante — positivo. |
| `POST /api/user` (admin crea utente) con `Role` non valido (es. stringa a caso) | Permesso, salvava comunque il ruolo non valido | Bloccato (`EnsureValidRole`) | Fix — evita utenti con ruolo "rotto" che romperebbero l'`[Authorize(Roles=...)]` a runtime. |
| `PUT /api/users/update/{id}` con nuova password o nuova email | Nessuna validazione password (lunghezza/carattere speciale), nessun controllo unicità email | Entrambe validate | Fix, coerente con le stesse regole già applicate in fase di creazione/registrazione. |

Nessuna di queste è, a mio avviso, da "correggere per tornare all'originale" — sono quasi tutte migliorie legittime. Le riporto solo perché **cambiano cosa un chiamante API riceve rispetto a prima**, e se hai un frontend o dei client esistenti che si aspettano il comportamento vecchio, li romperesti silenziosamente aggiornando il backend.

---

## 4️⃣ Verdetto finale: posso eliminare `Backend/ElectronicRegisterAPI`?

**Sì, con questa sequenza:**

1. Assicurati che la cartella sia recuperabile da Git (vedi risposta precedente) — se non lo è ancora, fai un commit dedicato prima di cancellare.
2. Applica le correzioni ai **4 bug reali** trovati finora:
   - §3.1 e §3.2 del report principale (`TeacherManager.UpdateAsync`, `AuthManager.RegisterAsync`)
   - §1.1 e §1.2 di questo addendum (`MicrosoftLoginAsync`, `GetStatisticsAsync`)
3. Prendi una decisione consapevole sulle differenze di comportamento del §3 (va bene quasi certamente tenerle così, ma è una tua scelta di prodotto, non solo tecnica — in particolare l'ampliamento di `GET /api/grade/count` a `student` e il nuovo controllo di unicità nome-materia meritano una verifica con chi conosce i dati/requisiti reali).
4. A quel punto la nuova architettura non solo replica l'originale, ma lo **supera** (corregge anche una vulnerabilità di sicurezza reale) — puoi eliminare `Backend/ElectronicRegisterAPI` senza perdere nulla.

Non elimino nulla in autonomia: fammi sapere quando hai applicato le correzioni (o se vuoi che te le scriva io direttamente nei file) e ti confermo che è tutto a posto.
