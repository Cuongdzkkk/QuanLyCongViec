import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

const aiPage = fs.readFileSync(new URL('../src/views/AIPage.vue', import.meta.url), 'utf8')

test('full AI serializes conversation history to the backend page-context contract', () => {
  assert.match(aiPage, /extra: \{ history: JSON\.stringify\(history\) \}/)
  assert.doesNotMatch(aiPage, /extra: \{ history \}/)
})
