# Property-Based Testing Review — .NET Reporter

## Current state

5 PBTs across 2 test files, 60 total tests in suite.

### TestReportSerializerTests (3 PBTs)

| Test                            | Strategy                     | Strength                            |
| ------------------------------- | ---------------------------- | ----------------------------------- |
| Structure validation            | Hard-to-prove-easy-to-verify | Weak — checks shapes, not values    |
| Roundtrip serialize/deserialize | Roundtrip                    | Strong — the best test in the suite |
| Special character handling      | Same as #1, biased generator | Near-redundant with #1              |

### TestRunSummariserTests (2 PBTs)

| Test                     | Strategy                              | Strength                               |
| ------------------------ | ------------------------------------- | -------------------------------------- |
| Preserves all inputs     | Compound invariant (5 sub-properties) | Strong but hard to diagnose on failure |
| Preserves error messages | Partial roundtrip                     | Strong                                 |

## Problems identified

### Compound properties hide failure reasons

`SummarisePreservesAllInputs` checks 5 things in one `Prop.ForAll` using `if (...) return false`. When it fails, you don't know which sub-property failed. Should split into separate tests or use FsCheck's `Label`/`Prop.And` API for diagnostics.

### `return false` style gives no diagnostics

Every property uses `if (condition) return false` which produces "property is false" with a shrunk counterexample but no indication of _what_ was wrong. FsCheck supports:

```csharp
(totalOutputTests == input.Count).Label("count preserved")
    .And((output.Reason == "failed") == anyFailed).Label("reason correct")
```

### Special character test is near-redundant

Test #3 uses a biased generator but tests the same property as #1 (valid JSON structure). The real value of special characters is roundtrip correctness, which test #2 already covers with arbitrary strings. Would add value if applied to the roundtrip test instead.

### No metamorphic properties

Metamorphic testing ("if I change the input in a known way, the output changes predictably") is absent. Good candidates for Summarise:

- Adding a Failed result flips reason to "failed"
- Adding a result with a new module ID increases module count by 1
- Removing all results from one module removes that module from output
- Summarise is deterministic (same input, same output)

### Generator domain gaps

- Module IDs always from `{"ModA", "ModB", "ModC"}` — never path-like (`/src/Tests.cs`), never many unique values
- Failed state always has non-empty error list (`NonEmptyListOf`) — empty list case untested by PBT
- Serializer generators never produce empty modules list or empty tests list
- No generator produces the full MTP event → JSON pipeline (outside-in)

## Improvement options

### A. Split and label compound properties (low effort, high diagnostic value)

Break `SummarisePreservesAllInputs` into 4-5 separate tests, each with one property. Use FsCheck `Label` API. Keep existing generators.

- Better failure diagnostics
- No new generators needed
- Quick to implement

### B. Add metamorphic properties for Summarise (medium effort, catches new bugs)

Properties that test _relationships_ between inputs and outputs:

- "Adding a Failed result flips reason"
- "Adding a result with new module ID adds exactly one module"
- "Determinism: same input always same output"

Requires generators that produce a base input plus a modification.

### C. Outside-in PBT through TestHarness (higher effort, highest value)

Generate random MTP event sequences, feed through `TestHarness.Arrange(...).Act().Assert(...)`, verify properties of the JSON output. Effectively a property-based integration test.

- Tests full pipeline with random inputs
- Catches wiring bugs between components
- Slower (file I/O), harder to shrink

Could reuse the TestHarness DSL:

```csharp
Prop.ForAll(ArbMtpEventSequence(), events =>
{
    // feed through full pipeline, verify JSON properties
})
```

### D. Tighten generator domain coverage (low effort, catches edge cases)

- Add path-like module IDs (`/src/foo.cs`, `C:\Tests\Unit.cs`)
- Include empty error lists in Failed state generator
- Add empty-list variants for modules/tests in serializer generators
- Widen module ID pool or use fully random strings

## TestHarness support for PBT

The TestHarness now has `ActPhase.Execute()` which returns a `PipelineResult` — a synchronous, disposable wrapper around the parsed JSON output. This bridges FsCheck's synchronous `Prop.ForAll` with the async test pipeline.

### API

```csharp
// Execute() runs the full pipeline synchronously and returns parsed JSON.
// Caller owns cleanup via Dispose/using.
using var result = TestHarness
    .Arrange(async listener => { /* feed events */ })
    .Act()
    .Execute();

result.HasOutput   // bool — true if test.json was written
result.Output      // JsonElement? — the parsed root, or null
```

