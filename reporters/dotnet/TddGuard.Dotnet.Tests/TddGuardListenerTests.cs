using Microsoft.Testing.Platform.Extensions.Messages;
using TddGuard.Dotnet.Core;
using static TddGuard.Dotnet.Tests.MtpStubs;

namespace TddGuard.Dotnet.Tests;

internal sealed class TddGuardListenerTests
{
    [Test("writes 'passed' reason for passing test")]
    public async Task WritesPassedReasonForPassingTest()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty()), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Reason()).IsEqualTo("passed");
            });
    }

    [Test("writes 'failed' reason when any test fails")]
    public async Task WritesFailedReasonWhenAnyTestFails()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty()), default);
                var failedState = new FailedTestNodeStateProperty(new InvalidOperationException("boom"), "assertion failed");
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test2", failedState), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Reason()).IsEqualTo("failed");
            });
    }

    [Test("skipped tests count as passed for reason")]
    public async Task SkippedTestsCountAsPassedForReason()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new SkippedTestNodeStateProperty()), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Reason()).IsEqualTo("passed");
                await Assert.That(root.Module().Test().State()).IsEqualTo("skipped");
            });
    }

    [Test("does not write when no tests run")]
    public async Task DoesNotWriteWhenNoTestsRun()
    {
        await TestHarness.RunExpectingNoOutput(
            act: _ => Task.CompletedTask);
    }

    [Test("includes error message from exception")]
    public async Task IncludesErrorMessageFromException()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                var failedState = new FailedTestNodeStateProperty(new InvalidOperationException("Expected 6 but got 5"), "assertion failed");
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", failedState), default);
            },
            assert: async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.ErrorMessage()).Contains("Expected 6 but got 5");
            });
    }

    [Test("uses explanation when no exception")]
    public async Task UsesExplanationWhenNoException()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                var failedState = new FailedTestNodeStateProperty("assertion failed");
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", failedState), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).IsEqualTo("assertion failed");
            });
    }

    [Test("passed test has no errors in JSON")]
    public async Task PassedTestHasNoErrorsInJson()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty()), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().Test().HasErrors()).IsFalse();
            });
    }

    [Test("uses file path as module ID")]
    public async Task UsesFilePathAsModuleId()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty(), filePath: "/src/Tests.cs"), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().ModuleId()).IsEqualTo("/src/Tests.cs");
            });
    }

    [Test("falls back to UID as module ID")]
    public async Task FallsBackToUidAsModuleId()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty(), filePath: null), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().ModuleId()).Contains("assembly/TestClass/test1");
            });
    }

    [Test("strips parameters from UID")]
    public async Task StripsParametersFromUid()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                var node = new TestNode
                {
                    Uid = new TestNodeUid("assembly/TestClass/TestMethod(1, 2)"),
                    DisplayName = "TestMethod",
                    Properties = new PropertyBag(new PassedTestNodeStateProperty()),
                };
                var update = new TestNodeUpdateMessage(default, node);
                await listener.ConsumeAsync(StubProducer(), update, default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().Test().FullName()).IsEqualTo("assembly/TestClass/TestMethod");
            });
    }

    [Test("uses display name as test name")]
    public async Task UsesDisplayNameAsTestName()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("Should_add_numbers", new PassedTestNodeStateProperty()), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().Test().Name()).IsEqualTo("Should_add_numbers");
            });
    }

    [Test("ignores in-progress nodes")]
    public async Task IgnoresInProgressNodes()
    {
        await TestHarness.RunExpectingNoOutput(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new InProgressTestNodeStateProperty()), default);
            });
    }

    [Test("ignores discovered nodes")]
    public async Task IgnoresDiscoveredNodes()
    {
        await TestHarness.RunExpectingNoOutput(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new DiscoveredTestNodeStateProperty()), default);
            });
    }

    [Test("maps error state to failed")]
    public async Task MapsErrorStateToFailed()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                var errorState = new ErrorTestNodeStateProperty(new InvalidOperationException("setup exploded"), "setup failed");
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", errorState), default);
            },
            assert: async root =>
            {
                var test = root.Module().Test();
                await Assert.That(test.State()).IsEqualTo("failed");
                await Assert.That(test.ErrorMessage()).Contains("setup exploded");
            });
    }

    [Test("clears results between sessions")]
    public async Task ClearsResultsBetweenSessions()
    {
        var (listener, readJson, tempDir) = TestHarness.Create();
        try
        {
            // First session
            await listener.OnTestSessionStartingAsync(StubSessionContext());
            await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty()), default);
            await listener.OnTestSessionFinishingAsync(StubSessionContext());

            await Assert.That(TestHarness.HasTestJson(tempDir)).IsTrue();

            // Delete the file so we can detect whether session 2 writes
            File.Delete(TestHarness.JsonPath(tempDir));

            // Second session with no tests
            await listener.OnTestSessionStartingAsync(StubSessionContext());
            await listener.OnTestSessionFinishingAsync(StubSessionContext());

            await Assert.That(TestHarness.HasTestJson(tempDir)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Test("groups tests by module")]
    public async Task GroupsTestsByModule()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty(), filePath: "/src/ModuleA.cs"), default);
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test2", new PassedTestNodeStateProperty(), filePath: "/src/ModuleB.cs"), default);
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test3", new PassedTestNodeStateProperty(), filePath: "/src/ModuleA.cs"), default);
            },
            assert: async root =>
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
        await TestHarness.Run(
            act: async listener =>
            {
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", new PassedTestNodeStateProperty()), default);
            },
            assert: async root =>
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
        await TestHarness.Run(
            act: async listener =>
            {
                var failedState = new FailedTestNodeStateProperty(new InvalidOperationException("Expected \"hello\" but got \"world\""), "assertion");
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", failedState), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).Contains("Expected \"hello\" but got \"world\"");
            });
    }

    [Test("escapes newlines in error messages")]
    public async Task EscapesNewlinesInErrorMessages()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                var failedState = new FailedTestNodeStateProperty(new InvalidOperationException("line1\nline2\nline3"), "assertion");
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", failedState), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).Contains("line1\nline2\nline3");
            });
    }

    [Test("escapes backslashes in error messages")]
    public async Task EscapesBackslashesInErrorMessages()
    {
        await TestHarness.Run(
            act: async listener =>
            {
                var failedState = new FailedTestNodeStateProperty(new InvalidOperationException("path: C:\\Users\\test\\file.cs"), "assertion");
                await listener.ConsumeAsync(StubProducer(), MakeTestUpdate("test1", failedState), default);
            },
            assert: async root =>
            {
                await Assert.That(root.Module().Test().ErrorMessage()).Contains("C:\\Users\\test\\file.cs");
            });
    }

    [Test("handles concurrent ConsumeAsync calls safely")]
    public async Task HandlesConcurrentConsumeAsyncCallsSafely()
    {
        var (listener, readJson, tempDir) = TestHarness.Create();
        try
        {
            await listener.OnTestSessionStartingAsync(StubSessionContext());

            using var barrier = new Barrier(100);
            var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await listener.ConsumeAsync(
                    StubProducer(),
                    MakeTestUpdate($"Test_{i}", new PassedTestNodeStateProperty(), "/src/Tests.cs"),
                    default);
            }));
            await Task.WhenAll(tasks);

            await listener.OnTestSessionFinishingAsync(StubSessionContext());

            var root = TestJsonAssert.Parse(readJson());
            var tests = root.Module().Tests();
            await Assert.That(tests.GetArrayLength()).IsEqualTo(100);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
