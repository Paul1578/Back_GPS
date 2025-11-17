using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class DriverRepository : IDriverRepository
    {
        private readonly AppDbContext _context;

        public DriverRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Driver?> GetByIdAsync(Guid id)
        {
            var entity = await _context.Drivers.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            return entity is null ? null : FleetMapper.ToDomain(entity);
        }

        public async Task<Driver?> GetByDocumentAsync(string documentNumber)
        {
            documentNumber = documentNumber.Trim();

            var entity = await _context.Drivers.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DocumentNumber == documentNumber);

            return entity is null ? null : FleetMapper.ToDomain(entity);
        }

        public async Task AddAsync(Driver driver)
        {
            var entity = FleetMapper.ToEntity(driver);
            await _context.Drivers.AddAsync(entity);
        }

        public async Task UpdateAsync(Driver driver)
        {
            var entity = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driver.Id);

            if (entity is null)
                throw new KeyNotFoundException("Conductor no encontrado para actualizar.");

            entity.FirstName = driver.FirstName;
            entity.LastName = driver.LastName;
            entity.DocumentNumber = driver.DocumentNumber;
            entity.PhoneNumber = driver.PhoneNumber;
            entity.IsActive = driver.IsActive;
            entity.VehicleId = driver.VehicleId;
        }

        public async Task<List<Driver>> GetAllAsync(bool? onlyActive = null)
        {
            IQueryable<DriverEntity> query = _context.Drivers.AsNoTracking();

            if (onlyActive.HasValue)
                query = query.Where(d => d.IsActive == onlyActive.Value);

            var entities = await query.ToListAsync();
            return entities.Select(FleetMapper.ToDomain).ToList();
        }
        
    }
}
