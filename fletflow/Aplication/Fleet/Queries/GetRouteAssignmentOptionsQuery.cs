using fletflow.Application.Fleet.Dtos;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries;

public sealed class GetRouteAssignmentOptionsQuery
{
    private readonly IVehicleRepository _vehicles;
    private readonly IDriverRepository _drivers;
    private readonly IRouteRepository _routes;

    public GetRouteAssignmentOptionsQuery(
        IVehicleRepository vehicles,
        IDriverRepository drivers,
        IRouteRepository routes)
    {
        _vehicles = vehicles;
        _drivers = drivers;
        _routes = routes;
    }

    public async Task<RouteAssignmentOptionsDto> Execute()
    {
        var vehicles = (await _vehicles.GetAllAsync(onlyActive: true))
            .Where(vehicle => vehicle.Status == VehicleStatus.Available)
            .ToList();
        var drivers = await _drivers.GetAssignableAsync();
        var busyDriverIds = (await _routes.GetAllAsync(status: RouteStatus.InProgress, onlyActive: true))
            .Select(route => route.DriverId)
            .ToHashSet();

        return new RouteAssignmentOptionsDto
        {
            Vehicles = vehicles.Select(vehicle => new RouteVehicleOptionDto
            {
                Id = vehicle.Id,
                Plate = vehicle.Plate,
                Brand = vehicle.Brand,
                Model = vehicle.Model,
                Year = vehicle.Year,
                Status = (int)vehicle.Status,
            }).ToList(),
            Drivers = drivers.Select(driver => new RouteDriverOptionDto
            {
                Id = driver.Id,
                FirstName = driver.FirstName,
                LastName = driver.LastName,
                UserId = driver.UserId,
                IsAvailable = !busyDriverIds.Contains(driver.Id),
            }).ToList(),
        };
    }
}
