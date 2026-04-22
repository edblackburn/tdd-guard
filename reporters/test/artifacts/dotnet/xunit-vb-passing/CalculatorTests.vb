Imports Xunit

Namespace Calculator.Tests

    Public Class CalculatorTests

        <Fact>
        Public Sub Should_add_numbers_correctly()
            Assert.Equal(5, 2 + 3)
        End Sub

    End Class

End Namespace
