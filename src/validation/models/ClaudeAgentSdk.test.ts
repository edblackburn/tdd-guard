import { describe, test, expect, vi, beforeEach } from 'vitest'
import { ClaudeAgentSdk } from './ClaudeAgentSdk'
import { Config } from '../../config/Config'
import { ModelClient } from '../../contracts/types/ModelClient'
import { query, type SDKResultMessage } from '@anthropic-ai/claude-agent-sdk'
import { SYSTEM_PROMPT } from '../prompts/system-prompt'

describe('ClaudeAgentSdk', () => {
  describe('constructor', () => {
    test('implements the ModelClient interface', () => {
      const client: ModelClient = new ClaudeAgentSdk()
      expect(client.ask).toBeDefined()
    })

    test('accepts optional Config in constructor', () => {
      const config = new Config()
      const client = new ClaudeAgentSdk(config)
      expect(client['config']).toBe(config)
    })

    test('uses default Config when not provided', () => {
      const client = new ClaudeAgentSdk()
      expect(client['config']).toBeInstanceOf(Config)
    })

    test('accepts query function as second parameter', () => {
      const customQuery = vi.fn()
      const config = new Config()
      const client = new ClaudeAgentSdk(config, customQuery)
      expect(client['queryFn']).toBe(customQuery)
    })

    test('uses query from @anthropic-ai/claude-agent-sdk when not provided', () => {
      const client = new ClaudeAgentSdk()
      expect(client['queryFn']).toBe(query)
    })
  })

  describe('query invocation', () => {
    const prompt = 'test prompt'
    const message = createSDKResultMessage()
    const modelVersion = 'claude-opus-4-1'
    const config = new Config({ modelVersion })
    const { client, getUsedOptions, getUsedPrompt } = setupClient(
      message,
      config
    )

    beforeEach(async () => {
      await client.ask(prompt)
    })

    test('calls queryFn with correct prompt', async () => {
      expect(getUsedPrompt()).toBe(prompt)
    })

    test('sets maxTurns to 1', async () => {
      expect(getUsedOptions().maxTurns).toBe(1)
    })

    test('sets allowedTools to empty array', async () => {
      expect(getUsedOptions().allowedTools).toEqual([])
    })

    test('sets disallowedTools to prevent file operations and other tools', async () => {
      const expectedDisallowedTools = [
        'Read',
        'Edit',
        'MultiEdit',
        'Write',
        'Grep',
        'Glob',
        'Bash',
        'WebFetch',
        'WebSearch',
        'Task',
        'TodoWrite',
      ]
      expect(getUsedOptions().disallowedTools).toEqual(expectedDisallowedTools)
    })

    test('disables thinking via the non-deprecated thinking option', async () => {
      expect(getUsedOptions().thinking).toEqual({ type: 'disabled' })
    })

    test('uses model version from config', async () => {
      expect(getUsedOptions().model).toBe(modelVersion)
    })

    test('sets strictMcpConfig to true', async () => {
      expect(getUsedOptions().strictMcpConfig).toBe(true)
    })

    test('uses SYSTEM_PROMPT for systemPrompt', async () => {
      expect(getUsedOptions().systemPrompt).toBe(SYSTEM_PROMPT)
    })

    test('does not set cwd so isolation comes from settingSources and persistSession instead', async () => {
      expect(getUsedOptions().cwd).toBeUndefined()
    })

    test('sets persistSession to false to keep validation out of session history', async () => {
      expect(getUsedOptions().persistSession).toBe(false)
    })

    test('sets settingSources to empty so no filesystem settings or CLAUDE.md are loaded', async () => {
      expect(getUsedOptions().settingSources).toEqual([])
    })

    test("sets permissionMode to 'dontAsk' so the non-interactive child denies instead of prompting", async () => {
      expect(getUsedOptions().permissionMode).toBe('dontAsk')
    })
  })

  describe('result handling', () => {
    test('returns result from successful response', async () => {
      const { client } = setupClient({ result: 'test result' })

      await expect(client.ask('test')).resolves.toBe('test result')
    })

    test('throws error when query returns error subtype', async () => {
      const { client } = setupClient({ subtype: 'error_max_turns', errors: [] })

      await expect(client.ask('test')).rejects.toThrow(
        'Claude Agent SDK error: error_max_turns'
      )
    })

    test('throws error when no result message is received', async () => {
      const { client } = setupClient({ type: 'other', data: 'something' })

      await expect(client.ask('test')).rejects.toThrow(
        'Claude Agent SDK error: No result message received'
      )
    })

    test('surfaces the API error text when a success result is flagged as an error', async () => {
      const { client } = setupClient({
        subtype: 'success',
        is_error: true,
        result: 'Credit balance too low',
      })

      await expect(client.ask('test')).rejects.toThrow('Credit balance too low')
    })

    test('surfaces the error details when the result subtype is an error', async () => {
      const { client } = setupClient({
        subtype: 'error_during_execution',
        errors: ['session crashed'],
      })

      await expect(client.ask('test')).rejects.toThrow('session crashed')
    })
  })
})

// Test Helpers
function setupClient(
  messageOverrides: Partial<SDKResultMessage> = {},
  config: Config = new Config()
) {
  const customQuery = createMockQuery(messageOverrides)
  const client = new ClaudeAgentSdk(config, customQuery)

  const getLastCall = () => customQuery.mock.lastCall![0]
  const getUsedOptions = () => getLastCall().options
  const getUsedPrompt = () => getLastCall().prompt

  return {
    client,
    customQuery,
    config,
    getUsedOptions,
    getUsedPrompt,
  }
}

function createMockQuery(messageOverrides: Partial<SDKResultMessage> = {}) {
  return vi.fn().mockReturnValue({
    async *[Symbol.asyncIterator]() {
      yield createSDKResultMessage(messageOverrides)
    },
  })
}

function createSDKResultMessage(
  overrides: Partial<SDKResultMessage> = {}
): SDKResultMessage {
  return {
    type: 'result',
    subtype: 'success',
    result: 'default result',
    ...overrides,
  } as SDKResultMessage
}
