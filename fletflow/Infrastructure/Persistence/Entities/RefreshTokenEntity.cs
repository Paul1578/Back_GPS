using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fletflow.Infrastructure.Persistence.Entities
{
    [Table("RefreshTokens")]
    public class RefreshTokenEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        [MaxLength(256)]
        public string TokenHash { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        [MaxLength(256)]
        public string? ReplacedByTokenHash { get; set; }

        public UserEntity User { get; set; } = default!;
    }
}
