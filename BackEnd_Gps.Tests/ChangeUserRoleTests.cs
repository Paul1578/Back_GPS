using fletflow.Application.Auth.Commands;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace BackEnd_Gps.Tests;

public class ChangeUserRoleTests
{
    [Fact]
    public async Task Execute_ReplacesRolesAndCommitsOnce()
    {
        var adminRole = new Role { Name = "Admin" };
        var logisticsRole = new Role { Name = "Logistics" };
        var user = new User();
        user.UserRoles.Add(new UserRole { User = user, Role = adminRole });
        var users = new RecordingUserRepository(user, adminRole.Id);
        var uow = new RecordingUnitOfWork(users, new RecordingRoleRepository(adminRole, logisticsRole));

        await new ChangeUserRoleCommand(uow).Execute(user.Id, "Logistics");

        Assert.Equal(new[] { logisticsRole.Id }, users.RoleIds);
        Assert.Equal(1, user.TokenVersion);
        Assert.Equal(1, uow.CommitCount);
        Assert.Equal(1, ((RecordingRefreshTokenRepository)uow.RefreshTokens).RevokeAllCount);
    }

    [Fact]
    public async Task Execute_DoesNotInvalidateSessionsWhenRoleIsUnchanged()
    {
        var logisticsRole = new Role { Name = "Logistics" };
        var user = new User();
        user.UserRoles.Add(new UserRole { User = user, Role = logisticsRole });
        var users = new RecordingUserRepository(user, logisticsRole.Id);
        var uow = new RecordingUnitOfWork(users, new RecordingRoleRepository(logisticsRole));

        await new ChangeUserRoleCommand(uow).Execute(user.Id, "Logistics");

        Assert.Equal(0, user.TokenVersion);
        Assert.Equal(0, ((RecordingRefreshTokenRepository)uow.RefreshTokens).RevokeAllCount);
        Assert.Equal(1, uow.CommitCount);
    }

    [Fact]
    public async Task Execute_DeactivatesLinkedDriverAndUnassignsVehicleWhenRoleChangesAway()
    {
        var adminRole = new Role { Name = "Admin" };
        var logisticsRole = new Role { Name = "Logistics" };
        var user = new User();
        user.UserRoles.Add(new UserRole { User = user, Role = adminRole });
        var driver = Driver.Create("Ana", "Paz", "100", "099", user.Id);
        var vehicleId = Guid.NewGuid();
        driver.AssignToVehicle(vehicleId);
        var users = new RecordingUserRepository(user, adminRole.Id);
        var uow = new RecordingUnitOfWork(users, new RecordingRoleRepository(adminRole, logisticsRole), driver);

        await new ChangeUserRoleCommand(uow).Execute(user.Id, "Logistics");

        Assert.False(driver.IsActive);
        Assert.Null(driver.VehicleId);
        Assert.Equal(((Guid?)vehicleId, (Guid?)driver.Id), uow.LockedAssignment);
        Assert.Equal(new[] { logisticsRole.Id }, users.RoleIds);
        Assert.Equal(1, user.TokenVersion);
        Assert.Equal(1, uow.CommitCount);
        Assert.Equal(1, ((RecordingRefreshTokenRepository)uow.RefreshTokens).RevokeAllCount);
    }

    [Fact]
    public async Task Execute_RejectsRoleChangeWhileLinkedDriverHasRouteInProgress()
    {
        var adminRole = new Role { Name = "Admin" };
        var logisticsRole = new Role { Name = "Logistics" };
        var user = new User();
        user.UserRoles.Add(new UserRole { User = user, Role = adminRole });
        var driver = Driver.Create("Ana", "Paz", "100", "099", user.Id);
        var users = new RecordingUserRepository(user, adminRole.Id);
        var uow = new RecordingUnitOfWork(
            users,
            new RecordingRoleRepository(adminRole, logisticsRole),
            driver,
            hasActiveRoute: true);

        await Assert.ThrowsAsync<BusinessConflictException>(() => new ChangeUserRoleCommand(uow).Execute(user.Id, "Logistics"));

        Assert.True(driver.IsActive);
        Assert.Equal(new[] { adminRole.Id }, users.RoleIds);
        Assert.Equal(0, user.TokenVersion);
        Assert.Equal(0, uow.CommitCount);
        Assert.Equal(1, uow.RollbackCount);
    }

