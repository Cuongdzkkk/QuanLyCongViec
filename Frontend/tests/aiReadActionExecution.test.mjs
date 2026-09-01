import assert from 'node:assert/strict'
import fs from 'node:fs'
import test from 'node:test'

const nexusLayout = fs.readFileSync(new URL('../src/components/layout/NexusLayout.vue', import.meta.url), 'utf8')

test('catalog read actions execute directly without preview or confirmation persistence', () => {
  assert.match(nexusLayout, /action\.directExecution === true/)
  assert.match(nexusLayout, /axiosClient\.post\('\/ai\/actions\/execute'/)
})

test('write actions retain the existing preview and explicit confirmation path', () => {
  assert.match(nexusLayout, /axiosClient\.post\('\/ai\/actions\/preview'/)
  assert.match(nexusLayout, /axiosClient\.post\(`\/ai\/actions\/\$\{action\.serverActionId\}\/confirm`\)/)
})
