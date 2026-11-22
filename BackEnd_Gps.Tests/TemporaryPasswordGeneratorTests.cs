using fletflow.Api.Security;

namespace BackEnd_Gps.Tests;

public class TemporaryPasswordGeneratorTests
{
    [Fact]
    public void Generate_ReturnsStrongUrlSafeUniquePasswords()
    {
        var first = TemporaryPasswordGenerator.Generate();
        var second = TemporaryPasswordGenerator.Generate();

        Assert.Equal(43, first.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", first);
        Assert.NotEqual(first, second);
    }
}
