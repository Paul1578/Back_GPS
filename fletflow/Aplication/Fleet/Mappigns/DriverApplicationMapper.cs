using fletflow.Application.Fleet.Dtos;
using fletflow.Domain.Fleet.Entities;

namespace fletflow.Application.Fleet.Mappings
{
    public static class DriverApplicationMapper
    {
        public static DriverDto ToDto(Driver driver)
        {
            return new DriverDto
            {
                Id = driver.Id,
                FirstName = driver.FirstName,
                LastName = driver.LastName,
                DocumentNumber = driver.DocumentNumber,
                PhoneNumber = driver.PhoneNumber,
                IsActive = driver.IsActive,
                VehicleId = driver.VehicleId
            };
        }

        public static List<DriverDto> ToDtoList(IEnumerable<Driver> drivers)
            => drivers.Select(ToDto).ToList();
    }
}
