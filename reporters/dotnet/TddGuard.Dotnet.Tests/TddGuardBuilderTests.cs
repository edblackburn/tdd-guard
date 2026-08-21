using Microsoft.Testing.Platform.Builder;

namespace TddGuard.Dotnet.Tests;

/// <summary>
/// Registration is exercised against a real <see cref="ITestApplicationBuilder"/> from
/// <see cref="TestApplication.CreateBuilderAsync(string[])"/> rather than a hand-written
/// double. <c>ITestHostManager</c> is a write-only sink — four <c>Add*</c> methods and no
/// query members — so there is nothing to assert about what was registered. These tests
/// therefore pin the observable behaviour: registering against the real platform succeeds,
/// and the reporter reports why it disabled itself when the project root cannot be found.
/// The registered listener's actual behaviour is covered by the extension tests, and the
/// end-to-end registration path by the <c>TddGuard.Dotnet.Compat.*</c> smoke projects.
/// </summary>
internal sealed class TddGuardBuilderTests
{
    [Test("registers against a real test host without throwing")]
    public async Task RegistersAgainstRealTestHost()
    {
        var builder = await TestApplication.CreateBuilderAsync([]);
        var projectRoot = TempPath();

        Dotnet.TddGuardBuilder.Register(
            builder,
            getEnv: _ => projectRoot,
            getCwd: () => projectRoot);

        await Assert.That(builder.TestHost).IsNotNull();
    }

    [Test("stays disabled without throwing when no project root is configured")]
    public async Task StaysDisabledWhenNoProjectRootConfigured()
    {
        var builder = await TestApplication.CreateBuilderAsync([]);

        Dotnet.TddGuardBuilder.Register(
            builder,
            getEnv: _ => null,
            getCwd: () => "/some/dir");

        await Assert.That(builder.TestHost).IsNotNull();
    }

    [Test("reports why it disabled itself when no project root is configured")]
    public async Task ReportsWhyItDisabledItself()
    {
        var builder = await TestApplication.CreateBuilderAsync([]);
        string? captured = null;

        Dotnet.TddGuardBuilder.Register(
            builder,
            getEnv: _ => null,
            getCwd: () => "/some/dir",
            log: msg => captured = msg);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!).Contains("disabled");
    }

    [Test("reports why it disabled itself when the working directory is outside the root")]
    public async Task ReportsWhyItDisabledItselfForCwdOutsideRoot()
    {
        var builder = await TestApplication.CreateBuilderAsync([]);
        string? captured = null;

        Dotnet.TddGuardBuilder.Register(
            builder,
            getEnv: _ => TempPath(),
            getCwd: () => TempPath(),
            log: msg => captured = msg);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!).Contains("disabled");
    }

    [Test("throws ArgumentNullException for a null builder")]
    public async Task ThrowsArgumentNullExceptionForNullBuilder()
    {
        await Assert.That(() => Dotnet.TddGuardBuilder.Register(builder: null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    private static string TempPath()
        => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
}
