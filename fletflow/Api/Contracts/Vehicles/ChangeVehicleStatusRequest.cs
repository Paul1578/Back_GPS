using fletflow.Domain.Fleet.Entities;

namespace fletflow.Api.Contracts.Vehicles
{
    public class ChangeVehicleStatusRequest
    {
        public VehicleStatus Status { get; set; }
    }
}
