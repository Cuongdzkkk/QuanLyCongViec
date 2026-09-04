import assert from 'node:assert/strict'
import test from 'node:test'
import {
  buildBillingCheckoutLocation,
  resolveBillingReturnTo
} from '../src/utils/billingPlanFlow.js'

test('billing return context keeps internal path and query', () => {
  assert.equal(
    resolveBillingReturnTo('/ai-assistant?conversation=conversation-42'),
    '/ai-assistant?conversation=conversation-42'
  )
  assert.equal(
    resolveBillingReturnTo('/space/project-42/integrations?provider=github'),
    '/space/project-42/integrations?provider=github'
  )
})

test('billing return context rejects external and executable destinations', () => {
  for (const value of [
    'https://evil.example/steal',
    'http://evil.example/steal',
    '//evil.example/steal',
    'javascript:alert(1)',
    'data:text/html,<script>alert(1)</script>'
  ]) {
    assert.equal(resolveBillingReturnTo(value), '/dashboard')
  }
})

test('checkout location carries a validated origin without trusting it for payment', () => {
  assert.deepEqual(buildBillingCheckoutLocation('Plus', 'order-42', '/ai-assistant?conversation=42'), {
    name: 'BillingCheckout',
    params: { planCode: 'plus' },
    query: { orderId: 'order-42', returnTo: '/ai-assistant?conversation=42' }
  })
  assert.equal(
    buildBillingCheckoutLocation('Plus', '', 'https://evil.example').query.returnTo,
    '/dashboard'
  )
})
