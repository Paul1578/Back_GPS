namespace fletflow.Api.Contracts.Vehicles
{
    public class UpdateVehicleRequest
    {
        public string Plate { get; set; } = default!;
        public string Brand { get; set; } = default!;
        public string Model { get; set; } = default!;
        public int Year { get; set; }
        public string? Description { get; set; }
    }
}
