using OneOf;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet;

/// <summary>
/// Establishes the project root the report is written beneath, or explains why the
/// reporter cannot run. Declared here rather than in the core because the result type
/// is a resolution concern, not part of the report the core builds.
/// </summary>
internal delegate OneOf<ProjectRoot, ResolveError> ResolveProjectRoot();

/// <summary>
/// Establishes the directory <c>.claude/tdd-guard/data/test.json</c> is written beneath.
/// Reads <c>TDD_GUARD_PROJECT_ROOT</c>, then <c>CLAUDE_PROJECT_DIR</c>, and reports a
/// <see cref="ResolveError"/> rather than throwing when neither is set (per ADR-010) or
/// when the working directory lies outside the root, so the reporter can disable itself.
/// </summary>
internal static class ProjectRootResolver
{
    internal static OneOf<ProjectRoot, ResolveError> Resolve(
        GetEnvironmentVariable getEnvVar,
        GetCurrentWorkingDirectory getCwd,
        CanonicalPath canonicalise)
    {
        var configured = getEnvVar("TDD_GUARD_PROJECT_ROOT")
            ?? getEnvVar("CLAUDE_PROJECT_DIR");

        if (string.IsNullOrEmpty(configured))
            return new ResolveError("No project root configured (set TDD_GUARD_PROJECT_ROOT)");

        var root = canonicalise(Path.GetFullPath(configured));
        var cwd = canonicalise(Path.GetFullPath(getCwd()));

        return IsWithin(cwd, root)
            ? new ProjectRoot(root)
            : new ResolveError($"Working directory '{cwd}' is not within project root '{root}'");
    }

    private static bool IsWithin(string child, string parent)
    {
        var normalisedParent = WithTrailingSeparator(parent);
        var normalisedChild = WithTrailingSeparator(child);

        // Case-insensitive to match Windows path semantics and .NET's own Path behaviour.
        // On case-sensitive Linux filesystems this is technically lenient, but consistent
        // with how Directory.GetCurrentDirectory() and Path.GetFullPath() normalise paths.
        return normalisedChild.StartsWith(normalisedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static string WithTrailingSeparator(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
}
