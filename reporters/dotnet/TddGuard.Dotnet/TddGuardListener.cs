using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Services;
using System.Collections.Concurrent;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet;

/// <summary>
/// MTP V2 extension that consumes <see cref="TestNodeUpdateMessage"/> events
/// and writes a <c>test.json</c> report when the test session finishes.
/// Thread-safe: concurrent <see cref="ConsumeAsync"/> calls are supported via <see cref="ConcurrentQueue{T}"/>.
/// </summary>
public sealed class TddGuardListener(WriteTestOutput writeOutput, MapTestNode mapNode, string version)
    : ITestSessionLifetimeHandler, IDataConsumer, IExtension
{
    private ConcurrentQueue<CollectedResult> _results = [];

    public string Uid => "TddGuard.Dotnet";

    public string Version => version;
    public string DisplayName => "TDD Guard";
    public string Description => "TDD Guard test reporter";

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        testSessionContext.CancellationToken.ThrowIfCancellationRequested();
        _results = [];
        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        testSessionContext.CancellationToken.ThrowIfCancellationRequested();
        _results.WriteTestReport(writeOutput);
        return Task.CompletedTask;
    }

    // The token must be observed through ThrowIfCancellationRequested rather than by
    // constructing an OperationCanceledException: the platform recognises the run's own
    // token and unwinds quietly, whereas an exception carrying no token escapes as
    // unhandled and aborts the host.
    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value is not TestNodeUpdateMessage update)
            return Task.CompletedTask;

        var node = update.TestNode;
        // MTP fires Discovered during enumeration and InProgress when a test starts;
        // we only collect terminal states (Passed, Failed, Skipped, Error).
        // SingleOrDefault is safe here: a PropertyBag rejects a second state property
        // outright, so more than one is unrepresentable rather than merely unexpected.
        var stateProperty = node.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (stateProperty is null or InProgressTestNodeStateProperty or DiscoveredTestNodeStateProperty)
            return Task.CompletedTask;

        Core.TestState state = stateProperty switch
        {
            FailedTestNodeStateProperty f => new Core.TestState.Failed(
                f.Exception is not null ? [new TestEntryError(f.Exception.Message)]
                : !string.IsNullOrEmpty(f.Explanation) ? [new TestEntryError(f.Explanation)]
                : []),
            ErrorTestNodeStateProperty e => new Core.TestState.Failed(
                e.Exception is not null ? [new TestEntryError(e.Exception.Message)]
                : !string.IsNullOrEmpty(e.Explanation) ? [new TestEntryError(e.Explanation)]
                : []),
            TimeoutTestNodeStateProperty t => new Core.TestState.Failed(
                t.Exception is not null ? [new TestEntryError(t.Exception.Message)]
                : !string.IsNullOrEmpty(t.Explanation) ? [new TestEntryError(t.Explanation)]
                : []),
            SkippedTestNodeStateProperty => new Core.TestState.Skipped(),
            PassedTestNodeStateProperty => new Core.TestState.Passed(),
            // Fail closed. Covers cancelled tests (the platform never synthesises a
            // terminal cancelled state, and the obsolete property is producer-only) and
            // any state MTP adds later. Explanation is declared on the base type, so the
            // reason survives without naming a deprecated subclass.
            // Reporting a failure rather than throwing is the opposite of
            // TestRunSummariser's catch-all, and deliberately so: TestNodeStateProperty
            // has a protected constructor, so a framework can define a state we have
            // never seen, and a guard must not read that as a pass.
            _ => new Core.TestState.Failed(
                !string.IsNullOrEmpty(stateProperty.Explanation)
                    ? [new TestEntryError(stateProperty.Explanation)]
                    : []),
        };
        // FirstOrDefault, not SingleOrDefault: a PropertyBag rejects duplicate state
        // properties but permits more than one location or identifier, and asking for
        // exactly one throws out of ConsumeAsync on the platform's event pump — losing
        // the whole report rather than one node's metadata.
        var filePath = node.Properties.FirstOrDefault<TestFileLocationProperty>()?.FilePath;

        var input = new TestNodeInput(
            Identity: Classify(node),
            FilePath: filePath,
            State: state);

        _results.Enqueue(mapNode(input));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Decides which shape of name this framework supplied, so that everything
    /// downstream reads a stated fact rather than re-deriving one.
    /// <para>
    /// A described method is preferred wherever it exists (MSTest, xUnit v3, TUnit).
    /// The remaining frameworks leave only the node identifier and the display name,
    /// exactly one of which is qualified — the xUnit family qualifies the display name
    /// and leaves a digest as the identifier, NUnit does the reverse — and MTP reports
    /// no framework name on the node to tell them apart. Looking for a namespace
    /// separator is therefore unavoidable here; confining it to this method keeps it
    /// out of the report-building code.
    /// </para>
    /// </summary>
    private static TestIdentity Classify(TestNode node)
    {
        var method = node.Properties.FirstOrDefault<TestMethodIdentifierProperty>();
        if (method is not null)
            return new TestIdentity.Structured(method.Namespace, method.TypeName, method.MethodName);

        var displayName = node.DisplayName;
        var uid = node.Uid.Value;

        if (IsQualified(displayName))
            return new TestIdentity.QualifiedName(displayName);

        if (IsQualified(uid))
            return new TestIdentity.QualifiedIdentifier(uid, displayName);

        return new TestIdentity.Unqualified(
            uid, !string.IsNullOrEmpty(displayName) ? displayName : uid);
    }

    /// <summary>
    /// Whether a name carries a namespace or path separator. Digests and GUIDs carry
    /// neither, which is what distinguishes them from a qualified name.
    /// </summary>
    private static bool IsQualified(string value)
        => value.Contains('.', StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal);
}
