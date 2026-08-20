export const getIntegrationAccountId = (user) => `${user?.id || user?.Id || ''}`

export const createIntegrationRequestEpoch = () => {
  let epoch = 0

  return {
    capture(accountId = '') {
      return { epoch, accountId: `${accountId}` }
    },
    invalidate() {
      epoch += 1
    },
    isCurrent(request, accountId = '') {
      return request?.epoch === epoch && request?.accountId === `${accountId}`
    }
  }
}

export const isReconnectRequiredMessage = (message = '') => {
  const normalized = `${message}`.toLowerCase()
  return normalized.includes('reconnect') || normalized.includes('kết nối lại')
}
