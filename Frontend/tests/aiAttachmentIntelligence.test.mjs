import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

const composer = await readFile(new URL('../src/composables/useAiComposer.js', import.meta.url), 'utf8')
const composerView = await readFile(new URL('../src/components/ai/AiComposer.vue', import.meta.url), 'utf8')
const aiPage = await readFile(new URL('../src/views/AIPage.vue', import.meta.url), 'utf8')
const nexus = await readFile(new URL('../src/components/layout/NexusLayout.vue', import.meta.url), 'utf8')

test('attachment composer supports explicit formats, clipboard, and screenshot capture', () => {
  for (const format of ['.txt', '.csv', '.pdf', '.docx', '.png', '.jpeg']) assert.match(composer, new RegExp(`['"]${format.replace('.', '\\.')}`))
  assert.match(composer, /navigator\.clipboard\.read/)
  assert.match(composer, /getDisplayMedia/)
  assert.match(composer, /canvas\.toBlob\(resolve, 'image\/png'/)
})

test('direct attachment chat renders returned task.create actions through the shared executor', () => {
  assert.match(aiPage, /payload\?\.actions \|\| \[\]/)
  assert.match(aiPage, /decorateAiAction\(action/)
  assert.match(nexus, /responseData\?\.actions \|\| \[\]/)
  assert.match(nexus, /previewAndConfirmAiAction/)
})

test('specific upload errors are retained instead of being replaced by a generic failure label', () => {
  assert.match(composerView, /Không thể xử lý attachment/)
  assert.match(aiPage, /attachment\.errorMessage = error\.response\?\.data\?\.message/)
  assert.match(nexus, /attachment\.errorMessage = error\.response\?\.data\?\.message/)
})
