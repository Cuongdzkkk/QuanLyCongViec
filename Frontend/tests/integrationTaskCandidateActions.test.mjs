import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const integrationHub = await readFile(new URL('../src/views/IntegrationHubView.vue', import.meta.url), 'utf8')
const nexus = await readFile(new URL('../src/components/layout/NexusLayout.vue', import.meta.url), 'utf8')

test('integration hub renders evidence-backed task candidate actions', () => {
  assert.match(integrationHub, /Task candidates/)
  assert.match(integrationHub, /Chỉnh sửa/)
  assert.match(integrationHub, /Tạo task/)
  assert.match(integrationHub, /Bỏ qua/)
  assert.match(integrationHub, /createSelectedAiCandidates/)
  assert.match(integrationHub, /\/ai\/actions\/preview/)
  assert.match(integrationHub, /type: 'task\.create'/)
  assert.match(integrationHub, /candidate\.evidence/)
})

test('floating AI resumes a pending action from a short confirmation', () => {
  assert.match(nexus, /isAiConfirmationMessage\(outgoing\)/)
  assert.match(nexus, /findPendingAiAction\(chatHistory\.value\)/)
  assert.match(nexus, /await executeAiAction\(pendingAction\)/)
})
