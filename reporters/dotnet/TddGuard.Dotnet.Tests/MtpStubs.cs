using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;

namespace TddGuard.Dotnet.Tests;

internal static class MtpStubs
{
    internal static StubTestSessionContext StubSessionContext()
    {
        return new StubTestSessionContext();
    }

    internal static StubDataProducer StubProducer()
    {
        return new StubDataProducer();
    }

    internal static TestNodeUpdateMessage MakeTestUpdate(
        string name,
        TestNodeStateProperty stateProperty,
        string? filePath = "DefaultFile.cs")
    {
        var properties = filePath != null
            ? new PropertyBag(stateProperty, new TestFileLocationProperty(filePath, new LinePositionSpan(new LinePosition(1, 0), new LinePosition(1, 0))))
            : new PropertyBag(stateProperty);

        var node = new TestNode
        {
            Uid = new TestNodeUid($"assembly/TestClass/{name}"),
            DisplayName = name,
            Properties = properties,
        };

        return new TestNodeUpdateMessage(default, node);
    }

    internal sealed class StubTestSessionContext : ITestSessionContext
    {
        public Microsoft.Testing.Platform.TestHost.SessionUid SessionUid => new("stub-session");
        public CancellationToken CancellationToken => CancellationToken.None;
    }

    internal sealed class StubDataProducer : IDataProducer
    {
        public string Uid => "stub";
        public string Version => "1.0.0";
        public string DisplayName => "Stub";
        public string Description => "Stub producer";
        public Type[] DataTypesProduced => [];
        public Task<bool> IsEnabledAsync() => Task.FromResult(true);
    }

}
