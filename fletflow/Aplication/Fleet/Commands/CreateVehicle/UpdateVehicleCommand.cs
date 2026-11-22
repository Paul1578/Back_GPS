using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

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
                ?? throw new KeyNotFoundException("Vehicle not found.");

            plate = VehicleCommandInput.Required(plate, "La placa", 20);
            brand = VehicleCommandInput.Required(brand, "La marca", 100);
            model = VehicleCommandInput.Required(model, "El modelo", 100);
            year = VehicleCommandInput.Year(year);
            description = VehicleCommandInput.Optional(description, "La descripción", 1000);

            if (!string.Equals(vehicle.Plate, plate, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await _vehicles.GetByPlateAsync(plate);
                if (existing is not null && existing.Id != vehicle.Id)
                    throw new BusinessConflictException($"Ya existe otro vehículo con la placa {plate} en esta empresa.");
            }

            // Actualizamos la entidad de dominio
            vehicle.Update(plate, brand, model, year, description);

            // Ahora sí se lo decimos al repositorio para que lo aplique a EF
            await _vehicles.UpdateAsync(vehicle);
            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException)
            {
                var existing = await _vehicles.GetByPlateAsync(plate);
                if (existing is not null && existing.Id != vehicle.Id)
                    throw new BusinessConflictException($"Ya existe otro vehículo con la placa {plate} en esta empresa.");
                throw;
            }

            return VehicleApplicationMapper.ToDto(vehicle);
        }
    }
}

