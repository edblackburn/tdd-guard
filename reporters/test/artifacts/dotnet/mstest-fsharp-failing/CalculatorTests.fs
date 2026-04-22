namespace Calculator.Tests

open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type CalculatorTests() =

    [<TestMethod>]
    member _.Should_add_numbers_correctly() =
        Assert.AreEqual(6, 2 + 3)
