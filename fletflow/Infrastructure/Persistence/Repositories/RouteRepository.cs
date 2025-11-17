using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class RouteRepository : IRouteRepository
    {
        private readonly AppDbContext _context;

        public RouteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RouteE?> GetByIdAsync(Guid id)
        {
            var entity = await _context.Routes.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            return entity is null ? null : FleetMapper.ToDomain(entity);
        }

        public async Task AddAsync(RouteE route)
        {
            var entity = FleetMapper.ToEntity(route);
            await _context.Routes.AddAsync(entity);
        }

        public async Task UpdateAsync(RouteE route)
        {
            var entity = await _context.Routes.FirstOrDefaultAsync(r => r.Id == route.Id);

            if (entity is null)
                throw new KeyNotFoundException("Ruta no encontrada para actualizar.");

            entity.VehicleId = route.VehicleId;
            entity.DriverId = route.DriverId;
            entity.Origin = route.Origin;
            entity.Destination = route.Destination;
            entity.CargoDescription = route.CargoDescription;
            entity.PlannedStart = route.PlannedStart;
            entity.PlannedEnd = route.PlannedEnd;
            entity.Status = (int)route.Status;
            entity.IsActive = route.IsActive;
        }

        public async Task<List<RouteE>> GetAllAsync(
            Guid? vehicleId = null,
            Guid? driverId = null,
            RouteStatus? status = null)
        {
            IQueryable<RouteEntity> query = _context.Routes.AsNoTracking();

            if (vehicleId.HasValue)
                query = query.Where(r => r.VehicleId == vehicleId.Value);

            if (driverId.HasValue)
                query = query.Where(r => r.DriverId == driverId.Value);

            if (status.HasValue)
            {
                var s = (int)status.Value;
                query = query.Where(r => r.Status == s);
            }

            var entities = await query.ToListAsync();
            return entities.Select(FleetMapper.ToDomain).ToList();
        }
    }
}
