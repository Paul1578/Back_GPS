using fletflow.Domain.Fleet.Entities;

namespace BackEnd_Gps.Tests;

public class RouteLifecycleTests
{
    [Fact]
    public void ChangeStatus_StoresActualStartAndEndTimes()
    {
        var pointA = RoutePoint.Create(-0.2, -78.5, "Origen");
        var pointB = RoutePoint.Create(-0.21, -78.51, "Destino");
        var route = RouteE.Create(Guid.NewGuid(), Guid.NewGuid(), "Entrega", pointA, pointB,
            new[] { pointA, pointB }, null, null, null);

        route.ChangeStatus(RouteStatus.InProgress);
        var startedAt = route.StartedAt;
        route.ChangeStatus(RouteStatus.Pending);
        route.ChangeStatus(RouteStatus.InProgress);
        route.ChangeStatus(RouteStatus.Completed);

        Assert.NotNull(startedAt);
        Assert.Equal(startedAt, route.StartedAt);
        Assert.NotNull(route.EndedAt);
        Assert.True(route.EndedAt >= route.StartedAt);
    }
}
