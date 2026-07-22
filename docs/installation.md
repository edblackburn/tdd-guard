# Manual Installation

If you installed TDD Guard as a [plugin](../plugin/README.md), you don't need to do any of this manually — the plugin configures hooks automatically and the `/tdd-guard:setup` skill handles reporter installation and configuration.

This document is a reference for manual setup or troubleshooting.

## 1. Install TDD Guard

Using npm:

```bash
npm install -g tdd-guard
```

Or using Homebrew:

```bash
brew install tdd-guard
```

## 2. Configure Claude Code Hooks

Add the following hooks to your settings file. See [Settings File Locations](configuration.md#settings-file-locations) for guidance on which file to use.

You can also configure hooks interactively by typing `/hooks` in Claude Code.

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Write|Edit|MultiEdit|TodoWrite",
        "hooks": [
          {
            "type": "command",
            "command": "tdd-guard"
          }
        ]
      }
    ],
    "UserPromptSubmit": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "tdd-guard"
          }
        ]
      }
    ],
    "SessionStart": [
      {
        "matcher": "startup|resume|clear",
        "hooks": [
          {
            "type": "command",
            "command": "tdd-guard"
          }
        ]
      }
    ]
  }
}
```

## 3. Add Test Reporter

TDD Guard needs to capture test results from your test runner. Choose your language below:

### JavaScript/TypeScript

Choose your test runner:

#### Vitest

Install the [tdd-guard-vitest](https://www.npmjs.com/package/tdd-guard-vitest) reporter in your project:

```bash
npm install --save-dev tdd-guard-vitest
```

Add to your `vitest.config.ts`:

```typescript
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    reporters: [
      'default',
      ['tdd-guard-vitest', { projectRoot: '/Users/username/projects/my-app' }],
    ],
  },
})
```

#### Jest

Install the [tdd-guard-jest](https://www.npmjs.com/package/tdd-guard-jest) reporter in your project:

```bash
npm install --save-dev tdd-guard-jest
```

Add to your `jest.config.ts`:

```typescript
import type { Config } from 'jest'

const config: Config = {
  reporters: [
    'default',
    [
      'tdd-guard-jest',
      {
        projectRoot: '/Users/username/projects/my-app',
      },
    ],
  ],
}

export default config
```

**Note:** For both Vitest and Jest, specify the project root path when your test config is not at the project root (e.g., in workspaces or monorepos). This ensures TDD Guard can find the test results. See the reporter configuration docs for more details:

- [Vitest configuration](../reporters/vitest/README.md#configuration)
- [Jest configuration](../reporters/jest/README.md#configuration)

#### Storybook

Install the [tdd-guard-storybook](https://www.npmjs.com/package/tdd-guard-storybook) reporter in your project:

```bash
npm install --save-dev tdd-guard-storybook
```

Add to your `.storybook/test-runner.ts`:

```typescript
import { StorybookReporter } from 'tdd-guard-storybook'
import path from 'path'

const reporter = new StorybookReporter({
  projectRoot: path.resolve(__dirname, '..'),
})

module.exports = {
  async postVisit(page, context) {
    await reporter.onStoryResult(context)
  },
}

