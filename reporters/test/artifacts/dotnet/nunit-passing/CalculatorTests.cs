using NUnit.Framework;

namespace Calculator.Tests
{
    [TestFixture]
    public class CalculatorTests
    {
        [Test]
        public void Should_add_numbers_correctly()
        {
            Assert.That(2 + 3, Is.EqualTo(5));
        }
    }
}
