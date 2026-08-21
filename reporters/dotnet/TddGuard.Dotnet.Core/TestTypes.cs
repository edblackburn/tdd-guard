namespace TddGuard.Dotnet.Core;

/// <summary>
/// Sealed discriminated union representing the outcome of a single test.
/// </summary>
public abstract record TestState
{
    private TestState() { }

    public sealed record Passed : TestState;
    public sealed record Failed(IReadOnlyList<TestEntryError> Errors) : TestState;
    public sealed record Skipped : TestState;
}

/// <summary>Error message captured from a failed test assertion or exception.</summary>
public sealed record TestEntryError(string Message);

/// <summary>
/// How a test framework named a test, classified at the point the report is received.
/// <para>
/// Frameworks disagree sharply about where the usable name lives. Some describe the
/// declaring method outright; the rest leave only two strings, one of which is an
/// opaque digest, and which one carries the name differs between them. Naming the
/// four shapes here means the ambiguity is resolved once, against the framework's
/// own metadata, rather than re-inferred from string punctuation wherever a name
/// is needed.
/// </para>
/// </summary>
public abstract record TestIdentity
{
    private TestIdentity() { }

    /// <summary>The framework described the declaring method.</summary>
    public sealed record Structured(string Namespace, string TypeName, string MethodName) : TestIdentity;

    /// <summary>
    /// No method description, but the fully qualified name is the display name.
    /// </summary>
    public sealed record QualifiedName(string Value) : TestIdentity;

    /// <summary>
    /// No method description, but the fully qualified name is the node identifier,
    /// leaving the display name as the bare member name.
    /// </summary>
    public sealed record QualifiedIdentifier(string Value, string MemberName) : TestIdentity;

    /// <summary>
    /// Nothing qualified was supplied — typically only a digest or a bare label.
    /// <see cref="Uid"/> is what the report distinguishes tests by: MTP documents the
    /// node UID as unique per test, a guarantee the display name does not carry, and
    /// two tests can otherwise share an identical unqualified label.
    /// </summary>
    public sealed record Unqualified(string Uid, string DisplayName) : TestIdentity;
}

/// <summary>
/// Raw input from the MTP test node, before module grouping.
/// Decoupled from MTP types so Core has no platform dependency.
/// </summary>
public sealed record TestNodeInput(TestIdentity Identity, string? FilePath, TestState State);

/// <summary>
/// Processed test result after UID parsing and module assignment.
/// Produced by <see cref="TestNodeMapper.ToCollectedResult"/>.
/// </summary>
public sealed record CollectedResult(string Name, string FullName, string ModuleId, TestState State);
