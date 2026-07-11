import { spawn, spawnSync } from 'node:child_process'
import type { ChildProcess, SpawnSyncReturns } from 'node:child_process'
import { writeFileSync, mkdirSync, symlinkSync, existsSync } from 'node:fs'
import { once } from 'node:events'
import { join } from 'node:path'
import getPort from 'get-port'
import type { ReporterConfig, TestScenarios } from '../types'
import { copyTestArtifacts, getReporterPath } from './helpers'

const ARTIFACT_DIR = 'storybook'
const ROOT_NODE_MODULES = join(__dirname, '../../../node_modules')
const STORYBOOK_BIN = join(ROOT_NODE_MODULES, '.bin', 'storybook')

const SERVER_READY_TIMEOUT_MS = 90_000
const SERVER_POLL_INTERVAL_MS = 500
const SERVER_STOP_GRACE_MS = 1_000

export function createStorybookReporter(): ReporterConfig {
  const testScenarios = {
    singlePassing: 'single-passing.stories.js',
    singleFailing: 'single-failing.stories.js',
    singleImportError: 'single-import-error.stories.js',
  }

  return {
    name: 'StorybookReporter',
    testScenarios,
    run: async (tempDir, scenario: keyof TestScenarios) => {
      const port = await getPort()
      scaffoldProject(tempDir, scenario, testScenarios)

      const server = await startStorybookServer(tempDir, port)
      try {
        const run = runTestRunner(tempDir, port)
        assertReporterProducedOutput(tempDir, run, server.readStderr())
      } finally {
        await server.stop()
      }
    },
  }
}

/**
 * Lays out the temp project the test-runner needs: the shared component, the
 * scenario story, the Storybook config, the test-runner hooks, an ESM manifest,
 * a writable cache dir, and a link to the hoisted node_modules.
 */
function scaffoldProject(
  tempDir: string,
  scenario: keyof TestScenarios,
  scenarios: TestScenarios
): void {
  copyTestArtifacts(
    ARTIFACT_DIR,
    { common: 'Calculator.js' },
    'common',
    tempDir
  )
  copyTestArtifacts(ARTIFACT_DIR, scenarios, scenario, tempDir)

  const storybookDir = join(tempDir, '.storybook')
  mkdirSync(storybookDir, { recursive: true })
  writeFileSync(join(storybookDir, 'main.js'), storybookConfig())
  writeFileSync(join(storybookDir, 'test-runner.js'), testRunnerHooks(tempDir))

  writeFileSync(join(tempDir, 'package.json'), esmManifest())
  mkdirSync(cacheDir(tempDir), { recursive: true })
  symlinkSync(ROOT_NODE_MODULES, join(tempDir, 'node_modules'), 'dir')
}

/** A running Storybook dev server with its captured stderr and a stop handle. */
interface StorybookServer {
  readStderr(): string
  stop(): Promise<void>
}

/**
 * Spawns `storybook dev` and resolves once it is serving the story index. On a
 * startup failure it tears the process down before rejecting, so callers only
 * need to stop a server they successfully received.
 */
async function startStorybookServer(
  tempDir: string,
  port: number
): Promise<StorybookServer> {
  const proc = spawn(
    STORYBOOK_BIN,
    ['dev', '--config-dir', '.storybook', '--port', String(port), '--ci'],
    {
      cwd: tempDir,
      // Custom cache dir avoids EACCES under the symlinked node_modules.
      env: { ...process.env, STORYBOOK_CACHE_DIR: cacheDir(tempDir) },
      stdio: 'pipe',
    }
  )
  const readStderr = captureOutput(proc)

  try {
    await waitUntilServing(proc, port)
  } catch (error) {
    await stopServer(proc)
    throw error
  }

  return { readStderr, stop: () => stopServer(proc) }
}

/**
 * Drains stdout so its pipe buffer cannot fill and stall the server, and
 * accumulates stderr behind a getter for diagnostics.
 */
