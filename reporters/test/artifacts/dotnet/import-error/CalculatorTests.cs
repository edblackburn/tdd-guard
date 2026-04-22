using NonExistent.Assembly;

namespace Calculator.Tests
{
    public class CalculatorTests
    {
        [Test]
        public async Task Should_add_numbers_correctly()
        {
            var calc = new Calculator();
            await Assert.That(calc.Add(2, 3)).IsEqualTo(5);
        }
    }
}
