namespace fletflow.Api.Contracts.Vehicles
{
    public class CreateVehicleRequest
    {
        public string Plate { get; set; } = default!;
        public string Brand { get; set; } = default!;
        public string Model { get; set; } = default!;
        public int Year { get; set; }
        public string? Description { get; set; }
    }
}
