using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet;

/// <summary>
/// Creates a <see cref="WriteTestOutput"/> delegate that writes serialised test output
/// to <c>{projectRoot}/.claude/tdd-guard/data/test.json</c>.
/// Uses atomic write (temp file + rename) to prevent partial reads.
/// </summary>
public static class ReportFileWriter
{
    private const string DataPath = ".claude/tdd-guard/data";
    private const string FileName = "test.json";

    public static WriteTestOutput Create(string projectRoot)
    {
        return output =>
        {
            try
            {
                var dir = Path.Combine(projectRoot, DataPath);
                Directory.CreateDirectory(dir);

                var targetPath = Path.Combine(dir, FileName);

                // The temp name carries the writing process so that concurrent runs
                // sharing a project root — a multi-targeted test project, or several
                // test projects in one solution — cannot write and rename the same
                // path and truncate each other's report.
                // Read ambiently rather than injected as a port: this is the identity of
                // the writing process, not a side-effect source with behaviour to
                // control, and the name is never observed — only the renamed target is.
                var tempPath = $"{targetPath}.{Environment.ProcessId}.{Environment.CurrentManagedThreadId}.tmp";

                var json = output.Serialize();
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, targetPath, overwrite: true);

                return new WriteResult.Success();
            }
            // Every failure here is handled identically, so the broad catch stays: an
            // unlisted case must become a WriteResult rather than escape into the test
            // host and fault the session. The type name keeps the failure identifiable
            // without the stack trace, which is noise in a one-line stderr diagnostic.
            catch (Exception ex)
            {
                return new WriteResult.Error($"{ex.GetType().Name}: {ex.Message}");
            }
        };
    }
}
