using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using OneOf;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet;

/// <summary>
/// Public entry point for MTP V2 auto-registration.
/// Called by the generated <c>TestingPlatformBuilderHook</c> via the
/// <c>buildTransitive/*.props</c> MSBuild item shipped in the NuGet package.
/// <para>
/// This is the composition root: it holds every production default and wires the
/// reporter from ports, so nothing below it reads the environment or names a file.
/// </para>
/// </summary>
public static class TddGuardBuilder
{
    /// <summary>
    /// Registers the reporter with a test host, unless no project root can be
    /// established — in which case it stays disabled and says why.
    /// </summary>
    /// <param name="builder">The host to register with.</param>
    /// <param name="getEnv">Reads configuration. Defaults to the process environment.</param>
    /// <param name="getCwd">Reads the working directory. Defaults to the process.</param>
    /// <param name="canonicalise">Follows symlinks when comparing paths. Defaults to the filesystem.</param>
    /// <param name="openOutput">How the report is written. Defaults to a file under the root.</param>
    /// <param name="log">Where diagnostics go. Defaults to standard error.</param>
    public static void Register(
        ITestApplicationBuilder builder,
        GetEnvironmentVariable? getEnv = null,
        GetCurrentWorkingDirectory? getCwd = null,
        CanonicalPath? canonicalise = null,
        OpenTestOutput? openOutput = null,
        LogDiagnostic? log = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var diagnose = log ?? WriteToStandardError;
        var open = openOutput ?? ReportFileWriter.Create;

        ResolveProjectRoot resolve = () => ProjectRootResolver.Resolve(
            getEnv ?? Environment.GetEnvironmentVariable,
            getCwd ?? Directory.GetCurrentDirectory,
            canonicalise ?? FollowLinks);

        resolve()
        .LogOnError(diagnose)
        .Switch(
            root =>
            {
                var write = open(root.Path).WithDiagnostics(diagnose);
                MapTestNode map = input => input.ToCollectedResult(root.Path);
                var compositeFactory = new CompositeExtensionFactory<TddGuardListener>(
                    () => new TddGuardListener(write, map, PackageVersion));
                builder.TestHost.AddTestSessionLifetimeHandler(compositeFactory);
                builder.TestHost.AddDataConsumer(compositeFactory);
            },
            error => { } // Disabled — already logged by LogOnError
        );
    }

    /// <summary>
    /// Sourced from the MSBuild <c>&lt;Version&gt;</c> in Directory.Build.props via assembly
    /// metadata, so what the platform reports never drifts from the published package.
    /// </summary>
    private static string PackageVersion
        => typeof(TddGuardBuilder).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Follows symlinks so that a root and a working directory naming the same
    /// directory by different paths compare equal — macOS reaches /private/var
    /// through /var, for instance. Paths that do not exist are left as given.
    /// </summary>
    private static string FollowLinks(string path)
    {
        try
        {
            return new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? path;
        }
        catch (IOException)
        {
            return path;
        }
    }

    private static void WriteToStandardError(string message)
        => Console.Error.WriteLine($"[tdd-guard-dotnet] {message}");
}
