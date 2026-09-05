const capabilityCache = new Map()

export const normalizeAiCapability = capability => {
  if (!capability?.actionKey || capability.availability === false || !capability.executor) return null
  return {
    ...capability,
    type: capability.legacyType || capability.actionKey,
    mode: String(capability.capabilityKind || '').toLowerCase() === 'write' ? 'write' : 'read',
    prompt: capability.quickPrompt || `${capability.displayName || capability.actionKey}.`,
    label: capability.displayName || capability.actionKey,
    icon: capability.icon || 'fa-solid fa-wand-magic-sparkles'
  }
}

export const getQuickAiCapabilities = capabilities => (capabilities || [])
  .map(normalizeAiCapability)
  .filter(capability => capability?.quickTool)

export const loadAiCapabilities = async axiosClient => {
  if (!axiosClient) return []
  const key = axiosClient
  if (!capabilityCache.has(key)) {
    capabilityCache.set(key, axiosClient.get('/ai/capabilities')
      .then(response => {
        const payload = response?.data?.data ?? response?.data ?? {}
        return Array.isArray(payload.capabilities) ? payload.capabilities : []
      })
      .catch(() => []))
  }
  return getQuickAiCapabilities(await capabilityCache.get(key))
}

export const isAiConfirmationMessage = (message = '') => {
  const normalized = `${message}`
    .trim()
    .toLocaleLowerCase('vi-VN')
    .replace(/[.!?。！？]+$/g, '')
  return ['ok', 'đồng ý', 'dong y', 'làm đi', 'lam di', 'xác nhận', 'xac nhan', 'confirm'].includes(normalized)
}

export const findPendingAiAction = messages => {
  for (const message of [...(messages || [])].reverse()) {
    const action = [...(message?.actions || [])].reverse().find(item =>
      item?.uiStatus === 'pending' && item?.requiresConfirmation !== false
    )
    if (action) return action
  }
  return null
}
