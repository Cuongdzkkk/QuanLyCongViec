const VALID_ORDER_STATUSES = new Set(['Pending', 'Paid', 'Expired', 'Failed', 'Rejected'])
const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export const getOrderExpiryTimestamp = (order) => {
  if (!order?.expiresAt) return null
  const timestamp = new Date(order.expiresAt).getTime()
  return Number.isFinite(timestamp) ? timestamp : null
}

export const isOrderForPlan = (order, planCode) => Boolean(
  GUID_PATTERN.test(String(order?.id || '')) &&
  String(order.planCode || '').toLowerCase() === String(planCode || '').toLowerCase()
)

export const isValidPaymentOrderPayload = (order, planCode, now = Date.now()) => {
  const expiry = getOrderExpiryTimestamp(order)
  return Boolean(
    isOrderForPlan(order, planCode) &&
    String(order.status || '').toLowerCase() === 'pending' &&
    expiry !== null &&
    expiry > now &&
    String(order.transferCode || '').trim() &&
    Number.isFinite(Number(order.amountVnd)) &&
    Number(order.amountVnd) > 0
  )
}

export const createCheckoutOrderGate = (createOrder) => {
  let pendingRequest = null
  return async (planCode, now = Date.now()) => {
    if (pendingRequest) return pendingRequest
    const request = (async () => {
      const order = await createOrder(planCode)
      if (!isValidPaymentOrderPayload(order, planCode, now)) throw new Error('Invalid payment order response.')
      return order
    })()
    pendingRequest = request
    try {
      return await request
    } finally {
      if (pendingRequest === request) pendingRequest = null
    }
  }
}

export const isExpiredPendingOrder = (order, now = Date.now()) => {
  if (!order || order.status !== 'Pending') return false
  const expiry = getOrderExpiryTimestamp(order)
  return expiry === null || expiry <= now
}

export const getOrderDisplayStatus = (order, now = Date.now()) => {
  if (!order) return ''
  return isExpiredPendingOrder(order, now) ? 'Expired' : order.status
}

export const isActivePendingOrder = (order, now = Date.now()) => {
  if (!order || order.status !== 'Pending') return false
  const expiry = getOrderExpiryTimestamp(order)
  return expiry !== null && expiry > now
}

export const canShowPaymentReceipt = (order, now = Date.now()) => getOrderDisplayStatus(order, now) === 'Paid'

export const getOrderRemainingSeconds = (order, now = Date.now()) => {
  if (!isActivePendingOrder(order, now) || !order.expiresAt) return 0
  return Math.max(0, Math.ceil((new Date(order.expiresAt).getTime() - now) / 1000))
}

export const formatRemainingTime = (seconds) => {
  const safeSeconds = Math.max(0, Number(seconds) || 0)
  const minutes = Math.floor(safeSeconds / 60)
  const remainder = safeSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(remainder).padStart(2, '0')}`
}

export const getCheckoutState = (order, now = Date.now()) => {
  const status = getOrderDisplayStatus(order, now)
  if (isActivePendingOrder(order, now)) return 'Pending'
  if (status === 'Paid') return 'Paid'
  if (status === 'Expired') return 'Expired'
  if (status === 'Failed' || status === 'Rejected') return status
  return 'Idle'
}

export const selectActivePendingOrder = (orders, planCode, now = Date.now()) => {
  return [...(orders || [])]
    .filter(order => isOrderForPlan(order, planCode) && isActivePendingOrder(order, now))
    .sort((left, right) => {
      const createdDifference = new Date(right.createdAt || 0).getTime() - new Date(left.createdAt || 0).getTime()
      return createdDifference || String(right.id).localeCompare(String(left.id))
    })[0] || null
}

export const shouldPollPaymentOrder = (order, now = Date.now()) => isActivePendingOrder(order, now)

export const getPaymentCopyValues = (order) => ({
  accountNumber: order?.paymentInstructions?.accountNumber || '',
  amount: order?.paymentInstructions?.amountVnd ?? order?.amountVnd ?? '',
  transferContent: order?.paymentInstructions?.transferContent || order?.transferCode || ''
})

export const isKnownPaymentOrderStatus = (status) => VALID_ORDER_STATUSES.has(status)
