import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'
import { resolveBillingReturnTo } from '../src/utils/billingPlanFlow.js'

const checkout = fs.readFileSync(new URL('../src/views/BillingCheckout.vue', import.meta.url), 'utf8')

test('checkout exit replaces the current checkout history entry', () => {
  assert.match(checkout, /const goBackToOrigin = \(\) => router\.replace\(returnTo\.value\)/)
  assert.doesNotMatch(checkout, /const goBackToOrigin = \(\) => router\.push\(returnTo\.value\)/)
})

test('checkout exit preserves validated path and query context', () => {
  assert.match(checkout, /const returnTo = computed\(\(\) => resolveBillingReturnTo\(route\.query\.returnTo\)\)/)
  assert.equal(resolveBillingReturnTo('/ai-assistant'), '/ai-assistant')
  assert.equal(resolveBillingReturnTo('/ai-assistant?conversationId=test-context'), '/ai-assistant?conversationId=test-context')
  assert.equal(resolveBillingReturnTo('/space/project-42/work-items?tab=active'), '/space/project-42/work-items?tab=active')
})

test('malicious checkout return context still falls back safely', () => {
  for (const value of ['https://evil.example', '//evil.example', 'javascript:alert(1)', 'data:text/html,evil']) {
    assert.equal(resolveBillingReturnTo(value), '/dashboard')
  }
})

test('leaving checkout is navigation only and does not change payment state', () => {
  const exitSource = checkout.match(/const goBackToOrigin = \(\) => router\.(?:push|replace)\(returnTo\.value\)/)?.[0] || ''
  assert.doesNotMatch(exitSource, /billingApi|createOrder|activateFree|Paid|credits/i)
  assert.match(checkout, /const paymentState = computed\(\(\) =>/)
})

test('paid and terminal-state returns share the safe exit handler', () => {
  assert.match(checkout, /@click="goBackToOrigin"/)
  assert.match(checkout, /@click="goBackToOrigin"[\s\S]*Tiếp tục sử dụng SprintA/)
  assert.doesNotMatch(checkout, /router\.push\(returnTo\.value\)/)
})

test('checkout is not auto-resumed by an exit handler or browser back', () => {
  assert.doesNotMatch(checkout, /router\.push\(returnTo\.value\)/)
  assert.doesNotMatch(checkout, /window\.history\.forward\(/)
})
