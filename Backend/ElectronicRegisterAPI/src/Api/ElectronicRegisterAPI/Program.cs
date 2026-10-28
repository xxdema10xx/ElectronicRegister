using Scalar.AspNetCore;
using ElectronicRegisterAPI.Api.Operations;
using ElectronicRegisterAPI.Infrastructure.DependencyInjection;
using ElectronicRegisterAPI.Business.DependencyInjection;
using ElectronicRegisterAPI.Application.DependencyInjection;
using ElectronicRegisterAPI.Api.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
var runtimeConfig = Environment.GetEnvironmentVariable("ER_RUNTIME_CONFIG");
if (!string.IsNullOrWhiteSpace(runtimeConfig))
    builder.Configuration.AddJsonFile(Path.GetFullPath(runtimeConfig), optional: false, reloadOnChange: false);
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
    builder.WebHost.UseUrls("http://127.0.0.1:5000");

var runtimePolicy = RuntimePolicy.Read(builder.Configuration, builder.Environment);
builder.AddOperationalServices(runtimePolicy);
builder.WebHost.ConfigureKestrel(o => { o.AddServerHeader = false; o.Limits.MaxRequestBodySize = 1048576; o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15); });

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBusinessServices();
builder.Services.AddApplicationManagers();
builder.Services.AddOptions<ElectronicRegisterAPI.Domain.Models.AcademicTransferOptions>()
    .Bind(builder.Configuration.GetSection(ElectronicRegisterAPI.Domain.Models.AcademicTransferOptions.Section))
    .Validate(ElectronicRegisterAPI.Domain.Models.AcademicTransferOptions.Valid,"Invalid academic transfer configuration")
    .ValidateOnStart();
builder.Services.AddSingleton(sp=>sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ElectronicRegisterAPI.Domain.Models.AcademicTransferOptions>>().Value);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApiServices();

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new ElectronicRegisterAPI.Api.Security.FormativePlanControllerFeatureProvider(builder.Configuration)))
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
            RequireExpirationTime = true, RequireSignedTokens = true,
            ValidAlgorithms = [Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
ElectronicRegisterAPI.Api.Security.InstitutionAuthentication.AddInstitutionAuthentication(builder.Services, builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseOperationalPipeline();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapOperationalEndpoints();

app.Run();
