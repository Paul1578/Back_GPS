using System.IO;
using fletflow.Infrastructure.Persistence.Entities;
using fletflow.Infrastructure.Persistence.Mappings;

namespace BackEnd_Gps.Tests;

public class FleetMapperTests
{
    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"latitude\": 5}")]
    [InlineData("[null]")]
    public void ToDomain_RejectsCorruptPersistedRoutePoints(string pointsJson)
    {
        var route = CreateRoute(pointsJson);

        var exception = Assert.Throws<InvalidDataException>(() => FleetMapper.ToDomain(route));

        Assert.Contains(route.Id.ToString(), exception.Message);
        Assert.IsAssignableFrom<Exception>(exception.InnerException);
    }

    [Fact]
    public void ToDomain_KeepsLegacyFallbackForEmptyPointsArray()
    {
        var route = CreateRoute("[]");

        var domain = FleetMapper.ToDomain(route);

        Assert.Equal(2, domain.Points.Count);
        Assert.Equal("Origen", domain.Points[0].Name);
        Assert.Equal("Destino", domain.Points[1].Name);
    }

    private static RouteEntity CreateRoute(string pointsJson) => new()
    {
        Id = Guid.NewGuid(),
        VehicleId = Guid.NewGuid(),
        DriverId = Guid.NewGuid(),
        Name = "Ruta de prueba",
        Origin = "Origen",
        OriginLat = -0.2,
        OriginLng = -78.5,
        Destination = "Destino",
        DestinationLat = -0.3,
        DestinationLng = -78.6,
        PointsJson = pointsJson,
        IsActive = true
    };
}
