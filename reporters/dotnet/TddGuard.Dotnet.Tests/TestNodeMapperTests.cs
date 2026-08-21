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

    // The display name carries no uniqueness guarantee and cannot be split into
    // anything more specific, so it stands as the short name unchanged. The full name
    // reports the node UID instead, since that is what MTP guarantees unique per test.
    [Test("reports an unqualified display name as the short name, and the UID as the full name")]
    public async Task ReportsUnqualifiedDisplayNameAsShortNameAndUidAsFullName()
    {
        var result = An.Node()
            .Unqualified("6cdcb5546dc9", "adds numbers correctly. handles zero")
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("6cdcb5546dc9");
        await Assert.That(result.Name).IsEqualTo("adds numbers correctly. handles zero");
    }

    // Two tests can report the same unqualified display name (a generic label, or a
    // framework quirk); the UID is what keeps their full names from colliding.
    [Test("gives two unqualified tests sharing a display name distinct full names")]
    public async Task GivesUnqualifiedTestsSharingADisplayNameDistinctFullNames()
    {
        var first = An.Node().Unqualified("6cdcb5546dc9", "should add").Build().ToCollectedResult(Root);
        var second = An.Node().Unqualified("8f31a02b7e14", "should add").Build().ToCollectedResult(Root);

        await Assert.That(first.FullName).IsNotEqualTo(second.FullName);
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

    [Test("uses an unqualified UID as its own module when it carries no separator and there is no file")]
    public async Task UsesUnqualifiedValueAsItsOwnModule()
    {
        var result = An.Node()
            .Unqualified("6cdcb5546dc9", "Should_add")
            .WithNoFile()
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("6cdcb5546dc9");
    }

    // A realistic UID carries the assembly/class path MTP builds it from; grouping by
    // its declaring scope keeps such tests together, the same as the other identity
    // shapes, rather than giving each one a module of its own.
    [Test("groups by declaring scope when an unqualified UID carries a separator and there is no file")]
    public async Task GroupsByDeclaringScopeWhenUnqualifiedUidHasNoFile()
    {
        var result = An.Node()
            .Unqualified("assembly/TestClass/6cdcb5546dc9", "should add")
            .WithNoFile()
            .Build()
            .ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("assembly/TestClass");
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
