using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal sealed class TestNodeMapperTests
{
    private const string Root = "/repo";

    // A described method is the richest case and needs no interpretation: the three
    // parts compose directly into the qualified name.
    [Test("builds the full name from a described method")]
    public async Task BuildsFullNameFromDescribedMethod()
    {
        var result = An.Node()
            .DescribedBy("Acme.Widgets.Tests", "WidgetTests", "Should_add")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("Acme.Widgets.Tests.WidgetTests.Should_add");
        await Assert.That(result.Name).IsEqualTo("Should_add");
    }

    [Test("omits an empty namespace from the full name")]
    public async Task OmitsEmptyNamespaceFromFullName()
    {
        var result = An.Node()
            .DescribedBy(string.Empty, "WidgetTests", "Should_add")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("WidgetTests.Should_add");
    }

    [Test("keeps a qualified display name and takes its last segment as the test name")]
    public async Task KeepsQualifiedNameAndTakesLastSegment()
    {
        var result = An.Node()
            .NamedBy("Acme.Widgets.Tests.WidgetTests.Should_add")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("Acme.Widgets.Tests.WidgetTests.Should_add");
        await Assert.That(result.Name).IsEqualTo("Should_add");
    }

    [Test("keeps a qualified identifier and its member name as the test name")]
    public async Task KeepsQualifiedIdentifierAndMemberName()
    {
        var result = An.Node()
            .IdentifiedBy("Acme.Widgets.Tests.WidgetTests.Should_add", "Should_add")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("Acme.Widgets.Tests.WidgetTests.Should_add");
        await Assert.That(result.Name).IsEqualTo("Should_add");
    }

    // Nothing can be split out of an unqualified value, so it stands as both names
    // rather than being carved up on punctuation that carries no meaning.
    [Test("reports an unqualified value unchanged as both names")]
    public async Task ReportsUnqualifiedValueUnchanged()
    {
        var result = An.Node()
            .Unqualified("adds numbers correctly. handles zero")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("adds numbers correctly. handles zero");
        await Assert.That(result.Name).IsEqualTo("adds numbers correctly. handles zero");
    }

    [Test("reports the module id relative to the project root")]
    public async Task ReportsModuleIdRelativeToProjectRoot()
    {
        var result = An.Node()
            .InFile("/repo/tests/WidgetTests.cs")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("tests/WidgetTests.cs");
    }

    [Test("leaves a file path outside the project root untouched")]
    public async Task LeavesFilePathOutsideProjectRootUntouched()
    {
        var result = An.Node()
            .InFile("/elsewhere/WidgetTests.cs")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("/elsewhere/WidgetTests.cs");
    }

    // Grouping by declaring type keeps a class's tests together for frameworks that
    // report no file, instead of giving each test a module of its own.
    [Test("groups by declaring type when a described method has no file")]
    public async Task GroupsByDeclaringTypeWhenDescribedMethodHasNoFile()
    {
        var result = An.Node()
            .DescribedBy("Acme.Widgets.Tests", "WidgetTests", "Should_add")
            .WithNoFile()
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("Acme.Widgets.Tests.WidgetTests");
    }

    [Test("groups by declaring scope when a qualified name has no file")]
    public async Task GroupsByDeclaringScopeWhenQualifiedNameHasNoFile()
    {
        var result = An.Node()
            .NamedBy("Acme.Widgets.Tests.WidgetTests.Should_add")
            .WithNoFile()
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("Acme.Widgets.Tests.WidgetTests");
    }

    [Test("groups by declaring scope when a qualified identifier has no file")]
    public async Task GroupsByDeclaringScopeWhenQualifiedIdentifierHasNoFile()
    {
        var result = An.Node()
            .IdentifiedBy("Acme.Widgets.Tests.WidgetTests.Should_add", "Should_add")
            .WithNoFile()
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("Acme.Widgets.Tests.WidgetTests");
    }

    [Test("uses an unqualified value as its own module when there is no file")]
    public async Task UsesUnqualifiedValueAsItsOwnModule()
    {
        var result = An.Node()
            .Unqualified("Should_add")
            .WithNoFile()
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("Should_add");
    }

    [Test("carries the test state through unchanged")]
    public async Task CarriesTestStateThrough()
    {
        var result = An.Node()
            .WithState(new Core.TestState.Failed([new TestEntryError("boom")]))
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.State is Core.TestState.Failed).IsTrue();
    }
}
