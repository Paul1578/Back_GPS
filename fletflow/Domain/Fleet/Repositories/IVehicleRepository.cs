using fletflow.Domain.Fleet.Entities;

namespace fletflow.Domain.Fleet.Repositories
{
    public interface IVehicleRepository
    {
        Task<Vehicle?> GetByIdAsync(Guid id);
        Task<Vehicle?> GetByPlateAsync(string plate);
        Task AddAsync(Vehicle vehicle);
        Task UpdateAsync(Vehicle vehicle);
        Task<List<Vehicle>> GetAllAsync(bool? onlyActive = null);
    }
}
