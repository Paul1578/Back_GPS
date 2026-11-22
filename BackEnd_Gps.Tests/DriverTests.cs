using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace BackEnd_Gps.Tests;

public class DriverTests
{
    [Fact]
    public async Task CreateDriver_RejectsDuplicateDocumentAsBusinessConflict()
    {
        var existing = Driver.Create("Existing", "Driver", "DOC-001", "0990000000");
        var repository = new FakeDriverRepository(existing);
        var unitOfWork = new FakeUnitOfWork();
        var command = new CreateDriverCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute("New", "Driver", "DOC-001", "0990000001"));

        Assert.Contains("Ya existe un conductor", error.Message);
        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    [Theory]
    [InlineData("123456789012345678901", "0990000000")]
    [InlineData("DOC-001", "123456789012345678901")]
    public async Task CreateDriver_RejectsDocumentOrPhoneLongerThanDatabaseLimit(string documentNumber, string phoneNumber)
    {
        var repository = new FakeDriverRepository();
        var unitOfWork = new FakeUnitOfWork();
        var command = new CreateDriverCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            command.Execute("New", "Driver", documentNumber, phoneNumber));

        Assert.Contains("no pueden exceder 20", error.Message);
        Assert.Equal(0, repository.AddCount);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task CreateDriver_TrimsFieldsAndAcceptsDatabaseLengthLimit()
    {
        var repository = new FakeDriverRepository();
        var unitOfWork = new FakeUnitOfWork();
        var command = new CreateDriverCommand(unitOfWork, repository);

        var result = await command.Execute(" Ana ", " Paz ", " 12345678901234567890 ", " 09876543210987654321 ");

        Assert.Equal(20, result.DocumentNumber.Length);
        Assert.Equal(20, result.PhoneNumber.Length);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task UpdateDriver_RejectsDocumentOwnedByAnotherDriver()
    {
        var current = Driver.Create("Current", "Driver", "DOC-001", "0990000000");
        var existing = Driver.Create("Existing", "Driver", "DOC-002", "0990000001");
        var repository = new FakeDriverRepository(current, existing);
        var unitOfWork = new FakeUnitOfWork();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"driver-update-{Guid.NewGuid()}")
            .Options;
        await using var context = new AppDbContext(options);
        var command = new UpdateDriverCommand(unitOfWork, repository, context);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute(current.Id, "Current", "Driver", "DOC-002", "0990000000"));

