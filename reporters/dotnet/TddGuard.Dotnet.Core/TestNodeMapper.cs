namespace TddGuard.Dotnet.Core;

/// <summary>
/// Maps raw MTP test node input into a <see cref="CollectedResult"/>.
/// <para>
/// MTP test node UIDs are framework-defined and opaque in several cases: MSTest
/// emits a GUID, xUnit v3 a SHA-256, xUnit v2 a SHA-1. They are therefore unusable
/// as a human-readable identifier, so the full name is derived from the structured
/// method identifier where the framework supplies one, falling back to the display
/// name (already fully qualified for the xUnit family) and only then to the UID.
/// </para>
/// <para>
/// Module IDs are reported relative to the project root so they match the other
/// TDD Guard reporters and stay stable across machines. Frameworks that supply no
/// file location (NUnit, xUnit v2) group by declaring type instead, which keeps
/// modules meaningful rather than keying each test to its own digest.
/// </para>
/// </summary>
public static class TestNodeMapper
{
    public static CollectedResult ToCollectedResult(this TestNodeInput input, string projectRoot)
    {
        var fullName = ResolveFullName(input);
        var moduleId = ResolveModuleId(input, projectRoot, fullName);
        var name = ResolveName(input);

        return new CollectedResult(name, fullName, moduleId, input.State);
    }

    /// <summary>
    /// The short, per-test name. The other TDD Guard reporters report the bare
    /// method name here, but xUnit sets the display name to the fully qualified
    /// name, so the structured identifier takes precedence when available.
    /// </summary>
    private static string ResolveName(TestNodeInput input)
    {
        if (input.MethodIdentifier is { MethodName.Length: > 0 } id)
            return id.MethodName;

        // xUnit v2 supplies no identifier, only a dotted fully qualified display
        // name, so the trailing segment is the nearest thing to a method name.
        // Frameworks whose display names are prose (containing spaces) are left
        // alone, since a dot there is punctuation rather than a namespace separator.
        var displayName = input.DisplayName;
        if (!displayName.Contains(' ', StringComparison.Ordinal))
        {
            var lastDot = displayName.LastIndexOf('.');
            if (lastDot > 0 && lastDot < displayName.Length - 1)
                return displayName[(lastDot + 1)..];
        }

        return displayName;
    }

    private static string ResolveFullName(TestNodeInput input)
    {
        if (input.MethodIdentifier is { } id)
            return Qualify(id.Namespace, id.TypeName, id.MethodName);

        // Without a method identifier the framework leaves only the UID and the
        // display name, and which one is qualified varies: xUnit puts the fully
        // qualified name in the display name and a digest in the UID, while NUnit
        // does the opposite. Prefer whichever already looks qualified, treating a
        // digest as unqualified because it carries no separator.
        var uid = input.Uid;
        var displayName = input.DisplayName;

        if (IsQualified(displayName))
            return displayName;

        if (IsQualified(uid))
            return uid;

        return !string.IsNullOrEmpty(displayName) ? displayName : uid;
    }

    /// <summary>
    /// Treats a name as qualified when it carries a namespace-style or path-style
    /// separator. Opaque UIDs (GUIDs, SHA digests) have none and are rejected.
    /// </summary>
    private static bool IsQualified(string value)
        => value.Contains('.', StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal);

    private static string ResolveModuleId(TestNodeInput input, string projectRoot, string fullName)
    {
        if (!string.IsNullOrEmpty(input.FilePath))
            return Relativize(input.FilePath, projectRoot);

        if (input.MethodIdentifier is { } id)
            return Qualify(id.Namespace, id.TypeName);

        // Last resort: strip the trailing member from the resolved full name so
        // sibling tests still share a module. Falls back to the whole name when
        // there is nothing to strip.
        var lastSeparator = fullName.LastIndexOfAny(['.', '/']);
        return lastSeparator > 0 ? fullName[..lastSeparator] : fullName;
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
