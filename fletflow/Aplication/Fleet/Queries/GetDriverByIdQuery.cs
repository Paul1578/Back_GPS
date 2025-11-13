using fletflow.Application.Fleet.Dtos;
using fletflow.Application.Fleet.Mappings;
using fletflow.Domain.Fleet.Repositories;

namespace fletflow.Application.Fleet.Queries
{
    public class GetDriverByIdQuery
    {
        private readonly IDriverRepository _drivers;

        public GetDriverByIdQuery(IDriverRepository drivers)
        {
            _drivers = drivers;
        }

        public async Task<DriverDto> Execute(Guid id)
        {
            var driver = await _drivers.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Conductor no encontrado.");

            return DriverApplicationMapper.ToDto(driver);
        }
    }
}
