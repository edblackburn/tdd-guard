namespace TddGuard.Dotnet.Core;

/// <summary>
/// Groups collected test results by module and converts them into
/// the wire-format <see cref="TestRunOutput"/> for JSON serialisation.
/// Pure function with no side effects.
/// </summary>
public static class TestRunSummariser
{
    public static TestRunOutput Summarise(this IReadOnlyCollection<CollectedResult> results)
    {
        var modules = results
            .GroupBy(r => r.ModuleId)
            .Select(g => new TestModuleOutput(
                ModuleId: g.Key,
                Tests: g.Select(r => r.State switch
                {
                    TestState.Passed => new TestEntryOutput(r.Name, r.FullName, "passed", null),
                    TestState.Failed f => new TestEntryOutput(r.Name, r.FullName, "failed",
                        f.Errors.Select(e => new TestEntryErrorOutput(e.Message)).ToList()),
                    TestState.Skipped => new TestEntryOutput(r.Name, r.FullName, "skipped", null),
                    // Unreachable. TestState's constructor is private and its variants are
                    // sealed, so no fourth variant can exist, but a private constructor is
                    // not a sealing mechanism the compiler reasons about: without this arm
                    // Roslyn reports CS8509 ("the pattern 'not null' is not covered").
                    // Throwing rather than failing closed is deliberate — unlike MTP's open
                    // TestNodeStateProperty hierarchy, an unknown value here would be a bug
                    // in this assembly, not a new state from a test framework.
                    _ => throw new InvalidOperationException($"Unknown TestState: {r.State}")
                }).ToList()))
            .ToList();

        var reason = results.Any(r => r.State is TestState.Failed)
            ? "failed"
            : "passed";

        return new TestRunOutput(modules, reason);
    }
}
