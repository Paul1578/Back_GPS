using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;

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

    [Fact]
    public async Task UpdateDriver_RejectsDocumentOwnedByAnotherDriver()
    {
        var current = Driver.Create("Current", "Driver", "DOC-001", "0990000000");
        var existing = Driver.Create("Existing", "Driver", "DOC-002", "0990000001");
        var repository = new FakeDriverRepository(current, existing);
        var unitOfWork = new FakeUnitOfWork();
        var command = new UpdateDriverCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute(current.Id, "Current", "Driver", "DOC-002", "0990000000"));

        Assert.Contains("Ya existe otro conductor", error.Message);
        Assert.Equal("DOC-001", current.DocumentNumber);
        Assert.Equal(0, repository.UpdateCount);
        Assert.Equal(0, unitOfWork.CommitCount);
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
