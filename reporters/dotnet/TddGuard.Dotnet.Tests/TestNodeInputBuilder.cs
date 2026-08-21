using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

/// <summary>
/// Test Data Builder for <see cref="TestNodeInput"/>, the Core-facing shape the
/// listener produces from an MTP test node.
/// Entry point: <c>An.Node()</c>
/// </summary>
internal sealed class TestNodeInputBuilder
{
    private string _uid = "assembly/Class/Method";
    private string _displayName = "Method";
    private string? _filePath = "/repo/tests/Default.cs";
    private TestMethodIdentifier? _methodIdentifier;
    private Core.TestState _state = new Core.TestState.Passed();

    internal TestNodeInputBuilder WithUid(string uid)
    {
        _uid = uid;
        return this;
    }

    internal TestNodeInputBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    internal TestNodeInputBuilder WithFilePath(string? filePath)
    {
        _filePath = filePath;
        return this;
    }

    internal TestNodeInputBuilder WithMethodIdentifier(
        string @namespace,
        string typeName,
        string methodName)
    {
        _methodIdentifier = new TestMethodIdentifier(@namespace, typeName, methodName);
        return this;
    }

    internal TestNodeInputBuilder WithState(Core.TestState state)
    {
        _state = state;
        return this;
    }

    internal TestNodeInput Build()
        => new(_uid, _displayName, _filePath, _state, _methodIdentifier);
}
