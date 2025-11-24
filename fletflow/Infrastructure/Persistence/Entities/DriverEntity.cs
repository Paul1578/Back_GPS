namespace fletflow.Infrastructure.Persistence.Entities
{
    public class DriverEntity
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string DocumentNumber { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
        public bool IsActive { get; set; }

        public Guid? VehicleId { get; set; }
        public VehicleEntity? Vehicle { get; set; }

        public Guid? UserId { get; set; }
    }
}