function captureOutput(proc: ChildProcess): () => string {
  let stderr = ''
  proc.stdout?.on('data', () => {})
  proc.stderr?.on('data', (chunk: Buffer) => {
    stderr += chunk.toString()
  })
  return () => stderr
}

/**
 * Polls the dev server's story index until it responds, failing fast if the
 * process exits first or the readiness timeout elapses.
 */
async function waitUntilServing(
  proc: ChildProcess,
  port: number
): Promise<void> {
  const indexUrl = `http://localhost:${port}/index.json`
  const maxAttempts = SERVER_READY_TIMEOUT_MS / SERVER_POLL_INTERVAL_MS

  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    if (proc.exitCode !== null) {
      throw new Error(
        `Storybook dev server exited early with code ${proc.exitCode}`
      )
    }
    if (await isServing(indexUrl)) {
      return
    }
    await delay(SERVER_POLL_INTERVAL_MS)
  }

  throw new Error('Storybook dev server timed out before serving stories')
}

/** Resolves true once the URL responds, false while it still refuses connections. */
async function isServing(url: string): Promise<boolean> {
  try {
    const response = await fetch(url)
    return response.ok
  } catch {
    return false
  }
}

/** Kills the dev server and waits for it to exit so its port is released. */
async function stopServer(proc: ChildProcess): Promise<void> {
  proc.kill('SIGKILL')
  await Promise.race([once(proc, 'exit'), delay(SERVER_STOP_GRACE_MS)])
}

/**
 * Runs the test-runner against the dev server. It exits non-zero when stories
 * fail, which is expected for the failing scenarios, so success is verified by
 * the reporter's output rather than the exit code.
 */
function runTestRunner(
  tempDir: string,
  port: number
): SpawnSyncReturns<Buffer> {
  return spawnSync(
    process.execPath,
    [
      require.resolve('@storybook/test-runner/dist/test-storybook'),
      '--url',
      `http://localhost:${port}`,
      '--config-dir',
      '.storybook',
      '--maxWorkers=1',
    ],
    {
      cwd: tempDir,
      env: { ...process.env, CI: 'true' },
      stdio: 'pipe',
    }
  )
}

/**
 * Turns a silent miss into an actionable error: if the reporter wrote no
 * results, throw with everything captured from the run.
 */
function assertReporterProducedOutput(
  tempDir: string,
  run: SpawnSyncReturns<Buffer>,
  serverStderr: string
): void {
  const resultsFile = join(tempDir, '.claude', 'tdd-guard', 'data', 'test.json')
  if (existsSync(resultsFile)) {
    return
  }
  throw new Error(
    [
      'Storybook test-runner produced no reporter output.',
      `stdout:\n${run.stdout.toString()}`,
      `stderr:\n${run.stderr.toString()}`,
      `server stderr:\n${serverStderr}`,
    ].join('\n')
  )
}

const delay = (ms: number): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, ms))

const cacheDir = (tempDir: string): string => join(tempDir, '.storybook-cache')

/**
 * ESM manifest for the temp project. Storybook 10 requires ESM config files,
 * and the test-runner's story transform needs the stories treated as ESM.
 */
function esmManifest(): string {
  return JSON.stringify({ name: 'storybook-test', type: 'module' })
}

/** Minimal Storybook config pointing at the scenario story files. */
function storybookConfig(): string {
  return `
export default {
  stories: ['../*.stories.js'],
  framework: '@storybook/react-vite',
  core: { disableTelemetry: true },
}
`
}

/**
 * test-runner hooks that drive the real StorybookReporter through the postVisit
 * hook, deriving pass/fail from context.hasFailure and flushing once per story
 * (postVisit is awaited, so the write completes).
 */
function testRunnerHooks(tempDir: string): string {
  const reporterPath = getReporterPath('storybook/dist/index.js')
  return `
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)
const { StorybookReporter } = require('${reporterPath}')

const reporter = new StorybookReporter({ projectRoot: '${tempDir}' })

export default {
  async postVisit(page, context) {
    await reporter.onStoryResult(
      context,
      context.hasFailure ? 'failed' : 'passed'
    )
    await reporter.onComplete()
  },
}
`
}
