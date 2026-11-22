using fletflow.Api.Controllers;
using fletflow.Application.DTOs.Auth;
using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using System.Security.Claims;
using fletflow.Domain.Fleet.Exceptions;

namespace BackEnd_Gps.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_CreatesTenantAdminRegardlessOfRequestedRole()
    {
        var registration = new RecordingRegistrationService();
        var controller = new AuthController(null!, registration, null!, null!, new TestWebHostEnvironment("Development"));
        var dto = new RegisterUserDto
        {
            Username = "operator",
            Email = "operator@example.test",
            Password = "safe-password",
            RoleName = "Driver"
        };

        var result = await controller.Register(dto);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Admin", registration.RoleName);
    }

    [Fact]
    public async Task Register_ReturnsSameNeutralResponseWhenEmailAlreadyExists()
    {
        var existingEmailRegistration = new RecordingRegistrationService(duplicateEmail: true);
        var controller = new AuthController(null!, existingEmailRegistration, null!, null!, new TestWebHostEnvironment("Development"));
        var dto = new RegisterUserDto
        {
            Username = "operator",
            Email = "operator@example.test",
            Password = "safe-password",
        };

        var result = Assert.IsType<OkObjectResult>(await controller.Register(dto));

        Assert.Equal("Si el registro puede completarse, podrás iniciar sesión con tus credenciales.",
            result.Value!.GetType().GetProperty("message")!.GetValue(result.Value));
    }

    [Fact]
    public void TestEmail_RequiresAdminRole()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.TestEmail));
        var authorization = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Equal("Admin", authorization.Roles);
    }

    [Fact]
    public async Task TestEmail_IsUnavailableOutsideDevelopment()
    {
        var sender = new RecordingEmailSender();
        var controller = CreateEmailController("Production", "admin@example.test", sender);

        var result = await controller.TestEmail("admin@example.test");

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, sender.PasswordResetEmailsSent);
    }

    [Fact]
    public async Task TestEmail_DevelopmentCanOnlySendToAuthenticatedAccount()
    {
        var sender = new RecordingEmailSender();
        var controller = CreateEmailController("Development", "admin@example.test", sender);

        var forbidden = await controller.TestEmail("other@example.test");
        var sent = await controller.TestEmail("ADMIN@example.test");

        Assert.IsType<ForbidResult>(forbidden);
        Assert.IsType<OkObjectResult>(sent);
        Assert.Equal(1, sender.PasswordResetEmailsSent);
        Assert.Equal("admin@example.test", sender.LastRecipient);
    }

    private static AuthController CreateEmailController(string environmentName, string email, RecordingEmailSender sender)
    {
        var controller = new AuthController(
            null!, null!, sender, null!, new TestWebHostEnvironment(environmentName));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim("email", email)
                ], "test"))
            }
        };
        return controller;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public int PasswordResetEmailsSent { get; private set; }
        public string? LastRecipient { get; private set; }

        public Task SendPasswordResetEmailAsync(string toEmail, string link, string plainToken, CancellationToken cancellationToken = default)
        {
            PasswordResetEmailsSent++;
            LastRecipient = toEmail;
            return Task.CompletedTask;
        }

        public Task SendUserInvitationEmailAsync(string toEmail, string activationLink, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BackEnd_Gps.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RecordingRegistrationService(bool duplicateEmail = false) : IUserRegistrationService
    {
        public string? RoleName { get; private set; }

        public Task RegisterAsync(string username, string email, string password, string roleName = "User")
        {
            RoleName = roleName;
            return duplicateEmail
                ? Task.FromException(new BusinessConflictException("El correo ya fue registrado."))
                : Task.CompletedTask;
        }
    }
}
