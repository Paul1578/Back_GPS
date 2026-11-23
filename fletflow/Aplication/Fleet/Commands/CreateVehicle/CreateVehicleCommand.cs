using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Exceptions;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;

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
            plate = VehicleCommandInput.Required(plate, "La placa", 20);
            brand = VehicleCommandInput.Required(brand, "La marca", 100);
            model = VehicleCommandInput.Required(model, "El modelo", 100);
            year = VehicleCommandInput.Year(year);
            description = VehicleCommandInput.Optional(description, "La descripción", 1000);

            // Reglas: placa única
            var existing = await _vehicles.GetByPlateAsync(plate);
            if (existing is not null)
                throw new BusinessConflictException($"Ya existe un vehículo con la placa {plate} en esta empresa.");

            var vehicle = Vehicle.Create(plate, brand, model, year, description);

            await _vehicles.AddAsync(vehicle);
            try
            {
                await _uow.CommitAsync();
            }
            catch (DbUpdateException)
            {
                if (await _vehicles.GetByPlateAsync(plate) is not null)
                    throw new BusinessConflictException($"Ya existe un vehículo con la placa {plate} en esta empresa.");
                throw;
            }

            return VehicleApplicationMapper.ToDto(vehicle);
        }
    }
}
