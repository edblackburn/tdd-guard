import { describe, test, expect } from 'vitest'
import { allow, block, stopSession, isAllow } from './validationResults'

describe('isAllow', () => {
  test('treats the allow result as an allow', () => {
    expect(isAllow(allow)).toBe(true)
  })

  test('does not treat a block result as an allow', () => {
    expect(isAllow(block('needs a test first'))).toBe(false)
  })

  test('does not treat a stop-session result as an allow', () => {
    expect(isAllow(stopSession('guard disabled'))).toBe(false)
  })

  test('does not treat a session-halting result as an allow', () => {
    expect(isAllow({ decision: undefined, reason: '', continue: false })).toBe(
      false
    )
  })
})
