import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')
const home = read('src/views/Home.vue')
const vi = read('src/i18n/locales/vi.js')
const en = read('src/i18n/locales/en.js')

test('landing AI confirmation uses localized, action-led copy', () => {
  assert.match(home, /t\('landing\.aiConfirmation\.title'\)/)
  assert.match(home, /t\('landing\.aiConfirmation\.supportingCopy'\)/)
  assert.match(home, /t\('landing\.aiConfirmation\.cancel'\)/)
  assert.match(home, /t\('landing\.aiConfirmation\.apply'\)/)
  assert.match(vi, /title: 'Chờ xác nhận'/)
  assert.match(vi, /supportingCopy: 'AI chỉ thực hiện thay đổi sau khi bạn xác nhận\.'/)
  assert.match(vi, /apply: 'Áp dụng thay đổi'/)
  assert.match(en, /apply: 'Apply changes'/)
  assert.doesNotMatch(home, /Xác nhận & Áp dụng|Confirm & Apply/)
})

test('copy-only confirmation polish does not add execution handlers', () => {
  const confirmationCard = home.slice(home.indexOf('<div class="confirm-card">'), home.indexOf('<div class="mascot-stage">'))
  assert.doesNotMatch(confirmationCard, /@click|axiosClient|confirmPageAction/)
  assert.match(home, /\.confirm-actions \{ display: grid; grid-template-columns: 1fr; \}/)
  assert.match(home, /\.confirm-actions \.btn \{ width: 100%; height: auto !important; min-height: 44px !important; white-space: nowrap; \}/)
})
