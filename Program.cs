using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Security.Claims;

using fletflow.Api.Controllers;
using fletflow.Api.Middleware;
using fletflow.Infrastructure.Services;
using fletflow.Infrastructure.Security;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence;
using fletflow.Infrastructure.Persistence.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Config;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Application.Users.Commands;
using fletflow.Application.Users.Queries;
using fletflow.Application.Auth.Commands;
using fletflow.Application.Auth.Commands.ActivateAccount;
using fletflow.Application.Auth.Queries;
using fletflow.Application.Fleet.Commands;
using fletflow.Application.Fleet.Queries;
using fletflow.Infrastructure.Security.Hashing;
using fletflow.Infrastructure.Services;
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
// Services
// =====================
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<PasswordResetTokenFactory>();

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IDriverRepository, DriverRepository>();
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IRoutePositionRepository, RoutePositionRepository>();

builder.Services.AddScoped<GetAllUsersQuery>();
builder.Services.AddScoped<RegisterUserCommand>();
builder.Services.AddScoped<UpdateUserCommand>();
builder.Services.AddScoped<CreateUserWithRoleCommand>();
builder.Services.AddScoped<GetMyTeamUsersQuery>();

builder.Services.AddScoped<CreateVehicleCommand>();
builder.Services.AddScoped<UpdateVehicleCommand>();
builder.Services.AddScoped<DeleteVehicleCommand>();
builder.Services.AddScoped<GetVehicleByIdQuery>();
builder.Services.AddScoped<GetVehiclesQuery>();
builder.Services.AddScoped<ChangeVehicleStatusCommand>();

builder.Services.AddScoped<CreateDriverCommand>();
builder.Services.AddScoped<GetDriverByIdQuery>();
builder.Services.AddScoped<GetDriversQuery>();
builder.Services.AddScoped<AssignDriverToVehicleCommand>();
builder.Services.AddScoped<UnassignDriverFromVehicleCommand>();

builder.Services.AddScoped<CreateRouteCommand>();
builder.Services.AddScoped<UpdateRouteCommand>();
builder.Services.AddScoped<DeleteRouteCommand>();
builder.Services.AddScoped<GetRouteByIdQuery>();
builder.Services.AddScoped<GetRoutesQuery>();
builder.Services.AddScoped<ChangeRouteStatusCommand>();
builder.Services.AddScoped<RegisterRoutePositionCommand>();
builder.Services.AddScoped<GetRoutePositionsQuery>();

builder.Services.AddScoped<ActivateAccountCommand>();
builder.Services.AddScoped<ChangePasswordCommand>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
builder.Services.AddTransient<ExceptionMiddleware>();



builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// =====================
// JWT
// =====================
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
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
    .AddApplicationPart(typeof(AuthController).Assembly);

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
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FletFlow API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
});

// =====================
// Build
// =====================
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(dbContext);
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("Front");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
