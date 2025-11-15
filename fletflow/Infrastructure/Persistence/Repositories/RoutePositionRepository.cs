using fletflow.Domain.Fleet.Entities;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Infrastructure.Persistence.Context;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;

namespace fletflow.Infrastructure.Persistence.Repositories
{
    public class RoutePositionRepository : IRoutePositionRepository
    {
        private readonly AppDbContext _context;

        public RoutePositionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(RoutePosition position)
        {
            var entity = FleetMapper.ToEntity(position);
            await _context.RoutePositions.AddAsync(entity);
        }

        public async Task<List<RoutePosition>> GetByRouteAsync(
            Guid routeId,
            DateTime? from = null,
            DateTime? to = null)
        {
            IQueryable<RoutePositionEntity> query = _context.RoutePositions
                .AsNoTracking()
                .Where(rp => rp.RouteId == routeId);

            if (from.HasValue)
            {
                var f = from.Value.ToUniversalTime();
                query = query.Where(rp => rp.RecordedAt >= f);
            }

            if (to.HasValue)
            {
                var t = to.Value.ToUniversalTime();
                query = query.Where(rp => rp.RecordedAt <= t);
            }

            query = query.OrderBy(rp => rp.RecordedAt);

            var entities = await query.ToListAsync();
            return entities.Select(FleetMapper.ToDomain).ToList();
        }
    }
}
