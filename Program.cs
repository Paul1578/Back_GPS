
using fletflow.Api.Controllers;
using Microsoft.EntityFrameworkCore;
using fletflow.Infrastructure.Services;
using fletflow.Infrastructure.Security; 

// 🧩 JWT y autenticación
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using fletflow.Application.Users.Queries;
using fletflow.Application.Users.Commands;
using System.Security.Claims;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Config;
using Microsoft.OpenApi.Models;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Persistence;
using fletflow.Api.Middleware;
using FluentValidation.AspNetCore;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------
// 🔧 Configuración de servicios
// ------------------------------------------
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<GetAllUsersQuery>();
builder.Services.AddScoped<UpdateUserCommand>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<fletflow.Application.Auth.Queries.GetAllRolesQuery>();
builder.Services.AddScoped<fletflow.Application.Auth.Commands.CreateRoleCommand>();
builder.Services.AddScoped<fletflow.Application.Auth.Commands.DeleteRoleCommand>();
builder.Services.AddScoped<fletflow.Application.Auth.Commands.ChangePasswordCommand>();
builder.Services.AddScoped<fletflow.Application.Auth.Commands.AssignRoleToUserCommand>();
builder.Services.AddScoped<fletflow.Application.Auth.Commands.RemoveRoleFromUserCommand>();builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterUserValidator>();
builder.Services.AddTransient<ExceptionMiddleware>();

builder.Services.AddInfrastructure(builder.Configuration);

// Configurar Entity Framework Core con MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});


// Configuración de JWT
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));
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
            ValidIssuer = "fletflow.api",
            ValidAudience = "fletflow.users",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])
            ),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ManagerOnly", policy => policy.RequireRole("Manager"));
    options.AddPolicy("DriverOnly", policy => policy.RequireRole("Driver"));
});


// Registrar controladores
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AuthController).Assembly);

// Swagger (para pruebas de endpoints)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FletFlow API",
        Version = "v1",
        Description = "API para la gestión de flotas con autenticación JWT",
        Contact = new OpenApiContact
        {
            Name = "Equipo fletflow",
            Email = "soporte@fletflow.com"
        }
    });

    // 🔐 Incluir esquema de autenticación JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Encabezado de autorización JWT usando el esquema Bearer. Ejemplo: 'Bearer {token}'",
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

// ------------------------------------------
// 🚀 Construcción de la aplicación
// ------------------------------------------
var app = builder.Build();

// ------------------------------------------
// 🌐 Configuración del pipeline HTTP
// ------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseMiddleware<ExceptionMiddleware>();
app.UseRouting(); 

// Habilitar autenticación y autorización JWT
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();

app.Run();

