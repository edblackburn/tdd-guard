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
public sealed class TddGuardListener(WriteTestOutput writeOutput, string projectRoot)
    : ITestSessionLifetimeHandler, IDataConsumer, IExtension
{
    private ConcurrentQueue<CollectedResult> _results = [];

    public string Uid => "TddGuard.Dotnet";

    // Sourced from the MSBuild <Version> in Directory.Build.props (via AssemblyVersion)
    // so this never drifts from the package version.
    public string Version => typeof(TddGuardListener).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    public string DisplayName => "TDD Guard";
    public string Description => "TDD Guard test reporter";

    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        _results = [];
        return Task.CompletedTask;
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        _results.WriteTestReport(writeOutput);
        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is not TestNodeUpdateMessage update)
            return Task.CompletedTask;

        var node = update.TestNode;
        // MTP fires Discovered during enumeration and InProgress when a test starts;
        // we only collect terminal states (Passed, Failed, Skipped, Error).
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
            // MTP0001 deprecates this state for test framework *authors*, directing them
            // to throw OperationCanceledException instead. As a data consumer we still
            // receive it from frameworks that have not migrated, and the cancellation
            // reason is worth surfacing, so it stays explicitly handled.
#pragma warning disable MTP0001
            CancelledTestNodeStateProperty c => new Core.TestState.Failed(
                c.Exception is not null ? [new TestEntryError(c.Exception.Message)]
                : !string.IsNullOrEmpty(c.Explanation) ? [new TestEntryError(c.Explanation)]
                : []),
#pragma warning restore MTP0001
            SkippedTestNodeStateProperty => new Core.TestState.Skipped(),
            PassedTestNodeStateProperty => new Core.TestState.Passed(),
            // Fail closed: any state MTP introduces in the future that we do not
            // explicitly recognise is treated as a failure, not a silent pass.
            _ => new Core.TestState.Failed([]),
        };
        var filePath = node.Properties.SingleOrDefault<TestFileLocationProperty>()?.FilePath;

        // Only MSTest, xUnit v3 and TUnit populate this; NUnit and xUnit v2 leave it
        // absent, in which case the mapper falls back to the display name.
        var method = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        var methodIdentifier = method is null
            ? null
            : new TestMethodIdentifier(method.Namespace, method.TypeName, method.MethodName);

        var input = new TestNodeInput(
            Uid: node.Uid.Value,
            DisplayName: node.DisplayName,
            FilePath: filePath,
            State: state,
            MethodIdentifier: methodIdentifier);

        _results.Enqueue(input.ToCollectedResult(projectRoot));
        return Task.CompletedTask;
    }
}
