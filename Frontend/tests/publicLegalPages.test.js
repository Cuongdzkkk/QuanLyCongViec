import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')

const router = read('src/router/index.js')
const publicRoutes = read('src/router/publicRoutes.js')
const layout = read('src/components/layout/PublicLegalLayout.vue')
const about = read('src/views/About.vue')
const privacy = read('src/views/Privacy.vue')
const terms = read('src/views/Terms.vue')

test('public legal routes are explicitly registered and bypass only the existing auth wall', () => {
  for (const route of ['/about', '/privacy', '/terms']) {
    assert.match(publicRoutes, new RegExp(`path: '${route}'`))
    assert.match(router, new RegExp(`'${route}'`))
  }

  assert.match(router, /const authRequired = !publicPages\.includes\(to\.path\)/)
  const publicPageList = router.match(/const publicPages = \[([^\]]+)\]/s)?.[1] || ''
  assert.doesNotMatch(publicPageList, /dashboard|admin|settings/)
})

test('public pages share Sprinta branding and required navigation links', () => {
  assert.match(layout, /SprintaBrand size="compact"/)
  for (const source of [about, privacy, terms]) {
    assert.match(source, /PublicLegalLayout/)
  }
  for (const route of ['/about', '/privacy', '/terms', '/login']) {
    assert.match(layout, new RegExp(`to="${route}"`))
  }
  assert.match(about, /Google Calendar.*Gmail/s)
  assert.match(privacy, /openid.*email.*profile/s)
  assert.match(privacy, /calendar\.readonly/)
  assert.match(privacy, /gmail\.readonly/)
  assert.match(terms, /Tích hợp bên thứ ba và dịch vụ Google/)
})

test('privacy policy uses the configured project support contact without inventing account deletion', () => {
  for (const source of [privacy, terms]) {
    assert.match(source, /support@sprinta\.id\.vn/)
  }
  assert.match(privacy, /yêu cầu xóa tài khoản hoặc dữ liệu/)
})
