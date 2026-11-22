using fletflow.Application.Fleet.Queries;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;

namespace BackEnd_Gps.Tests;

public sealed class GetRouteAssignmentOptionsQueryTests
{
    [Fact]
    public async Task Execute_ReturnsOnlyAvailableActiveVehiclesAndAssignableDrivers()
    {
        var available = Vehicle.Create("QA-AVAILABLE", "FleetFlow", "Van", 2025, null);
        var maintenance = Vehicle.Create("QA-MAINTENANCE", "FleetFlow", "Truck", 2024, null);
        maintenance.SetStatus(VehicleStatus.Maintenance);
        var onRoute = Vehicle.Create("QA-ON-ROUTE", "FleetFlow", "Van", 2023, null);
        onRoute.SetStatus(VehicleStatus.OnRoute);
        var unlinkedDriver = Driver.Create("Ana", "Paz", "QA-100", "0990000000");
        var linkedDriver = Driver.Create("Luis", "Vega", "QA-101", "0990000001", Guid.NewGuid());

        var query = new GetRouteAssignmentOptionsQuery(
            new FakeVehicleRepository(available, maintenance, onRoute),
            new FakeDriverRepository(unlinkedDriver, linkedDriver));

        var result = await query.Execute();

        var vehicle = Assert.Single(result.Vehicles);
        Assert.Equal(available.Id, vehicle.Id);
        Assert.Equal((int)VehicleStatus.Available, vehicle.Status);
        Assert.Equal(new[] { unlinkedDriver.Id, linkedDriver.Id }, result.Drivers.Select(driver => driver.Id));
    }

    private sealed class FakeVehicleRepository(params Vehicle[] vehicles) : IVehicleRepository
    {
        public Task<Vehicle?> GetByIdAsync(Guid id) => Task.FromResult(vehicles.FirstOrDefault(vehicle => vehicle.Id == id));
        public Task<Vehicle?> GetByPlateAsync(string plate) => Task.FromResult<Vehicle?>(null);
        public Task AddAsync(Vehicle vehicle) => Task.CompletedTask;
        public Task UpdateAsync(Vehicle vehicle) => Task.CompletedTask;
        public Task<List<Vehicle>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(vehicles.ToList());
        public Task<List<Vehicle>> GetByIdsAsync(IReadOnlyCollection<Guid> ids) =>
            Task.FromResult(vehicles.Where(vehicle => ids.Contains(vehicle.Id)).ToList());
    }

    private sealed class FakeDriverRepository(params Driver[] drivers) : IDriverRepository
    {
        public Task<Driver?> GetByIdAsync(Guid id) => Task.FromResult(drivers.FirstOrDefault(driver => driver.Id == id));
        public Task<Driver?> GetByDocumentAsync(string documentNumber) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetByUserIdAsync(Guid userId) => Task.FromResult<Driver?>(null);
        public Task<Driver?> GetOtherAssignedToVehicleAsync(Guid vehicleId, Guid excludedDriverId) => Task.FromResult<Driver?>(null);
        public Task AddAsync(Driver driver) => Task.CompletedTask;
        public Task UpdateAsync(Driver driver) => Task.CompletedTask;
        public Task<List<Driver>> GetAllAsync(bool? onlyActive = null) => Task.FromResult(drivers.ToList());
        public Task<List<Driver>> GetAssignableAsync() => Task.FromResult(drivers.ToList());
    }
}
