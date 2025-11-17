using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class VehicleRepository : IVehicleRepository
    {
        private readonly AppDbContext _context;

        public VehicleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Vehicle?> GetByIdAsync(Guid id)
        {
            var entity = await _context.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == id);

            return entity is null ? null : FleetMapper.ToDomain(entity);
        }

        public async Task<Vehicle?> GetByPlateAsync(string plate)
        {
            var entity = await _context.Vehicles
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Plate == plate);

            return entity is null ? null : FleetMapper.ToDomain(entity);
        }

        public async Task AddAsync(Vehicle vehicle)
        {
            var entity = FleetMapper.ToEntity(vehicle);
            await _context.Vehicles.AddAsync(entity);
        }

        public async Task<List<Vehicle>> GetAllAsync(bool? onlyActive = null)
        {
            IQueryable<VehicleEntity> query = _context.Vehicles.AsNoTracking();

            if (onlyActive.HasValue)
            {
                query = query.Where(v => v.IsActive == onlyActive.Value);
            }

            var entities = await query.ToListAsync();
            return entities.Select(FleetMapper.ToDomain).ToList();
        }

        public async Task UpdateAsync(Vehicle vehicle)
        {
            var entity = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicle.Id);

            if (entity is null)
                throw new KeyNotFoundException("Vehículo no encontrado para actualizar.");

            // Copiamos los valores desde la entidad de dominio a la entidad EF
            entity.Plate = vehicle.Plate;
            entity.Brand = vehicle.Brand;
            entity.Model = vehicle.Model;
            entity.Year = vehicle.Year;
            entity.Description = vehicle.Description;
            entity.IsActive = vehicle.IsActive;
        }

    }
}
