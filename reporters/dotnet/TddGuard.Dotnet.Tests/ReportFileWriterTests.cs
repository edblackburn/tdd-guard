using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal sealed class ReportFileWriterTests
{
    [Test("writes JSON to .claude/tdd-guard/data/")]
    public async Task WritesJsonToDataDirectory()
    {
        await TempDir.Run(async tempDir =>
        {
            var write = ReportFileWriter.Create(tempDir);
            var result = write(MakePassingOutput());

            await Assert.That(result is WriteResult.Success).IsTrue();

            var expectedPath = Path.Combine(tempDir, ".claude", "tdd-guard", "data", "test.json");
            await Assert.That(File.Exists(expectedPath)).IsTrue();

            var json = await File.ReadAllTextAsync(expectedPath);
            await Assert.That(json).Contains("\"testModules\"");
        });
    }

    [Test("returns error for invalid path")]
    public async Task ReturnsErrorForInvalidPath()
    {
        var write = ReportFileWriter.Create("/dev/null/impossible/path");
        var result = write(MakePassingOutput());

        await Assert.That(result is WriteResult.Error).IsTrue();
        await Assert.That(((WriteResult.Error)result).Message).Contains("impossible");
    }

    [Test("creates intermediate directories when they do not exist")]
    public async Task CreatesIntermediateDirectories()
    {
        await TempDir.Run(async tempDir =>
        {
            await Assert.That(Directory.Exists(tempDir)).IsFalse();

            var write = ReportFileWriter.Create(tempDir);
            var result = write(MakePassingOutput());

            await Assert.That(result is WriteResult.Success).IsTrue();
            var expectedPath = Path.Combine(tempDir, ".claude", "tdd-guard", "data", "test.json");
            await Assert.That(File.Exists(expectedPath)).IsTrue();
        });
    }

    [Test("overwrites existing test.json with new content")]
    public async Task OverwritesExistingTestJson()
    {
        await TempDir.Run(async tempDir =>
        {
            var write = ReportFileWriter.Create(tempDir);

            var firstOutput = MakeOutputWithReason("passed");
            var firstResult = write(firstOutput);
            await Assert.That(firstResult is WriteResult.Success).IsTrue();

            var secondOutput = MakeOutputWithReason("failed");
            var secondResult = write(secondOutput);
            await Assert.That(secondResult is WriteResult.Success).IsTrue();

            var expectedPath = Path.Combine(tempDir, ".claude", "tdd-guard", "data", "test.json");
            var json = await File.ReadAllTextAsync(expectedPath);
            await Assert.That(json).Contains("\"failed\"");
            await Assert.That(json).DoesNotContain("\"passed\"");
        });
    }

    // Two test processes can share a project root — a multi-targeted test project, or
    // several test projects in one solution. A temp file named only after the target
    // would have them writing and renaming the same path, so one run's report can be
    // truncated or lost. Concurrent writers must each land a complete file.
    [Test("survives concurrent writers sharing a project root")]
    public async Task SurvivesConcurrentWritersSharingProjectRoot()
    {
        await TempDir.Run(async tempDir =>
        {
            const int writers = 8;
            var write = ReportFileWriter.Create(tempDir);

            using var barrier = new Barrier(writers);
            var results = new WriteResult[writers];
            var threads = Enumerable.Range(0, writers).Select(i => new Thread(() =>
            {
                barrier.SignalAndWait();
                results[i] = write(MakeOutputWithReason(i % 2 == 0 ? "passed" : "failed"));
            })).ToList();

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            await Assert.That(results).All().Satisfy(r => r.IsTypeOf<WriteResult.Success>());

            // Whichever writer landed last, the file must be complete and parseable.
            var path = Path.Combine(tempDir, ".claude", "tdd-guard", "data", "test.json");
            var json = await File.ReadAllTextAsync(path);
            await Assert.That(json).Contains("\"testModules\"");
            await Assert.That(() => System.Text.Json.JsonDocument.Parse(json)).ThrowsNothing();
        });
    }

    // A failed move must not leave the temp file behind: nothing ever reads it, and it
    // would otherwise accumulate under repeated failures to the same broken target.
    [Test("removes the temp file when the report cannot be moved into place")]
    public async Task RemovesTempFileWhenMoveFails()
    {
        await TempDir.Run(async tempDir =>
        {
            var dataDir = Path.Combine(tempDir, ".claude", "tdd-guard", "data");
            var targetPath = Path.Combine(dataDir, "test.json");
            Directory.CreateDirectory(targetPath); // occupies the target path as a directory, so the move fails

            var write = ReportFileWriter.Create(tempDir);
            var result = write(MakePassingOutput());

            await Assert.That(result is WriteResult.Error).IsTrue();

            var leftoverTempFiles = Directory.GetFiles(dataDir, "test.json.*.tmp");
            await Assert.That(leftoverTempFiles).IsEmpty();
        });
    }

    private static TestRunOutput MakePassingOutput()
        => MakeOutputWithReason("passed");

    private static TestRunOutput MakeOutputWithReason(string reason)
    {
        var state = reason == "failed" ? "failed" : "passed";
        var entry = new TestEntryOutput("test1", "Module/test1", state, null);
        var module = new TestModuleOutput("Module", [entry]);
        return new TestRunOutput([module], reason);
    }
}
