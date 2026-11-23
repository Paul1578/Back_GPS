namespace fletflow.Application.Fleet.Dtos;

public sealed class RouteAssignmentOptionsDto
{
    public List<RouteVehicleOptionDto> Vehicles { get; init; } = [];
    public List<RouteDriverOptionDto> Drivers { get; init; } = [];
}

public sealed class RouteVehicleOptionDto
{
    public Guid Id { get; init; }
    public string Plate { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int Year { get; init; }
    public int Status { get; init; }
}

public sealed class RouteDriverOptionDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public Guid? UserId { get; init; }
    public bool IsAvailable { get; init; } = true;
}
