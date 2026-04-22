using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using TddGuard.Dotnet.Core;

namespace TddGuard.Dotnet.Tests;

internal sealed class TestReportSerializerTests
{
    [Test("roundtrip: Serialize produces valid JSON preserving structure")]
    public void RoundtripSerializeProducesValidJsonPreservingStructure()
    {
        var arbOutput = ArbTestRunOutput();
        Prop.ForAll(arbOutput, output =>
        {
            var json = output.Serialize();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // reason is a string
            var reason = root.GetProperty("reason").GetString();
            if (reason is null) return false;

            // testModules is an array
            if (root.GetProperty("testModules").ValueKind != JsonValueKind.Array) return false;

            foreach (var module in root.GetProperty("testModules").EnumerateArray())
            {
                // Each module has moduleId (string) and tests (array)
                if (module.GetProperty("moduleId").ValueKind != JsonValueKind.String) return false;
                if (module.GetProperty("tests").ValueKind != JsonValueKind.Array) return false;

                foreach (var test in module.GetProperty("tests").EnumerateArray())
                {
                    // Each test has name, fullName, state (all strings)
                    if (test.GetProperty("name").ValueKind != JsonValueKind.String) return false;
                    if (test.GetProperty("fullName").ValueKind != JsonValueKind.String) return false;
                    if (test.GetProperty("state").ValueKind != JsonValueKind.String) return false;

                    // errors is either absent or an array
                    if (test.TryGetProperty("errors", out var errors) && errors.ValueKind != JsonValueKind.Array)
                        return false;
                }
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }

    [Test("roundtrip: Serialize then deserialize preserves all values")]
    public void RoundtripSerializeDeserializePreservesValues()
    {
        var arbOutput = ArbTestRunOutput();

        // Independent deserialization options — tests output against separate config
        var deserializeOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        Prop.ForAll(arbOutput, output =>
        {
            var json = output.Serialize();
            var deserialized = JsonSerializer.Deserialize<TestRunOutput>(json, deserializeOptions);
            if (deserialized is null) return false;

            if (deserialized.Reason != output.Reason) return false;
            if (deserialized.TestModules.Count != output.TestModules.Count) return false;

            for (var i = 0; i < output.TestModules.Count; i++)
            {
                var orig = output.TestModules[i];
                var deser = deserialized.TestModules[i];
                if (deser.ModuleId != orig.ModuleId) return false;
                if (deser.Tests.Count != orig.Tests.Count) return false;

                for (var j = 0; j < orig.Tests.Count; j++)
                {
                    if (deser.Tests[j].Name != orig.Tests[j].Name) return false;
                    if (deser.Tests[j].FullName != orig.Tests[j].FullName) return false;
                    if (deser.Tests[j].State != orig.Tests[j].State) return false;

                    var origErrors = orig.Tests[j].Errors;
                    var deserErrors = deser.Tests[j].Errors;
                    if (origErrors is null && deserErrors is null) continue;
                    if (origErrors is null || deserErrors is null) return false;
                    if (origErrors.Count != deserErrors.Count) return false;
                    for (var k = 0; k < origErrors.Count; k++)
                    {
                        if (deserErrors[k].Message != origErrors[k].Message) return false;
                    }
                }
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }

    [Test("roundtrip: Serialize handles special characters in all string fields")]
    public void RoundtripSerializeHandlesSpecialCharacters()
    {
        var arbOutput = ArbTestRunOutputWithSpecialChars();
        Prop.ForAll(arbOutput, output =>
        {
            var json = output.Serialize();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("reason").GetString() is null) return false;
            if (root.GetProperty("testModules").ValueKind != JsonValueKind.Array) return false;

            foreach (var module in root.GetProperty("testModules").EnumerateArray())
            {
                if (module.GetProperty("moduleId").ValueKind != JsonValueKind.String) return false;
                foreach (var test in module.GetProperty("tests").EnumerateArray())
                {
                    if (test.GetProperty("name").ValueKind != JsonValueKind.String) return false;
                    if (test.GetProperty("fullName").ValueKind != JsonValueKind.String) return false;
                }
            }

            return true;
        }).QuickCheckThrowOnFailure();
    }

    // --- helpers ---

    private static Arbitrary<TestRunOutput> ArbTestRunOutput()
    {
        var states = new[] { "passed", "failed", "skipped" };
        var reasons = new[] { "passed", "failed" };

        var genNonEmpty = ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get);

        var genError = genNonEmpty.Select(s => new TestEntryErrorOutput(s));

        var genErrors = Gen.OneOf(
            Gen.Constant<IReadOnlyList<TestEntryErrorOutput>?>(null),
            genError.ListOf().Select(es => (IReadOnlyList<TestEntryErrorOutput>?)es.ToList()));

        var genEntry = from name in genNonEmpty
                       from fullName in genNonEmpty
                       from state in Gen.Elements(states)
                       from errors in genErrors
                       select new TestEntryOutput(name, fullName, state, errors);

        var genModule = from moduleId in genNonEmpty
                        from tests in genEntry.NonEmptyListOf().Select(es => (IReadOnlyList<TestEntryOutput>)es.ToList())
                        select new TestModuleOutput(moduleId, tests);

        var genOutput = from modules in genModule.NonEmptyListOf().Select(ms => (IReadOnlyList<TestModuleOutput>)ms.ToList())
                        from reason in Gen.Elements(reasons)
                        select new TestRunOutput(modules, reason);

        return Arb.From(genOutput);
    }

    private static Arbitrary<TestRunOutput> ArbTestRunOutputWithSpecialChars()
    {
        var states = new[] { "passed", "failed", "skipped" };
        var reasons = new[] { "passed", "failed" };

        var genSpecialString = Gen.OneOf(
            Gen.Constant("simple"),
            Gen.Constant("has \"quotes\""),
            Gen.Constant("has\nnewlines\nhere"),
            Gen.Constant("has\\backslashes\\path"),
            Gen.Constant("has\ttabs\there"),
            Gen.Constant("unicode: \u00e9\u00f1\u00fc"),
            ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get));

        var genError = genSpecialString.Select(s => new TestEntryErrorOutput(s));

        var genErrors = Gen.OneOf(
            Gen.Constant<IReadOnlyList<TestEntryErrorOutput>?>(null),
            genError.NonEmptyListOf().Select(es => (IReadOnlyList<TestEntryErrorOutput>?)es.ToList()));

        var genEntry = from name in genSpecialString
                       from fullName in genSpecialString
                       from state in Gen.Elements(states)
                       from errors in genErrors
                       select new TestEntryOutput(name, fullName, state, errors);

        var genModule = from moduleId in genSpecialString
                        from tests in genEntry.NonEmptyListOf().Select(es => (IReadOnlyList<TestEntryOutput>)es.ToList())
                        select new TestModuleOutput(moduleId, tests);

        var genOutput = from modules in genModule.NonEmptyListOf().Select(ms => (IReadOnlyList<TestModuleOutput>)ms.ToList())
                        from reason in Gen.Elements(reasons)
                        select new TestRunOutput(modules, reason);

        return Arb.From(genOutput);
    }
}
