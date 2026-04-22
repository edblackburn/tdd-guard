using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Calculator.Tests
{
    [TestClass]
    public class CalculatorTests
    {
        [TestMethod]
        public void Should_add_numbers_correctly()
        {
            Assert.AreEqual(6, 2 + 3);
        }
    }
}
