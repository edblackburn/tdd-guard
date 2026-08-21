using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

/// <summary>
/// Test Data Builder for <see cref="TestNodeInput"/>, the Core-facing shape the
/// listener produces once it has classified a test node.
/// Entry point: <c>An.Node()</c>
/// </summary>
internal sealed class TestNodeInputBuilder
{
    private TestIdentity _identity = new TestIdentity.Structured("Acme.Tests", "WidgetTests", "Should_add");
    private string? _filePath = "/repo/tests/Default.cs";
    private Core.TestState _state = new Core.TestState.Passed();

    /// <summary>The framework described the declaring method (MSTest, xUnit v3, TUnit).</summary>
    internal TestNodeInputBuilder DescribedBy(string @namespace, string typeName, string methodName)
    {
        _identity = new TestIdentity.Structured(@namespace, typeName, methodName);
        return this;
    }

    /// <summary>The framework qualified the display name (xUnit v2).</summary>
    internal TestNodeInputBuilder NamedBy(string qualifiedName)
    {
        _identity = new TestIdentity.QualifiedName(qualifiedName);
        return this;
    }

    /// <summary>The framework qualified the node identifier instead (NUnit).</summary>
    internal TestNodeInputBuilder IdentifiedBy(string qualifiedIdentifier, string memberName)
    {
        _identity = new TestIdentity.QualifiedIdentifier(qualifiedIdentifier, memberName);
        return this;
    }

    /// <summary>The framework supplied nothing qualified — a digest or a bare label.</summary>
    internal TestNodeInputBuilder Unqualified(string uid, string displayName)
    {
        _identity = new TestIdentity.Unqualified(uid, displayName);
        return this;
    }

    internal TestNodeInputBuilder InFile(string? filePath)
    {
        _filePath = filePath;
        return this;
    }

    internal TestNodeInputBuilder WithNoFile()
    {
        _filePath = null;
        return this;
    }

    internal TestNodeInputBuilder WithState(Core.TestState state)
    {
        _state = state;
        return this;
    }

    internal TestNodeInput Build() => new(_identity, _filePath, _state);
}
