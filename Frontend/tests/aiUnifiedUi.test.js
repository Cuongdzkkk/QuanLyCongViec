import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const root = new URL('../src/', import.meta.url)
const aiPage = fs.readFileSync(new URL('views/AIPage.vue', root), 'utf8')
const nexusLayout = fs.readFileSync(new URL('components/layout/NexusLayout.vue', root), 'utf8')
const composer = fs.readFileSync(new URL('components/ai/AiComposer.vue', root), 'utf8')
const message = fs.readFileSync(new URL('components/ai/AiMessage.vue', root), 'utf8')

test('panel and full chat use the same composer and message components', () => {
  assert.match(aiPage, /import AiComposer from ['"]@\/components\/ai\/AiComposer\.vue['"]/)
  assert.match(aiPage, /import AiMessage from ['"]@\/components\/ai\/AiMessage\.vue['"]/)
  assert.match(nexusLayout, /import AiComposer from ['"]@\/components\/ai\/AiComposer\.vue['"]/)
  assert.match(nexusLayout, /import AiMessage from ['"]@\/components\/ai\/AiMessage\.vue['"]/)
})

test('AI composer focus uses the exposed component ref contract', () => {
  assert.match(composer, /ref="textareaInput"/)
  assert.match(composer, /focusInput: \(\) => textareaInput\.value\?\.focus\(\)/)
  assert.match(nexusLayout, /ref="aiComposerRef"/)
  assert.match(nexusLayout, /aiComposerRef\.value\?\.focusInput\?\.\(\)/)
  assert.match(aiPage, /ref="aiComposerRef"/)
  assert.match(aiPage, /aiComposerRef\.value\?\.focusInput\?\.\(\)/)
  assert.doesNotMatch(nexusLayout, /querySelector\([^\n]*textarea/)
  assert.doesNotMatch(aiPage, /querySelector\([^\n]*textarea/)
})

test('full composer exposes the same attachment, screenshot, voice, and send controls', () => {
  for (const contract of ['attachment-command', 'screenshot', 'start-voice', 'use-transcript', 'drop', 'send']) {
    assert.match(composer, new RegExp(contract))
  }
  assert.match(aiPage, /@attachment-command="handleAttachmentCommand"/)
  assert.match(aiPage, /@start-voice="startVoiceRecording"/)
  assert.match(aiPage, /@send="sendMessage"/)
  assert.match(aiPage, /\/ai\/attachment-chat/)
})

test('both surfaces use the sanitized markdown renderer and explicit action events', () => {
  assert.match(message, /DOMPurify\.sanitize/)
  assert.match(message, /execute-action/)
  assert.match(message, /cancel-action/)
  assert.match(message, /retry-action/)
  assert.match(aiPage, /@execute-action="confirmPageAction"/)
  assert.match(nexusLayout, /@execute-action="executeAiAction"/)
})

test('shared AI UI styles use semantic theme tokens for state colors', () => {
  assert.match(composer, /var\(--color-on-accent/)
  assert.match(message, /var\(--color-on-accent/)
  assert.match(composer, /var\(--color-on-accent/)
  assert.match(message, /var\(--color-text-primary\)/)
})
