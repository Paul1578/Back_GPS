using System.ComponentModel.DataAnnotations;

namespace fletflow.Api.Contracts.Vehicles
{
    public class CreateVehicleRequest
    {
        [Required, StringLength(20, MinimumLength = 1)]
        public string Plate { get; set; } = default!;

        [Required, StringLength(100, MinimumLength = 1)]
        public string Brand { get; set; } = default!;

        [Required, StringLength(100, MinimumLength = 1)]
        public string Model { get; set; } = default!;

        [Range(1900, 2100)]
        public int Year { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }
    }
}
