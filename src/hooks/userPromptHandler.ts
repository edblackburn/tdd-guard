import { GuardManager } from '../guard/GuardManager'
import { ValidationResult } from '../contracts/types/ValidationResult'
import { allow, stopSession } from '../contracts/validationResults'

export class UserPromptHandler {
  private readonly guardManager: GuardManager
  private readonly GUARD_COMMANDS = {
    ON: 'tdd-guard on',
    OFF: 'tdd-guard off',
  } as const

  constructor(guardManager?: GuardManager) {
    this.guardManager = guardManager ?? new GuardManager()
  }

  async processUserCommand(
    hookData: string
  ): Promise<ValidationResult | undefined> {
    const data = JSON.parse(hookData)

    // Only process UserPromptSubmit events
    if (data.hook_event_name !== 'UserPromptSubmit') {
      return undefined
    }

    const command = data.prompt?.toLowerCase()

    switch (command) {
      case this.GUARD_COMMANDS.ON:
        await this.guardManager.enable()
        return stopSession('TDD Guard enabled')

      case this.GUARD_COMMANDS.OFF:
        await this.guardManager.disable()
        return stopSession('TDD Guard disabled')

      default:
        return undefined
    }
  }

  async getDisabledResult(): Promise<ValidationResult | undefined> {
    const isEnabled = await this.guardManager.isEnabled()
    if (!isEnabled) {
      return allow
    }
    return undefined
  }
}
