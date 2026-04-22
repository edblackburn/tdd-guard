Imports Microsoft.VisualStudio.TestTools.UnitTesting

Namespace Calculator.Tests

    <TestClass>
    Public Class CalculatorTests

        <TestMethod>
        Public Sub Should_add_numbers_correctly()
            Assert.AreEqual(5, 2 + 3)
        End Sub

    End Class

End Namespace
