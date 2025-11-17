using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;

namespace fletflow.Application.Fleet.Commands
{
    public class UpdateVehicleCommand
    {
        private readonly IUnitOfWork _uow;
        private readonly IVehicleRepository _vehicles;

        public UpdateVehicleCommand(IUnitOfWork uow, IVehicleRepository vehicles)
        {
            _uow = uow;
            _vehicles = vehicles;
        }

        public async Task<VehicleDto> Execute(
            Guid id,
            string plate,
            string brand,
            string model,
            int year,
            string? description)
        {
            var vehicle = await _vehicles.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Vehículo no encontrado.");

            plate = plate.Trim();
            brand = brand.Trim();
            model = model.Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            if (!string.Equals(vehicle.Plate, plate, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _vehicles.GetByPlateAsync(plate);
                if (existing is not null && existing.Id != vehicle.Id)
                    throw new InvalidOperationException($"Ya existe otro vehículo con la placa {plate}.");
            }

            // Actualizamos la entidad de dominio
            vehicle.Update(plate, brand, model, year, description);

            // 👈 Ahora sí se lo decimos al repositorio para que lo aplique a EF
            await _vehicles.UpdateAsync(vehicle);
            await _uow.CommitAsync();

            return VehicleApplicationMapper.ToDto(vehicle);
        }
    }
}