### Outside-in PBT examples

**Count preservation:**

```csharp
[Test("every consumed event produces exactly one test entry")]
public void EveryConsumedEventProducesOneTestEntry()
{
    Prop.ForAll(Arb.From(Gen.Choose(1, 20)), count =>
    {
        using var result = TestHarness
            .Arrange(async listener =>
            {
                for (var i = 0; i < count; i++)
                    await listener.ConsumeAsync(
                        StubProducer(),
                        MakeTestUpdate($"test_{i}", new PassedTestNodeStateProperty()),
                        default);
            })
            .Act()
            .Execute();

        var totalTests = result.Output!.Value
            .GetProperty("testModules").EnumerateArray()
            .Sum(m => m.GetProperty("tests").GetArrayLength());
        return totalTests == count;
    }).QuickCheckThrowOnFailure();
}
```

**Reason reflects failures (metamorphic):**

```csharp
[Test("reason is 'failed' iff any event is a failure")]
public void ReasonReflectsFailures()
{
    Prop.ForAll(Arb.From(Gen.NonEmptyListOf(GenTaggedEvent())), events =>
    {
        var anyFailed = events.Any(e => e.IsFailed);

        using var result = TestHarness
            .Arrange(async listener =>
            {
                foreach (var evt in events)
                    await listener.ConsumeAsync(StubProducer(), evt.Message, default);
            })
            .Act()
            .Execute();

        var reason = result.Output!.Value.GetProperty("reason").GetString();
        return reason == (anyFailed ? "failed" : "passed");
    }).QuickCheckThrowOnFailure();
}
```

**Module grouping matches file paths:**

```csharp
[Test("output has one module per distinct file path")]
public void OneModulePerDistinctFilePath()
{
    var genPaths = Gen.NonEmptyListOf(
        Gen.Elements("/src/A.cs", "/src/B.cs", "/src/C.cs", "/tests/D.cs"));

    Prop.ForAll(Arb.From(genPaths), filePaths =>
    {
        using var result = TestHarness
            .Arrange(async listener =>
            {
                for (var i = 0; i < filePaths.Count; i++)
                    await listener.ConsumeAsync(
                        StubProducer(),
                        MakeTestUpdate($"test_{i}", new PassedTestNodeStateProperty(), filePaths[i]),
                        default);
            })
            .Act()
            .Execute();

        var moduleCount = result.Output!.Value.GetProperty("testModules").GetArrayLength();
        return moduleCount == filePaths.Distinct().Count();
    }).QuickCheckThrowOnFailure();
}
```

### Event generator pattern

For PBTs that need to generate varied MTP events, add a tagged wrapper to MtpStubs:

```csharp
internal record TaggedEvent(TestNodeUpdateMessage Message, bool IsFailed);

internal static Gen<TaggedEvent> GenTaggedEvent()
{
    var genName = ArbMap.Default.GeneratorFor<NonEmptyString>().Select(s => s.Get);
    var genPath = Gen.Elements("/src/A.cs", "/src/B.cs", "/src/C.cs");

    return Gen.OneOf(
        from n in genName from p in genPath
        select new TaggedEvent(MakeTestUpdate(n, new PassedTestNodeStateProperty(), p), false),

        from n in genName from p in genPath
        select new TaggedEvent(
            MakeTestUpdate(n, new FailedTestNodeStateProperty(
                new InvalidOperationException("fail"), "fail"), p), true),

        from n in genName from p in genPath
        select new TaggedEvent(MakeTestUpdate(n, new SkippedTestNodeStateProperty(), p), false));
}
```

### Design rationale

- **`Execute()` is synchronous** — FsCheck 3.x `Prop.ForAll` expects `bool`, not `Task<bool>`. Execute bridges with `.GetAwaiter().GetResult()` internally, which is safe in test contexts.
- **`PipelineResult` is `IDisposable`** — temp directory cleanup is the caller's responsibility via `using`. This avoids hiding cleanup in the assertion phase.
- **Generators stay outside the harness** — the harness is pipeline infrastructure, FsCheck generators are input factories. The two compose but don't depend on each other.

## Recommended priority

1. **A + D** — quick wins, split properties and widen generators
2. **B** — metamorphic properties for Summarise
3. **C** — outside-in PBT through the full pipeline (harness + Execute() is ready)