process.on('exit', () => {
  reporter.onComplete()
})
```

**Note:** The reporter is constructed directly in your `.storybook/test-runner.ts` and wired into Storybook test-runner's `postVisit` hook. Specify the project root path when your Storybook config is not at the project root. See the [Storybook reporter configuration](../reporters/storybook/README.md#configuration) for more details.

**Tip:** For Storybook 10+ with Vite-based frameworks, consider using [`@storybook/addon-vitest`](https://storybook.js.org/docs/writing-tests/integrations/vitest-addon) instead. This runs Storybook tests through Vitest, allowing you to use the `tdd-guard-vitest` reporter for faster test execution. See [Storybook with Vitest Addon](storybook-vitest-addon.md) for setup instructions.

### Python (pytest)

Install the [tdd-guard-pytest](https://pypi.org/project/tdd-guard-pytest) reporter:

```bash
pip install tdd-guard-pytest
```

Configure the project root in your `pyproject.toml`:

```toml
[tool.pytest.ini_options]
tdd_guard_project_root = "/Users/username/projects/my-app"
```

**Note:** Specify the project root path when your tests run from a subdirectory or in a monorepo setup. This ensures TDD Guard can find the test results. See the [pytest reporter configuration](../reporters/pytest/README.md#configuration) for alternative configuration methods (pytest.ini, setup.cfg).

### PHP (PHPUnit)

Install the [tdd-guard/phpunit](https://packagist.org/packages/tdd-guard/phpunit) reporter in your project:

```bash
composer require --dev tdd-guard/phpunit
```

For PHPUnit 9.x, add to your `phpunit.xml`:

```xml
<listeners>
    <listener class="TddGuard\PHPUnit\TddGuardListener">
        <arguments>
            <string>/Users/username/projects/my-app</string>
        </arguments>
    </listener>
</listeners>
```

For PHPUnit 10.x/11.x/12.x, add to your `phpunit.xml`:

```xml
<extensions>
    <bootstrap class="TddGuard\PHPUnit\TddGuardExtension">
        <parameter name="projectRoot" value="/Users/username/projects/my-app"/>
    </bootstrap>
</extensions>
```

**Note:** Specify the project root path when your phpunit.xml is not at the project root (e.g., in subdirectories or monorepos). This ensures TDD Guard can find the test results. The reporter saves results to `.claude/tdd-guard/data/test.json`.

### Go

Install the tdd-guard-go reporter:

```bash
go install github.com/nizos/tdd-guard/reporters/go/cmd/tdd-guard-go@latest
```

Pipe `go test -json` output to the reporter:

```bash
go test -json ./... 2>&1 | tdd-guard-go -project-root /Users/username/projects/my-app
```

For Makefile integration:

```makefile
test:
	go test -json ./... 2>&1 | tdd-guard-go -project-root /Users/username/projects/my-app
```

**Note:** The reporter acts as a filter that passes test output through unchanged while capturing results for TDD Guard. See the [Go reporter configuration](../reporters/go/README.md#configuration) for more details.

### Rust

Install the [tdd-guard-rust](https://crates.io/crates/tdd-guard-rust) reporter:

```bash
cargo install tdd-guard-rust
```

Use it to capture test results from `cargo test` or `cargo nextest`:

```bash
# With nextest (recommended)
cargo nextest run 2>&1 | tdd-guard-rust --project-root /Users/username/projects/my-app --passthrough

# With cargo test
cargo test -- -Z unstable-options --format json 2>&1 | tdd-guard-rust --project-root /Users/username/projects/my-app --passthrough
```

For Makefile integration:

```makefile
test:
	cargo nextest run 2>&1 | tdd-guard-rust --project-root $(PWD) --passthrough
```

**Note:** The reporter acts as a filter that passes test output through unchanged while capturing results for TDD Guard. See the [Rust reporter configuration](../reporters/rust/README.md#configuration) for more details.

### .NET

Planned distribution: NuGet.org (published by the maintainer).

Add a `PackageReference` to your test project:

```xml
<ItemGroup>
  <PackageReference Include="TddGuard.Dotnet" Version="*" />
</ItemGroup>
```

Then run `dotnet test`:

```bash
dotnet test
```

**Note:** Unlike the Go and Rust reporters, this is a Microsoft Testing Platform (MTP) V2 extension that registers automatically via `buildTransitive` props — no piping test output through a filter is needed. Works with TUnit, MSTest, xUnit, NUnit, and other MTP V2 frameworks. See the [.NET reporter configuration](../reporters/dotnet/README.md#configuration) for more details.

## Troubleshooting

### Hooks not working

The recommended installation method is via the plugin, as
described in the [README](../README.md#installation).

Non-plugin installations might be incompatible with your current Claude Code version, hooks will
fail silently or exit with errors.

If you have an existing Homebrew or npm installation, remove it and switch to the plugin
installation.
