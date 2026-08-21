using Microsoft.Testing.Platform.Extensions.Messages;
using TddGuard.Dotnet.Core;
using static TddGuard.Dotnet.Tests.MtpStubs;

namespace TddGuard.Dotnet.Tests;

internal sealed class TddGuardListenerTests
{
    [Test("writes 'passed' reason for passing test")]
    public async Task WritesPassedReasonForPassingTest()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Passed(), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Reason()).IsEqualTo("passed");
            });
    }

    [Test("writes 'failed' reason when any test fails")]
    public async Task WritesFailedReasonWhenAnyTestFails()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Passed(), default);
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test2").Failed("boom"), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Reason()).IsEqualTo("failed");
            });
    }

    [Test("skipped tests count as passed for reason")]
    public async Task SkippedTestsCountAsPassedForReason()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Skipped(), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Reason()).IsEqualTo("passed");
                await Assert.That(root.Module().Test().State()).IsEqualTo("skipped");
            });
    }

    [Test("does not write when no tests run")]
    public async Task DoesNotWriteWhenNoTestsRun()
    {
        await ListenerFixture
            .Arrange()
            .Act()
            .AssertNoOutput();
    }

    [Test("includes error message from exception")]
    public async Task IncludesErrorMessageFromException()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Failed("Expected 6 but got 5"), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.ErrorMessage()).Contains("Expected 6 but got 5");
            });
    }

    [Test("uses explanation when no exception")]
    public async Task UsesExplanationWhenNoException()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").FailedWithExplanation("assertion failed"), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).IsEqualTo("assertion failed");
            });
    }

    [Test("passed test has no errors in JSON")]
    public async Task PassedTestHasNoErrorsInJson()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Passed(), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().HasErrors()).IsFalse();
            });
    }

    [Test("uses file path as module ID")]
    public async Task UsesFilePathAsModuleId()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").InFile("/src/Tests.cs").Passed(), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().ModuleId()).IsEqualTo("/src/Tests.cs");
            });
    }

    // Frameworks that supply no file location (NUnit, xUnit v2) must still group
    // into a meaningful module rather than one module per test.
    [Test("falls back to the declaring type as module ID when no file path is present")]
    public async Task FallsBackToDeclaringTypeAsModuleId()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").WithNoFilePath().Passed(), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().ModuleId()).IsEqualTo("assembly/TestClass");
                await Assert.That(root.Module().Test().FullName()).IsEqualTo("assembly/TestClass/test1");
            });
    }

    // MSTest emits a GUID, xUnit v3 a SHA-256 and xUnit v2 a SHA-1 as the node UID,
    // so an opaque UID must never reach test.json as the full name.
    [Test("prefers the structured method identifier over an opaque UID")]
    public async Task PrefersMethodIdentifierOverOpaqueUid()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                var node = new TestNode
                {
                    Uid = new TestNodeUid("65964d4e887280606a1ad75414c458b8910c3e2c7fb4dde0575f5209b48a4dd4"),
                    DisplayName = "Should_add_numbers",
                    Properties = new PropertyBag(
                        new PassedTestNodeStateProperty(),
                        new TestMethodIdentifierProperty(
                            assemblyFullName: "Acme.Tests",
                            @namespace: "Acme.Tests",
                            typeName: "CalculatorTests",
                            methodName: "Should_add_numbers",
                            methodArity: 0,
                            parameterTypeFullNames: [],
                            returnTypeFullName: "System.Void")),
                };
                var update = new TestNodeUpdateMessage(default, node);
                await listener.ConsumeAsync(StubProducer(), update, default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().FullName())
                    .IsEqualTo("Acme.Tests.CalculatorTests.Should_add_numbers");
                await Assert.That(root.Module().ModuleId()).IsEqualTo("Acme.Tests.CalculatorTests");
            });
    }

    // PropertyBag permits more than one TestFileLocationProperty on a node (unlike state
    // properties, which it rejects outright). A reporter that asks for exactly one would
    // throw out of ConsumeAsync on the platform's event pump, losing the whole report —
    // the false green the fail-closed mapping exists to prevent.
    [Test("reports a test whose node carries more than one file location")]
    public async Task ReportsTestWithDuplicateFileLocations()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                var span = new LinePositionSpan(new LinePosition(1, 0), new LinePosition(1, 0));
                var node = new TestNode
                {
                    Uid = new TestNodeUid("assembly/TestClass/test1"),
                    DisplayName = "test1",
                    Properties = new PropertyBag(
                        new PassedTestNodeStateProperty(),
                        new TestFileLocationProperty("/src/First.cs", span),
                        new TestFileLocationProperty("/src/Second.cs", span)),
                };
                await listener.ConsumeAsync(StubProducer(), new TestNodeUpdateMessage(default, node), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().State()).IsEqualTo("passed");
            });
    }

    [Test("reports a test whose node carries more than one method identifier")]
    public async Task ReportsTestWithDuplicateMethodIdentifiers()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                var node = new TestNode
                {
                    Uid = new TestNodeUid("assembly/TestClass/test1"),
                    DisplayName = "test1",
                    Properties = new PropertyBag(
                        new PassedTestNodeStateProperty(),
                        new TestMethodIdentifierProperty(
                            assemblyFullName: "Acme.Tests",
                            @namespace: "Acme.Tests",
                            typeName: "CalculatorTests",
                            methodName: "First",
                            methodArity: 0,
                            parameterTypeFullNames: [],
                            returnTypeFullName: "System.Void"),
                        new TestMethodIdentifierProperty(
                            assemblyFullName: "Acme.Tests",
                            @namespace: "Acme.Tests",
                            typeName: "CalculatorTests",
                            methodName: "Second",
                            methodArity: 0,
                            parameterTypeFullNames: [],
                            returnTypeFullName: "System.Void")),
                };
                await listener.ConsumeAsync(StubProducer(), new TestNodeUpdateMessage(default, node), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().State()).IsEqualTo("passed");
            });
    }

    [Test("uses display name as test name")]
    public async Task UsesDisplayNameAsTestName()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("Should_add_numbers").Passed(), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().Name()).IsEqualTo("Should_add_numbers");
            });
    }

    [Test("ignores in-progress nodes")]
    public async Task IgnoresInProgressNodes()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").InProgress(), default);
            })
            .Act()
            .AssertNoOutput();
    }

    [Test("ignores discovered nodes")]
    public async Task IgnoresDiscoveredNodes()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Discovered(), default);
            })
            .Act()
            .AssertNoOutput();
    }

    [Test("maps error state to failed")]
    public async Task MapsErrorStateToFailed()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Error("setup exploded"), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.ErrorMessage()).Contains("setup exploded");
            });
    }

    [Test("maps timeout state to failed")]
    public async Task MapsTimeoutStateToFailed()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").TimedOut("exceeded 30s"), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.ErrorMessage()).Contains("exceeded 30s");
            });
    }

    [Test("maps cancelled state to failed with the cancellation message")]
    public async Task MapsCancelledStateToFailed()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Cancelled("run was cancelled"), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.ErrorMessage()).Contains("run was cancelled");
            });
    }

    [Test("timeout with no exception and no explanation produces empty errors array")]
    public async Task TimeoutWithNoExceptionNoExplanationProducesEmptyErrorsArray()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").TimedOutBare(), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.GetProperty("errors").GetArrayLength()).IsEqualTo(0);
            });
    }

    [Test("cancelled with no exception and no explanation produces empty errors array")]
    public async Task CancelledWithNoExceptionNoExplanationProducesEmptyErrorsArray()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").CancelledBare(), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.GetProperty("errors").GetArrayLength()).IsEqualTo(0);
            });
    }

    [Test("ignores non-TestNodeUpdateMessage data")]
    public async Task IgnoresNonTestNodeUpdateMessageData()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                var stubData = new MtpStubs.StubData("not a test update", null);
                await listener.ConsumeAsync(StubProducer(), stubData, default);
            })
            .Act()
            .AssertNoOutput();
    }

    [Test("failed with no exception and no explanation produces empty errors array")]
    public async Task FailedWithNoExceptionNoExplanationProducesEmptyErrorsArray()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").FailedBare(), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.GetProperty("errors").GetArrayLength()).IsEqualTo(0);
            });
    }

    [Test("error with no exception and no explanation produces empty errors array")]
    public async Task ErrorWithNoExceptionNoExplanationProducesEmptyErrorsArray()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").ErrorBare(), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.GetProperty("errors").GetArrayLength()).IsEqualTo(0);
            });
    }

    // The platform recognises an OperationCanceledException carrying the run's own token
    // and unwinds without faulting the host, which is why the token is observed through
    // ThrowIfCancellationRequested rather than by constructing the exception directly.
    [Test("stops consuming once the run is cancelled")]
    public async Task StopsConsumingOnceRunIsCancelled()
    {
        var (listener, _, tempDir) = ListenerFixture.Create();
        try
        {
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.That(() => listener.ConsumeAsync(
                    StubProducer(),
                    An.Event().Named("test1").Passed(),
                    cancelled.Token))
                .Throws<OperationCanceledException>();

            await listener.OnTestSessionFinishingAsync(StubSessionContext());
            await Assert.That(ListenerFixture.HasTestJson(tempDir)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test("clears results between sessions")]
    public async Task ClearsResultsBetweenSessions()
    {
        var (listener, readJson, tempDir) = ListenerFixture.Create();
        try
        {
            // First session: one passing test produces output
            await listener.OnTestSessionStartingAsync(StubSessionContext());
            await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Passed(), default);
            await listener.OnTestSessionFinishingAsync(StubSessionContext());

            await Assert.That(ListenerFixture.HasTestJson(tempDir)).IsTrue();

            // Delete the file so we can detect whether session 2 writes
            File.Delete(ListenerFixture.JsonPath(tempDir));

            // Second session: no tests, should not recreate file
            await listener.OnTestSessionStartingAsync(StubSessionContext());
            await listener.OnTestSessionFinishingAsync(StubSessionContext());

            await Assert.That(ListenerFixture.HasTestJson(tempDir)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test("groups tests by module")]
    public async Task GroupsTestsByModule()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").InFile("/src/ModuleA.cs").Passed(), default);
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test2").InFile("/src/ModuleB.cs").Passed(), default);
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test3").InFile("/src/ModuleA.cs").Passed(), default);
            })
            .Act()
            .Assert(async root =>
            {
                var modules = root.GetProperty("testModules");
                await Assert.That(modules.GetArrayLength()).IsEqualTo(2);
                await Assert.That(modules[0].ModuleId()).IsEqualTo("/src/ModuleA.cs");
                await Assert.That(modules[0].Tests().GetArrayLength()).IsEqualTo(2);
                await Assert.That(modules[1].ModuleId()).IsEqualTo("/src/ModuleB.cs");
                await Assert.That(modules[1].Tests().GetArrayLength()).IsEqualTo(1);
            });
    }

    [Test("uses camelCase keys and omits nulls")]
    public async Task UsesCamelCaseKeysAndOmitsNulls()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Passed(), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.TryGetProperty("testModules", out _)).IsTrue();
                await Assert.That(root.TryGetProperty("TestModules", out _)).IsFalse();

                var module = root.Module();
                await Assert.That(module.TryGetProperty("moduleId", out _)).IsTrue();
                await Assert.That(module.TryGetProperty("ModuleId", out _)).IsFalse();

                await Assert.That(module.Test().TryGetProperty("errors", out _)).IsFalse();
            });
    }

    [Test("escapes quotes in error messages")]
    public async Task EscapesQuotesInErrorMessages()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Failed("Expected \"hello\" but got \"world\""), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).Contains("Expected \"hello\" but got \"world\"");
            });
    }

    [Test("escapes newlines in error messages")]
    public async Task EscapesNewlinesInErrorMessages()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Failed("line1\nline2\nline3"), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).Contains("line1\nline2\nline3");
            });
    }

    [Test("escapes backslashes in error messages")]
    public async Task EscapesBackslashesInErrorMessages()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Failed("path: C:\\Users\\test\\file.cs"), default);
            })
            .Act()
            .Assert(async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).Contains("C:\\Users\\test\\file.cs");
            });
    }

    [Test("handles concurrent ConsumeAsync calls safely")]
    public async Task HandlesConcurrentConsumeAsyncCallsSafely()
    {
        // 16 concurrent callers is enough to reliably trigger a race on unsynchronized
        // shared state; dedicated threads (rather than Task.Run) avoid ThreadPool
        // starvation, since the pool's slow-growth policy can stall a large batch of
        // threads that all block on the barrier before doing any real work.
        const int concurrentCallers = 16;
        var (listener, readJson, tempDir) = ListenerFixture.Create();
        try
        {
            await listener.OnTestSessionStartingAsync(StubSessionContext());

            using var barrier = new Barrier(concurrentCallers);
            var threads = Enumerable.Range(0, concurrentCallers).Select(i => new Thread(() =>
            {
                barrier.SignalAndWait();
                listener.ConsumeAsync(
                    StubProducer(),
                    An.Event().Named($"Test_{i}").InFile("/src/Tests.cs").Passed(),
                    default).GetAwaiter().GetResult();
            })).ToList();

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            await listener.OnTestSessionFinishingAsync(StubSessionContext());

            var root = TestJsonAssert.Parse(readJson());
            var tests = root.Module().Tests();
            await Assert.That(tests.GetArrayLength()).IsEqualTo(concurrentCallers);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // When both the identifier and the display name look qualified, the display name
    // wins. The xUnit family qualifies the display name while leaving a digest as the
    // identifier, so preferring the identifier would report the digest for any
    // framework whose identifier merely happens to contain a separator.
    [Test("prefers a qualified display name over a qualified identifier")]
    public async Task PrefersQualifiedDisplayNameOverQualifiedIdentifier()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                var node = new TestNode
                {
                    Uid = new TestNodeUid("assembly/Opaque/6cdcb5546dc9"),
                    DisplayName = "Acme.Tests.CalculatorTests.Should_add",
                    Properties = new PropertyBag(new PassedTestNodeStateProperty()),
                };
                await listener.ConsumeAsync(StubProducer(), new TestNodeUpdateMessage(default, node), default);
            })
            .Act()
            .Assert(async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.FullName()).IsEqualTo("Acme.Tests.CalculatorTests.Should_add");
                await Assert.That(test.Name()).IsEqualTo("Should_add");
                await Assert.That(root.Module().ModuleId()).IsEqualTo("Acme.Tests.CalculatorTests");
            });
    }

    // Neither string carries a separator, so Classify cannot tell these two tests apart
    // by punctuation alone. The node UID is documented by MTP as unique per test node,
    // so distinguishing on it — rather than the unqualified display name, which carries
    // no such guarantee — is what keeps unrelated tests from merging into one report.
    [Test("distinguishes two unqualified tests that share a display name")]
    public async Task DistinguishesUnqualifiedTestsSharingADisplayName()
    {
        await ListenerFixture
            .Arrange(async listener =>
            {
                TestNode NodeNamed(string uid) => new()
                {
                    Uid = new TestNodeUid(uid),
                    DisplayName = "should add",
                    Properties = new PropertyBag(new PassedTestNodeStateProperty()),
                };

                await listener.ConsumeAsync(StubProducer(), new TestNodeUpdateMessage(default, NodeNamed("6cdcb5546dc9")), default);
                await listener.ConsumeAsync(StubProducer(), new TestNodeUpdateMessage(default, NodeNamed("8f31a02b7e14")), default);
            })
            .Act()
            .Assert(async root =>
            {
                var firstFullName = root.Module(0).Test().FullName();
                var secondFullName = root.Module(1).Test().FullName();
                await Assert.That(firstFullName).IsNotEqualTo(secondFullName);
            });
    }

    // A failed write is the one outcome the developer cannot see for themselves: the
    // hook reads a stale test.json, or none, and the reason has to reach stderr or the
    // reporter has silently stopped guarding. This drives the real listener and the real
    // diagnostics decorator, faking only the file write.
    [Test("reports the reason to the diagnostic log when the report cannot be written")]
    public async Task ReportsReasonWhenReportCannotBeWritten()
    {
        string? captured = null;
        WriteTestOutput failing = _ => new WriteResult.Error("UnauthorizedAccessException: read-only volume");
        var listener = new Dotnet.TddGuardListener(
            failing.WithDiagnostics(msg => captured = msg),
            input => input.ToCollectedResult("/repo"),
            version: "0.0.0-test");

        await listener.OnTestSessionStartingAsync(StubSessionContext());
        await listener.ConsumeAsync(StubProducer(), An.Event().Named("test1").Passed(), default);
        await listener.OnTestSessionFinishingAsync(StubSessionContext());

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured!).Contains("test.json");
        await Assert.That(captured!).Contains("read-only volume");
    }

    // MTP reads these to identify and route to the extension. The Uid is the platform's
    // handle for this extension, and a message type missing from DataTypesConsumed is
    // never delivered — so both are contracts rather than incidental metadata.
    [Test("identifies itself to the platform and subscribes to test node updates")]
    public async Task IdentifiesItselfAndSubscribesToTestNodeUpdates()
    {
        var (listener, _, tempDir) = ListenerFixture.Create();
        try
        {
            await Assert.That(listener.Uid).IsEqualTo("TddGuard.Dotnet");
            await Assert.That(listener.DataTypesConsumed).Contains(typeof(TestNodeUpdateMessage));
            await Assert.That(await listener.IsEnabledAsync()).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    // MTP surfaces this in its extension listing. The version is supplied by the
    // composition root from assembly metadata rather than read here, so the listener
    // reports whatever it was told.
    [Test("reports the version it was given")]
    public async Task ReportsTheVersionItWasGiven()
    {
        var listener = new Dotnet.TddGuardListener(
            _ => new WriteResult.Success(),
            input => input.ToCollectedResult("/repo"),
            version: "1.2.3");

        await Assert.That(listener.Version).IsEqualTo("1.2.3");
    }
}
