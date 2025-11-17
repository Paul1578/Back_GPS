using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetDriversQuery
    {
        private readonly IDriverRepository _drivers;

        public GetDriversQuery(IDriverRepository drivers)
        {
            _drivers = drivers;
        }

        public async Task<List<DriverDto>> Execute(bool? onlyActive = null)
        {
            var drivers = await _drivers.GetAllAsync(onlyActive);
            return DriverApplicationMapper.ToDtoList(drivers);
        }
    }
}
