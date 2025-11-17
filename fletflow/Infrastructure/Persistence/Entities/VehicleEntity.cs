namespace fletflow.Infrastructure.Persistence.Entities
{
    public class VehicleEntity
    {
        public Guid Id { get; set; }
        public string Plate { get; set; } = default!;
        public string Brand { get; set; } = default!;
        public string Model { get; set; } = default!;
        public int Year { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
