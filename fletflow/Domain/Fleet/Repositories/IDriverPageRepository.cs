using fletflow.Domain.Fleet.Entities;

namespace fletflow.Domain.Fleet.Repositories;

public interface IDriverPageRepository
{
    Task<(List<Driver> Items, int TotalCount)> GetPageAsync(
        int page, int pageSize, string? search = null, bool? onlyActive = null);
}
