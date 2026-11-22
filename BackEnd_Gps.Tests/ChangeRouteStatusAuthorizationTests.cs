using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace BackEnd_Gps.Tests;

public sealed class ChangeRouteStatusAuthorizationTests
{
    [Fact]
    public async Task Execute_DriverReassignedAfterInitialRead_CannotChangeRouteStatus()
    {
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicle = Vehicle.Create("AUTH-001", "QA", "Van", 2025, null);
        var originalRoute = CreateRoute(vehicle.Id, driverId);
        var routes = new MutableRoutes(originalRoute);
        var unitOfWork = new ReassignOnLockUnitOfWork(routes, vehicle.Id, Guid.NewGuid());
        var drivers = new Drivers(Driver.Create("QA", "Driver", "AUTH-D1", "0990000000", userId));
        var command = new ChangeRouteStatusCommand(unitOfWork, routes, new Vehicles(vehicle), drivers, null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            command.Execute(originalRoute.Id, RouteStatus.Cancelled, driverId, userId));

        Assert.Equal(1, unitOfWork.LockCount);
        Assert.Equal(0, routes.UpdateCount);
        Assert.Equal(RouteStatus.Pending, (await routes.GetByIdAsync(originalRoute.Id))!.Status);
    }

    private static RouteE CreateRoute(Guid vehicleId, Guid driverId)
    {
        var origin = RoutePoint.Create(-0.2, -78.5, "Origen");
        var destination = RoutePoint.Create(-0.21, -78.51, "Destino");
        return RouteE.CreateExisting(Guid.NewGuid(), vehicleId, driverId, "QA autorización",
            origin, destination, new[] { origin, destination }, null, null, null,
            RouteStatus.Pending, true);
    }

    private sealed class MutableRoutes(RouteE route) : IRouteRepository
    {
        public RouteE Current { get; set; } = route;
        public int UpdateCount { get; private set; }
        public Task<RouteE?> GetByIdAsync(Guid id) => Task.FromResult<RouteE?>(Current.Id == id ? Current : null);
        public Task AddAsync(RouteE value) => Task.CompletedTask;
        public Task UpdateAsync(RouteE value) { UpdateCount++; Current = value; return Task.CompletedTask; }
        public Task<List<RouteE>> GetAllAsync(Guid? vehicleId = null, Guid? driverId = null,
            RouteStatus? status = null, bool? onlyActive = null) =>
            Task.FromResult(new[] { Current }.Where(item =>
                (!vehicleId.HasValue || item.VehicleId == vehicleId)
                && (!driverId.HasValue || item.DriverId == driverId)
                && (!status.HasValue || item.Status == status)
                && (!onlyActive.HasValue || item.IsActive == onlyActive)).ToList());
    }

    private sealed class ReassignOnLockUnitOfWork : IUnitOfWork
    {
        private readonly MutableRoutes _routes;
        private readonly Guid _vehicleId;
        private readonly Guid _newDriverId;

        public ReassignOnLockUnitOfWork(MutableRoutes routes, Guid vehicleId, Guid newDriverId)
        {
            _routes = routes;
            _vehicleId = vehicleId;
            _newDriverId = newDriverId;
        }

        public int LockCount { get; private set; }
        public IUserRepository Users => null!;
        public IRoleRepository Roles => null!;
        public IRefreshTokenRepository RefreshTokens => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => _routes;
        public IRoutePositionRepository RoutePositions => null!;
        public Task<int> CommitAsync() => Task.FromResult(1);
        public Task LockRouteAssignmentAsync(Guid? vehicleId, Guid? driverId, Guid? routeId = null)
        {
            LockCount++;
            var previous = _routes.Current;
            var origin = RoutePoint.Create(-0.2, -78.5, "Origen");
            var destination = RoutePoint.Create(-0.21, -78.51, "Destino");
            _routes.Current = RouteE.CreateExisting(previous.Id, vehicleId ?? _vehicleId,
                _newDriverId, previous.Name, origin, destination, new[] { origin, destination },
                null, null, null, previous.Status, previous.IsActive);
            return Task.CompletedTask;
        }
        public void Rollback() { }
    }

    private sealed class Vehicles(Vehicle vehicle) : IVehicleRepository
    {
        public Task<Vehicle?> GetByIdAsync(Guid id) => Task.FromResult<Vehicle?>(vehicle.Id == id ? vehicle : null);
        public Task<Vehicle?> GetByPlateAsync(string plate) => Task.FromResult<Vehicle?>(null);
        public Task AddAsync(Vehicle value) => Task.CompletedTask;
        public Task UpdateAsync(Vehicle value) => Task.CompletedTask;
        public Task<List<Vehicle>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(new List<Vehicle> { vehicle });
        public Task<List<Vehicle>> GetByIdsAsync(IReadOnlyCollection<Guid> ids) => Task.FromResult(new List<Vehicle> { vehicle });
    }

    private sealed class Drivers(Driver driver) : IDriverRepository
    {
        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult<Driver?>(driver.Id == id ? driver : null);
        public Task<Driver?> GetByDocumentAsync(string documentNumber) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetByUserIdAsync(Guid id) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetOtherAssignedToVehicleAsync(Guid vehicleId, Guid excludedDriverId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver value) => Task.CompletedTask;
        public Task UpdateAsync(Driver value) => Task.CompletedTask;
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(new List<Driver> { driver });
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(new List<Driver> { driver });
    }
}
