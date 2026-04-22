namespace Calculator.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type CalculatorTests() =

    [<TestMethod>]
    member _.Should_add_numbers_correctly() =
        Assert.AreEqual(5, 2 + 3)
