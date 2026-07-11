import {
  isTodoWriteOperation,
  ToolOperation,
} from '../contracts/schemas/toolSchemas'
import { Storage } from '../storage/Storage'

export class HookEvents {
  constructor(private readonly storage: Storage) {}

  async processEvent(operation: ToolOperation | undefined): Promise<void> {
    if (!operation) return

    await this.persistOperation(operation)
  }

  private async persistOperation(operation: ToolOperation): Promise<void> {
    const content = JSON.stringify(operation, null, 2)

    if (isTodoWriteOperation(operation)) {
      await this.storage.saveTodo(content)
    } else {
      await this.storage.saveModifications(content)
    }
  }
}
