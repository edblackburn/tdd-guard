Imports NUnit.Framework

Namespace Calculator.Tests

    <TestFixture>
    Public Class CalculatorTests

        <Test>
        Public Sub Should_add_numbers_correctly()
            Assert.That(2 + 3, [Is].EqualTo(5))
        End Sub

    End Class

End Namespace
