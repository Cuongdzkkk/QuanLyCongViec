export const isExpiredPendingOrder = (order, now = Date.now()) => {
  if (!order || order.status !== 'Pending' || !order.expiresAt) return false
  return new Date(order.expiresAt).getTime() <= now
}

export const getOrderDisplayStatus = (order, now = Date.now()) => {
  if (!order) return ''
  return isExpiredPendingOrder(order, now) ? 'Expired' : order.status
}

export const isActivePendingOrder = (order, now = Date.now()) => {
  if (!order || order.status !== 'Pending') return false
  return !order.expiresAt || new Date(order.expiresAt).getTime() > now
}

export const canShowPaymentReceipt = (order, now = Date.now()) => getOrderDisplayStatus(order, now) === 'Paid'
