import { buildContext } from '../cli/buildContext'
import { HookEvents } from './HookEvents'
import { PostToolLintHandler } from './postToolLint'
import { isTestFile, detectLanguage, type Language } from './fileTypeDetection'
import { LinterProvider } from '../providers/LinterProvider'
import { UserPromptHandler } from './userPromptHandler'
import { SessionHandler } from './sessionHandler'
import { GuardManager } from '../guard/GuardManager'
import { Storage } from '../storage/Storage'
import { FileStorage } from '../storage/FileStorage'
import { ValidationResult } from '../contracts/types/ValidationResult'
import { allow, block } from '../contracts/validationResults'
import { Context } from '../contracts/types/Context'
import { countTestDefinitions } from './testCounter'
import {
  HookDataSchema,
  ToolOperationSchema,
  isEditOperation,
  isMultiEditOperation,
  isWriteOperation,
  isFileModification,
  type ToolOperation,
  type FileModification,
} from '../contracts/schemas/toolSchemas'
import { PytestResultSchema } from '../contracts/schemas/pytestSchemas'
import {
  isTestPassing,
  TestResultSchema,
} from '../contracts/schemas/reporterSchemas'
import { LintDataSchema } from '../contracts/schemas/lintSchemas'
import { readOldFileContent } from './readOldFileContent'

export interface ProcessHookDataDeps {
  storage?: Storage
  validator?: (context: Context) => Promise<ValidationResult>
  userPromptHandler?: UserPromptHandler
}

function extractFilePath(parsedData: unknown): string | null {
  if (!parsedData || typeof parsedData !== 'object') {
    return null
  }

  const data = parsedData as Record<string, unknown>
  const toolInput = data.tool_input

  if (
    !toolInput ||
    typeof toolInput !== 'object' ||
    !('file_path' in toolInput)
  ) {
    return null
  }

  const filePath = (toolInput as Record<string, unknown>).file_path
  if (typeof filePath !== 'string') {
    return null
  }

  return filePath
}

async function enrichWriteOperation(
  operation: ToolOperation | undefined
): Promise<ToolOperation | undefined> {
  if (!operation || !isWriteOperation(operation)) return operation
  if (operation.hook_event_name !== 'PreToolUse') return operation

  try {
    const oldContent = await readOldFileContent(operation.tool_input.file_path)
    return {
      ...operation,
      tool_input: { ...operation.tool_input, old_content: oldContent },
    }
  } catch {
    // Unreadable for reasons other than ENOENT (e.g., EISDIR, EACCES);
    // skip enrichment rather than failing the hook.
    return operation
  }
}

export async function processHookData(
  inputData: string,
  deps: ProcessHookDataDeps = {}
): Promise<ValidationResult> {
  const parsedData = JSON.parse(inputData)

  // Initialize dependencies
  const storage = deps.storage ?? new FileStorage()
  const guardManager = new GuardManager(storage)
  const userPromptHandler =
    deps.userPromptHandler ?? new UserPromptHandler(guardManager)

  // Skip validation for ignored files based on patterns
  const filePath = extractFilePath(parsedData)
  if (filePath && (await guardManager.shouldIgnoreFile(filePath))) {
    return allow
  }

  const sessionHandler = new SessionHandler(storage)

  // Process SessionStart events
  if (parsedData.hook_event_name === 'SessionStart') {
    await sessionHandler.processSessionStart(inputData)
    return allow
  }

  // Process user commands
  const stateResult = await userPromptHandler.processUserCommand(inputData)
  if (stateResult) {
    return stateResult
  }

  // Check if guard is disabled and return early if so
  const disabledResult = await userPromptHandler.getDisabledResult()
  if (disabledResult) {
    return disabledResult
  }

  // Create lintHandler with linter from provider
  const linterProvider = new LinterProvider()
  const linter = linterProvider.getLinter()
  const lintHandler = new PostToolLintHandler(storage, linter)

  const hookResult = HookDataSchema.safeParse(parsedData)
  if (!hookResult.success) {
    return allow
  }

  const operation = await enrichWriteOperation(
    ToolOperationSchema.safeParse(hookResult.data).data
  )

  await processHookEvent(operation, storage)

  // Check if this is a PostToolUse event
  if (hookResult.data.hook_event_name === 'PostToolUse') {
    return await lintHandler.handle(inputData)
  }

  if (!operation || !isFileModification(operation)) {
    return allow
  }

  // For PreToolUse, check if we should notify about lint issues
  if (hookResult.data.hook_event_name === 'PreToolUse') {
    const lintNotification = await checkLintNotification(
      storage,
      operation.tool_input.file_path
    )
    if (lintNotification.decision === 'block') {
      return lintNotification
    }
  }

  if (isAllowedTestAddition(operation)) {
    return allow
  }

  return await performValidation(deps)
}

