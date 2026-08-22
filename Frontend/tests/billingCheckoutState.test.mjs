import assert from 'node:assert/strict'
import test from 'node:test'
import {
  canShowPaymentReceipt,
  getOrderDisplayStatus,
  isActivePendingOrder,
  isExpiredPendingOrder
} from '../src/utils/billingCheckoutState.js'

const now = Date.parse('2026-08-22T10:00:00Z')

test('checkout state treats expired pending orders as expired and inactive', () => {
  const order = { status: 'Pending', expiresAt: '2026-08-22T09:59:00Z' }

  assert.equal(isExpiredPendingOrder(order, now), true)
  assert.equal(isActivePendingOrder(order, now), false)
  assert.equal(getOrderDisplayStatus(order, now), 'Expired')
  assert.equal(canShowPaymentReceipt(order, now), false)
})

test('checkout state keeps active pending orders separate from paid receipts', () => {
  const order = { status: 'Pending', expiresAt: '2026-08-22T10:30:00Z' }
  const paid = { status: 'Paid', expiresAt: '2026-08-22T09:00:00Z' }

  assert.equal(isActivePendingOrder(order, now), true)
  assert.equal(getOrderDisplayStatus(order, now), 'Pending')
  assert.equal(canShowPaymentReceipt(order, now), false)
  assert.equal(canShowPaymentReceipt(paid, now), true)
})
