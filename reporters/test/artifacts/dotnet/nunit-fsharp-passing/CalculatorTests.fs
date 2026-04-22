namespace Calculator.Tests

open NUnit.Framework

[<TestFixture>]
type CalculatorTests() =

    [<Test>]
    member _.Should_add_numbers_correctly() =
        Assert.That(2 + 3, Is.EqualTo(5))