    private sealed class RecordingUserRepository(User user, Guid existingRoleId) : IUserRepository
    {
        public HashSet<Guid> RoleIds { get; } = new() { existingRoleId };
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(null);
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult(id == user.Id ? user : null);
        public Task AddAsync(User value) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<bool> HasRoleAsync(Guid userId, Guid roleId) => Task.FromResult(RoleIds.Contains(roleId));
        public Task AssignRoleAsync(Guid userId, Guid roleId) { RoleIds.Add(roleId); return Task.CompletedTask; }
        public Task RemoveRoleAsync(Guid userId, Guid roleId) { RoleIds.Remove(roleId); return Task.CompletedTask; }
        public Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash) => Task.CompletedTask;
        public Task UpdatePasswordAndClearFlagAsync(Guid userId, string newPasswordHash, bool clearMustChangePassword) => Task.CompletedTask;
        public Task IncrementTokenVersionAsync(Guid userId) { user.TokenVersion++; return Task.CompletedTask; }
        public Task<List<User>> GetByOwnerAsync(Guid ownerUserId) => Task.FromResult(new List<User>());
    }

    private sealed class RecordingRoleRepository(params Role[] roles) : IRoleRepository
    {
        public Task<Role?> GetByNameAsync(string name) => Task.FromResult(roles.FirstOrDefault(role => role.Name == name));
        public Task AddAsync(Role role) => Task.CompletedTask;
        public Task<bool> DeleteByNameAsync(string name) => Task.FromResult(false);
        public Task<IReadOnlyList<Role>> GetAllAsync() => Task.FromResult<IReadOnlyList<Role>>(roles);
    }

    private sealed class RecordingDriverRepository(Driver? driver) : IDriverRepository
    {
        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult(driver?.Id == id ? driver : null);
        public Task<Driver?> GetByDocumentAsync(string documentNumber) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetByUserIdAsync(Guid userId) => Task.FromResult(driver?.UserId == userId ? driver : null);
        public Task<Driver?> GetOtherAssignedToVehicleAsync(Guid vehicleId, Guid excludedDriverId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver value) => Task.CompletedTask;
        public Task UpdateAsync(Driver value) => Task.CompletedTask;
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(driver is null ? new List<Driver>() : new List<Driver> { driver });
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(driver is null ? new List<Driver>() : new List<Driver> { driver });
    }

    private sealed class RecordingRouteRepository(bool hasActiveRoute) : IRouteRepository
    {
        public Task<RouteE?> GetByIdAsync(Guid id) => Task.FromResult<RouteE?>(null);
        public Task AddAsync(RouteE route) => Task.CompletedTask;
        public Task UpdateAsync(RouteE route) => Task.CompletedTask;
        public Task<List<RouteE>> GetAllAsync(Guid? vehicleId = null, Guid? driverId = null, RouteStatus? status = null, bool? onlyActive = null)
        {
            if (!hasActiveRoute) return Task.FromResult(new List<RouteE>());
            var point = RoutePoint.Create(0, 0);
            return Task.FromResult(new List<RouteE>
            {
                RouteE.CreateExisting(Guid.NewGuid(), Guid.NewGuid(), driverId!.Value, "Ruta", point, point,
                    new[] { point, point }, null, null, null, RouteStatus.InProgress, true)
            });
        }
    }

    private sealed class RecordingUnitOfWork(
        IUserRepository users,
        IRoleRepository roles,
        Driver? driver = null,
        bool hasActiveRoute = false) : IUnitOfWork
    {
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }
        public (Guid? VehicleId, Guid? DriverId)? LockedAssignment { get; private set; }
        public IUserRepository Users => users;
        public IRoleRepository Roles => roles;
        public IRefreshTokenRepository RefreshTokens { get; } = new RecordingRefreshTokenRepository();
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => new RecordingDriverRepository(driver);
        public IRouteRepository Routes => new RecordingRouteRepository(hasActiveRoute);
        public IRoutePositionRepository RoutePositions => null!;
        public Task<int> CommitAsync() { CommitCount++; return Task.FromResult(1); }
        public Task LockRouteAssignmentAsync(Guid? vehicleId, Guid? driverId, Guid? routeId = null)
        {
            LockedAssignment = (vehicleId, driverId);
            return Task.CompletedTask;
        }
        public void Rollback() { RollbackCount++; }
    }

    private sealed class RecordingRefreshTokenRepository : IRefreshTokenRepository
    {
        public int RevokeAllCount { get; private set; }
        public Task AddAsync(RefreshToken token) => Task.CompletedTask;
        public Task<RefreshToken?> GetByTokenAsync(string rawToken) => Task.FromResult<RefreshToken?>(null);
        public Task RevokeAsync(Guid id, string? replacedByTokenHash = null) => Task.CompletedTask;
        public Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement) => Task.FromResult(false);
        public Task RemoveExpiredAsync() => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId) { RevokeAllCount++; return Task.CompletedTask; }
    }
}
