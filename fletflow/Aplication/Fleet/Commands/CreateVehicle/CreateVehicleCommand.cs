using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class CreateVehicleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IVehicleRepository _vehicles;

        public CreateVehicleCommand(IUnitOfWork uow, IVehicleRepository vehicles)
        {
            _uow = uow;
            _vehicles = vehicles;
        }

        public async Task<VehicleDto> Execute(
            string plate,
            string brand,
            string model,
            int year,
            string? description)
        {
            plate = plate.Trim();
            brand = brand.Trim();
            model = model.Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            // Reglas: placa única
            var existing = await _vehicles.GetByPlateAsync(plate);
            if (existing is not null)
                throw new InvalidOperationException($"Ya existe un vehículo con la placa {plate}.");

            var vehicle = Vehicle.Create(plate, brand, model, year, description);

            await _vehicles.AddAsync(vehicle);
            await _uow.CommitAsync();

            return VehicleApplicationMapper.ToDto(vehicle);
        }
    }
}
