using fletflow.Domain.Auth.Repositories;

namespace fletflow.Application.Auth.Queries
{
    public class GetAllRolesQuery
    {
        private readonly IRoleRepository _roles;
        public GetAllRolesQuery(IRoleRepository roles) => _roles = roles;

        public async Task<IReadOnlyList<string>> Execute()
        {
            var list = await _roles.GetAllAsync();
            return list.Select(r => r.Name).OrderBy(n => n).ToList();
        }
    }
}