async function processHookEvent(
  operation: ToolOperation | undefined,
  storage?: Storage
): Promise<void> {
  if (storage) {
    const hookEvents = new HookEvents(storage)
    await hookEvents.processEvent(operation)
  }
}

function isAllowedTestAddition(operation: FileModification): boolean {
  const filePath = operation.tool_input.file_path
  if (!isTestFile(filePath)) return false

  const language = detectLanguage(filePath)
  if (!language) return false

  return countAddedTests(operation, language) === 1
}

function diffTestCount(
  oldContent: string | undefined,
  newContent: string,
  language: Language
): number {
  const newCount = countTestDefinitions(newContent, language)
  const oldCount = oldContent ? countTestDefinitions(oldContent, language) : 0
  return newCount - oldCount
}

function countAddedTests(operation: ToolOperation, language: Language): number {
  if (isEditOperation(operation)) {
    return diffTestCount(
      operation.tool_input.old_string,
      operation.tool_input.new_string,
      language
    )
  }
  if (isWriteOperation(operation)) {
    return diffTestCount(
      operation.tool_input.old_content,
      operation.tool_input.content,
      language
    )
  }
  if (isMultiEditOperation(operation)) {
    return operation.tool_input.edits.reduce(
      (total, edit) =>
        total + diffTestCount(edit.old_string, edit.new_string, language),
      0
    )
  }
  return 0
}

async function performValidation(
  deps: ProcessHookDataDeps
): Promise<ValidationResult> {
  if (deps.validator && deps.storage) {
    const context = await buildContext(deps.storage)
    return await deps.validator(context)
  }

  return allow
}

async function checkLintNotification(
  storage: Storage,
  filePath: string
): Promise<ValidationResult> {
  // Get test results to check if tests are passing
  let testsPassing = false
  try {
    const testStr = await storage.getTest()
    if (testStr) {
      const isPython = detectLanguage(filePath) === 'python'
      const testResult = isPython
        ? PytestResultSchema.safeParse(JSON.parse(testStr))
        : TestResultSchema.safeParse(JSON.parse(testStr))
      if (testResult.success) {
        testsPassing = isTestPassing(testResult.data)
      }
    }
  } catch {
    testsPassing = false
  }

  // Only proceed if tests are passing
  if (!testsPassing) {
    return allow
  }

  // Get lint data
  let lintData
  try {
    const lintStr = await storage.getLint()
    if (lintStr) {
      lintData = LintDataSchema.parse(JSON.parse(lintStr))
    }
  } catch {
    return allow
  }

  // Only proceed if lint data exists
  if (!lintData) {
    return allow
  }

  const hasIssues = lintData.errorCount > 0 || lintData.warningCount > 0

  // Block if:
  // 1. Tests are passing (already checked)
  // 2. There are lint issues
  // 3. hasNotifiedAboutLintIssues is false (not yet notified)
  if (hasIssues && !lintData.hasNotifiedAboutLintIssues) {
    // Update the notification flag and save
    const updatedLintData = {
      ...lintData,
      hasNotifiedAboutLintIssues: true,
    }
    await storage.saveLint(JSON.stringify(updatedLintData))

    return block(
      'Code quality issues detected. You need to fix those first before making any other changes. Remember to exercise system thinking and design awareness to ensure continuous architectural improvements. Consider: design patterns, SOLID principles, DRY, types and interfaces, and architectural improvements. Apply equally to implementation and test code. Use test data factories, helpers, and beforeEach to better organize tests.'
    )
  }

  return allow
}
