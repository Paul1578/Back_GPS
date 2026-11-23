using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace BackEnd_Gps.Tests;

public sealed class RegisterRoutePositionAuthorizationTests
{
    [Fact]
    public async Task Execute_DriverReassignedBeforeLockCannotRegisterGps()
    {
        var vehicle = Vehicle.Create("GPS-AUTH-001", "QA", "Van", 2025, null);
        var driverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherDriverId = Guid.NewGuid();
        var origin = RoutePoint.Create(-0.2, -78.5, "Origin");
        var destination = RoutePoint.Create(-0.21, -78.51, "Destination");
        var route = RouteE.CreateExisting(Guid.NewGuid(), vehicle.Id, driverId, "GPS authorization",
            origin, destination, [origin, destination], null, null, null,
            RouteStatus.InProgress, true);
        var routes = new MutableRoutes(route, otherDriverId);
        var positions = new RecordingPositions();
        var unitOfWork = new ReassignOnLockUnitOfWork(routes);
        var drivers = new Drivers(Driver.Create("QA", "Driver", "GPS-AUTH-D", "0990000000", userId));
        var command = new RegisterRoutePositionCommand(unitOfWork, routes, positions, drivers);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => command.Execute(
            route.Id, -0.2, -78.5, null, null, null, driverId, userId));

        Assert.Equal(1, unitOfWork.LockCount);
        Assert.Empty(positions.Items);
        Assert.Equal(otherDriverId, (await routes.GetByIdAsync(route.Id))!.DriverId);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    private sealed class MutableRoutes(RouteE route, Guid newDriverId) : IRouteRepository
    {
        public RouteE Current { get; private set; } = route;
        public void Reassign()
        {
            var origin = RoutePoint.Create(-0.2, -78.5, "Origin");
            var destination = RoutePoint.Create(-0.21, -78.51, "Destination");
            Current = RouteE.CreateExisting(Current.Id, Current.VehicleId, newDriverId, Current.Name,
                origin, destination, [origin, destination], null, null, null,
                Current.Status, Current.IsActive);
        }
        public Task<RouteE?> GetByIdAsync(Guid id) => Task.FromResult<RouteE?>(Current.Id == id ? Current : null);
        public Task AddAsync(RouteE value) => Task.CompletedTask;
        public Task UpdateAsync(RouteE value) { Current = value; return Task.CompletedTask; }
        public Task<List<RouteE>> GetAllAsync(Guid? vehicleId = null, Guid? driverId = null,
            RouteStatus? status = null, bool? onlyActive = null) => Task.FromResult(new List<RouteE> { Current });
    }

    private sealed class RecordingPositions : IRoutePositionRepository
    {
        public List<RoutePosition> Items { get; } = [];
        public Task AddAsync(RoutePosition position) { Items.Add(position); return Task.CompletedTask; }
        public Task<List<RoutePosition>> GetByRouteAsync(Guid routeId, DateTime? from = null, DateTime? to = null) =>
            Task.FromResult(Items.Where(item => item.RouteId == routeId).ToList());
    }

    private sealed class ReassignOnLockUnitOfWork(MutableRoutes routes) : IUnitOfWork
    {
        public int LockCount { get; private set; }
        public int CommitCount { get; private set; }
        public IUserRepository Users => null!;
        public IRoleRepository Roles => null!;
        public IRefreshTokenRepository RefreshTokens => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => routes;
        public IRoutePositionRepository RoutePositions => null!;
        public Task<int> CommitAsync() { CommitCount++; return Task.FromResult(1); }
        public Task LockRouteAssignmentAsync(Guid? vehicleId, Guid? driverId, Guid? routeId = null)
        {
            LockCount++;
            routes.Reassign();
            return Task.CompletedTask;
        }
        public void Rollback() { }
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
