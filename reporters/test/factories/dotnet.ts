import { spawnSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, writeFileSync, realpathSync } from 'node:fs'
import { join } from 'node:path'
import { tmpdir } from 'node:os'
import type { ReporterConfig, TestScenarios } from '../types'
import { copyTestArtifacts } from './helpers'

let nupkgsDir: string | null = null

function packNuGetPackages(): string {
  if (nupkgsDir) return nupkgsDir

  nupkgsDir = mkdtempSync(join(tmpdir(), 'tddguard-nupkgs-'))
  const solutionDir = join(__dirname, '../../dotnet')

  const projects = [
    'TddGuard.Dotnet.Core/TddGuard.Dotnet.Core.csproj',
    'TddGuard.Dotnet/TddGuard.Dotnet.csproj',
  ]

  for (const project of projects) {
    const dotnet = 'dotnet'
    // eslint-disable-next-line sonarjs/no-os-command-from-path
    const result = spawnSync(dotnet, ['pack', project, '--output', nupkgsDir], {
      cwd: solutionDir,
      stdio: 'pipe',
      encoding: 'utf8',
      timeout: 120000,
    })
    if (result.status !== 0) {
      throw new Error(`dotnet pack ${project} failed: ${result.stderr}`)
    }
  }

  return nupkgsDir
}

function writeNugetConfig(tempDir: string, localFeedPath: string): void {
  const config = `<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="${localFeedPath}" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
`
  writeFileSync(join(tempDir, 'nuget.config'), config)
}

export function runDotnetArtifact(tempDir: string): void {
  const feedDir = packNuGetPackages()
  writeNugetConfig(tempDir, feedDir)

  // Resolve symlinks (macOS: /var -> /private/var) so the .NET reporter's
  // cwd-within-root check passes when Directory.GetCurrentDirectory()
  // returns the resolved path.
  const env = {
    ...process.env,
    TDD_GUARD_PROJECT_ROOT: realpathSync(tempDir),
  }

  // eslint-disable-next-line sonarjs/no-os-command-from-path
  const buildResult = spawnSync('dotnet', ['build', tempDir], {
    cwd: tempDir,
    stdio: 'pipe',
    encoding: 'utf8',
    env,
    timeout: 120000,
  })

  // Handle compilation failures with synthetic test module (Go/Rust pattern)
  if (buildResult.status !== 0) {
    const output = (buildResult.stdout || '') + (buildResult.stderr || '')
    const errorLines = output
      .split('\n')
      .filter((line) => line.includes(' error '))
      .map((line) => line.trim())
    const message =
      errorLines.length > 0 ? errorLines.join('\n') : output.trim()

    const testJson = {
      testModules: [
        {
          moduleId: 'compilation',
          tests: [
            {
              name: 'build',
              fullName: 'compilation::build',
              state: 'failed',
              errors: [{ message }],
            },
          ],
        },
      ],
      reason: 'failed',
    }

    const dataDir = join(tempDir, '.claude', 'tdd-guard', 'data')
    mkdirSync(dataDir, { recursive: true })
    writeFileSync(join(dataDir, 'test.json'), JSON.stringify(testJson))
    return
  }

  // eslint-disable-next-line sonarjs/no-os-command-from-path
  spawnSync('dotnet', ['run', '--no-build', '--project', tempDir], {
    cwd: tempDir,
    stdio: 'pipe',
    encoding: 'utf8',
    env,
    timeout: 120000,
  })
}

export function createDotnetReporter(): ReporterConfig {
  const artifactDir = 'dotnet'
  const testScenarios = {
    singlePassing: 'passing',
    singleFailing: 'failing',
    singleImportError: 'import-error',
  }

  return {
    name: 'DotnetReporter',
    testScenarios,
    run: (tempDir, scenario: keyof TestScenarios) => {
      copyTestArtifacts(artifactDir, testScenarios, scenario, tempDir)
      runDotnetArtifact(tempDir)
    },
  }
}
