using FsCheck;
using FsCheck.Fluent;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal sealed class TestRunSummariserTests
{
    [Test("Summarise preserves all inputs without loss or duplication")]
    public void SummarisePreservesAllInputs()
    {
        var arb = ArbCollectedResults();
        Prop.ForAll(arb, input =>
        {
            var output = input.Summarise();

            // No tests lost or duplicated
            var totalOutputTests = output.TestModules.Sum(m => m.Tests.Count);
            if (totalOutputTests != input.Count) return false;

            // Every output ModuleId exists in input
            var inputModuleIds = input.Select(r => r.ModuleId).ToHashSet();
            if (!output.TestModules.All(m => inputModuleIds.Contains(m.ModuleId))) return false;

            // Every input ModuleId appears in output
            var outputModuleIds = output.TestModules.Select(m => m.ModuleId).ToHashSet();
            if (!inputModuleIds.All(id => outputModuleIds.Contains(id))) return false;

            // All state strings are valid
            var validStates = new HashSet<string> { "passed", "failed", "skipped" };
            if (!output.TestModules.SelectMany(m => m.Tests).All(t => validStates.Contains(t.State))) return false;

            // reason == "failed" iff any input is Failed
            var anyFailed = input.Any(r => r.State is Core.TestState.Failed);
            if ((output.Reason == "failed") != anyFailed) return false;

            return true;
        }).QuickCheckThrowOnFailure();
    }

    [Test("Summarise preserves error messages from Failed state")]
    public void SummarisePreservesErrorMessages()
    {
        var arb = ArbCollectedResultsWithFailures();
        Prop.ForAll(arb, input =>
        {
            var output = input.Summarise();

            // Walk inputs grouped by module and match positionally against output
            var inputByModule = input.GroupBy(r => r.ModuleId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var module in output.TestModules)
            {
                if (!inputByModule.TryGetValue(module.ModuleId, out var inputTests)) return false;
                if (module.Tests.Count != inputTests.Count) return false;

                for (var i = 0; i < inputTests.Count; i++)
                {
                    if (inputTests[i].State is not Core.TestState.Failed failed) continue;

                    var entry = module.Tests[i];
                    if (entry.Errors is null) return false;

                    var expectedMessages = failed.Errors.Select(e => e.Message).ToList();
                    var actualMessages = entry.Errors.Select(e => e.Message).ToList();
                    if (!expectedMessages.SequenceEqual(actualMessages)) return false;
                }
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }

    // --- generators ---

    private static Arbitrary<IReadOnlyCollection<CollectedResult>> ArbCollectedResults()
    {
        var genNonEmpty = ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get);

        var genTestState = Gen.OneOf<Core.TestState>(
            Gen.Constant<Core.TestState>(new Core.TestState.Passed()),
            Gen.Constant<Core.TestState>(new Core.TestState.Skipped()),
            genNonEmpty.NonEmptyListOf()
                .Select(msgs => (Core.TestState)new Core.TestState.Failed(
                    msgs.Select(m => new TestEntryError(m)).ToList())));

        var genResult = from name in genNonEmpty
                        from fullName in genNonEmpty
                        from moduleId in Gen.Elements("ModA", "ModB", "ModC")
                        from state in genTestState
                        select new CollectedResult(name, fullName, moduleId, state);

        return Arb.From(genResult.NonEmptyListOf()
            .Select(list => (IReadOnlyCollection<CollectedResult>)list.ToList()));
    }

    private static Arbitrary<IReadOnlyCollection<CollectedResult>> ArbCollectedResultsWithFailures()
    {
        var genNonEmpty = ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get);

        var genFailed = genNonEmpty.NonEmptyListOf()
            .Select(msgs => (Core.TestState)new Core.TestState.Failed(
                msgs.Select(m => new TestEntryError(m)).ToList()));

        var genAnyState = Gen.OneOf<Core.TestState>(
            Gen.Constant<Core.TestState>(new Core.TestState.Passed()),
            Gen.Constant<Core.TestState>(new Core.TestState.Skipped()),
            genFailed);

        var genResult = from name in genNonEmpty
                        from fullName in genNonEmpty
                        from moduleId in Gen.Elements("ModA", "ModB", "ModC")
                        from state in genAnyState
                        select new CollectedResult(name, fullName, moduleId, state);

        // Ensure at least one Failed entry by prepending a guaranteed failure
        var genFailedResult = from name in genNonEmpty
                              from fullName in genNonEmpty
                              from moduleId in Gen.Elements("ModA", "ModB", "ModC")
                              from state in genFailed
                              select new CollectedResult(name, fullName, moduleId, state);

        return Arb.From(
            from failed in genFailedResult
            from rest in genResult.ListOf()
            select (IReadOnlyCollection<CollectedResult>)rest.Prepend(failed).ToList());
    }
}
