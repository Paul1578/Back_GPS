using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Entities;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BackEnd_Gps.Tests;

public sealed class UnlinkDriverFromUserTests
{
    [Theory]
    [InlineData(RouteStatus.Pending)]
    [InlineData(RouteStatus.InProgress)]
    public async Task Unlink_RejectsDriverWithActiveRouteWithoutRevokingSessions(RouteStatus status)
    {
        var userId = Guid.NewGuid();
        var driver = Driver.Create("Ana", "Paz", "DOC-UNLINK", "0990000000", userId);
        var users = new RecordingUsers();
        var refreshTokens = new RecordingRefreshTokens();
        var uow = new RecordingUnitOfWork(users, refreshTokens);
        var command = new UnlinkDriverFromUserCommand(uow, new Drivers(driver), new Routes(driver.Id, status));

        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(driver.Id));

        Assert.Equal(userId, driver.UserId);
        Assert.Equal(0, users.TokenVersionIncrements);
        Assert.Equal(0, refreshTokens.Revocations);
        Assert.Equal(0, uow.Commits);
        Assert.Equal(1, uow.Rollbacks);
    }

    [Fact]
    public async Task Unlink_PreservesDriverAndUserRoleButRevokesOldSessions()
    {
        var userId = Guid.NewGuid();
        var driver = Driver.Create("Ana", "Paz", "DOC-UNLINK", "0990000000", userId);
        var users = new RecordingUsers();
        var refreshTokens = new RecordingRefreshTokens();
        var uow = new RecordingUnitOfWork(users, refreshTokens);
        var command = new UnlinkDriverFromUserCommand(uow, new Drivers(driver), new Routes(driver.Id, null));

        await command.Execute(driver.Id);

        Assert.Null(driver.UserId);
        Assert.Equal(1, users.TokenVersionIncrements);
        Assert.Equal(userId, users.LastTokenVersionUserId);
        Assert.Equal(1, refreshTokens.Revocations);
        Assert.Equal(userId, refreshTokens.LastUserId);
        Assert.Equal(1, uow.Commits);
        Assert.Equal(0, uow.Rollbacks);
        Assert.Equal(driver.Id, uow.LockedDriverId);
        Assert.Equal(userId, uow.LockedUserId);
    }

    [Fact]
    public async Task Unlink_PersistsNullUserIdThroughDriverRepository()
    {
        var ownerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"driver-unlink-persistence-{Guid.NewGuid()}")
            .Options;
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("owner_id", ownerId.ToString())], authenticationType: "test"))
            }
        };
        await using var context = new AppDbContext(options, accessor);
        context.Users.Add(new UserEntity
        {
            Id = userId,
            Username = "unlink-driver",
            Email = "unlink-driver@example.test",
            PasswordHash = "test-hash",
            OwnerUserId = ownerId
        });
        context.Drivers.Add(new DriverEntity
        {
            Id = driverId,
            UserId = userId,
            OwnerUserId = ownerId,
            FirstName = "Ana",
            LastName = "Paz",
            DocumentNumber = "DOC-UNLINK-DB",
            PhoneNumber = "0990000000",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var users = new RecordingUsers();
        var refreshTokens = new RecordingRefreshTokens();
        var driverRepository = new DriverRepository(context);
        var uow = new RecordingUnitOfWork(users, refreshTokens, async () => { await context.SaveChangesAsync(); });
        var command = new UnlinkDriverFromUserCommand(uow, driverRepository, new Routes(driverId, null));

        await command.Execute(driverId);

        context.ChangeTracker.Clear();
        var persistedDriver = await context.Drivers.IgnoreQueryFilters().AsNoTracking().SingleAsync(driver => driver.Id == driverId);
        Assert.Null(persistedDriver.UserId);
    }

    [Fact]
    public async Task Unlink_WithoutLinkedUserIsIdempotent()
    {
        var driver = Driver.Create("Ana", "Paz", "DOC-UNLINK", "0990000000");
        var users = new RecordingUsers();
        var refreshTokens = new RecordingRefreshTokens();
        var uow = new RecordingUnitOfWork(users, refreshTokens);
        var command = new UnlinkDriverFromUserCommand(uow, new Drivers(driver), new Routes(driver.Id, null));

        await command.Execute(driver.Id);

        Assert.Null(driver.UserId);
        Assert.Equal(0, users.TokenVersionIncrements);
        Assert.Equal(0, refreshTokens.Revocations);
        Assert.Equal(0, uow.Commits);
        Assert.Equal(1, uow.Rollbacks);
    }

    private sealed class Drivers(Driver driver) : IDriverRepository
    {
        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult(id == driver.Id ? driver : null);
        public Task<Driver?> GetByDocumentAsync(string documentNumber) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetByUserIdAsync(Guid userId) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetOtherAssignedToVehicleAsync(Guid vehicleId, Guid excludedDriverId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver value) => Task.CompletedTask;
        public Task UpdateAsync(Driver value) => Task.CompletedTask;
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(new List<Driver>());
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(new List<Driver>());
    }

    private sealed class Routes : IRouteRepository
    {
        private readonly Guid _driverId;
        private readonly RouteStatus? _status;

        public Routes(Guid driverId, RouteStatus? status)
        {
            _driverId = driverId;
            _status = status;
        }

        public Task<RouteE?> GetByIdAsync(Guid id) => Task.FromResult<RouteE?>(null);
        public Task AddAsync(RouteE route) => Task.CompletedTask;
        public Task UpdateAsync(RouteE route) => Task.CompletedTask;
        public Task<List<RouteE>> GetAllAsync(Guid? vehicleId = null, Guid? driverId = null, RouteStatus? status = null, bool? onlyActive = null)
        {
            if (driverId != _driverId || !_status.HasValue || onlyActive == false)
                return Task.FromResult(new List<RouteE>());
            var point = RoutePoint.Create(0, 0);
            return Task.FromResult(new List<RouteE>
            {
                RouteE.CreateExisting(Guid.NewGuid(), Guid.NewGuid(), _driverId, "Ruta", point, point,
                    new[] { point, point }, null, null, null, _status.Value, true)
            });
        }
    }

    private sealed class RecordingUsers : IUserRepository
    {
        public int TokenVersionIncrements { get; private set; }
        public Guid? LastTokenVersionUserId { get; private set; }
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult<User?>(null);
        public Task<User?> GetByIdAsync(Guid id) => Task.FromResult<User?>(null);
        public Task AddAsync(User user) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<bool> HasRoleAsync(Guid userId, Guid roleId) => Task.FromResult(false);
        public Task AssignRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
        public Task RemoveRoleAsync(Guid userId, Guid roleId) => Task.CompletedTask;
        public Task UpdatePasswordHashAsync(Guid userId, string newPasswordHash) => Task.CompletedTask;
        public Task UpdatePasswordAndClearFlagAsync(Guid userId, string newPasswordHash, bool clearMustChangePassword) => Task.CompletedTask;
        public Task IncrementTokenVersionAsync(Guid userId) { TokenVersionIncrements++; LastTokenVersionUserId = userId; return Task.CompletedTask; }
        public Task<List<User>> GetByOwnerAsync(Guid ownerUserId) => Task.FromResult(new List<User>());
    }

    private sealed class RecordingRefreshTokens : IRefreshTokenRepository
    {
        public int Revocations { get; private set; }
        public Guid? LastUserId { get; private set; }
        public Task AddAsync(RefreshToken token) => Task.CompletedTask;
        public Task<RefreshToken?> GetByTokenAsync(string rawToken) => Task.FromResult<RefreshToken?>(null);
        public Task RevokeAsync(Guid id, string? replacedByTokenHash = null) => Task.CompletedTask;
        public Task<bool> TryRotateAsync(Guid currentTokenId, RefreshToken replacement) => Task.FromResult(false);
        public Task RemoveExpiredAsync() => Task.CompletedTask;
        public Task RevokeAllForUserAsync(Guid userId) { Revocations++; LastUserId = userId; return Task.CompletedTask; }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly Func<Task>? _saveChanges;

        public RecordingUnitOfWork(IUserRepository users, IRefreshTokenRepository refreshTokens, Func<Task>? saveChanges = null)
        {
            _users = users;
            _refreshTokens = refreshTokens;
            _saveChanges = saveChanges;
        }

        public int Commits { get; private set; }
        public int Rollbacks { get; private set; }
        public Guid? LockedDriverId { get; private set; }
        public Guid? LockedUserId { get; private set; }
        public IUserRepository Users => _users;
        public IRefreshTokenRepository RefreshTokens => _refreshTokens;
        public IRoleRepository Roles => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => null!;
        public IRoutePositionRepository RoutePositions => null!;
        public async Task<int> CommitAsync() { if (_saveChanges is not null) await _saveChanges(); Commits++; return 1; }
        public Task LockRouteAssignmentAsync(Guid? vehicleId, Guid? driverId, Guid? routeId = null) { LockedDriverId = driverId; return Task.CompletedTask; }
        public Task LockLinkedUserForRouteAssignmentAsync(Guid userId) { LockedUserId = userId; return Task.CompletedTask; }
        public void Rollback() => Rollbacks++;
    }
}
