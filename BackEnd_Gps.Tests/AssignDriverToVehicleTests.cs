using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace BackEnd_Gps.Tests;

public class AssignDriverToVehicleTests
{
    [Fact]
    public async Task Execute_RejectsVehicleInMaintenance()
    {
        var driver = Driver.Create("Test", "Driver", "QA-100", "0990000000");
        var vehicle = Vehicle.Create("QA-100", "Test", "Van", 2025, null);
        vehicle.SetStatus(VehicleStatus.Maintenance);
        var drivers = new FakeDrivers(driver);
        var vehicles = new FakeVehicles(vehicle);
        var uow = new FakeUnitOfWork();
        var command = new AssignDriverToVehicleCommand(uow, drivers, vehicles);

        await Assert.ThrowsAsync<InvalidOperationException>(() => command.Execute(driver.Id, vehicle.Id));
        Assert.Null(driver.VehicleId);
        Assert.Equal(0, uow.CommitCount);
    }

    private sealed class FakeDrivers(Driver driver) : IDriverRepository
    {
        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult(id == driver.Id ? driver : null);
        public Task<Driver?> GetByDocumentAsync(string documentNumber) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetByUserIdAsync(Guid userId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver value) => Task.CompletedTask;
        public Task UpdateAsync(Driver value) => Task.CompletedTask;
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(new List<Driver> { driver });
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(new List<Driver> { driver });
    }

    private sealed class FakeVehicles(Vehicle vehicle) : IVehicleRepository
    {
        public Task<Vehicle?> GetByIdAsync(Guid id) => Task.FromResult(id == vehicle.Id ? vehicle : null);
        public Task<Vehicle?> GetByPlateAsync(string plate) => Task.FromResult<Vehicle?>(null);
        public Task AddAsync(Vehicle value) => Task.CompletedTask;
        public Task UpdateAsync(Vehicle value) => Task.CompletedTask;
        public Task<List<Vehicle>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(new List<Vehicle> { vehicle });
        public Task<List<Vehicle>> GetByIdsAsync(IReadOnlyCollection<Guid> ids) => Task.FromResult(new List<Vehicle> { vehicle });
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public IUserRepository Users => null!;
        public IRoleRepository Roles => null!;
        public IRefreshTokenRepository RefreshTokens => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => null!;
        public IRoutePositionRepository RoutePositions => null!;
        public int CommitCount { get; private set; }
        public Task<int> CommitAsync() { CommitCount++; return Task.FromResult(1); }
        public void Rollback() { }
    }
}
