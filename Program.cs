using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

using fletflow.Api.Controllers;
using fletflow.Api.Middleware;
using fletflow.Api.Filters;

using fletflow.Infrastructure.Security;
using fletflow.Infrastructure.Security.Hashing;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Config;
using fletflow.Infrastructure.Services;

using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;

using fletflow.Application;

using FluentValidation;
using FluentValidation.AspNetCore;

// Values injected by the process/deployment environment must take precedence over
// the local .env file (CI and production supply their own database/JWT settings).
Env.NoClobber().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
var trustedProxyValues = builder.Configuration
    .GetSection("ForwardedHeaders:KnownProxies")
    .Get<string[]>() ?? Array.Empty<string>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();

    foreach (var value in trustedProxyValues)
    {
        if (!IPAddress.TryParse(value, out var address))
            throw new InvalidOperationException(
                "ForwardedHeaders:KnownProxies sólo acepta direcciones IP explícitas.");
        options.KnownProxies.Add(address);
    }
});
var keyRingPath = Environment.GetEnvironmentVariable("FLETFLOW_DATA_PROTECTION_KEYS_PATH");
var keyEncryptionCertificatePath = Environment.GetEnvironmentVariable("FLETFLOW_DATA_PROTECTION_CERT_PATH");
var keyEncryptionCertificatePassword = Environment.GetEnvironmentVariable("FLETFLOW_DATA_PROTECTION_CERT_PASSWORD");
var previousKeyEncryptionCertificatePath = Environment.GetEnvironmentVariable("FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PATH");
var previousKeyEncryptionCertificatePassword = Environment.GetEnvironmentVariable("FLETFLOW_DATA_PROTECTION_PREVIOUS_CERT_PASSWORD");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("FletFlow");
X509Certificate2? keyEncryptionCertificate = null;
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(keyRingPath))
    throw new InvalidOperationException(
        "Producción requiere FLETFLOW_DATA_PROTECTION_KEYS_PATH persistente para los tokens del outbox de correo.");
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(keyEncryptionCertificatePath))
    throw new InvalidOperationException(
        "Producción requiere FLETFLOW_DATA_PROTECTION_CERT_PATH para cifrar el key ring de Data Protection.");
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    var fullKeyRingPath = Path.GetFullPath(keyRingPath);
    Directory.CreateDirectory(fullKeyRingPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(fullKeyRingPath));
}
if (!string.IsNullOrWhiteSpace(keyEncryptionCertificatePath))
{
    keyEncryptionCertificate = new X509Certificate2(
        Path.GetFullPath(keyEncryptionCertificatePath),
        keyEncryptionCertificatePassword,
        X509KeyStorageFlags.EphemeralKeySet);
    if (!keyEncryptionCertificate.HasPrivateKey)
        throw new InvalidOperationException("El certificado de Data Protection debe incluir su clave privada.");
    dataProtection.ProtectKeysWithCertificate(keyEncryptionCertificate);
}
if (!string.IsNullOrWhiteSpace(previousKeyEncryptionCertificatePath))
{
    var previousCertificate = new X509Certificate2(
        Path.GetFullPath(previousKeyEncryptionCertificatePath),
        previousKeyEncryptionCertificatePassword,
        X509KeyStorageFlags.EphemeralKeySet);
    if (!previousCertificate.HasPrivateKey)
        throw new InvalidOperationException("El certificado anterior de Data Protection debe incluir su clave privada.");
    X509Certificate2[] decryptionCertificates = keyEncryptionCertificate is null
        ? [previousCertificate]
        : [keyEncryptionCertificate, previousCertificate];
    dataProtection.UnprotectKeysWithAnyCertificate(decryptionCertificates);
}

