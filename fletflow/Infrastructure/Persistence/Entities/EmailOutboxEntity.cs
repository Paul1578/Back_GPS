using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fletflow.Infrastructure.Persistence.Entities;

[Table("EmailOutbox")]
public sealed class EmailOutboxEntity
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    [Required]
    [Column(TypeName = "longtext")]
    public string ProtectedPayload { get; set; } = default!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LockedUntilUtc { get; set; }
    public Guid? LockToken { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeadLetteredAtUtc { get; set; }
    public int AttemptCount { get; set; }

    [MaxLength(100)]
    public string? LastFailureType { get; set; }

    public UserEntity User { get; set; } = default!;
}
