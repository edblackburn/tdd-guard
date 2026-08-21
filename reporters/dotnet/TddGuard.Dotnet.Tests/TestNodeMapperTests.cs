using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal sealed class TestNodeMapperTests
{
    private const string Root = "/repo";

    [Test("builds full name from the structured method identifier")]
    public async Task BuildsFullNameFromMethodIdentifier()
    {
        var input = An.Node()
            .WithUid("65964d4e887280606a1ad75414c458b8910c3e2c7fb4dde0575f5209b48a4dd4")
            .WithDisplayName("PassingTest")
            .WithMethodIdentifier("Acme.Widgets.Tests", "WidgetTests", "Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("Acme.Widgets.Tests.WidgetTests.Should_add");
    }

    [Test("omits empty namespace from the full name")]
    public async Task OmitsEmptyNamespaceFromFullName()
    {
        var input = An.Node()
            .WithMethodIdentifier(string.Empty, "WidgetTests", "Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("WidgetTests.Should_add");
    }

    // xunit v2 and NUnit do not supply TestMethodIdentifierProperty. Their
    // DisplayName is already the fully qualified name, so it beats the UID
    // (which is an opaque digest for xunit v2).
    [Test("falls back to display name when no method identifier is present")]
    public async Task FallsBackToDisplayNameWhenNoMethodIdentifier()
    {
        var input = An.Node()
            .WithUid("5543c04c2aded8984de435023306e357e9b5900f")
            .WithDisplayName("Acme.Widgets.Tests.WidgetTests.Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("Acme.Widgets.Tests.WidgetTests.Should_add");
    }

    // NUnit is the mirror image of xUnit: the UID carries the fully qualified name
    // while the display name is only the bare method.
    [Test("prefers a qualified uid over a bare display name")]
    public async Task PrefersQualifiedUidOverBareDisplayName()
    {
        var input = An.Node()
            .WithUid("Acme.Widgets.Tests.WidgetTests.Should_add")
            .WithDisplayName("Should_add")
            .WithFilePath(null)
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("Acme.Widgets.Tests.WidgetTests.Should_add");
        await Assert.That(result.ModuleId).IsEqualTo("Acme.Widgets.Tests.WidgetTests");
    }

    // A GUID (MSTest) has hyphens but no namespace separator, so it must not be
    // mistaken for a qualified name.
    [Test("does not treat a guid uid as qualified")]
    public async Task DoesNotTreatGuidUidAsQualified()
    {
        var input = An.Node()
            .WithUid("18961c1b-a633-8e2c-b388-602643b3d46b")
            .WithDisplayName("Should_add")
            .WithFilePath(null)
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("Should_add");
    }

    [Test("falls back to the uid when neither identifier nor display name is present")]
    public async Task FallsBackToUidWhenNothingElseAvailable()
    {
        var input = An.Node()
            .WithUid("some/opaque/uid")
            .WithDisplayName(string.Empty)
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo("some/opaque/uid");
    }

    [Test("reports the module id relative to the project root")]
    public async Task ReportsModuleIdRelativeToProjectRoot()
    {
        var input = An.Node()
            .WithFilePath("/repo/tests/WidgetTests.cs")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("tests/WidgetTests.cs");
    }

    [Test("leaves a file path outside the project root untouched")]
    public async Task LeavesFilePathOutsideProjectRootUntouched()
    {
        var input = An.Node()
            .WithFilePath("/elsewhere/WidgetTests.cs")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("/elsewhere/WidgetTests.cs");
    }

    // NUnit and xunit v2 supply no TestFileLocationProperty at all. Grouping by
    // the declaring type keeps modules meaningful instead of keying them on an
    // opaque per-test digest, which would make every test its own module.
    [Test("uses the declaring type as module id when no file path is available")]
    public async Task UsesDeclaringTypeAsModuleIdWhenNoFilePath()
    {
        var input = An.Node()
            .WithUid("5543c04c2aded8984de435023306e357e9b5900f")
            .WithFilePath(null)
            .WithMethodIdentifier("Acme.Widgets.Tests", "WidgetTests", "Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("Acme.Widgets.Tests.WidgetTests");
    }

    [Test("derives module id from the display name when only that is available")]
    public async Task DerivesModuleIdFromDisplayNameWhenOnlyThatIsAvailable()
    {
        var input = An.Node()
            .WithUid("5543c04c2aded8984de435023306e357e9b5900f")
            .WithDisplayName("Acme.Widgets.Tests.WidgetTests.Should_add")
            .WithFilePath(null)
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.ModuleId).IsEqualTo("Acme.Widgets.Tests.WidgetTests");
    }

    [Test("keeps the short name from the display name")]
    public async Task KeepsShortNameFromDisplayName()
    {
        var input = An.Node()
            .WithDisplayName("Should_add")
            .WithMethodIdentifier("Acme.Widgets.Tests", "WidgetTests", "Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.Name).IsEqualTo("Should_add");
    }

    // xUnit sets DisplayName to the fully qualified name, but the other reporters
    // report the bare method name here, so the identifier wins when present.
    [Test("uses the method name as the short name when the display name is qualified")]
    public async Task UsesMethodNameWhenDisplayNameIsQualified()
    {
        var input = An.Node()
            .WithDisplayName("Acme.Widgets.Tests.WidgetTests.Should_add")
            .WithMethodIdentifier("Acme.Widgets.Tests", "WidgetTests", "Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.Name).IsEqualTo("Should_add");
    }

    [Test("keeps the display name as the short name when no identifier is present")]
    public async Task KeepsDisplayNameWhenNoIdentifierIsPresent()
    {
        var input = An.Node()
            .WithDisplayName("Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.Name).IsEqualTo("Should_add");
    }

    // xUnit v2 supplies no method identifier at all, only a qualified display name,
    // so the trailing segment is the closest available short name.
    [Test("takes the trailing segment when only a qualified display name is available")]
    public async Task TakesTrailingSegmentOfQualifiedDisplayName()
    {
        var input = An.Node()
            .WithUid("5543c04c2aded8984de435023306e357e9b5900f")
            .WithDisplayName("Acme.Widgets.Tests.WidgetTests.Should_add")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.Name).IsEqualTo("Should_add");
    }

    // A sentence-style display name must survive intact: the dot is punctuation,
    // not a namespace separator.
    [Test("leaves a prose display name intact")]
    public async Task LeavesProseDisplayNameIntact()
    {
        var input = An.Node()
            .WithDisplayName("adds numbers correctly. handles zero")
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.Name).IsEqualTo("adds numbers correctly. handles zero");
    }

    [Test("handles an empty uid without throwing")]
    public async Task HandlesEmptyUidWithoutThrowing()
    {
        var input = An.Node()
            .WithUid(string.Empty)
            .WithDisplayName(string.Empty)
            .WithFilePath(null)
            .Build();

        var result = input.ToCollectedResult(Root);

        await Assert.That(result.FullName).IsEqualTo(string.Empty);
    }
}
