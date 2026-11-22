using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace BackEnd_Gps.Tests;

public class AssignDriverToVehicleTests
{
    [Fact]
    public async Task Execute_AssignsVehicleToActiveUnlinkedDriverAndCommits()
    {
        var driver = Driver.Create("Test", "Driver", "QA-099", "0990000000");
        var vehicle = Vehicle.Create("QA-099", "Test", "Van", 2025, null);
        var uow = new FakeUnitOfWork();
        var command = new AssignDriverToVehicleCommand(
            uow,
            new FakeDrivers(driver),
            new FakeVehicles(vehicle));

        await command.Execute(driver.Id, vehicle.Id);

        Assert.Equal(vehicle.Id, driver.VehicleId);
        Assert.Equal(1, uow.CommitCount);
    }

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

    [Fact]
    public async Task Execute_RejectsVehicleAlreadyAssignedToAnotherDriver()
    {
        var currentDriver = Driver.Create("Current", "Driver", "QA-101", "0990000001");
        var assignedDriver = Driver.Create("Assigned", "Driver", "QA-102", "0990000002");
        var vehicle = Vehicle.Create("QA-101", "Test", "Van", 2025, null);
        assignedDriver.AssignToVehicle(vehicle.Id);
        var drivers = new FakeDrivers(currentDriver, assignedDriver);
        var uow = new FakeUnitOfWork();
        var command = new AssignDriverToVehicleCommand(uow, drivers, new FakeVehicles(vehicle));

        var error = await Assert.ThrowsAsync<BusinessConflictException>(
            () => command.Execute(currentDriver.Id, vehicle.Id));

        Assert.Contains("ya está asignado a otro conductor", error.Message);
        Assert.Null(currentDriver.VehicleId);
        Assert.Equal(vehicle.Id, assignedDriver.VehicleId);
        Assert.Equal(0, uow.CommitCount);
    }

    private sealed class FakeDrivers(params Driver[] drivers) : IDriverRepository
    {
        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult(drivers.FirstOrDefault(driver => driver.Id == id));
        public Task<Driver?> GetByDocumentAsync(string documentNumber) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetByUserIdAsync(Guid userId) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetOtherAssignedToVehicleAsync(Guid vehicleId, Guid excludedDriverId) =>
            Task.FromResult(drivers.FirstOrDefault(driver => driver.Id != excludedDriverId && driver.VehicleId == vehicleId));
        public Task AddAsync(Driver value) => Task.CompletedTask;
        public Task UpdateAsync(Driver value) => Task.CompletedTask;
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(drivers.ToList());
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(drivers.ToList());
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
        public IRouteRepository Routes { get; } = new FakeRoutes();
        public IRoutePositionRepository RoutePositions => null!;
        public int CommitCount { get; private set; }
        public Task<int> CommitAsync() { CommitCount++; return Task.FromResult(1); }
        public void Rollback() { }
    }

    private sealed class FakeRoutes : IRouteRepository
    {
        public Task<RouteE?> GetByIdAsync(Guid id) => Task.FromResult<RouteE?>(null);
        public Task AddAsync(RouteE route) => Task.CompletedTask;
        public Task UpdateAsync(RouteE route) => Task.CompletedTask;
        public Task<List<RouteE>> GetAllAsync(
            Guid? vehicleId = null,
            Guid? driverId = null,
            RouteStatus? status = null,
            bool? onlyActive = null) => Task.FromResult(new List<RouteE>());
    }
}
