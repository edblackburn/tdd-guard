using NUnit.Framework;

namespace TddGuard.Dotnet.Compat.NUnit;

#pragma warning disable CA1515 // Test class must be public for NUnit discovery

[TestFixture]
public class SmokeTests
{
    [Test]
    public void PassingTest()
    {
        Assert.That(2 + 3, Is.EqualTo(5));
    }
}
