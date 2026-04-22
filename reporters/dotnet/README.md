# TDD Guard .NET Reporter

Microsoft Testing Platform extension that captures test results for TDD Guard validation.

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

For solutions with multiple test projects, add it once via `Directory.Build.props` in your tests directory:

```xml
<Project>
  <ItemGroup>
    <PackageReference Include="TddGuard.Dotnet" Version="*" />
  </ItemGroup>
</Project>
```

No manual hook code is needed — the package auto-registers via `buildTransitive/*.props`.

## Usage

The extension registers automatically. Running `dotnet test` is enough once the package is referenced.

## Configuration

### Project Root Configuration

Set the `TDD_GUARD_PROJECT_ROOT` environment variable to your project root:

```bash
export TDD_GUARD_PROJECT_ROOT="/absolute/path/to/project/root"
```

Or use a relative path, which resolves against the test runner's cwd.

When running under Claude Code, `CLAUDE_PROJECT_DIR` is set automatically and used as the fallback when `TDD_GUARD_PROJECT_ROOT` is not set.

### Configuration Rules

- Absolute and relative paths are both accepted (per ADR-009)
- Current directory must be within the configured project root
- Silently disables when no valid root can be resolved (per ADR-010)

## Compatibility

| Framework                                      | Language   | Package           | Status    |
| ---------------------------------------------- | ---------- | ----------------- | --------- |
| [TUnit](https://github.com/thomhurst/TUnit)    | C#         | `TddGuard.Dotnet` | Supported |
| [MSTest](https://github.com/microsoft/testfx)  | C#, F#, VB | `TddGuard.Dotnet` | Supported |
| [NUnit](https://docs.nunit.org/) (MTP adapter) | C#, F#, VB | `TddGuard.Dotnet` | Supported |
| [xUnit v2](https://xunit.net/)                 | C#         | `TddGuard.Dotnet` | Supported |
| [Reqnroll](https://reqnroll.net/)              | C#         | `TddGuard.Dotnet` | Supported |

## Development

```bash
cd reporters/dotnet
dotnet test
```

## More Information

- Test results are saved to `.claude/tdd-guard/data/test.json`
- See [TDD Guard documentation](https://github.com/nizos/tdd-guard) for complete setup

## License

MIT
