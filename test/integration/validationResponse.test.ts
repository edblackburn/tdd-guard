import { describe, test, expect } from 'vitest'
import { Config } from '../../src/config/Config'
import { ModelClientProvider } from '../../src/providers/ModelClientProvider'
import { generateDynamicContext } from '../../src/validation/context/context'
import { Context } from '../../src/contracts/types/Context'
import { testData } from '../utils'

describe('Validation response shape (real model)', () => {
  const config = new Config({ mode: 'test' })
  const model = new ModelClientProvider().getModelClient(config)
  const { createWriteOperation, languages } = testData
  const lang = languages[0]

  test('omits the reason when allowing an operation', async () => {
    const context: Context = {
      modifications: createWriteOperation(
        lang.testFile,
        lang.testModifications.singleTestComplete.content
      ),
      todo: JSON.stringify(lang.todos.empty.content),
      test: lang.testResults.empty.content,
    }

    const raw = await model.ask(generateDynamicContext(context))

    expect(raw).not.toContain('"reason"')
  })
})
