using System.Text.Json;
using fletflow.Api.Contracts.Routes;
using fletflow.Domain.Fleet.Entities;

namespace BackEnd_Gps.Tests;

public class RoutePointTests
{
    [Theory]
    [InlineData(RoutePointOperation.None, RoutePointStatus.Pending)]
    [InlineData(RoutePointOperation.Pickup, RoutePointStatus.InProgress)]
    [InlineData(RoutePointOperation.Delivery, RoutePointStatus.Completed)]
    [InlineData(RoutePointOperation.PickupAndDelivery, RoutePointStatus.Failed)]
    public void Create_PreservesOperationAndStatus(RoutePointOperation operation, RoutePointStatus status)
    {
        var point = RoutePoint.Create(-0.18, -78.47, "  Cliente  ", operation, status);

        Assert.Equal(-0.18, point.Latitude);
        Assert.Equal(-78.47, point.Longitude);
        Assert.Equal("Cliente", point.Name);
        Assert.Equal(operation, point.Operation);
        Assert.Equal(status, point.Status);
    }

    [Theory]
    [InlineData(-90.01, 0, nameof(RoutePoint.Latitude))]
    [InlineData(90.01, 0, nameof(RoutePoint.Latitude))]
    [InlineData(0, -180.01, nameof(RoutePoint.Longitude))]
    [InlineData(0, 180.01, nameof(RoutePoint.Longitude))]
    public void Create_RejectsCoordinatesOutsideGeographicBounds(double latitude, double longitude, string parameter)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => RoutePoint.Create(latitude, longitude));
        Assert.Equal(parameter.ToLowerInvariant(), error.ParamName);
    }

    [Fact]
    public void RequestContract_RoundTripsPickupAndDeliveryTogether()
    {
        var request = new RoutePointRequest
        {
            Lat = -0.18,
            Lng = -78.47,
            Name = "Bodega",
            Operation = RoutePointOperation.PickupAndDelivery,
            Status = RoutePointStatus.Pending
        };

        var json = JsonSerializer.Serialize(request);
        var restored = JsonSerializer.Deserialize<RoutePointRequest>(json)!;

        Assert.Equal(RoutePointOperation.PickupAndDelivery, restored.Operation);
        Assert.Equal(RoutePointStatus.Pending, restored.Status);
        Assert.Equal(request.Lat, restored.Lat);
        Assert.Equal(request.Lng, restored.Lng);
    }

    [Fact]
    public void RouteCannotCompleteUntilOperationalStopIsCompleteWithEvidence()
    {
        var route = CreateRoute();
        route.ChangeStatus(RouteStatus.InProgress);
        route.UpdatePointStatus(1, RoutePointStatus.InProgress);
        route.AddPointEvidence(1, RoutePointEvidence.Create("note", "Recibido por cliente", null));

        Assert.Throws<InvalidOperationException>(() => route.ChangeStatus(RouteStatus.Completed));
        route.UpdatePointStatus(1, RoutePointStatus.Completed);
        route.ChangeStatus(RouteStatus.Completed);

        Assert.Equal(RouteStatus.Completed, route.Status);
        Assert.Single(route.Points[1].Evidence);
    }

    [Fact]
    public void RouteWithPickupDeliveryAndCombinedStopsRequiresEveryStopBeforeCompletion()
    {
        var origin = RoutePoint.Create(-0.18, -78.46, "Origen");
        var pickup = RoutePoint.Create(-0.19, -78.47, "Recogida", RoutePointOperation.Pickup);
        var delivery = RoutePoint.Create(-0.195, -78.475, "Entrega", RoutePointOperation.Delivery);
        var combined = RoutePoint.Create(-0.198, -78.478, "Recogida y entrega", RoutePointOperation.PickupAndDelivery);
        var destination = RoutePoint.Create(-0.20, -78.48, "Destino");
        var route = RouteE.Create(Guid.NewGuid(), Guid.NewGuid(), "Ruta multi-operación", origin, destination,
            new[] { origin, pickup, delivery, combined, destination }, null, null, null);

        route.ChangeStatus(RouteStatus.InProgress);

        for (var index = 1; index <= 3; index++)
        {
            route.UpdatePointStatus(index, RoutePointStatus.InProgress);
            route.AddPointEvidence(index, RoutePointEvidence.Create("note", $"Operación {index} confirmada", null));
            route.UpdatePointStatus(index, RoutePointStatus.Completed);

            if (index < 3)
            {
                Assert.Throws<InvalidOperationException>(() => route.ChangeStatus(RouteStatus.Completed));
                Assert.Equal(RouteStatus.InProgress, route.Status);
            }
        }

        route.ChangeStatus(RouteStatus.Completed);

        Assert.Equal(RouteStatus.Completed, route.Status);
        Assert.Equal(
            new[] { RoutePointOperation.Pickup, RoutePointOperation.Delivery, RoutePointOperation.PickupAndDelivery },
            route.Points.Skip(1).Take(3).Select(point => point.Operation));
        Assert.All(route.Points.Skip(1).Take(3), point =>
        {
            Assert.Equal(RoutePointStatus.Completed, point.Status);
            Assert.Single(point.Evidence);
        });
    }

    [Fact]
    public void PointStatusTransitionMustBeOperationalAndValid()
    {
        var route = CreateRoute();
        route.ChangeStatus(RouteStatus.InProgress);

        Assert.Throws<InvalidOperationException>(() => route.UpdatePointStatus(0, RoutePointStatus.Completed));
        Assert.Throws<InvalidOperationException>(() => route.UpdatePointStatus(1, RoutePointStatus.Completed));
    }

    [Theory]
    [InlineData(RoutePointStatus.Completed)]
    [InlineData(RoutePointStatus.Skipped)]
    public void OperationalStopCannotCloseWithoutEvidence(RoutePointStatus terminalStatus)
    {
        var route = CreateRoute();
        route.ChangeStatus(RouteStatus.InProgress);
        if (terminalStatus == RoutePointStatus.Completed)
            route.UpdatePointStatus(1, RoutePointStatus.InProgress);

        Assert.Throws<InvalidOperationException>(() => route.UpdatePointStatus(1, terminalStatus));

        Assert.Equal(
            terminalStatus == RoutePointStatus.Completed ? RoutePointStatus.InProgress : RoutePointStatus.Pending,
            route.Points[1].Status);
        Assert.Empty(route.Points[1].Evidence);
    }

    [Fact]
    public void OperationalStopCanBeSkippedAfterEvidenceIsRecorded()
    {
        var route = CreateRoute();
        route.ChangeStatus(RouteStatus.InProgress);
        route.AddPointEvidence(1, RoutePointEvidence.Create("note", "Cliente ausente", null));

        route.UpdatePointStatus(1, RoutePointStatus.Skipped);
        route.ChangeStatus(RouteStatus.Completed);

        Assert.Equal(RoutePointStatus.Skipped, route.Points[1].Status);
        Assert.Single(route.Points[1].Evidence);
        Assert.Equal(RouteStatus.Completed, route.Status);
    }

    [Fact]
    public void RouteRejectsEvidenceAfterFiftyItemsWithoutChangingTheRoute()
    {
        var route = CreateRoute();
        route.ChangeStatus(RouteStatus.InProgress);
        var evidence = RoutePointEvidence.Create("note", "Evidencia QA", null);

        for (var index = 0; index < 50; index++)
            route.AddPointEvidence(1, evidence);

        Assert.Throws<InvalidOperationException>(() => route.AddPointEvidence(1, evidence));
        Assert.Equal(50, route.Points[1].Evidence.Count);
    }

    [Fact]
    public void RouteRejectsEvidenceThatExceedsEightMegabytesWithoutChangingTheRoute()
    {
        var route = CreateRoute();
        route.ChangeStatus(RouteStatus.InProgress);
        var imageBytes = new byte[3 * 1024 * 1024];
        var imageContent = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";
        var evidence = RoutePointEvidence.Create("image", imageContent, null);
        route.AddPointEvidence(1, evidence);

        Assert.Throws<InvalidOperationException>(() => route.AddPointEvidence(1, evidence));
        Assert.Single(route.Points[1].Evidence);
    }

    [Theory]
    [InlineData("note", "", null)]
    [InlineData("video", "content", null)]
    [InlineData("image", "data:image/svg+xml;base64,PHN2Zz4=", null)]
    public void RejectsInvalidEvidence(string type, string content, string? description)
    {
        Assert.Throws<ArgumentException>(() => RoutePointEvidence.Create(type, content, description));
    }

    private static RouteE CreateRoute()
    {
        var origin = RoutePoint.Create(-0.18, -78.46, "Origen");
        var stop = RoutePoint.Create(-0.19, -78.47, "Entrega", RoutePointOperation.Delivery);
        var destination = RoutePoint.Create(-0.20, -78.48, "Destino");
        return RouteE.Create(Guid.NewGuid(), Guid.NewGuid(), "Ruta de prueba", origin, destination,
            new[] { origin, stop, destination }, null, null, null);
    }
}
