using Xunit;

namespace TddGuard.Dotnet.Compat.XUnit;

#pragma warning disable CA1515 // Test class must be public for xUnit discovery

public class SmokeTests
{
    [Fact]
    public void PassingTest()
    {
        Assert.Equal(5, 2 + 3);
    }
}
