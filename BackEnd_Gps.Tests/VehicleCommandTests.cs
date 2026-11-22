using fletflow.Application.Fleet.Commands;
using fletflow.Domain.Auth.Repositories;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

namespace BackEnd_Gps.Tests;

public class VehicleCommandTests
{
    [Fact]
    public async Task CreateVehicle_RejectsDuplicatePlateAsBusinessConflict()
    {
        var existing = Vehicle.Create("QA-001", "FleetFlow", "Van", 2025, null);
        var repository = new FakeVehicles(existing);
        var unitOfWork = new FakeUnitOfWork();
        var command = new CreateVehicleCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute("QA-001", "Other", "Van", 2025, null));

        Assert.Contains("Ya existe un vehículo", error.Message);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task UpdateVehicle_RejectsPlateOwnedByAnotherVehicle()
    {
        var current = Vehicle.Create("QA-001", "FleetFlow", "Van", 2025, null);
        var existing = Vehicle.Create("QA-002", "FleetFlow", "Truck", 2025, null);
        var repository = new FakeVehicles(existing, current);
        var unitOfWork = new FakeUnitOfWork();
        var command = new UpdateVehicleCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute(current.Id, "QA-002", "FleetFlow", "Van", 2025, null));

        Assert.Contains("Ya existe otro vehículo", error.Message);
        Assert.Equal("QA-001", current.Plate);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task CreateVehicle_RejectsValuesOutsidePersistenceLimits()
    {
        var repository = new FakeVehicles();
        var unitOfWork = new FakeUnitOfWork();
        var command = new CreateVehicleCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            command.Execute(new string('A', 21), "FleetFlow", "Van", 2025, null));

        Assert.Contains("placa", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, unitOfWork.CommitCount);
        Assert.Equal(0, repository.AddCount);
    }

    [Fact]
    public async Task CreateVehicle_MapsConcurrentDuplicatePlateToConflict()
    {
        var repository = new FakeVehicles();
        var unitOfWork = new FakeUnitOfWork(() =>
        {
            repository.AddExternal(Vehicle.Create("RACE-001", "FleetFlow", "Van", 2025, null));
            throw new DbUpdateException("Unique owner/plate index conflict.", new Exception("duplicate"));
        });
        var command = new CreateVehicleCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute("RACE-001", "FleetFlow", "Van", 2025, null));

        Assert.Contains("Ya existe un vehículo", error.Message);
        Assert.Equal(1, unitOfWork.CommitCount);
        Assert.Equal(1, repository.AddCount);
    }

    [Fact]
    public async Task UpdateVehicle_MapsConcurrentDuplicatePlateToConflictWithoutChangingPersistedVehicle()
    {
        var current = Vehicle.Create("QA-001", "FleetFlow", "Van", 2025, null);
        var repository = new FakeVehicles(current);
        var unitOfWork = new FakeUnitOfWork(() =>
        {
            repository.AddExternal(Vehicle.Create("RACE-002", "FleetFlow", "Truck", 2025, null));
            throw new DbUpdateException("Unique owner/plate index conflict.", new Exception("duplicate"));
        });
        var command = new UpdateVehicleCommand(unitOfWork, repository);

        var error = await Assert.ThrowsAsync<BusinessConflictException>(() =>
            command.Execute(current.Id, "RACE-002", "FleetFlow", "Van", 2025, null));

        Assert.Contains("Ya existe otro vehículo", error.Message);
        Assert.Equal(1, repository.UpdateCount);
    }

    private sealed class FakeVehicles(params Vehicle[] vehicles) : IVehicleRepository
    {
        private readonly List<Vehicle> _vehicles = vehicles.ToList();
        public int AddCount { get; private set; }
        public int UpdateCount { get; private set; }

        public void AddExternal(Vehicle vehicle) => _vehicles.Add(vehicle);

        public Task<Vehicle?> GetByIdAsync(Guid id) =>
            Task.FromResult(_vehicles.FirstOrDefault(vehicle => vehicle.Id == id) is { } vehicle
                ? Vehicle.CreateExisting(vehicle.Id, vehicle.Plate, vehicle.Brand, vehicle.Model, vehicle.Year,
                    vehicle.Description, vehicle.IsActive, vehicle.Status)
                : null);

        public Task<Vehicle?> GetByPlateAsync(string plate) =>
            Task.FromResult(_vehicles.FirstOrDefault(vehicle =>
                string.Equals(vehicle.Plate, plate, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Vehicle vehicle)
        {
            AddCount++;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Vehicle vehicle)
        {
            UpdateCount++;
            return Task.CompletedTask;
        }

        public Task<List<Vehicle>> GetAllAsync(bool? onlyActive = null) =>
            Task.FromResult(_vehicles);

        public Task<List<Vehicle>> GetByIdsAsync(IReadOnlyCollection<Guid> ids) =>
            Task.FromResult(_vehicles.Where(vehicle => ids.Contains(vehicle.Id)).ToList());
    }

    private sealed class FakeUnitOfWork(Func<Task<int>>? commit = null) : IUnitOfWork
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

        public Task<int> CommitAsync()
        {
            CommitCount++;
            if (commit is not null)
                return commit();
            return Task.FromResult(1);
        }

        public void Rollback() { }
    }
}
