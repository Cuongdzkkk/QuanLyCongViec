import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

const kudosView = fs.readFileSync(new URL('../src/views/HomeSite/Teams/TeamKudos.vue', import.meta.url), 'utf8')
const avatarHelper = fs.readFileSync(new URL('../src/utils/avatarHelper.js', import.meta.url), 'utf8')

test('Kudos composer resolves the avatar color helper used by its target list', () => {
  assert.match(kudosView, /import\s+\{[^}]*getAvatarColor[^}]*\}\s+from\s+['"]@\/utils\/avatarHelper['"]/)
  assert.match(kudosView, /avatarColor:\s*getAvatarColor\(user\.email\s*\|\|\s*user\.id\)/)
  assert.match(avatarHelper, /export function getAvatarColor\(/)
})
