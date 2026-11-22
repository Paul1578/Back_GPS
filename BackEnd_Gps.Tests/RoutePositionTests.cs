using fletflow.Domain.Fleet.Entities;

namespace BackEnd_Gps.Tests;

public class RoutePositionTests
{
    [Fact]
    public void Create_AcceptsValidGpsMetadata()
    {
        var point = RoutePosition.Create(Guid.NewGuid(), -0.18, -78.47, speedKmh: 35, heading: 359.9);

        Assert.Equal(35, point.SpeedKmh);
        Assert.Equal(359.9, point.Heading);
    }

    [Theory]
    [InlineData(double.NaN, -78.47)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    public void Create_RejectsInvalidCoordinates(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoutePosition.Create(Guid.NewGuid(), latitude, longitude));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(300.1)]
    [InlineData(double.NaN)]
    public void Create_RejectsInvalidSpeed(double speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoutePosition.Create(Guid.NewGuid(), 0, 0, speedKmh: speed));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(360)]
    [InlineData(double.PositiveInfinity)]
    public void Create_RejectsInvalidHeading(double heading)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RoutePosition.Create(Guid.NewGuid(), 0, 0, heading: heading));
    }
}
