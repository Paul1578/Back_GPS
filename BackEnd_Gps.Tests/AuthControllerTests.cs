using fletflow.Api.Controllers;
using fletflow.Application.DTOs.Auth;
using fletflow.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackEnd_Gps.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_CreatesTenantAdminRegardlessOfRequestedRole()
    {
        var registration = new RecordingRegistrationService();
        var controller = new AuthController(null!, registration, null!, null!);
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
    public void TestEmail_RequiresAdminRole()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.TestEmail));
        var authorization = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Equal("Admin", authorization.Roles);
    }

    private sealed class RecordingRegistrationService : IUserRegistrationService
    {
        public string? RoleName { get; private set; }

        public Task<AuthResponseDto> RegisterAsync(string username, string email, string password, string roleName = "User")
        {
            RoleName = roleName;
            return Task.FromResult(new AuthResponseDto());
        }
    }
}
