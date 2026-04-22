using Xunit;

namespace Calculator.Tests
{
    public class CalculatorTests
    {
        [Fact]
        public void Should_add_numbers_correctly()
        {
            Assert.Equal(5, 2 + 3);
        }
    }
}
