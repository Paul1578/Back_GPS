using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using fletflow.Infrastructure.Persistence.Repositories;
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

    [Fact]
    public async Task CreateDriver_ConcurrentDocumentInsertReturnsBusinessConflict()
    {
        var ownerId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var accessor = CreateOwnerAccessor(ownerId);
        await using (var setup = new AppDbContext(options, accessor))
            await setup.Database.EnsureCreatedAsync();

        await using var context = new AppDbContext(options, accessor);
        await using var competingContext = new AppDbContext(options, accessor);
        var unitOfWork = new CommitInterleavingUnitOfWork(context, async () =>
        {
            competingContext.Drivers.Add(new DriverEntity
            {
                Id = Guid.NewGuid(),
                FirstName = "Concurrent",
                LastName = "Winner",
                DocumentNumber = "DOC-CREATE-RACE",
                PhoneNumber = "0990000000",
                IsActive = true
            });
            await competingContext.SaveChangesAsync();
        });
        var command = new CreateDriverCommand(unitOfWork, new DriverRepository(context));

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute("Concurrent", "Loser", "DOC-CREATE-RACE", "0990000001"));

        Assert.Contains("Ya existe un conductor", error.Message);
        Assert.Equal(1, await context.Drivers.CountAsync(driver => driver.DocumentNumber == "DOC-CREATE-RACE"));
    }

    [Fact]
    public async Task UpdateDriver_ConcurrentDocumentChangeReturnsBusinessConflict()
    {
        var ownerId = Guid.NewGuid();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var accessor = CreateOwnerAccessor(ownerId);
        var currentId = Guid.NewGuid();
        var competingId = Guid.NewGuid();
        await using (var setup = new AppDbContext(options, accessor))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Drivers.AddRange(
                NewDriverEntity(currentId, "Current", "DOC-UPDATE-OLD", ownerId),
                NewDriverEntity(competingId, "Concurrent", "DOC-UPDATE-OTHER", ownerId));
            await setup.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options, accessor);
        await using var competingContext = new AppDbContext(options, accessor);
        var unitOfWork = new CommitInterleavingUnitOfWork(context, async () =>
        {
            var other = await competingContext.Drivers.SingleAsync(driver => driver.Id == competingId);
            other.DocumentNumber = "DOC-UPDATE-RACE";
            await competingContext.SaveChangesAsync();
        });
        var command = new UpdateDriverCommand(unitOfWork, new DriverRepository(context), context);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute(currentId, "Current", "Driver", "DOC-UPDATE-RACE", "0990000000"));

        Assert.Contains("Ya existe otro conductor", error.Message);
        Assert.Equal("DOC-UPDATE-OLD", await context.Drivers.Where(driver => driver.Id == currentId)
            .Select(driver => driver.DocumentNumber).SingleAsync());
        Assert.Equal("DOC-UPDATE-RACE", await context.Drivers.Where(driver => driver.Id == competingId)
            .Select(driver => driver.DocumentNumber).SingleAsync());
    }

    [Fact]
    public async Task CreateDriver_RethrowsDatabaseFailureWhenDocumentConflictIsNotConfirmed()
    {
        var repository = new FakeDriverRepository();
        var unitOfWork = new FakeUnitOfWork { CommitException = new DbUpdateException("Non-unique database failure.") };
        var command = new CreateDriverCommand(unitOfWork, repository);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            command.Execute("New", "Driver", "DOC-NO-CONFLICT", "0990000000"));
    }

    [Fact]
    public async Task UpdateDriver_RethrowsDatabaseFailureWhenOtherDriverDoesNotOwnDocument()
    {
        var current = Driver.Create("Current", "Driver", "DOC-NOCONFLICT1", "0990000000");
        var repository = new FakeDriverRepository(current);
        var unitOfWork = new FakeUnitOfWork { CommitException = new DbUpdateException("Non-unique database failure.") };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"driver-update-db-error-{Guid.NewGuid()}")
            .Options;
        await using var context = new AppDbContext(options);
        var command = new UpdateDriverCommand(unitOfWork, repository, context);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            command.Execute(current.Id, "Current", "Driver", "DOC-NOCONFLICT1", "0990000000"));
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

    [Theory]
    [InlineData("", "Paz", "DOC-001", "0990000000")]
    [InlineData("Ana", " ", "DOC-001", "0990000000")]
    [InlineData("Ana", "Paz", " ", "0990000000")]
    [InlineData("Ana", "Paz", "DOC-001", "")]
    public async Task CreateDriver_RejectsMissingRequiredFields(string firstName, string lastName, string documentNumber, string phoneNumber)
    {
        var repository = new FakeDriverRepository();
        var unitOfWork = new FakeUnitOfWork();
        var command = new CreateDriverCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            command.Execute(firstName, lastName, documentNumber, phoneNumber));

        Assert.Contains("son obligatorios", error.Message);
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
    public async Task UpdateDriver_RereadsAssignmentAfterLockAndPreservesConcurrentVehicleAssignment()
    {
        var vehicleId = Guid.NewGuid();
        var repository = new ConcurrentAssignmentDriverRepository();
        var unitOfWork = new FakeUnitOfWork
        {
            OnLockRouteAssignment = () => repository.AssignVehicle(vehicleId)
        };
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"driver-update-assignment-{Guid.NewGuid()}")
            .Options;
        await using var context = new AppDbContext(options);
        var command = new UpdateDriverCommand(unitOfWork, repository, context);

        await command.Execute(repository.DriverId, "Updated", "Name", "DOC-CONCURRENT", "0990000000");

        Assert.Equal(vehicleId, repository.PersistedVehicleId);
        Assert.Equal(1, repository.UpdateCount);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(repository.DriverId, unitOfWork.LockedDriverId);
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

    [Fact]
    public async Task ChangeStatus_RejectsDeactivationWhileDriverHasPendingRoute()
    {
        var driver = Driver.Create("Ana", "Paz", "100", "099");
        var vehicleId = Guid.NewGuid();
        driver.AssignToVehicle(vehicleId);
        var routes = new FakeRouteRepository(driver.Id, RouteStatus.Pending);
        var unitOfWork = new FakeUnitOfWork();
        var command = new ChangeDriverStatusCommand(unitOfWork, new FakeDriverRepository(driver), routes);

        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(driver.Id, false));

        Assert.True(driver.IsActive);
        Assert.Equal(vehicleId, driver.VehicleId);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    [Fact]
    public async Task UnassignVehicle_RejectsWhenDriverHasPendingRoute()
    {
        var driver = Driver.Create("Ana", "Paz", "100", "099");
        var vehicleId = Guid.NewGuid();
        driver.AssignToVehicle(vehicleId);
        var routes = new FakeRouteRepository(driver.Id, RouteStatus.Pending);
        var unitOfWork = new FakeUnitOfWork();
        var command = new UnassignDriverFromVehicleCommand(unitOfWork, new FakeDriverRepository(driver), routes);

        await Assert.ThrowsAsync<BusinessConflictException>(() => command.Execute(driver.Id));

        Assert.Equal(vehicleId, driver.VehicleId);
        Assert.Equal(0, unitOfWork.CommitCount);
    }

    private static IHttpContextAccessor CreateOwnerAccessor(Guid ownerId) => new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("owner_id", ownerId.ToString())],
                authenticationType: "test"))
        }
    };

    private static DriverEntity NewDriverEntity(Guid id, string firstName, string documentNumber, Guid ownerId) => new()
    {
        Id = id,
        FirstName = firstName,
        LastName = "Driver",
        DocumentNumber = documentNumber,
        PhoneNumber = "0990000000",
        IsActive = true,
        OwnerUserId = ownerId
    };

    private sealed class CommitInterleavingUnitOfWork(AppDbContext context, Func<Task> beforeCommit) : IUnitOfWork
    {
        public IUserRepository Users => null!;
        public IRoleRepository Roles => null!;
        public IRefreshTokenRepository RefreshTokens => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => null!;
        public IRoutePositionRepository RoutePositions => null!;
        public async Task<int> CommitAsync()
        {
            await beforeCommit();
            return await context.SaveChangesAsync();
        }
        public void Rollback() { }
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
        public Task<Driver?> GetOtherAssignedToVehicleAsync(Guid vehicleId, Guid excludedDriverId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver value) { AddCount++; return Task.CompletedTask; }
        public Task UpdateAsync(Driver value) { UpdateCount++; return Task.CompletedTask; }
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(_drivers);
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(_drivers.Where(driver => driver.IsActive).ToList());
    }

    private sealed class ConcurrentAssignmentDriverRepository : IDriverRepository
    {
        private Guid? _vehicleId;
        public Guid DriverId { get; } = Guid.NewGuid();
        public Guid? PersistedVehicleId => _vehicleId;
        public int UpdateCount { get; private set; }

        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult<Driver?>(id == DriverId
            ? Driver.CreateExisting(DriverId, "Before", "Name", "DOC-CONCURRENT", "0990000000", true, _vehicleId, null)
            : null);
        public Task<Driver?> GetByDocumentAsync(string documentNumber) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetByUserIdAsync(Guid userId) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetOtherAssignedToVehicleAsync(Guid vehicleId, Guid excludedDriverId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver value) => Task.CompletedTask;
        public Task UpdateAsync(Driver value)
        {
            UpdateCount++;
            _vehicleId = value.VehicleId;
            return Task.CompletedTask;
        }
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(new List<Driver>());
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(new List<Driver>());
        public void AssignVehicle(Guid vehicleId) => _vehicleId = vehicleId;
    }

    private sealed class FakeRouteRepository(Guid driverId, RouteStatus status) : IRouteRepository
    {
        public Task<RouteE?> GetByIdAsync(Guid id) => Task.FromResult<RouteE?>(null);
        public Task AddAsync(RouteE route) => Task.CompletedTask;
        public Task UpdateAsync(RouteE route) => Task.CompletedTask;
        public Task<List<RouteE>> GetAllAsync(Guid? vehicleId = null, Guid? driverIdFilter = null, RouteStatus? statusFilter = null, bool? onlyActive = null)
        {
            if (driverIdFilter != driverId || statusFilter.HasValue && statusFilter != status || onlyActive == false)
                return Task.FromResult(new List<RouteE>());
            var point = RoutePoint.Create(0, 0);
            var route = RouteE.CreateExisting(Guid.NewGuid(), Guid.NewGuid(), driverId, "Ruta", point, point,
                new[] { point, point }, null, null, null, status, true);
            return Task.FromResult(new List<RouteE> { route });
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int CommitCount { get; private set; }
        public Exception? CommitException { get; init; }
        public Action? OnLockRouteAssignment { get; init; }
        public Guid? LockedDriverId { get; private set; }
        public IUserRepository Users => null!;
        public IRoleRepository Roles => null!;
        public IRefreshTokenRepository RefreshTokens => null!;
        public IPasswordResetTokenRepository PasswordResetTokens => null!;
        public IVehicleRepository Vehicles => null!;
        public IDriverRepository Drivers => null!;
        public IRouteRepository Routes => null!;
        public IRoutePositionRepository RoutePositions => null!;
        public Task<int> CommitAsync()
        {
            CommitCount++;
            if (CommitException is not null)
                throw CommitException;
            return Task.FromResult(1);
        }
        public Task LockRouteAssignmentAsync(Guid? vehicleId, Guid? driverId, Guid? routeId = null)
        {
            LockedDriverId = driverId;
            OnLockRouteAssignment?.Invoke();
            return Task.CompletedTask;
        }
        public void Rollback() { }
    }
}
