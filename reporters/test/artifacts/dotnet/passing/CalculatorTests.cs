namespace Calculator.Tests
{
    public class CalculatorTests
    {
        [Test]
        public async Task Should_add_numbers_correctly()
        {
            await Assert.That(2 + 3).IsEqualTo(5);
        }
    }
}
