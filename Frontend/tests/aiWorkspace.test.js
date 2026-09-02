import test from 'node:test'
import assert from 'node:assert/strict'
import {
  AI_PANEL_MIN_HEIGHT,
  AI_PANEL_MIN_WIDTH,
  clampAiPanelSize,
  getAiPanelMaxWidth,
  isAiPanelResizable,
  isComposerSendKey,
  readAiPanelSize,
  writeActionsOnly
} from '../src/utils/aiWorkspace.js'

test('desktop panel bounds stay within the viewport contract', () => {
  const size = clampAiPanelSize({ width: 1200, height: 200 }, { width: 1280, height: 720, topInset: 68 })
  assert.equal(size.width, getAiPanelMaxWidth(1280))
  assert.equal(size.height, AI_PANEL_MIN_HEIGHT)
  assert.ok(size.width >= AI_PANEL_MIN_WIDTH)
})

test('panel size never persists conversation content and mobile disables resize', () => {
  assert.equal(isAiPanelResizable(1024), false)
  assert.equal(isAiPanelResizable(1280), true)
  assert.deepEqual(clampAiPanelSize({ width: 380, height: 560 }, { width: 820, height: 1180 }), { width: 380, height: 560 })
})

test('panel width and height persist through the dedicated UI key only', () => {
  const values = new Map()
  const storage = { getItem: key => values.get(key) || null, setItem: (key, value) => values.set(key, value) }
  const size = { width: 520, height: 640 }
  storage.setItem('sprinta-ai-panel-size', JSON.stringify(size))
  assert.deepEqual(readAiPanelSize(storage, { width: 1280, height: 900 }), size)
  assert.equal(values.has('sprinta-ai-conversation'), false)
})

test('composer sends on Enter, keeps Shift+Enter for multiline and respects IME', () => {
  assert.equal(isComposerSendKey({ key: 'Enter', shiftKey: false, isComposing: false }), true)
  assert.equal(isComposerSendKey({ key: 'Enter', shiftKey: true, isComposing: false }), false)
  assert.equal(isComposerSendKey({ key: 'Enter', shiftKey: false, isComposing: true }), false)
})

test('read-only actions stay out of write confirmation cards', () => {
  const actions = [{ type: 'summarize_project' }, { type: 'create_task', requiresConfirmation: true }]
  assert.deepEqual(writeActionsOnly(actions, type => type === 'summarize_project'), [actions[1]])
})
