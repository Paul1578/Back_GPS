
namespace fletflow.Infrastructure.Persistence.Entities
{
    public class UserRoleEntity
    {
        public Guid UserId { get; set; }
        public UserEntity User { get; set; } = default!;

        public Guid RoleId { get; set; }
        public RoleEntity Role { get; set; } = default!;
    }
}
