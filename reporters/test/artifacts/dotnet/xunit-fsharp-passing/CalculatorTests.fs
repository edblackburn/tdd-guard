namespace Calculator.Tests

open Xunit

type CalculatorTests() =

    [<Fact>]
    member _.Should_add_numbers_correctly() =
        Assert.Equal(5, 2 + 3)
