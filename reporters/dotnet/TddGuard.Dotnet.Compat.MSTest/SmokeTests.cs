using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TddGuard.Dotnet.Compat.MSTest;

#pragma warning disable CA1515 // Test class must be public for MSTest discovery

[TestClass]
public class SmokeTests
{
    [TestMethod]
    public void PassingTest()
    {
        var numbers = new[] { 2, 3 };
        Assert.AreEqual(5, numbers.Sum());
    }
}
