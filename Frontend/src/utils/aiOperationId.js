const BILLABLE_AI_PATHS = [
  /^\/?ai\/(?!usage(?:-summary)?(?:\/|$)|conversations(?:\/|$))/i,
  /^\/?inbox\/[^/]+\/ai(?:\/|$)/i
]

export const isBillableAiRequest = (url = '') => {
  const path = String(url).split('?')[0]
  return BILLABLE_AI_PATHS.some(pattern => pattern.test(path))
}

export const createAiOperationId = () => {
  if (globalThis.crypto?.randomUUID) return globalThis.crypto.randomUUID()
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, char => {
    const value = Math.floor(Math.random() * 16)
    const nibble = char === 'x' ? value : (value & 0x3) | 0x8
    return nibble.toString(16)
  })
}

export const ensureAiOperationId = config => {
  if (!config || !isBillableAiRequest(config.url)) return config
  config.headers = config.headers || {}
  if (!config.headers['X-AI-Operation-Id'] && !config.headers['x-ai-operation-id']) {
    config.headers['X-AI-Operation-Id'] = createAiOperationId()
  }
  return config
}
