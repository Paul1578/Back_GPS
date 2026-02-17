using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Security.Claims;

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

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// =====================
// DB (.env)
// =====================
var host = Environment.GetEnvironmentVariable("DB_HOST");
var port = Environment.GetEnvironmentVariable("DB_PORT");
var db   = Environment.GetEnvironmentVariable("DB_NAME");
var user = Environment.GetEnvironmentVariable("DB_USER");
var pass = Environment.GetEnvironmentVariable("DB_PASSWORD");

if (string.IsNullOrEmpty(host))
    throw new Exception("Variables de entorno de BD no cargadas (.env)");

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
builder.Services.AddScoped<IEmailSender, EmailService>();
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

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// =====================
// JWT
// =====================
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();

if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.Key))
    throw new Exception("JWT key is not properly configured.");

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
    o.AddPolicy("Front", p => p
        .AllowAnyOrigin()
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("Front");
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(dbContext);
}

//app.UseHttpsRedirection();

app.UseMiddleware<SimpleRateLimitMiddleware>();
app.UseMiddleware<HeaderValidationMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseMiddleware<JwtMiddleware>();
app.UseMiddleware<RoleValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
