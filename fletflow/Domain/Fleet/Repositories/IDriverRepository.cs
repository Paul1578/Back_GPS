using fletflow.Domain.Fleet.Entities;

namespace fletflow.Domain.Fleet.Repositories
{
    public interface IDriverRepository
    {
        Task<Driver?> GetByIdAsync(Guid id);
        Task<Driver?> GetByDocumentAsync(string documentNumber);
        Task<Driver?> GetByUserIdAsync(Guid userId);
        Task AddAsync(Driver driver);
        Task UpdateAsync(Driver driver);
        Task<List<Driver>> GetAllAsync(bool? onlyActive = null);
    }
}
