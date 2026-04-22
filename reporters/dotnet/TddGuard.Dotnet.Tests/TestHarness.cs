using System.Text.Json;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal static class TestHarness
{
    internal sealed record ListenerResult(
        Dotnet.TddGuardListener Listener,
        Func<string> ReadTestJson,
        string TempDir);

    internal static ListenerResult Create(
        GetEnvironmentVariable? getEnv = null,
        GetCurrentWorkingDirectory? getCwd = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var root = ProjectRootResolver.Resolve(
            getEnv ?? (_ => tempDir),
            getCwd ?? (() => tempDir)).AsT0; // Safe in test harness — always valid
        var write = ReportFileWriter.Create(root.Path);
        var listener = new Dotnet.TddGuardListener(write);

        var jsonPath = Path.Combine(
            tempDir, ".claude", "tdd-guard", "data", "test.json");

        return new ListenerResult(
            Listener: listener,
            ReadTestJson: () => File.ReadAllText(jsonPath),
            TempDir: tempDir);
    }

    internal static async Task Run(
        Func<Dotnet.TddGuardListener, Task> act,
        Func<JsonElement, Task> assert)
    {
        var (listener, readJson, tempDir) = Create();
        try
        {
            await listener.OnTestSessionStartingAsync(MtpStubs.StubSessionContext());
            await act(listener);
            await listener.OnTestSessionFinishingAsync(MtpStubs.StubSessionContext());

            var root = TestJsonAssert.Parse(readJson());
            await assert(root);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    internal static async Task RunExpectingNoOutput(
        Func<Dotnet.TddGuardListener, Task> act)
    {
        var (listener, _, tempDir) = Create();
        try
        {
            await listener.OnTestSessionStartingAsync(MtpStubs.StubSessionContext());
            await act(listener);
            await listener.OnTestSessionFinishingAsync(MtpStubs.StubSessionContext());

            await Assert.That(HasTestJson(tempDir)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    internal static string JsonPath(string tempDir)
        => Path.Combine(tempDir, ".claude", "tdd-guard", "data", "test.json");

    internal static bool HasTestJson(string tempDir)
        => File.Exists(JsonPath(tempDir));
}
