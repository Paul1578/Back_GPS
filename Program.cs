using fletflow.Infrastructure.Persistence;
using fletflow.Api.Controllers;
using Microsoft.EntityFrameworkCore;
using fletflow.Infrastructure.Services;

// 🧩 JWT y autenticación
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------
// 🔧 Configuración de servicios
// ------------------------------------------

builder.Services.AddScoped<AuthService>();

// Configurar Entity Framework Core con MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// Configuración de JWT
var jwtKey = builder.Configuration["Jwt:Key"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Registrar controladores
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AuthController).Assembly);

// Swagger (para pruebas de endpoints)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


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

app.UseHttpsRedirection();

// Habilitar autenticación y autorización JWT
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
