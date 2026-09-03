import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')

test('connected accounts use explicit authenticated provider-link flows', () => {
  const api = read('src/api/authApi.js')
  const profile = read('src/views/Profile.vue')
  const sidebar = read('src/components/profile/ProfileSidebar.vue')
  const callback = read('src/views/GitHubCallback.vue')

  assert.match(api, /\/auth\/google-code\/link\/start/)
  assert.match(api, /\/auth\/github-link\/start/)
  assert.match(api, /\/auth\/external-logins/)
  assert.match(sidebar, /value: 'connected'/)
  assert.match(profile, /ConnectedAccountsCard/)
  assert.match(callback, /if \(state\)/)
  assert.match(callback, /linkGitHubAccount\(code, state\)/)
})
