// PROGRAM.CS: il punto di partenza del BACKEND (come index.js per il frontend).
// Qui si "monta" il server: configurazione, servizi, sicurezza; poi lo si accende con app.Run().
// Fasi: 1) leggere la configurazione  2) registrare i servizi (builder.Services)  3) costruire l'app  4) definire la "catena" di controlli sulle richieste.

// Strumenti importati (come import in JavaScript, vedi GUIDA §5).
using ElectronicRegisterAPI.Domain.Configuration;
using Scalar.AspNetCore;                                        // pagina web per provare le API
using ElectronicRegisterAPI.Api.Operations;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection; // registra database, token…
using ElectronicRegisterAPI.Business.DependencyInjection;       // registra i Services
using ElectronicRegisterAPI.Application.DependencyInjection;    // registra i Managers
using ElectronicRegisterAPI.Api.DependencyInjection;            // registra Controller e simili

// Il "builder" è il cantiere: si prepara tutto prima di costruire il server.
var builder = WebApplication.CreateBuilder(args);

// ---- 1) CONFIGURAZIONE: da dove il server legge le impostazioni ----
// Un file di configurazione indicato dalla variabile d'ambiente ER_RUNTIME_CONFIG (se presente).
var runtimeConfig = Environment.GetEnvironmentVariable("ER_RUNTIME_CONFIG");
if (!string.IsNullOrWhiteSpace(runtimeConfig))
    builder.Configuration.AddJsonFile(Path.GetFullPath(runtimeConfig), optional: false, reloadOnChange: false);
// I "segreti" (password, chiavi) letti da file nella cartella /run/secrets (usata in Docker).
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);
// Variabili d'ambiente e opzioni scritte quando si avvia il programma (le ultime vincono sulle precedenti).
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
// Controlli di sicurezza all'avvio: se un servizio è mal collegato, il server si rifiuta di partire
// invece di rompersi più tardi.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
// Se nessuno ha indicato l'indirizzo, il server ascolta solo sul computer locale, porta 5000.
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
    builder.WebHost.UseUrls("http://127.0.0.1:5000");
// Legge le regole di funzionamento (ambiente di sviluppo o produzione) e le applica.
var runtimePolicy = RuntimePolicy.Read(builder.Configuration, builder.Environment);
builder.AddOperationalServices(runtimePolicy);
// Kestrel = il motore web integrato. Qui i limiti di sicurezza:
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;                                    // non rivela che server è (meno informazioni agli attaccanti)
    o.Limits.MaxRequestBodySize = 1048576;                        // massimo 1 MB per richiesta
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);    // max 15 secondi per ricevere le intestazioni
});

// ---- 2) SERVIZI: si registra cosa il sistema può "consegnare" quando una classe lo chiede (GUIDA §5.1) ----
builder.Services.AddInfrastructure(builder.Configuration);   // database, repository, token
builder.Services.AddBusinessServices();                      // le regole (Services)
builder.Services.AddApplicationManagers();                   // i coordinatori (Managers)
// Opzioni per i trasferimenti di studenti: lette dalla configurazione, verificate subito all'avvio (ValidateOnStart).
builder.Services.AddOptions<ElectronicRegisterAPI.Domain.Configuration.AcademicTransferOptions>().Bind(builder.Configuration.GetSection(ElectronicRegisterAPI.Domain.Configuration.AcademicTransferOptions.Section)).Validate(ElectronicRegisterAPI.Domain.Configuration.AcademicTransferOptions.Valid, "Invalid academic transfer configuration").ValidateOnStart();
// Le rende disponibili direttamente come oggetto semplice (Singleton = una sola copia per tutto il programma).
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ElectronicRegisterAPI.Domain.Configuration.AcademicTransferOptions>>().Value);
// L'orologio di sistema, sostituibile nei test per "viaggiare nel tempo".
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApiServices();
builder.Services.AddApiControllers(builder.Configuration);   // i Controller (le "reception")
// Autenticazione: come il server capisce CHI sei (token istituzionale, vedi GUIDA §6).
builder.Services.AddAuthentication(ElectronicRegisterAPI.Infrastructure.Security.InstitutionTokenContract.AccessScheme);
// Autorizzazione: come decide COSA puoi fare (ruoli e permessi).
builder.Services.AddAuthorization();
ElectronicRegisterAPI.Api.Security.InstitutionAuthentication.AddInstitutionAuthentication(builder.Services, builder.Configuration);
// Documentazione automatica delle API (OpenAPI).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// ---- 3) COSTRUZIONE: da cantiere a server pronto ----
var app = builder.Build();

// ---- 4) CATENA DI CONTROLLI: ogni richiesta attraversa questi passaggi NELL'ORDINE scritto ----
app.UseOperationalPipeline();   // controlli operativi (limiti, log…)
app.UseExceptionHandler();      // se qualcosa va storto, trasforma l'errore in una risposta ordinata
// Solo in sviluppo: pagine per esplorare e provare le API dal browser.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();        // "chi sei?": legge il token
app.UseAuthorization();         // "cosa puoi fare?": controlla ruoli e permessi
app.MapControllers();           // collega gli indirizzi (/api/...) ai Controller
app.MapOperationalEndpoints();  // indirizzi di servizio (es. controllo che il server sia vivo)
app.Run();                      // ACCENDE il server: da qui resta in ascolto di richieste
