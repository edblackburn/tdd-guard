import { Config } from '../../config/Config'
import { query, type Options } from '@anthropic-ai/claude-agent-sdk'
import { ModelClient } from '../../contracts/types/ModelClient'
import { SYSTEM_PROMPT } from '../prompts/system-prompt'

export class ClaudeAgentSdk implements ModelClient {
  constructor(
    private readonly config: Config = new Config(),
    private readonly queryFn: typeof query = query
  ) {}

  async ask(prompt: string): Promise<string> {
    const queryResult = this.queryFn({
      prompt,
      options: this.getQueryOptions(),
    })

    for await (const message of queryResult) {
      if (message.type !== 'result') continue

      if (message.subtype === 'success') {
        if (message.is_error) {
          throw new Error(message.result)
        }
        return message.result
      }
      throw new Error(
        message.errors.length
          ? message.errors.join('; ')
          : `Claude Agent SDK error: ${message.subtype}`
      )
    }

    throw new Error('Claude Agent SDK error: No result message received')
  }

  private getQueryOptions(): Options {
    return {
      maxTurns: 1,
      systemPrompt: SYSTEM_PROMPT,
      allowedTools: [],
      disallowedTools: [
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
      ],
      thinking: { type: 'disabled' },
      model: this.config.modelVersion,
      permissionMode: 'dontAsk',
      strictMcpConfig: true,
      settingSources: [],
      persistSession: false,
    }
  }
}
