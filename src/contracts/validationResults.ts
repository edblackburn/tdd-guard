import { ValidationResult } from './types/ValidationResult'

export const allow: ValidationResult = {
  decision: undefined,
  reason: '',
}

export function block(reason: string): ValidationResult {
  return {
    decision: 'block',
    reason,
  }
}

export function stopSession(message: string): ValidationResult {
  return {
    decision: undefined,
    reason: message,
    continue: false,
    stopReason: message,
  }
}

export function isAllow(result: ValidationResult): boolean {
  return (
    result.decision === undefined &&
    result.reason === '' &&
    result.continue !== false
  )
}