// =====================
// DB (.env)
// =====================
static string RequiredEnvironment(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Falta la variable de entorno requerida: {name}.");

var host = RequiredEnvironment("DB_HOST");
var port = RequiredEnvironment("DB_PORT");
var db = RequiredEnvironment("DB_NAME");
var user = RequiredEnvironment("DB_USER");
var pass = RequiredEnvironment("DB_PASSWORD");

var connectionString =
    $"Server={host};Port={port};Database={db};User={user};Password={pass};TreatTinyAsBoolean=false;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

// =====================
// Application (Commands & Queries)
// =====================
builder.Services.AddApplication();

// =====================
// Core Services
// =====================
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<PasswordResetTokenFactory>();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IUserRegistrationService>(services => services.GetRequiredService<AuthService>());
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<IEmailDeliveryQueue, EmailDeliveryQueue>();
builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// =====================
// Repositories
// =====================
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IDriverPageRepository>(services =>
    (IDriverPageRepository)services.GetRequiredService<IDriverRepository>());
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IRoutePositionRepository, RoutePositionRepository>();

// =====================
// Validation & Middleware
// =====================
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();

builder.Services.AddTransient<ExceptionMiddleware>();
builder.Services.AddTransient<JwtMiddleware>();
builder.Services.AddTransient<RoleValidationMiddleware>();
builder.Services.AddTransient<SimpleRateLimitMiddleware>();
builder.Services.AddTransient<HeaderValidationMiddleware>();

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<EmailSettings>, EmailSettingsValidator>();
builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection("Email"))
    .ValidateOnStart();

// =====================
// JWT
// =====================
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();

if (jwtSettings == null ||
    Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32 ||
    string.IsNullOrWhiteSpace(jwtSettings.Issuer) ||
    string.IsNullOrWhiteSpace(jwtSettings.Audience))
    throw new InvalidOperationException("JWT key (mínimo 32 bytes), emisor y audiencia deben estar configurados.");

var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Principal?.FindFirstValue("nameid");
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Token sin identidad válida.");
                    return;
                }

                var unitOfWork = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
                var user = await unitOfWork.Users.GetByIdAsync(userId);
                if (user is null || !user.IsActive)
                {
                    context.Fail("La cuenta está desactivada.");
                    return;
                }

                var currentRoles = user.UserRoles.Select(userRole => userRole.Role.Name.Trim()).ToArray();
                var tokenVersionValue = context.Principal?.FindFirstValue("token_version");
                if (!int.TryParse(tokenVersionValue, out var tokenVersion) || tokenVersion != user.TokenVersion)
                {
                    context.Fail("La contraseña cambió. Inicia sesión nuevamente.");
                    return;
                }

                var tokenRoles = context.Principal?.FindAll(ClaimTypes.Role)
                    .Select(claim => claim.Value.Trim()).ToArray() ?? Array.Empty<string>();
                if (currentRoles.Length != 1 || tokenRoles.Length != 1 ||
                    !currentRoles[0].Equals(tokenRoles[0], StringComparison.OrdinalIgnoreCase))
                    context.Fail("El rol de la cuenta cambió. Inicia sesión nuevamente.");
            }
        };
    });

builder.Services.AddAuthorization();

// =====================
// Controllers + CORS + Swagger
// =====================
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AuthController).Assembly)
    .AddMvcOptions(options =>
    {
        options.Filters.Add(typeof(ValidateModelStateFilter));
    });

builder.Services.AddCors(o =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? new[] { "http://localhost:3000" };

    o.AddPolicy("Front", p => p
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = "BackEnd_Gps.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FletFlow API",
        Version = "v1",
        Description = "REST API for fleet, routes, users management and JWT authentication."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        Description = "Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.OperationFilter<SwaggerUnauthorizedResponseOperationFilter>();
});

// =====================
// Build
// =====================
var app = builder.Build();
app.UseForwardedHeaders();

// Validate email security settings before touching the database or starting the outbox worker.
_ = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailSettings>>().Value;

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Front");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(dbContext);
}

app.UseMiddleware<SimpleRateLimitMiddleware>();
app.UseMiddleware<HeaderValidationMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<JwtMiddleware>();
app.UseMiddleware<RoleValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
