using Microsoft.Testing.Platform.Builder;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

/// <summary>
/// Where the reporter writes its report is decided when it registers, from the
/// environment and the working directory. Exercised through
/// <see cref="Dotnet.TddGuardBuilder.Register(ITestApplicationBuilder, GetEnvironmentVariable?, GetCurrentWorkingDirectory?, LogDiagnostic?)"/>
/// — the only entry point a consumer has — so the observable outcome is whether the
/// reporter stays enabled or disables itself and says why.
/// </summary>
internal sealed class ProjectRootResolutionTests
{
    [Test("enables the reporter from TDD_GUARD_PROJECT_ROOT")]
    public async Task EnablesFromProjectRootEnvVar()
        => await Assert.That(await DisabledReason(EnvOf("TDD_GUARD_PROJECT_ROOT", "/custom/root"), "/custom/root"))
            .IsNull();

    [Test("falls back to CLAUDE_PROJECT_DIR when TDD_GUARD_PROJECT_ROOT is unset")]
    public async Task FallsBackToClaudeProjectDir()
        => await Assert.That(await DisabledReason(EnvOf("CLAUDE_PROJECT_DIR", "/claude/dir"), "/claude/dir"))
            .IsNull();

    [Test("prefers TDD_GUARD_PROJECT_ROOT over CLAUDE_PROJECT_DIR")]
    public async Task PrefersProjectRootOverClaudeProjectDir()
    {
        var root = TempPath();
        var reason = await DisabledReason(
            name => name switch
            {
                "TDD_GUARD_PROJECT_ROOT" => root,
                "CLAUDE_PROJECT_DIR" => "/elsewhere",
                _ => null,
            },
            cwd: root);

        await Assert.That(reason).IsNull();
    }

    // ADR-010: no silent fallback to the working directory, because that writes the
    // report where the hook will not look for it.
    [Test("disables itself and names the variable when no project root is configured")]
    public async Task DisablesItselfWhenNoProjectRootConfigured()
    {
        var reason = await DisabledReason(_ => null, cwd: "/fallback/cwd");

        await Assert.That(reason).IsNotNull();
        await Assert.That(reason!).Contains("TDD_GUARD_PROJECT_ROOT");
    }

    [Test("disables itself when the configured project root is empty")]
    public async Task DisablesItselfWhenProjectRootIsEmpty()
        => await Assert.That(await DisabledReason(EnvOf("TDD_GUARD_PROJECT_ROOT", string.Empty), "/some/dir"))
            .IsNotNull();

    [Test("disables itself when the working directory is outside the project root")]
    public async Task DisablesItselfWhenWorkingDirectoryIsOutsideRoot()
        => await Assert.That(await DisabledReason(EnvOf("TDD_GUARD_PROJECT_ROOT", "/project/root"), "/somewhere/else"))
            .IsNotNull();

    [Test("accepts a working directory equal to the project root")]
    public async Task AcceptsWorkingDirectoryEqualToRoot()
    {
        var root = TempPath();
        await Assert.That(await DisabledReason(EnvOf("TDD_GUARD_PROJECT_ROOT", root), root)).IsNull();
    }

    [Test("accepts a working directory beneath the project root")]
    public async Task AcceptsWorkingDirectoryBeneathRoot()
    {
        var root = TempPath();
        var cwd = Path.Combine(root, "src", "tests");

        await Assert.That(await DisabledReason(EnvOf("TDD_GUARD_PROJECT_ROOT", root), cwd)).IsNull();
    }

    [Test("normalises parent segments in the configured project root")]
    public async Task NormalisesParentSegmentsInRoot()
    {
        var root = TempPath();
        var rootViaParentSegment = Path.Combine(root, "subdir", "..");

        await Assert.That(await DisabledReason(EnvOf("TDD_GUARD_PROJECT_ROOT", rootViaParentSegment), root))
            .IsNull();
    }

    [Test("tolerates a trailing separator on the configured project root")]
    public async Task ToleratesTrailingSeparatorOnRoot()
    {
        var root = TempPath();
        var rootWithTrailing = root + Path.DirectorySeparatorChar;

        await Assert.That(await DisabledReason(EnvOf("TDD_GUARD_PROJECT_ROOT", rootWithTrailing), root))
            .IsNull();
    }

    // macOS reaches /private/var through /var, so a configured root and a working
    // directory can name the same place by different paths. Following links is a port,
    // so the case is stated rather than staged on disk.
    [Test("accepts a project root that reaches the working directory through a link")]
    public async Task AcceptsRootReachingWorkingDirectoryThroughLink()
    {
        const string link = "/var/project";
        const string real = "/private/var/project";

        var reason = await DisabledReason(
            EnvOf("TDD_GUARD_PROJECT_ROOT", link),
            cwd: real,
            canonicalise: path => path == link ? real : path);

        await Assert.That(reason).IsNull();
    }

    [Test("disables itself when a link resolves outside the project root")]
    public async Task DisablesItselfWhenLinkResolvesOutsideRoot()
    {
        var reason = await DisabledReason(
            EnvOf("TDD_GUARD_PROJECT_ROOT", "/project/root"),
            cwd: "/project/root/link",
            canonicalise: path => path == "/project/root/link" ? "/elsewhere" : path);

        await Assert.That(reason).IsNotNull();
    }

    /// <summary>
    /// Registers against a real test host and returns the reason the reporter gave for
    /// disabling itself, or null when it stayed enabled.
    /// </summary>
    private static async Task<string?> DisabledReason(
        GetEnvironmentVariable getEnv,
        string cwd,
        CanonicalPath? canonicalise = null)
    {
        var builder = await TestApplication.CreateBuilderAsync([]);
        string? captured = null;

        Dotnet.TddGuardBuilder.Register(
            builder,
            getEnv: getEnv,
            getCwd: () => cwd,
            canonicalise: canonicalise ?? (path => path),
            log: msg => captured = msg);

        return captured;
    }

    private static GetEnvironmentVariable EnvOf(string name, string value)
        => requested => requested == name ? value : null;

    private static string TempPath()
        => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
}
