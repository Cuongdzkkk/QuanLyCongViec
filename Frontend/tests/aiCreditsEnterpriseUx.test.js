import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

const root = new URL('../src/', import.meta.url)
const modal = fs.readFileSync(new URL('components/ai/AiCreditsPurchaseModal.vue', root), 'utf8')
const aiPage = fs.readFileSync(new URL('views/AIPage.vue', root), 'utf8')
const nexusLayout = fs.readFileSync(new URL('components/layout/NexusLayout.vue', root), 'utf8')
const home = fs.readFileSync(new URL('views/Home.vue', root), 'utf8')
const contact = fs.readFileSync(new URL('components/landing/EnterpriseLeadModal.vue', root), 'utf8')

test('AI credits modal uses a visible existing icon and truthful Enterprise copy', () => {
  assert.match(modal, /icon="bi bi-stars"/)
  assert.match(modal, /Credits theo thỏa thuận/)
  assert.match(modal, /Giá theo nhu cầu/)
  assert.doesNotMatch(modal, /0 AI credits \/ tháng/)
  assert.doesNotMatch(modal, /Extra credits chưa có mức giá mua lẻ được công bố/)
  assert.match(modal, /Giá gói được cập nhật trực tiếp từ hệ thống/)
})

test('Enterprise CTA reuses public contact flow and preserves a safe return context', () => {
  assert.match(modal, /contact: 'enterprise'/)
  assert.match(modal, /returnTo: route\.fullPath/)
  assert.match(modal, /source: 'ai-credits'/)
  assert.match(modal, /plan: 'enterprise'/)
  assert.match(home, /EnterpriseLeadModal[\s\S]*:prefill="enterpriseContactPrefill"/)
  assert.match(home, /enterpriseContactReturnTo/)
  assert.match(home, /resolveBillingReturnTo/)
  assert.match(contact, /defineProps\(/)
  assert.match(contact, /getStoredUserSession/)
  assert.doesNotMatch(modal, /disabled="[^"]*isEnterprisePlan/)
})

test('AI context header groups selectors separately from the credit wallet', () => {
  assert.match(aiPage, /class="scope-selectors"/)
  assert.match(aiPage, /class="ai-credit-wallet"/)
  assert.match(aiPage, /AI Credit Wallet/)
  assert.match(aiPage, /AI Credits · \{\{ aiPlanLabel \}\}/)
  assert.match(aiPage, /class="ai-credit-wallet-cta"/)
  assert.doesNotMatch(aiPage, /class="credit-pill"/)
  assert.doesNotMatch(aiPage, /class="credit-buy-inline"/)
  assert.match(nexusLayout, /contact-context=/)
})

test('pricing and payment history have separate visual sections', () => {
  assert.match(modal, /class="plans-section"/)
  assert.match(modal, /class="history-section"/)
  assert.match(modal, /\.history-section[\s\S]*border-top:/)
})
