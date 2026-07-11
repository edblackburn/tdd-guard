export interface ModelClient {
  ask(prompt: string): Promise<string>
}
