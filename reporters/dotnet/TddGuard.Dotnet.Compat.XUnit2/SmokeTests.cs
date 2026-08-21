using Xunit;

namespace TddGuard.Dotnet.Compat.XUnit2;

#pragma warning disable CA1515 // Test class must be public for xUnit discovery

public class SmokeTests
{
    [Fact]
    public void PassingTest()
    {
        var numbers = new[] { 2, 3 };
        Assert.Equal(5, numbers.Sum());
    }
}
