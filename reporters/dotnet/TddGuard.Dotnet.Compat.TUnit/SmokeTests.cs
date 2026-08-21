namespace TddGuard.Dotnet.Compat.TUnit;

internal sealed class SmokeTests
{
    [Test]
    public async Task PassingTest()
    {
        var numbers = new[] { 2, 3 };
        await Assert.That(numbers.Sum()).IsEqualTo(5);
    }
}
