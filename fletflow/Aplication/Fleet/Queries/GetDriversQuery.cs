using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;
using fletflow.Application.Shared;

namespace fletflow.Application.Fleet.Queries
{
    public class GetDriversQuery
    {
        private readonly IDriverRepository _drivers;
        private readonly IDriverPageRepository? _pagedDrivers;

        public GetDriversQuery(IDriverRepository drivers, IDriverPageRepository? pagedDrivers = null)
        {
            _drivers = drivers;
            _pagedDrivers = pagedDrivers;
        }

        public async Task<List<DriverDto>> Execute(bool? onlyActive = null)
        {
            var drivers = await _drivers.GetAllAsync(onlyActive);
            return DriverApplicationMapper.ToDtoList(drivers);
        }

        public async Task<PagedResult<DriverDto>> ExecutePage(
            int page = 1,
            int pageSize = PaginationLimits.DefaultPageSize,
            string? search = null,
            bool? onlyActive = null)
        {
            if (!PaginationLimits.IsValid(page, pageSize))
                throw new ArgumentOutOfRangeException(nameof(page), "La página debe ser >= 1 y pageSize debe estar entre 1 y 100.");

            if (_pagedDrivers is null)
                throw new InvalidOperationException("No se configuró el repositorio paginado de conductores.");

            var (drivers, totalCount) = await _pagedDrivers.GetPageAsync(page, pageSize, search, onlyActive);
            return new PagedResult<DriverDto>
            {
                Items = DriverApplicationMapper.ToDtoList(drivers),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };
        }
    }
}
