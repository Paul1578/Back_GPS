namespace fletflow.Domain.Fleet.Entities
{
    public enum RoutePointOperation
    {
        None = 0,
        Pickup = 1,
        Delivery = 2,
        PickupAndDelivery = 3
    }

    public enum RoutePointStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3,
        Skipped = 4
    }
}
