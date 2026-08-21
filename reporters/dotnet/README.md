# TDD Guard .NET Reporter

Microsoft Testing Platform (MTP) V2 extension that captures test results
for TDD Guard validation. Works with any .NET test framework that supports
MTP V2 — one package covers TUnit, MSTest, xUnit, NUnit, and more.

## How it works

The reporter is an MTP V2 extension that loads in-process alongside your
test framework. It subscribes to `TestNodeUpdateMessage` events — the
standard MTP event type that all frameworks publish when a test completes.

When a test session finishes, the extension writes results to
`.claude/tdd-guard/data/test.json` in the TDD Guard wire format. The
TDD Guard validation hook reads this file to enforce TDD discipline.

The extension is framework-agnostic. It does not know or care which
framework is running. MTP handles the framework integration; we handle
the test result capture.

## Requirements

- .NET 10+
- [TDD Guard](https://github.com/nizos/tdd-guard)

## Installation

Planned distribution: NuGet.org (published by the maintainer).

Add a PackageReference to your test project:

```xml
<ItemGroup>
  <PackageReference Include="TddGuard.Dotnet" Version="*" />
</ItemGroup>
```

For solutions with multiple test projects, add it once via
`Directory.Build.props` in your tests directory:

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="TddGuard.Dotnet" Version="*" />
  </ItemGroup>
</Project>
```

No manual hook code is needed — the package auto-registers via
`buildTransitive/*.props` which injects the MTP builder hook at build time.

## Usage

The extension registers automatically. Running `dotnet test` is enough
once the package is referenced.

## Configuration

### Project Root

Set the `TDD_GUARD_PROJECT_ROOT` environment variable to your project root:

```bash
export TDD_GUARD_PROJECT_ROOT="/absolute/path/to/project/root"
```

`CLAUDE_PROJECT_DIR` is used as a fallback when `TDD_GUARD_PROJECT_ROOT`
is not set. Claude Code sets this variable for hook commands, though it
may not be available in all execution contexts.

### Rules

- Absolute and relative paths are both accepted (per ADR-009)
- Current directory must be within the configured project root
- When neither env var is set, the extension logs a diagnostic to stderr
  and disables itself (per ADR-010) — it does not silently fall back to cwd

## Compatibility

Verified with smoke tests in `TddGuard.Dotnet.Compat.*` projects:

| Framework                                        | MTP V2 support | Status   | Notes                                            |
| ------------------------------------------------ | -------------- | -------- | ------------------------------------------------ |
| [TUnit](https://github.com/thomhurst/TUnit)      | Native         | Verified | File path as module ID                           |
| [MSTest v4](https://github.com/microsoft/testfx) | Native         | Verified | File path as module ID                           |
| [xUnit v3](https://xunit.net/)                   | Native         | Verified | Via `xunit.v3.mtp-v2` package                    |
| [xUnit v2](https://xunit.net/)                   | Via adapter    | Verified | Via `YTest.MTP.XUnit2` (third-party, unofficial) |
| [NUnit 4](https://docs.nunit.org/)               | Via runner     | Verified | UID as module ID (NUnit omits file paths)        |
| [Reqnroll](https://reqnroll.net/)                | Inherited      | Expected | BDD layer over MSTest/NUnit/xUnit/TUnit          |

## Project structure

```
TddGuard.Dotnet.slnx         Solution referencing all projects below
TddGuard.Dotnet.Core/       Domain types and pure functions (no MTP, no filesystem)
TddGuard.Dotnet/             MTP V2 extension, filesystem adapters, and the composition root
TddGuard.Dotnet.Tests/       Unit tests, property-based tests, test infrastructure
TddGuard.Dotnet.Compat.*/    Framework compatibility smoke tests
```

`Core` depends on nothing platform-specific: no MTP, no filesystem, no
environment access. It defines the domain types (`TestState`,
`TestIdentity`, `CollectedResult`, `TestRunOutput`), the port
delegates the domain calls out through (`WriteTestOutput`,
`CanonicalPath`, `MapTestNode`, …), and the pure mapping/serialisation
functions. `Dotnet` depends on Core, MTP, and the filesystem — it is
where every side-effecting concern lives: reading env vars, resolving
the project root, following symlinks, writing the report file, and
wiring it all together at `TddGuardBuilder.Register`, the composition
root.

This separation means the domain logic can be tested without MTP or a
disk, and the MTP integration can change (a platform version bump, a
different write strategy) without the domain noticing. See
[Design notes](#design-notes) below for how this shaped a few
decisions that are easy to second-guess without the context.

## Design notes

These are the trade-offs behind the current shape of `TddGuard.Dotnet`
and `TddGuard.Dotnet.Core`, kept here because a reviewer meeting this
code for the first time is likely to ask about exactly these three
things.

### The MTP 2.0.1 → 2.3.3 bump changed what the reporter could assume

The reporter targets `Microsoft.Testing.Platform` 2.3.3, up from the
2.0.1 it first shipped against. Two parts of that bump reached into the
reporter's own contract with the platform, not just its dependency
version:

- **Cancellation is cooperative, not racing.** MTP 2.3.3 expects an
  extension to observe the run's own `CancellationToken` via
  `ThrowIfCancellationRequested()` rather than finish out of band. The
  platform recognises an `OperationCanceledException` carrying its own
  token and unwinds the host quietly; anything else — including one
  constructed without that token — escapes as unhandled and faults the
  session. `TddGuardListener.OnTestSessionStartingAsync`,
  `OnTestSessionFinishingAsync`, and `ConsumeAsync` all check the token
  now, and the deprecated `CancelledTestNodeStateProperty` — MTP0001,
  a state authors are told to signal via that exception instead — is
  read but no longer specially handled; it falls into the same
  fail-closed default as any state the reporter does not recognise.
- **A `PropertyBag` permits more of some properties than others.**
  `TestNodeStateProperty` is rejected past one per node, but
  `TestFileLocationProperty` and `TestMethodIdentifierProperty` are
  not — a node can legitimately (or via a misbehaving framework)
  carry more than one. Reading either with `SingleOrDefault` throws
  out of `ConsumeAsync`, on the platform's own event pump, which loses
  the _entire_ report rather than one node's metadata. Both are read
  with `FirstOrDefault` instead.

Neither is cosmetic: a contract mismatch on either point turns one bad
test node into a session-wide failure to report at all — the opposite
of TDD Guard's fail-closed intent, applied to the reporter's own
plumbing instead of to a test's outcome.

### The identity redesign: reading what a framework said, not guessing from punctuation

Before this pass, the reporter decided a test's name from a single
`MethodIdentifier?` field plus a fallback heuristic: if the display
name or the node UID contained a `.` or `/`, treat it as "qualified"
and use it; otherwise fall back to whichever string was present. That
heuristic is now `TestIdentity`, a closed union with four cases
(`Structured`, `QualifiedName`, `QualifiedIdentifier`, `Unqualified`),
built once in `TddGuardListener.Classify` and consumed exhaustively
everywhere a name is needed. The heuristic itself didn't disappear —
MTP gives no framework name on the node, so _some_ frameworks still
have to be told apart by which of their two strings looks qualified —
but it is now confined to one method instead of re-derived at every
call site, and the four possible shapes are named types a `switch`
can be checked exhaustively against, rather than an implicit contract
carried by nullable fields.

One gap surfaced by review after this landed: two tests that both fall
through to `Unqualified` — neither string qualified, which no
mainstream framework does today but nothing prevents a future one from
doing — used to report their shared display name as their _whole_
identity, so two such tests would collide into one entry. `Unqualified`
now also carries the node UID, which MTP documents as unique per test
node, and the report's full name and module grouping key off that
while the display name stays the short, human-readable name. See
`fix(dotnet): distinguish unqualified tests sharing a display name`
for the regression test and fix.

### Why `internal`, and why that isn't fixed with `InternalsVisibleTo`

`ProjectRootResolver`, `ResolveProjectRoot`, and their surrounding
types moved from `public` (their original shape, in `TddGuard.Dotnet.Core`)
to `internal`, inside `TddGuard.Dotnet`, as part of this pass. That is
a deliberate narrowing, not an oversight, and the natural next move —
add `[assembly: InternalsVisibleTo("TddGuard.Dotnet.Tests")]` so the
test project can keep calling `ProjectRootResolver.Resolve(...)`
directly — was considered and rejected.

`InternalsVisibleTo` weakens the boundary the same access modifier
should be one, for one caller, forever: `internal` is documentation
that says "nothing outside this assembly should depend on this", and
a friend assembly makes that untrue for exactly the assembly that
should be enforcing it hardest. The type didn't become internal
because it was too unimportant to be public — it became internal
because it's an implementation detail of _how_ the reporter locates
its output, and the actual contract a consumer or a test should be
able to rely on is "does the reporter find the right root and disable
itself with a clear reason when it can't" — the outcome exercised
through `TddGuardBuilder.Register`, the type that never needed to
change visibility because it was always the intended entry point.

The `ProjectRootResolutionTests` that used to call `Resolve` directly
now call `Register` against a real
`TestApplication.CreateBuilderAsync([])` and assert on the diagnostic
message it logs when it disables itself. This is strictly the
Chicago-school position already followed elsewhere in this test suite
(see `TddGuard.Dotnet.Tests/README.md`'s "Why outside-in" section) —
"exporting internals / widening visibility for a test → fix the
design, not the checker" — applied to a type that had drifted away
from it. The cost is real: these tests now spin up an actual MTP
builder per case, and a failure inside `Resolve` is one call frame
further from the assertion that catches it. The alternative cost —
a widened seam that every future internal type can point to as
precedent — was judged worse for a boundary this narrow and this
rarely touched.

## Development

All commands run inside the devcontainer:

```bash
docker exec -w /workspace/reporters/dotnet <container> dotnet build
docker exec -w /workspace/reporters/dotnet <container> dotnet test --project TddGuard.Dotnet.Tests
```

Find the container name via `docker ps`.

See `TddGuard.Dotnet.Tests/README.md` for testing conventions,
infrastructure, and the framework smoke test guide.

## License

MIT
