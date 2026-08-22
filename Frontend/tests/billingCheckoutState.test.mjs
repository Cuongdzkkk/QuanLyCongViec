import assert from 'node:assert/strict'
import test from 'node:test'
import {
  canShowPaymentReceipt,
  createCheckoutOrderGate,
  formatRemainingTime,
  getCheckoutState,
  getPaymentCopyValues,
  getOrderDisplayStatus,
  getOrderRemainingSeconds,
  isOrderForPlan,
  isActivePendingOrder,
  isExpiredPendingOrder,
  selectActivePendingOrder,
  shouldPollPaymentOrder
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

test('checkout state exposes the payment lifecycle and display countdown', () => {
  const active = { status: 'Pending', expiresAt: '2026-08-22T10:28:42Z' }
  const expired = { status: 'Pending', expiresAt: '2026-08-22T09:59:00Z' }

  assert.equal(getCheckoutState(active, now), 'Pending')
  assert.equal(getOrderRemainingSeconds(active, now), 1722)
  assert.equal(formatRemainingTime(1722), '28:42')
  assert.equal(getCheckoutState(expired, now), 'Expired')
  assert.equal(getCheckoutState({ status: 'Paid' }, now), 'Paid')
  assert.equal(getCheckoutState(null, now), 'Idle')
})

test('validated checkout order gate makes one request and rejects invalid success payloads', async () => {
  const validOrder = {
    id: '00000000-0000-4000-8000-000000000001',
    planCode: 'pro',
    status: 'Pending',
    expiresAt: '2026-08-22T10:30:00Z',
    transferCode: 'SEVQR SPA123',
    amountVnd: 150000
  }
  let calls = 0
  let resolveRequest
  const responsePending = new Promise(resolve => { resolveRequest = resolve })
  const gate = createCheckoutOrderGate(async () => {
    calls += 1
    await responsePending
    return validOrder
  })

  const first = gate('pro', now)
  const second = gate('pro', now)
  assert.equal(calls, 1)
  resolveRequest()
  assert.equal((await first).id, validOrder.id)
  assert.equal((await second).id, validOrder.id)

  const invalidGate = createCheckoutOrderGate(async () => ({ ...validOrder, id: undefined }))
  await assert.rejects(invalidGate('pro', now), /Invalid payment order response/)
})

test('active order selection requires the same plan and a valid future expiry', () => {
  const activeOlder = { id: '00000000-0000-4000-8000-000000000001', planCode: 'pro', status: 'Pending', expiresAt: '2026-08-22T10:30:00Z', createdAt: '2026-08-22T09:00:00Z' }
  const activeNewer = { id: '00000000-0000-4000-8000-000000000002', planCode: 'pro', status: 'Pending', expiresAt: '2026-08-22T10:30:00Z', createdAt: '2026-08-22T09:00:00Z' }
  const candidates = [
    activeOlder,
    activeNewer,
    { ...activeNewer, id: '00000000-0000-4000-8000-000000000003', planCode: 'plus' },
    { ...activeNewer, id: '00000000-0000-4000-8000-000000000004', status: 'Paid' },
    { ...activeNewer, id: '00000000-0000-4000-8000-000000000005', expiresAt: null },
    { ...activeNewer, id: '00000000-0000-4000-8000-000000000006', expiresAt: 'invalid' },
    { ...activeNewer, id: '00000000-0000-4000-8000-000000000007', expiresAt: '2026-08-22T09:59:00Z' }
  ]

  assert.equal(selectActivePendingOrder(candidates, 'pro', now).id, activeNewer.id)
  assert.equal(isActivePendingOrder(candidates[4], now), false)
  assert.equal(isActivePendingOrder(candidates[5], now), false)
  assert.equal(getCheckoutState(candidates[4], now), 'Expired')
  assert.equal(getCheckoutState(candidates[5], now), 'Expired')
  assert.equal(isOrderForPlan(candidates[2], 'pro'), false)
  assert.equal(isExpiredPendingOrder(candidates[6], now), true)
})

test('polling and copy contracts follow the active payment lifecycle', () => {
  const active = { id: '00000000-0000-4000-8000-000000000001', planCode: 'pro', status: 'Pending', expiresAt: '2026-08-22T10:30:00Z' }
  assert.equal(shouldPollPaymentOrder(active, now), true)
  assert.equal(shouldPollPaymentOrder({ ...active, status: 'Paid' }, now), false)
  assert.equal(shouldPollPaymentOrder({ ...active, expiresAt: '2026-08-22T09:59:00Z' }, now), false)

  const copyValues = getPaymentCopyValues({
    ...active,
    amountVnd: 999,
    transferCode: 'fallback',
    paymentInstructions: { accountNumber: '0123456789', amountVnd: 150000, transferContent: 'SEVQR SPA123' }
  })
  assert.deepEqual(copyValues, { accountNumber: '0123456789', amount: 150000, transferContent: 'SEVQR SPA123' })
  assert.equal(canShowPaymentReceipt({ ...active, status: 'Pending' }, now), false)
  assert.equal(canShowPaymentReceipt({ ...active, status: 'Expired' }, now), false)
  assert.equal(canShowPaymentReceipt({ ...active, status: 'Paid' }, now), true)
})
