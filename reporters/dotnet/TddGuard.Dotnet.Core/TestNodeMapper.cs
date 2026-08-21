namespace TddGuard.Dotnet.Core;

/// <summary>
/// Maps a classified test node into a <see cref="CollectedResult"/>.
/// <para>
/// Every name is derived from the <see cref="TestIdentity"/> case, so each framework
/// shape is handled by an arm the compiler checks rather than by inspecting string
/// punctuation. Module IDs are reported relative to the project root to match the
/// other TDD Guard reporters and stay stable across machines, falling back to the
/// declaring type when the framework reports no file.
/// </para>
/// </summary>
public static class TestNodeMapper
{
    public static CollectedResult ToCollectedResult(this TestNodeInput input, string projectRoot)
    {
        var fullName = FullName(input.Identity);

        return new CollectedResult(
            Name: MemberName(input.Identity),
            FullName: fullName,
            ModuleId: ModuleId(input, projectRoot, fullName),
            State: input.State);
    }

    /// <summary>The fully qualified name, as far as the framework supplied one.</summary>
    private static string FullName(TestIdentity identity)
        => identity switch
        {
            TestIdentity.Structured s => Qualify(s.Namespace, s.TypeName, s.MethodName),
            TestIdentity.QualifiedName q => q.Value,
            TestIdentity.QualifiedIdentifier q => q.Value,
            // The UID, not the display name: MTP guarantees the UID unique per test,
            // so it is what keeps two unqualified tests sharing a label from merging.
            TestIdentity.Unqualified u => u.Uid,
            // Unreachable: TestIdentity's constructor is private and its variants are
            // sealed. See TestRunSummariser for why the arm is still required.
            _ => throw new InvalidOperationException($"Unknown TestIdentity: {identity}"),
        };

    /// <summary>
    /// The short, per-test name. The other TDD Guard reporters report the bare member
    /// name here, so a qualified name is reduced to its last segment.
    /// </summary>
    private static string MemberName(TestIdentity identity)
        => identity switch
        {
            TestIdentity.Structured s => s.MethodName,
            TestIdentity.QualifiedName q => LastSegment(q.Value),
            TestIdentity.QualifiedIdentifier q => q.MemberName,
            TestIdentity.Unqualified u => u.DisplayName,
            _ => throw new InvalidOperationException($"Unknown TestIdentity: {identity}"),
        };

    /// <summary>
    /// Groups tests by source file when the framework reports one. Otherwise groups by
    /// declaring type, so sibling tests still share a module rather than each becoming
    /// a module of its own.
    /// </summary>
    private static string ModuleId(TestNodeInput input, string projectRoot, string fullName)
    {
        if (!string.IsNullOrEmpty(input.FilePath))
            return Relativize(input.FilePath, projectRoot);

        return input.Identity switch
        {
            TestIdentity.Structured s => Qualify(s.Namespace, s.TypeName),
            _ => DeclaringScope(fullName),
        };
    }

    /// <summary>Everything before the final separator, or the whole name if there is none.</summary>
    private static string DeclaringScope(string fullName)
    {
        var lastSeparator = fullName.LastIndexOfAny(['.', '/']);
        return lastSeparator > 0 ? fullName[..lastSeparator] : fullName;
    }

    private static string LastSegment(string value)
    {
        var lastSeparator = value.LastIndexOfAny(['.', '/']);
        return lastSeparator >= 0 && lastSeparator < value.Length - 1
            ? value[(lastSeparator + 1)..]
            : value;
    }

    private static string Qualify(params string[] parts)
        => string.Join('.', parts.Where(p => !string.IsNullOrEmpty(p)));

    /// <summary>
    /// Expresses <paramref name="filePath"/> relative to <paramref name="projectRoot"/>
    /// using forward slashes. Paths outside the root are returned unchanged, since a
    /// <c>../</c> chain is less useful to a reader than the original absolute path.
    /// </summary>
    private static string Relativize(string filePath, string projectRoot)
    {
        if (string.IsNullOrEmpty(projectRoot))
            return filePath;

        var relative = Path.GetRelativePath(projectRoot, filePath);

        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            return filePath;

        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
