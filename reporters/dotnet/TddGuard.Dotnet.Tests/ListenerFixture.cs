using System.Text.Json;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal static class ListenerFixture
{
    internal static ArrangePhase Arrange(Func<Dotnet.TddGuardListener, Task> arrange)
        => new(arrange);

    internal static ArrangePhase Arrange()
        => new(_ => Task.CompletedTask);

    internal sealed class ArrangePhase(Func<Dotnet.TddGuardListener, Task> arrange)
    {
        internal ActPhase Act() => new(arrange);
    }

    internal sealed class ActPhase(Func<Dotnet.TddGuardListener, Task> arrange)
    {
        internal async Task Assert(Func<JsonElement, Task> assert)
        {
            var result = await Run();
            try
            {
                var json = await File.ReadAllTextAsync(JsonPath(result.TempDir));
                var root = JsonDocument.Parse(json).RootElement;
                await assert(root);
            }
            finally
            {
                result.Cleanup();
            }
        }

        internal async Task AssertNoOutput()
        {
            var result = await Run();
            try
            {
                await global::TUnit.Assertions.Assert.That(File.Exists(JsonPath(result.TempDir))).IsFalse();
            }
            finally
            {
                result.Cleanup();
            }
        }

        /// <summary>
        /// Runs the full pipeline synchronously and returns the parsed JSON output,
        /// or null when the session produced no output. Caller owns cleanup via Dispose.
        /// Designed for property-based tests where FsCheck expects a synchronous bool.
        /// </summary>
        internal PipelineResult Execute()
        {
            var result = Run().GetAwaiter().GetResult();
            var path = JsonPath(result.TempDir);

            if (!File.Exists(path))
                return new PipelineResult(null, result.TempDir);

            var json = File.ReadAllText(path);
            var root = JsonDocument.Parse(json).RootElement;
            return new PipelineResult(root, result.TempDir);
        }

        private async Task<RunResult> Run()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var listener = CreateListener(tempDir);

            await listener.OnTestSessionStartingAsync(MtpStubs.StubSessionContext());
            await arrange(listener);
            await listener.OnTestSessionFinishingAsync(MtpStubs.StubSessionContext());

            return new RunResult(tempDir);
        }

        private sealed record RunResult(string TempDir)
        {
            internal void Cleanup()
            {
                if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true);
            }
        }
    }

    /// <summary>
    /// Result of a synchronous pipeline execution. Holds the parsed JSON (or null
    /// for no output) and owns temp directory cleanup. Use with <c>using</c>.
    /// </summary>
    internal sealed class PipelineResult(JsonElement? output, string tempDir) : IDisposable
    {
        internal JsonElement? Output => output;
        internal bool HasOutput => output.HasValue;

        public void Dispose()
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    internal sealed record ListenerResult(
        Dotnet.TddGuardListener Listener,
        Func<string> ReadTestJson,
        string TempDir);

    internal static ListenerResult Create()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        var listener = CreateListener(tempDir);

        var jsonPath = Path.Combine(
            tempDir, ".claude", "tdd-guard", "data", "test.json");

        return new ListenerResult(
            Listener: listener,
            ReadTestJson: () => File.ReadAllText(jsonPath),
            TempDir: tempDir);
    }

    internal static string JsonPath(string tempDir)
        => Path.Combine(tempDir, ".claude", "tdd-guard", "data", "test.json");

    internal static bool HasTestJson(string tempDir)
        => File.Exists(JsonPath(tempDir));

    /// <summary>
    /// Builds a listener that writes its report beneath <paramref name="tempDir"/>.
    /// A stub write stands in for the real file writer so these tests exercise the
    /// listener alone; the writer has its own tests, and where the report lands is
    /// decided at registration (see <see cref="ProjectRootResolutionTests"/>).
    /// </summary>
    private static Dotnet.TddGuardListener CreateListener(string tempDir)
        => new(
            WriteBeneath(tempDir),
            input => input.ToCollectedResult(tempDir),
            version: "0.0.0-test");

    private static WriteTestOutput WriteBeneath(string projectRoot)
        => output =>
        {
            var path = JsonPath(projectRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, output.Serialize());
            return new WriteResult.Success();
        };
}
