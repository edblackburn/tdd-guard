export type ValidationResult = {
  decision: 'block' | undefined
  reason: string
  continue?: boolean
  stopReason?: string
}
