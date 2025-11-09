
namespace fletflow.Infrastructure.Persistence.Entities
{
    public class RoleEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = default!;
        public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
    }
}