        Assert.Contains("Ya existe otro conductor", error.Message);
        Assert.Equal("DOC-001", current.DocumentNumber);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task UpdateDriver_SynchronizesLinkedUserNames()
    {
        var userId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var driver = Driver.Create("Before", "Name", "DOC-010", "0990000010", userId);
        var repository = new FakeDriverRepository(driver);
        var unitOfWork = new FakeUnitOfWork();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"driver-profile-{Guid.NewGuid()}")
            .Options;
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("owner_id", ownerUserId.ToString())],
                    authenticationType: "test"))
            }
        };
        await using var context = new AppDbContext(options, accessor);
        context.Users.Add(new UserEntity
        {
            Id = userId,
            Username = "driver",
            Email = "driver@example.test",
            PasswordHash = "test-hash",
            FirstName = "Before",
            LastName = "Name",
            OwnerUserId = ownerUserId
        });
        context.Drivers.Add(new DriverEntity
        {
            Id = driver.Id,
            UserId = userId,
            OwnerUserId = ownerUserId,
            FirstName = driver.FirstName,
            LastName = driver.LastName,
            DocumentNumber = driver.DocumentNumber,
            PhoneNumber = driver.PhoneNumber,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var command = new UpdateDriverCommand(unitOfWork, repository, context);

        await command.Execute(driver.Id, " After ", " Name ", " DOC-010 ", " 0990000010 ");

        var linkedUser = await context.Users.FindAsync(userId);
        Assert.Equal("After", linkedUser!.FirstName);
        Assert.Equal("Name", linkedUser.LastName);
        Assert.Equal(1, unitOfWork.CommitCount);
    }

    [Fact]
    public void Update_TrimsProfileFields()
    {
        var driver = Driver.Create("Ana", "Paz", "100", "099", Guid.NewGuid());

        driver.Update("  Ana María ", " Paz ", " 200 ", " 098 ");

        Assert.Equal("Ana María", driver.FirstName);
        Assert.Equal("Paz", driver.LastName);
        Assert.Equal("200", driver.DocumentNumber);
        Assert.Equal("098", driver.PhoneNumber);
    }

    [Fact]
    public void DeactivateAndReactivate_ChangeAvailabilityWithoutChangingIdentity()
    {
        var userId = Guid.NewGuid();
        var driver = Driver.Create("Ana", "Paz", "100", "099", userId);
        var driverId = driver.Id;

        driver.Deactivate();
        Assert.False(driver.IsActive);

        driver.Activate();
        Assert.True(driver.IsActive);
        Assert.Equal(driverId, driver.Id);
        Assert.Equal(userId, driver.UserId);
    }

    [Fact]
    public async Task ChangeStatus_RejectsDeactivationWhileDriverHasRouteInProgress()
    {
        var driver = Driver.Create("Ana", "Paz", "100", "099");
        var routes = new FakeRouteRepository(driver.Id, RouteStatus.InProgress);
        var command = new ChangeDriverStatusCommand(new FakeUnitOfWork(), new FakeDriverRepository(driver), routes);

        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(driver.Id, false));
        Assert.True(driver.IsActive);
    }

    private sealed class FakeDriverRepository(params Driver[] drivers) : IDriverRepository
    {
        private readonly List<Driver> _drivers = drivers.ToList();
        public int AddCount { get; private set; }
        public int UpdateCount { get; private set; }
        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult(_drivers.FirstOrDefault(driver => driver.Id == id));
        public Task<Driver?> GetByDocumentAsync(string documentNumber) =>
            Task.FromResult(_drivers.FirstOrDefault(driver => string.Equals(driver.DocumentNumber, documentNumber, StringComparison.OrdinalIgnoreCase)));
        public Task<Driver?> GetByUserIdAsync(Guid userId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver value) { AddCount++; return Task.CompletedTask; }
        public Task UpdateAsync(Driver value) { UpdateCount++; return Task.CompletedTask; }
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(_drivers);
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(_drivers.Where(driver => driver.IsActive).ToList());
    }

    private sealed class FakeRouteRepository(Guid driverId, RouteStatus status) : IRouteRepository
    {
        public Task<RouteE?> GetByIdAsync(Guid id) => Task.FromResult<RouteE?>(null);
        public Task AddAsync(RouteE route) => Task.CompletedTask;
        public Task UpdateAsync(RouteE route) => Task.CompletedTask;
        public Task<List<RouteE>> GetAllAsync(Guid? vehicleId = null, Guid? driverIdFilter = null, RouteStatus? statusFilter = null, bool? onlyActive = null)
        {
            if (driverIdFilter != driverId || statusFilter != status) return Task.FromResult(new List<RouteE>());
            var point = RoutePoint.Create(0, 0);
            var route = RouteE.CreateExisting(Guid.NewGuid(), Guid.NewGuid(), driverId, "Ruta", point, point,
                new[] { point, point }, null, null, null, status, true);
            return Task.FromResult(new List<RouteE> { route });
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int CommitCount { get; private set; }
        public IUserRepository Users => null!;
        public IRoleRepository Roles => null!;
        public IRefreshTokenRepository RefreshTokens => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => null!;
        public IRoutePositionRepository RoutePositions => null!;
        public Task<int> CommitAsync() { CommitCount++; return Task.FromResult(1); }
        public void Rollback() { }
    }
}
