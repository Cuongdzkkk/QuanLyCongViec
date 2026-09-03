import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const root = path.resolve(import.meta.dirname, '..')
const read = file => fs.readFileSync(path.join(root, file), 'utf8')
const home = read('src/views/Home.vue')
const modal = read('src/components/landing/EnterpriseLeadModal.vue')
const admin = read('src/views/admin/EnterpriseLeads.vue')
const routes = read('src/router/adminRoutes.js')

test('Enterprise pricing CTA opens a real anonymous contact flow', () => {
  assert.match(home, /EnterpriseLeadModal/)
  assert.match(home, /enterpriseLeadOpen\.value = true/)
  assert.match(home, /copy\.enterpriseContact/)
  assert.match(modal, /axiosClient\.post\('\/public\/enterprise-leads'/)
  assert.match(modal, /Liên hệ tư vấn/)
  assert.match(modal, /Cảm ơn bạn\. SprintA đã nhận được yêu cầu và sẽ liên hệ lại\./)
  assert.match(modal, /error\.response\?\.data\?\.message/)
  const catchBlock = modal.slice(modal.indexOf('} catch'))
  assert.doesNotMatch(catchBlock, /submitted\.value = true/)
})

test('Enterprise form keeps required fields and mobile-safe layout', () => {
  for (const field of ['contactName', 'workEmail', 'company', 'teamSize']) assert.match(modal, new RegExp(`v-model\\.trim="form\\.${field}"|v-model="form\\.${field}"`))
  assert.match(modal, /maxlength="2000"/)
  assert.match(modal, /@media \(max-width: 620px\)/)
  assert.match(modal, /grid-template-columns: 1fr/)
})

test('Admin lead route is protected and separates internal notes', () => {
  assert.match(routes, /path: '\/admin\/enterprise-leads'/)
  assert.match(routes, /meta: adminMeta/)
  assert.match(admin, /\/admin\/enterprise-leads/)
  assert.match(admin, /internalNote/)
  assert.match(admin, /Chỉ admin nhìn thấy/)
  assert.match(admin, /axiosClient\.patch\(`\/admin\/enterprise-leads\//)
})
